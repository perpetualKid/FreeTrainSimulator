﻿// COPYRIGHT 2019, 2020 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts;
using Orts.Simulation;
using Orts.Simulation.MultiPlayer;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.WebServices
{
    /// <summary>
    /// An in-browser Track Monitor that duplicates much of the functionality of the native Track Monitor.
    /// </summary>
    /// <remarks>
    /// Each row of the in-browser "popup" is represented by a ListLabel.
    /// </remarks>
    public static class TrackMonitorDisplay
    {
        /// <summary>
        /// A Track Monitor row with data fields and image coordinates.
        /// </summary>
        public struct ListLabel
        {
            public string FirstCol;
            public string TrackColLeft;
            public string TrackCol;
            public Rectangle TrackColItem;
            public string TrackColRight;
            public string LimitCol;
            public string SignalCol;
            public Rectangle SignalColItem;
            public string DistCol;
        }

        private const float MaximumDistanceM = 5000f;
        private const int TrackLength = 17;
        private const int MonitorWidth = 200;
        private const int MonitorHeight = 320;
        private const float MonitorScale = MonitorWidth / 150f;

        /// <summary>
        /// Vertical offset on window for additional out-of-range info at top and bottom.
        /// </summary>
        private const int AdditionalInfoHeight = 16;

        /// <summary>
        /// Minimum vertical distance between two labels.
        /// </summary>
        private const int TextSpacing = 10;

        /// <summary>
        /// Table of Colors to client-side color codes.
        /// </summary>
        /// <remarks>
        /// Compare codes with index.css.
        /// </remarks>
        private static readonly Dictionary<Color, string> ColorCode = new Dictionary<Color, string>
        {
            { Color.Yellow, "???" },
            { Color.Green, "??!" },
            { Color.Black, "?!?" },
            { Color.PaleGreen, "?!!" },
            { Color.White, "!??" },
            { Color.Orange, "!!?" },
            { Color.OrangeRed, "!!!" },
            { Color.Cyan, "%%%" },
            { Color.Brown, "%$$" },
            { Color.LightGreen, "%%$" },
            { Color.Blue, "$%$" },
            { Color.LightSkyBlue, "$$$" },
        };

        private enum TrainDirection
        {
            Forward,
            Backward,
        }

        private enum TrainPosition
        {
            AutoForwards,
            AutoBackwards,
            ManualOnRoute,
            ManualOffRoute,
        }

        private static class Symbols
        {
            public const string TrackWS = "\u2502\u2502";
            public const string GradientDownWS = "\u2198";
            public const string GradientUpWS = "\u2197";
            public const string WaitingPointWS = "✋";
            public const string ReversalWS = "↶";
            public static readonly string EyeWS = $"⛯{ColorCode[Color.LightGreen]}";
            public static readonly string EndAuthorityWS = $"\u25AC{ColorCode[Color.OrangeRed]}";
            public static readonly string OppositeTrainForwardWS = $"\u2588{ColorCode[Color.Orange]}";
            public static readonly string OppositeTrainBackwardWS = $"\u2588{ColorCode[Color.Orange]}";
            public static readonly string StationLeftWS = $"▐{ColorCode[Color.Blue]}";
            public static readonly string StationRightWS = $"▌{ColorCode[Color.Blue]}";
            public static readonly string InvalidReversalWS = $"▬{ColorCode[Color.Orange]}";
            public static readonly string LeftArrowWS = $"◄{ColorCode[Color.Orange]}";
            public static readonly string RightArrowWS = $"►{ColorCode[Color.Orange]}";
            public static readonly Dictionary<TrainDirection, string> ArrowWS = new Dictionary<TrainDirection, string>
            {
                { TrainDirection.Forward, $"▲{ColorCode[Color.PaleGreen]}" },
                { TrainDirection.Backward, $"▼{ColorCode[Color.PaleGreen]}" },
            };
            public static readonly Dictionary<TrainPosition, string> TrainPositionWS = new Dictionary<TrainPosition, string>
            {
                { TrainPosition.AutoForwards, $"⧯{ColorCode[Color.White]}" },
                { TrainPosition.AutoBackwards, $"⧯{ColorCode[Color.White]}" },
                { TrainPosition.ManualOnRoute, $"⧯{ColorCode[Color.White]}" },
                { TrainPosition.ManualOffRoute, $"⧯{ColorCode[Color.OrangeRed]}" },
            };
            public static readonly Dictionary<TrackMonitorSignalAspect, string> SignalMarkersWebApi = new Dictionary<TrackMonitorSignalAspect, string>
            {
                { TrackMonitorSignalAspect.Clear2, $"\u25D5{ColorCode[Color.PaleGreen]}" },
                { TrackMonitorSignalAspect.Clear1, $"\u25D5{ColorCode[Color.PaleGreen]}" },
                { TrackMonitorSignalAspect.Approach3, $"\u25D5{ColorCode[Color.Yellow]}" },
                { TrackMonitorSignalAspect.Approach2, $"\u25D5{ColorCode[Color.Yellow]}" },
                { TrackMonitorSignalAspect.Approach1, $"\u25D5{ColorCode[Color.Yellow]}" },
                { TrackMonitorSignalAspect.Restricted, $"\u25D5{ColorCode[Color.OrangeRed]}" },
                { TrackMonitorSignalAspect.StopAndProceed, $"\u25D5{ColorCode[Color.OrangeRed]}" },
                { TrackMonitorSignalAspect.Stop, $"\u25D5{ColorCode[Color.OrangeRed]}"},
                { TrackMonitorSignalAspect.Permission, $"\u25D5{ColorCode[Color.OrangeRed]}" },
                { TrackMonitorSignalAspect.None, $"\u25D5{ColorCode[Color.Black]}" },
            };
        }

        private static class Sprites
        {
            public static readonly Rectangle EyeSprite = new Rectangle(0, 144, 24, 24);
            public static readonly Rectangle EndAuthoritySprite = new Rectangle(0, 0, 24, 24);
            public static readonly Rectangle OppositeTrainForwardSprite = new Rectangle(24, 120, 24, 24);
            public static readonly Rectangle OppositeTrainBackwardSprite = new Rectangle(0, 120, 24, 24);
            public static readonly Rectangle LeftArrowSprite = new Rectangle(0, 168, 24, 24);
            public static readonly Rectangle RightArrowSprite = new Rectangle(24, 168, 24, 24);
            public static readonly Rectangle ReversalSprite = new Rectangle(0, 24, 24, 24);
            public static readonly Rectangle WaitingPointSprite = new Rectangle(24, 24, 24, 24);
            public static readonly Rectangle InvalidReversalSprite = new Rectangle(24, 144, 24, 24);
            public static readonly Dictionary<TrainPosition, Rectangle> TrainPositionSprite = new Dictionary<TrainPosition, Rectangle>
            {
                { TrainPosition.AutoForwards, new Rectangle(0, 72, 24, 24) },
                { TrainPosition.AutoBackwards, new Rectangle(24, 72, 24, 24) },
                { TrainPosition.ManualOnRoute, new Rectangle(24, 96, 24, 24) },
                { TrainPosition.ManualOffRoute, new Rectangle(0, 96, 24, 24) },
            };

            /// <remarks>
            /// Equivalent to <see cref="Popups.TrackMonitorWindow.SignalMarkers"/>.
            /// </remarks>
            public static readonly Dictionary<TrackMonitorSignalAspect, Rectangle> SignalMarkers = new Dictionary<TrackMonitorSignalAspect, Rectangle>
            {
                { TrackMonitorSignalAspect.Clear2, new Rectangle(0, 0, 16, 16) },
                { TrackMonitorSignalAspect.Clear1, new Rectangle(16, 0, 16, 16) },
                { TrackMonitorSignalAspect.Approach3, new Rectangle(0, 16, 16, 16) },
                { TrackMonitorSignalAspect.Approach2, new Rectangle(16, 16, 16, 16) },
                { TrackMonitorSignalAspect.Approach1, new Rectangle(0, 32, 16, 16) },
                { TrackMonitorSignalAspect.Restricted, new Rectangle(16, 32, 16, 16) },
                { TrackMonitorSignalAspect.StopAndProceed, new Rectangle(0, 48, 16, 16) },
                { TrackMonitorSignalAspect.Stop, new Rectangle(16, 48, 16, 16) },
                { TrackMonitorSignalAspect.Permission, new Rectangle(0, 64, 16, 16) },
                { TrackMonitorSignalAspect.None, new Rectangle(16, 64, 16, 16) }
            };
        }

        private static class Positions
        {
            public static readonly int[] Train = new int[5] { 42, -12, -12, 24, 24 };
            public static readonly int[] Arrow = new int[5] { 22, -12, -12, 24, 24 };
        }

        /// <summary>
        /// Fixed distance rounding values as function of maximum distance.
        /// </summary>
        /// <remarks>
        /// Equivalent to <see cref="Popups.TrackMonitorWindow.roundingValues"/>.
        /// </remarks>
        private static readonly Dictionary<float, float> RoundingValues = new Dictionary<float, float>
        {
            { 0.0f, 0.5f },
            { 5.0f, 1.0f },
            { 10.0f, 2.0f },
        };

        private static int RowOffset { get => MultiPlayerManager.IsMultiPlayer() ? 1 : 2; }

        /// <summary>
        /// Retrieve a formatted list ListLabels to be displayed as an in-browser Track Monitor.
        /// </summary>
        /// <param name="viewer">The Viewer to read train data from.</param>
        /// <returns>A list of ListLabels, one per row of the popup.</returns>
        public static IEnumerable<ListLabel> TrackMonitorDisplayList(this Viewer viewer)
        {
            bool useMetric = Simulator.Instance.MetricUnits;
            var labels = new List<ListLabel>();
            void AddLabel(ListLabel label)
            {
                CheckLabel(ref label);
                labels.Add(label);
            }
            void AddSeparator() => AddLabel(new ListLabel
            {
                FirstCol = "Sprtr",
            });

            // Always get train details to pass on to TrackMonitor.
            Orts.Simulation.Physics.TrainInfo thisInfo = viewer.PlayerTrain.GetTrainInfo();
            Color speedColor = SpeedColor(thisInfo.Speed, thisInfo.AllowedSpeed);
            Color trackColor = TrackColor(thisInfo.Speed, thisInfo.AllowedSpeed);

            // Speed
            AddLabel(new ListLabel
            {
                FirstCol = Viewer.Catalog.GetString("Speed"),
                TrackCol = $"{FormatStrings.FormatSpeedDisplay(Math.Abs(thisInfo.Speed), useMetric)}{ColorCode[speedColor]}",
            });

            // Projected
            AddLabel(new ListLabel
            {
                FirstCol = Viewer.Catalog.GetString("Projected"),
                TrackCol = FormatStrings.FormatSpeedDisplay(Math.Abs(thisInfo.ProjectedSpeed), useMetric),
            });

            // Allowed speed
            AddLabel(new ListLabel
            {
                FirstCol = Viewer.Catalog.GetString("Limit"),
                TrackCol = FormatStrings.FormatSpeedLimit(thisInfo.AllowedSpeed, useMetric),
            });
            AddSeparator();

            // Gradient
            float gradient = -thisInfo.Gradient;
            const float minSlope = 0.00015f;
            string gradientIndicator;
            if (gradient < -minSlope)
                gradientIndicator = $"  {gradient:F1}%{Symbols.GradientDownWS} {ColorCode[Color.LightSkyBlue]}";
            else if (gradient > minSlope)
                gradientIndicator = $"  {gradient:F1}%{Symbols.GradientUpWS} {ColorCode[Color.Yellow]}";
            else
                gradientIndicator = "-";
            AddLabel(new ListLabel
            {
                FirstCol = Viewer.Catalog.GetString("Gradient"),
                TrackCol = gradientIndicator,
            });
            AddSeparator();

            // Direction
            Train playerTrain = viewer.PlayerLocomotive.Train;
            bool showMUReverser = Math.Abs(playerTrain.MUReverserPercent) != 100;
            AddLabel(new ListLabel
            {
                FirstCol = viewer.PlayerLocomotive.EngineType == EngineType.Steam ? Viewer.Catalog.GetString("Reverser") : Viewer.Catalog.GetString("Direction"),
                TrackCol = (showMUReverser ? $"{Math.Abs(playerTrain.MUReverserPercent):0}% " : "") + viewer.PlayerLocomotive.Direction.GetLocalizedDescription(),
            });

            // Present cab orientation (0=forward, 1=backward)
            AddLabel(new ListLabel
            {
                FirstCol = Viewer.Catalog.GetString("Cab ORIEN"),
                TrackCol = thisInfo.CabOrientation.GetLocalizedDescription(),
            });

            // Control mode
            string controlIndicator;
            if (thisInfo.ControlMode == TrainControlMode.AutoNode)
                controlIndicator = FindAuthorityInfo(thisInfo.ObjectInfoForward, thisInfo.ControlMode.GetLocalizedDescription());
            else if (thisInfo.ControlMode == TrainControlMode.OutOfControl)
                controlIndicator = $"{thisInfo.ControlMode.GetLocalizedDescription()}: {thisInfo.ObjectInfoForward.First().OutOfControlReason.GetLocalizedDescription()}";
            else
                controlIndicator = thisInfo.ControlMode.GetLocalizedDescription();
            AddSeparator();
            AddLabel(new ListLabel
            {
                FirstCol = controlIndicator,
            });
            AddSeparator();

            // Milepost Limit Dist
            AddLabel(new ListLabel
            {
                FirstCol = Viewer.Catalog.GetString("Milepost"),
                LimitCol = Viewer.Catalog.GetString("Limit"),
                DistCol = Viewer.Catalog.GetString("Dist"),
            });
            AddSeparator();

            // TrackMonitor: Control text emulation
            var trackLabels = new List<ListLabel>(MakeTracks(trackColor));
            if (thisInfo != null)
            {
                if (MultiPlayerManager.IsMultiPlayer())
                {
                    DrawMPModeInfo(trackLabels, thisInfo, useMetric);
                }
                else
                {
                    switch (thisInfo.ControlMode)
                    {
                        case TrainControlMode.AutoNode:
                        case TrainControlMode.AutoSignal:
                            DrawAutoModeInfo(trackLabels, thisInfo, useMetric);
                            break;
                        case TrainControlMode.TurnTable:
                            break;
                        default:
                            DrawManualModeInfo(trackLabels, thisInfo, useMetric);
                            break;
                    }
                }
            }

            labels.AddRange(trackLabels);
            return labels;
        }

        /// <summary>
        /// Sanitize the fields in a ListLabel.
        /// </summary>
        /// <param name="label">A reference to the ListLabel to check.</param>
        private static void CheckLabel(ref ListLabel label)
        {
            void CheckString(ref string s) => s = (s ?? "").Length == 0 ? " " : s;
//            void CheckRectangle(ref Rectangle r) => r = r == null ? new Rectangle(0, 0, 0, 0) : r;
            CheckString(ref label.FirstCol);
            CheckString(ref label.TrackColLeft);
            CheckString(ref label.TrackCol);
//            CheckRectangle(ref label.TrackColItem);
            CheckString(ref label.TrackColRight);
            CheckString(ref label.LimitCol);
            CheckString(ref label.SignalCol);
//            CheckRectangle(ref label.SignalColItem);
            CheckString(ref label.DistCol);
        }

        /// <summary>
        /// Retrieve the color of the numerals that represent the train's current speed.
        /// </summary>
        /// <param name="speedMpS">The train's current speed.</param>
        /// <param name="allowedSpeedMpS">The maximum authorized speed.</param>
        /// <returns>The color, encoded as a string of '!' and '?'.</returns>
        private static Color SpeedColor(float speedMpS, float allowedSpeedMpS)
        {
            speedMpS = Math.Abs(speedMpS);
            if (speedMpS < allowedSpeedMpS - 1.0f)
                return Color.White;
            else if (speedMpS < allowedSpeedMpS)
                return Color.PaleGreen;
            else if (speedMpS < allowedSpeedMpS + 5.0f)
                return Color.Orange;
            else
                return Color.OrangeRed;
        }

        /// <summary>
        /// Retrieve the color of the track that represents the train's current speed.
        /// </summary>
        /// <param name="speedMpS">The train's current speed.</param>
        /// <param name="allowedSpeedMpS">The maximum authorized speed.</param>
        /// <returns>The color, encoded as a string of '!' and '?'.</returns>
        private static Color TrackColor(float speedMpS, float allowedSpeedMpS)
        {
            speedMpS = Math.Abs(speedMpS);
            if (speedMpS < allowedSpeedMpS - 1.0f)
                return Color.Green;
            else if (speedMpS < allowedSpeedMpS)
                return Color.PaleGreen;
            else if (speedMpS < allowedSpeedMpS + 5.0f)
                return Color.Orange;
            else
                return Color.OrangeRed;
        }

        /// <summary>
        /// Retrieve the current authorized status of the train.
        /// </summary>
        /// <remarks>
        /// Equivalent to <see cref="Popups.TrackMonitorWindow.FindAuthorityInfo"/>.
        /// </remarks>
        private static string FindAuthorityInfo(IEnumerable<TrainPathItem> objects, string controlText)
        {
            TrainPathItem authInfo = objects.SingleOrDefault((info) => info.ItemType == TrainPathItemType.Authority);
            return authInfo == null ? controlText : $"{controlText} : {authInfo.AuthorityType.GetLocalizedDescription()}";
        }

        /// <summary>
        /// Draw the track and associated track items.
        /// </summary>
        /// <param name="trackColor">The color of the track.</param>
        /// <returns>A formatted list of ListLabels.</returns>
        private static IEnumerable<ListLabel> MakeTracks(Color trackColor)
        {
            foreach (int _ in Enumerable.Range(0, TrackLength))
            {
                var label = new ListLabel
                {
                    TrackCol = $"{Symbols.TrackWS}{ColorCode[trackColor]}",
                };
                CheckLabel(ref label);
                yield return label;
            }
        }

        /// <summary>
        /// Draw train position and upcoming track items on the ListLabel list.
        /// </summary>
        /// <param name="labels">The list of labels to modify.</param>
        private static void DrawMPModeInfo(List<ListLabel> labels, Orts.Simulation.Physics.TrainInfo trainInfo, bool useMetric)
        {
            int startObjectArea = AdditionalInfoHeight;
            int endObjectArea = MonitorHeight - (MonitorHeight - (int)Math.Ceiling(MonitorHeight / MonitorScale)) - AdditionalInfoHeight;
            int zeroObjectPointTop, zeroObjectPointMiddle, zeroObjectPointBottom;
            if (trainInfo.Direction == Direction.Forward)
            {
                zeroObjectPointTop = endObjectArea - Positions.Train[4];
                zeroObjectPointMiddle = zeroObjectPointTop - Positions.Train[1];
                zeroObjectPointBottom = zeroObjectPointMiddle - Positions.Train[2];
            }
            else if (trainInfo.Direction == Direction.Backward)
            {
                zeroObjectPointTop = startObjectArea;
                zeroObjectPointMiddle = zeroObjectPointTop - Positions.Train[1];
                zeroObjectPointBottom = zeroObjectPointMiddle - Positions.Train[2];
            }
            else
            {
                zeroObjectPointMiddle = startObjectArea + (endObjectArea - startObjectArea) / 2;
                zeroObjectPointTop = zeroObjectPointMiddle + Positions.Train[1];
                zeroObjectPointBottom = zeroObjectPointMiddle - Positions.Train[2];
            }
            float distanceFactor = (endObjectArea - startObjectArea - Positions.Train[4]) / MaximumDistanceM / (trainInfo.Direction == (Direction)(-1) ? 2 : 1);

            // Draw direction arrow
            if (trainInfo.Direction == Direction.Forward)
                DrawArrow(labels, TrainDirection.Forward, zeroObjectPointMiddle);
            else if (trainInfo.Direction == Direction.Backward)
                DrawArrow(labels, TrainDirection.Backward, zeroObjectPointMiddle);

            if (trainInfo.Direction != Direction.Backward)
            {
                // Draw fixed distance indications
                float markerIntervalM = DrawDistanceMarkers(labels, distanceFactor, zeroObjectPointTop, numberOfMarkers: 4, direction: TrainDirection.Forward, useMetric);

                // Draw forward items
                DrawTrackItems(labels, trainInfo.ObjectInfoForward, zeroObjectPointTop, distanceFactor, markerIntervalM, direction: TrainDirection.Forward, useMetric);
            }
            if (trainInfo.Direction !=0)
            {
                // Draw fixed distance indications
                float markerIntervalM = DrawDistanceMarkers(labels, distanceFactor, zeroObjectPointBottom, numberOfMarkers: 4, direction: TrainDirection.Backward, useMetric);

                // Draw backward items
                DrawTrackItems(labels, trainInfo.ObjectInfoBackward, zeroObjectPointBottom, distanceFactor, markerIntervalM, direction: TrainDirection.Backward, useMetric);
            }

            // Draw own train marker
            TrainPosition trainPosition;
            if (trainInfo.Direction == (Direction)(-1))
                trainPosition = TrainPosition.ManualOnRoute;
            else if (trainInfo.Direction == 0)
                trainPosition = TrainPosition.AutoForwards;
            else
                trainPosition = TrainPosition.AutoBackwards;
            DrawOwnTrain(labels, trainPosition, zeroObjectPointTop);
        }

        /// <summary>
        /// Draw train position and upcoming track items on the ListLabel list.
        /// </summary>
        /// <param name="labels">The list of labels to modify.</param>
        private static void DrawAutoModeInfo(List<ListLabel> labels, Orts.Simulation.Physics.TrainInfo trainInfo, bool useMetric)
        {
            int startObjectArea = AdditionalInfoHeight;
            int endObjectArea = MonitorHeight - (MonitorHeight - (int)Math.Ceiling(MonitorHeight / MonitorScale)) - AdditionalInfoHeight - Positions.Train[4];
            int zeroObjectPointTop = endObjectArea;
            int zeroObjectPointMiddle = zeroObjectPointTop - Positions.Train[1];
            int zeroObjectPointBottom = zeroObjectPointMiddle - Positions.Train[2];
            float distanceFactor = (endObjectArea - startObjectArea) / MaximumDistanceM;

            // Translate itemLocation value to row value
            int itemLocationWS = ItemLocationToRow(zeroObjectPointBottom, zeroObjectPointBottom);

            // Draw train position line
            // Use red if no info for reverse move available
            TrainPathItem backwardObject = trainInfo.ObjectInfoBackward?.FirstOrDefault();
            ChangeLabelAt(labels, itemLocationWS, (ListLabel _) => new ListLabel
            {
                FirstCol = Viewer.Catalog.GetString(
                    backwardObject?.ItemType == TrainPathItemType.Authority &&
                    backwardObject?.AuthorityType == EndAuthorityType.NoPathReserved ? "SprtrRed" : "SprtrDarkGray"),
            });

            // Draw direction arrow
            if (trainInfo.Direction == Direction.Forward)
                DrawArrow(labels, TrainDirection.Forward, zeroObjectPointMiddle);
            else if (trainInfo.Direction == Direction.Backward)
                DrawArrow(labels, TrainDirection.Backward, zeroObjectPointMiddle);

            // Draw eye
            DrawEye(labels, trainInfo);

            // Draw fixed distance indications
            float markerIntervalM = DrawDistanceMarkers(labels, distanceFactor, zeroObjectPointTop, numberOfMarkers: 4, direction: TrainDirection.Forward, useMetric);

            // Draw forward items
            DrawTrackItems(labels, trainInfo.ObjectInfoForward, zeroObjectPointTop, distanceFactor, markerIntervalM, direction: TrainDirection.Forward, useMetric);

            // Draw own train marker
            DrawOwnTrain(labels, TrainPosition.AutoForwards, zeroObjectPointTop);
        }

        /// <summary>
        /// Draw train position and upcoming track items on the ListLabel list.
        /// </summary>
        /// <param name="labels">The list of labels to modify.</param>
        private static void DrawManualModeInfo(List<ListLabel> labels, Orts.Simulation.Physics.TrainInfo trainInfo, bool useMetric)
        {
            int startObjectArea = AdditionalInfoHeight;
            int endObjectArea = MonitorHeight - (MonitorHeight - (int)Math.Ceiling(MonitorHeight / MonitorScale)) - AdditionalInfoHeight;
            int zeroObjectPointMiddle = startObjectArea + (endObjectArea - startObjectArea) / 2;
            int zeroObjectPointTop = zeroObjectPointMiddle + Positions.Train[1];
            int zeroObjectPointBottom = zeroObjectPointMiddle - Positions.Train[2];
            float distanceFactor = (zeroObjectPointTop - startObjectArea) / MaximumDistanceM;

            // Draw train position line
            ListLabel DarkGraySeparator(ListLabel _) => new ListLabel
            {
                FirstCol = "SprtrDarkGray",
            };
            ChangeLabelAt(labels, ItemLocationToRow(zeroObjectPointTop, zeroObjectPointTop) + 1, DarkGraySeparator);
            ChangeLabelAt(labels, ItemLocationToRow(zeroObjectPointBottom, zeroObjectPointBottom) - 1, DarkGraySeparator);

            // Draw direction arrow
            if (trainInfo.Direction == Direction.Forward)
                DrawArrow(labels, TrainDirection.Forward, zeroObjectPointMiddle);
            else if (trainInfo.Direction == Direction.Backward)
                DrawArrow(labels, TrainDirection.Backward, zeroObjectPointMiddle);

            // Draw eye
            DrawEye(labels, trainInfo);

            // Draw fixed distance indications
            float markerIntervalM = DrawDistanceMarkers(labels, distanceFactor, zeroObjectPointTop, numberOfMarkers: 3, direction: TrainDirection.Forward, useMetric);
            DrawDistanceMarkers(labels, distanceFactor, zeroObjectPointBottom, numberOfMarkers: 3, direction: TrainDirection.Backward, useMetric); // no return required

            // Draw forward items
            DrawTrackItems(labels, trainInfo.ObjectInfoForward, zeroObjectPointTop, distanceFactor, markerIntervalM, direction: TrainDirection.Forward, useMetric);

            // Draw backward items
            DrawTrackItems(labels, trainInfo.ObjectInfoBackward, zeroObjectPointBottom, distanceFactor, markerIntervalM, direction: TrainDirection.Backward, useMetric);

            // Draw own train marker
            DrawOwnTrain(labels, trainInfo.PathDefined ? TrainPosition.ManualOnRoute : TrainPosition.ManualOffRoute, zeroObjectPointTop);
        }

        private static void DrawArrow(List<ListLabel> labels, TrainDirection direction, int zeroObjectPointMiddle)
        {
            int position = zeroObjectPointMiddle + Positions.Arrow[direction == TrainDirection.Forward ? 1 : 2];
            int itemLocationWS = ItemLocationToRow(position, position) + RowOffset;
            ChangeLabelAt(labels, itemLocationWS, (ListLabel dataCol) =>
            {
                dataCol.TrackColLeft = Symbols.ArrowWS[direction];
                return dataCol;
            });
        }

        private static void DrawEye(List<ListLabel> labels, Orts.Simulation.Physics.TrainInfo trainInfo)
        {
            int position = trainInfo.CabOrientation == Direction.Forward ? 0 : MonitorHeight;
            int itemLocationWS = ItemLocationToRow(position, position);
            ChangeLabelAt(labels, itemLocationWS, (ListLabel dataCol) =>
            {
                dataCol.TrackCol = Symbols.EyeWS;
                dataCol.TrackColItem = Sprites.EyeSprite;
                return dataCol;
            });
        }

        /// <summary>
        /// Draw distance markers on the ListLabel list.
        /// </summary>
        /// <param name="labels">The list of ListLabels.</param>
        /// <param name="distanceFactor"></param>
        /// <param name="zeroPoint"></param>
        /// <param name="numberOfMarkers"></param>
        /// <param name="direction">The direction to draw markers in.</param>
        /// <returns>The computed interval between markers.</returns>
        private static float DrawDistanceMarkers(List<ListLabel> labels, float distanceFactor, int zeroPoint, int numberOfMarkers, TrainDirection direction, bool useMetric)
        {
            float maxDistanceD = (float)Size.Length.FromM(MaximumDistanceM, useMetric);
            float markerIntervalD = maxDistanceD / numberOfMarkers;
            float roundingValue = RoundingValues
                .Where((KeyValuePair<float, float> pair) => pair.Key == 0f || markerIntervalD > pair.Key)
                .Last()
                .Value;

            // From display back to meter
            float markerIntervalM = (float)Size.Length.ToM(Convert.ToInt32(markerIntervalD / roundingValue) * roundingValue, useMetric);

            IEnumerable<int> imarkers = Enumerable.Range(1, numberOfMarkers + 1)
                .Where((int ipos) => markerIntervalM * ipos < MaximumDistanceM);
            foreach (int ipos in imarkers)
            {
                float actDistanceM = markerIntervalM * ipos;
                int itemOffset = Convert.ToInt32(actDistanceM * distanceFactor);
                int itemLocationWS = ItemLocationToRow(zeroPoint, zeroPoint + itemOffset * (direction == TrainDirection.Forward ? -1 : 1));
                ChangeLabelAt(labels, itemLocationWS, (ListLabel dataCol) =>
                {
                    dataCol.DistCol = FormatStrings.FormatDistanceDisplay(actDistanceM, useMetric);
                    return dataCol;
                });
            }

            return markerIntervalM;
        }

        private static void DrawTrackItems(List<ListLabel> labels, IEnumerable<TrainPathItem> itemList, int zeroPoint, float distanceFactor, float markerIntervalM, TrainDirection direction, bool useMetric)
        {
            TrackItem MakeTrackItem(TrainPathItem item)
            {
                switch (item.ItemType)
                {
                    case TrainPathItemType.Authority:
                        return new TrackAuthorityItem(item, direction);
                    case TrainPathItemType.Signal:
                        return new TrackSignalForwardItem(item, useMetric);
                    case TrainPathItemType.SpeedSignal:
                        return new TrackSpeedpostItem(item, useMetric);
                    case TrainPathItemType.Speedpost:
                        return new TrackSpeedpostItem(item, useMetric);
                    case TrainPathItemType.Station:
                        return new TrackStationItem(item);
                    case TrainPathItemType.WaitingPoint:
                        return new TrackWaitingPointItem(item);
                    case TrainPathItemType.Milepost:
                        return new TrackMilePostItem(item);
                    case TrainPathItemType.FacingSwitch:
                        return new TrackSwitchItem(item);
                    case TrainPathItemType.Reversal:
                        return new TrackReversalItem(item);
                    default:
                        return new TrackUnknownItem();
                }
            }
            var trackItems = new List<TrackItem>(itemList
                .Select(MakeTrackItem)
                .Where((TrackItem item) => item.Render)
                .Where((TrackItem item) => (item.Item.DistanceToTrainM < MaximumDistanceM - TextSpacing / distanceFactor)
                    || (item.Item.ItemType == TrainPathItemType.Signal && item.Item.DistanceToTrainM > markerIntervalM && item.Item.SignalState != TrackMonitorSignalAspect.Stop)));
            // Keep a pointer to the next available row. If a track item conflicts with a previously placed one, bump it to the next row.
            var nextLocations = new Dictionary<TrackItemColumn, int>();
            foreach (TrackItem trackItem in trackItems)
            {
                int itemOffset = (int)(trackItem.Item.DistanceToTrainM * distanceFactor);
                int itemLocationWS;
                ColumnAttribute columnAttribute = Attribute.GetCustomAttributes(trackItem.GetType())
                    .Where((Attribute attr) => attr is ColumnAttribute)
                    .Cast<ColumnAttribute>()
                    .FirstOrDefault();
                TrackItemColumn column = columnAttribute == null ? TrackItemColumn.None : columnAttribute.Column;
                if (!nextLocations.TryGetValue(column, out int nextLocationWS))
                    nextLocationWS = zeroPoint;
                if (direction == TrainDirection.Forward)
                {
                    itemLocationWS = Math.Min(nextLocationWS, ItemLocationToRow(zeroPoint, zeroPoint - itemOffset));
                    // Signal at top
                    if (trackItem.Item.ItemType == TrainPathItemType.Signal && trackItem.Item.SignalState != TrackMonitorSignalAspect.Stop && trackItem.Item.DistanceToTrainM > MaximumDistanceM)
                    {
                        ChangeLabelAt(labels, itemLocationWS, (ListLabel dataCol) =>
                        {
                            dataCol.DistCol = FormatStrings.FormatDistance(trackItem.Item.DistanceToTrainM, useMetric);
                            return dataCol;
                        });
                    }
                    nextLocationWS = itemLocationWS - 1;
                }
                else
                {
                    itemLocationWS = ItemLocationToRow(zeroPoint, Math.Max(nextLocationWS, zeroPoint + itemOffset));
                    nextLocationWS = itemLocationWS + 1;
                }
                nextLocations[column] = nextLocationWS;
                if (itemLocationWS < 0 || itemLocationWS >= TrackLength)
                    continue;
                ChangeLabelAt(labels, itemLocationWS, trackItem.TransformLabel);
            }

            // Count down the distance to the closest track item.
            TrackItem closestTrackItem = trackItems
                .Where((TrackItem item) => item.ShowDistance)
                .FirstOrDefault();
            if (closestTrackItem != null && closestTrackItem.Item.DistanceToTrainM < markerIntervalM)
            {
                int itemOffset = (int)(closestTrackItem.Item.DistanceToTrainM * distanceFactor);
                int itemLocationWS = ItemLocationToRow(zeroPoint, zeroPoint + itemOffset * (direction == TrainDirection.Forward ? -1 : 1));
                ChangeLabelAt(labels, itemLocationWS, (ListLabel dataCol) =>
                {
                    dataCol.DistCol = FormatStrings.FormatDistance(closestTrackItem.Item.DistanceToTrainM, useMetric);
                    return dataCol;
                });
            }
        }

        /// <summary>
        /// Base class for an upcoming track item to display on the track monitor.
        /// </summary>
        private abstract class TrackItem
        {
            public readonly TrainPathItem Item;

            public TrackItem(TrainPathItem item)
            {
                Item = item;
            }

            public abstract bool Render { get; }

            public abstract bool ShowDistance { get; }

            public abstract ListLabel TransformLabel(ListLabel dataCol);
        }

        private enum TrackItemColumn
        {
            None,
            First,
            Track,
            Signal,
            Limit,
            Distance,
        }

        /// <summary>
        /// Associates each TrackItem class with the column it modifies, so that the
        /// <see cref="DrawTrackItems(List{ListLabel}, IEnumerable{Train.TrainObjectItem}, int, float, float, TrainDirection, bool)"/>
        /// method can stack items that conflict with each other.
        /// </summary>
        [AttributeUsage(AttributeTargets.Class)]
        private class ColumnAttribute : Attribute
        {
            public TrackItemColumn Column { get; }

            public ColumnAttribute(TrackItemColumn column)
            {
                Column = column;
            }
        }

        private sealed class TrackUnknownItem : TrackItem
        {
            public TrackUnknownItem() : base(null) { }

            public override bool Render => false;

            public override bool ShowDistance => false;

            public override ListLabel TransformLabel(ListLabel dataCol) => dataCol;
        }

        [Column(TrackItemColumn.Track)]
        private class TrackAuthorityItem : TrackItem
        {
            private readonly Rectangle Sprite;
            private readonly string Symbol;

            public TrackAuthorityItem(TrainPathItem item, TrainDirection direction) : base(item)
            {
                switch (item.AuthorityType)
                {
                    case EndAuthorityType.EndOfAuthority:
                    case EndAuthorityType.EndOfPath:
                    case EndAuthorityType.EndOfTrack:
                    case EndAuthorityType.ReservedSwitch:
                    case EndAuthorityType.Loop:
                        Sprite = Sprites.EndAuthoritySprite;
                        Symbol = Symbols.EndAuthorityWS;
                        Render = true;
                        break;
                    case EndAuthorityType.TrainAhead:
                        Sprite = direction == TrainDirection.Forward ? Sprites.OppositeTrainForwardSprite : Sprites.OppositeTrainBackwardSprite;
                        Symbol = direction == TrainDirection.Forward ? Symbols.OppositeTrainForwardWS : Symbols.OppositeTrainBackwardWS;
                        Render = true;
                        break;
                    default:
                        Render = false;
                        break;
                }
            }

            public override bool Render { get; }
            public override bool ShowDistance => true;

            public override ListLabel TransformLabel(ListLabel dataCol)
            {
                dataCol.TrackCol = Symbol;
                dataCol.TrackColItem = Sprite;
                return dataCol;
            }
        }

        [Column(TrackItemColumn.Signal)]
        private class TrackSignalForwardItem : TrackItem
        {
            private readonly bool Metric;

            public TrackSignalForwardItem(TrainPathItem item, bool useMetric) : base(item)
            {
                Metric = useMetric;
            }

            public override bool Render => true;
            public override bool ShowDistance => true;

            public override ListLabel TransformLabel(ListLabel dataCol)
            {
                if (Item.SignalState != TrackMonitorSignalAspect.Stop && Item.AllowedSpeedMpS > 0)
                    dataCol.LimitCol = $"{FormatStrings.FormatSpeedLimitNoUoM(Item.AllowedSpeedMpS, Metric)}{ColorCode[Color.White]}";
                dataCol.SignalCol = Symbols.SignalMarkersWebApi[Item.SignalState];
                dataCol.SignalColItem = Sprites.SignalMarkers[Item.SignalState];
                return dataCol;
            }
        }

        [Column(TrackItemColumn.Limit)]
        private class TrackSpeedpostItem : TrackItem
        {
            private readonly bool Metric;

            public TrackSpeedpostItem(TrainPathItem item, bool useMetric) : base(item)
            {
                Metric = useMetric;
            }

            public override bool Render => true;
            public override bool ShowDistance => true;

            public override ListLabel TransformLabel(ListLabel dataCol)
            {
                float allowedSpeedMpS = Math.Min(Item.AllowedSpeedMpS, (float)Simulator.Instance.Route.SpeedLimit);
                Color color;
                switch (Item.SpeedObjectType)
                {
                    case SpeedItemType.Standard:
                        color = Color.White;
                        break;
                    case SpeedItemType.TemporaryRestrictionStart:
                        color = Color.OrangeRed;
                        break;
                    default:
                        color = Color.LightGreen;
                        break;
                }
                dataCol.LimitCol = $"{FormatStrings.FormatSpeedLimitNoUoM(allowedSpeedMpS, Metric)}{ColorCode[color]}";
                return dataCol;
            }
        }

        [Column(TrackItemColumn.Track)]
        private class TrackStationItem : TrackItem
        {
            public TrackStationItem(TrainPathItem item) : base(item) { }

            public override bool Render => true;
            public override bool ShowDistance => false;

            public override ListLabel TransformLabel(ListLabel dataCol)
            {
                // NOTE: Mauricio's original version included lots more information here.
                dataCol.TrackColLeft = Symbols.StationLeftWS;
                dataCol.TrackColRight = Symbols.StationRightWS;
                return dataCol;
            }
        }

        [Column(TrackItemColumn.Track)]
        private class TrackWaitingPointItem : TrackItem
        {
            public TrackWaitingPointItem(TrainPathItem item) : base(item) { }

            public override bool Render => true;

            public override bool ShowDistance => true;

            public override ListLabel TransformLabel(ListLabel dataCol)
            {
                Color color = Item.Enabled ? Color.Yellow : Color.OrangeRed;
                dataCol.TrackCol = $"{Symbols.WaitingPointWS}{ColorCode[color]}";
                dataCol.TrackColItem = Sprites.WaitingPointSprite;
                return dataCol;
            }
        }

        [Column(TrackItemColumn.First)]
        private class TrackMilePostItem : TrackItem
        {
            public TrackMilePostItem(TrainPathItem item) : base(item) { }

            public override bool Render => true;

            public override bool ShowDistance => false;

            public override ListLabel TransformLabel(ListLabel dataCol)
            {
                dataCol.FirstCol = $"{Item.Miles}";
                return dataCol;
            }
        }

        [Column(TrackItemColumn.Track)]
        private class TrackSwitchItem : TrackItem
        {
            public TrackSwitchItem(TrainPathItem item) : base(item) { }

            public override bool Render => true;

            public override bool ShowDistance => true;

            public override ListLabel TransformLabel(ListLabel dataCol)
            {
                dataCol.TrackCol = Item.SwitchDivertsRight ? Symbols.RightArrowWS : Symbols.LeftArrowWS;
                dataCol.TrackColItem = Item.SwitchDivertsRight ? Sprites.RightArrowSprite : Sprites.LeftArrowSprite;
                return dataCol;
            }
        }

        [Column(TrackItemColumn.Track)]
        private class TrackReversalItem : TrackItem
        {
            public TrackReversalItem(TrainPathItem item) : base(item) { }

            public override bool Render => true;

            public override bool ShowDistance => true;

            public override ListLabel TransformLabel(ListLabel dataCol)
            {
                string symbol;
                Rectangle sprite;
                if (Item.Valid)
                {
                    Color color = Item.Enabled ? Color.LightGreen : Color.White;
                    symbol = $"{Symbols.ReversalWS}{ColorCode[color]}";
                    sprite = Sprites.ReversalSprite;
                }
                else
                {
                    symbol = Symbols.InvalidReversalWS;
                    sprite = Sprites.InvalidReversalSprite;
                }
                dataCol.TrackCol = symbol;
                dataCol.TrackColItem = sprite;
                return dataCol;
            }
        }

        private static void DrawOwnTrain(List<ListLabel> labels, TrainPosition trainPosition, int zeroObjectPointTop)
        {
            int itemLocationWs = ItemLocationToRow(zeroObjectPointTop, zeroObjectPointTop) + RowOffset;
            ChangeLabelAt(labels, itemLocationWs, (ListLabel dataCol) =>
            {
                dataCol.TrackCol = Symbols.TrainPositionWS[trainPosition];
                dataCol.TrackColItem = Sprites.TrainPositionSprite[trainPosition];
                return dataCol;
            });
        }

        private static void ChangeLabelAt(IList<ListLabel> labels, int index, Func<ListLabel, ListLabel> modifier)
        {
            index = Math.Max(index, 0); // Fix invalid row indices.
            if (index <= labels.Count)
            {
                var label = modifier(labels[index]);
                CheckLabel(ref label);
                labels[index] = label;
            }
            else
            {
                while (labels.Count < index)
                {
                    ListLabel empty = new ListLabel();
                    CheckLabel(ref empty);
                    labels.Add(empty);
                }
                var label = modifier(new ListLabel());
                CheckLabel(ref label);
                labels.Add(label);
            }
        }

        /// <summary>
        /// Translate itemLocation graphic value to equivalent row text position
        /// </summary>
        /// <param name="zeroPoint">The origin position.</param>
        /// <param name="itemLocation">The requested item position.</param>
        /// <returns>The translated row text position.</returns>
        private static int ItemLocationToRow(int zeroPoint, int itemLocation)
        {
            int Round(float x) => (int)Math.Round(x);
            switch (zeroPoint)
            {
                case 200: // Auto mode track zone only
                    return Round(MathHelper.Clamp(itemLocation * (11f / 200f), 0, 11));
                case 224: // Auto mode track + train zone
                    return Round(MathHelper.Clamp(itemLocation * (12f / 200f), 0, 12));
                case 320: // forwardsY
                case 240: // forwardsY
                case 0:   // backwardsY
                    return Round(MathHelper.Clamp(itemLocation * (16f / 240f), 0, 16));
                case 108: // Manual mode upper zone
                    return (int)MathHelper.Clamp(itemLocation * (6f / 93f), 0, 6);
                case 132:// lower zone
                    return MultiPlayerManager.IsMultiPlayer() ? Round(MathHelper.Clamp(itemLocation * (16.0f / 266.0f), 9, 16))// MultiPlayer mode
                        : Round(MathHelper.Clamp(itemLocation * (16f / 232f), 10, 16));// Manual mode
                default:
                    return 0;
            }
        }

        private static int BottomLabelRowLocation(int zeroPoint)
        {
            switch (zeroPoint)
            {
                case 200: // Auto mode track zone only
                    return 11;
                case 224: // Auto mode track + train zone
                    return 12;
                case 240: // forwardsY
                case 0:   // backwardsY
                    return 16;
                case 108: // Manual mode upper zone
                    return 6;
                case 132: // Manual mode lower zone
                    return 16;
                default:
                    return 0;
            }
        }
    }
}
