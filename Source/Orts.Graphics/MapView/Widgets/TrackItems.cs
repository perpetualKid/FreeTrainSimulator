
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Graphics.DrawableComponents;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    #region TrackItemBase
    internal abstract class TrackItemBase : PointWidget
    {
        private protected static System.Drawing.Font font;

        public TrackItemBase(TrackItem source)
        {
            Size = 3;
            ref readonly WorldLocation location = ref source.Location;
            base.location = PointD.FromWorldLocation(location);
            base.tile = new Tile(location.TileX, location.TileZ);
        }

        internal static void SetFont(System.Drawing.Font font)
        {
            TrackItemBase.font = font;
        }

        public static List<TrackItemBase> Create(IList<TrackItem> trackItems)
        {
            List<TrackItemBase> result = new List<TrackItemBase>();
            if (trackItems == null)
                return result;
            Dictionary<int, SidingTrackItem> sidingItems = new Dictionary<int, SidingTrackItem>();

            foreach (TrackItem trackItem in trackItems)
            {
                switch (trackItem)
                {
                    case RoadLevelCrossingItem roadLevelCrossingItem:
                        result.Add(new LevelCrossingTrackItem(roadLevelCrossingItem));
                        break;
                    case RoadCarSpawner carSpawner:
                        result.Add(new CarSpawnerTrackItem(carSpawner));
                        break;
                    case EmptyItem emptyItem:
                        result.Add(new EmptyTrackItem(emptyItem));
                        break;
                    default:
                        Trace.TraceWarning($"{trackItem.GetType().Name} not supported for Road Track Items");
                        break;
                }
            }

            result.AddRange(SidingTrackItem.LinkSidingItems(sidingItems));

            return result;
        }

        public static List<TrackItemBase> Create(IList<TrackItem> trackItems, SignalConfigurationFile signalConfig, TrackDB trackDb, bool signalsOnly = false)
        {
            List<TrackItemBase> result = new List<TrackItemBase>();
            if (trackItems == null)
                return result;
            Dictionary<int, SidingTrackItem> sidingItems = new Dictionary<int, SidingTrackItem>();

            TrackVectorNode[] trackItemNodes = new TrackVectorNode[trackItems.Count];

            foreach (TrackNode node in trackDb.TrackNodes)
            {
                if (node is TrackVectorNode trackVectorNode && trackVectorNode.TrackItemIndices?.Length > 0)
                {
                    foreach (int trackItemIndex in trackVectorNode.TrackItemIndices)
                    {
                        trackItemNodes[trackItemIndex] = trackVectorNode;
                    }
                }
            }
            foreach (TrackItem trackItem in signalsOnly ? trackItems.Where(t => t is SignalItem) : trackItems)
            {
                if (trackItem.Location == WorldLocation.None)
                    continue;

                switch (trackItem)
                {
                    case SidingItem sidingItem:
                        SidingTrackItem trackSidingItem = new SidingTrackItem(sidingItem);
                        sidingItems.Add(trackSidingItem.Id, trackSidingItem);
                        break;
                    case PlatformItem platformItem:
                        result.Add(new PlatformTrackItem(platformItem));
                        break;
                    case SpeedPostItem speedPostItem:
                        result.Add(new SpeedPostTrackItem(speedPostItem));
                        break;
                    case HazardItem hazardItem:
                        result.Add(new HazardTrackItem(hazardItem));
                        break;
                    case PickupItem pickupItem:
                        result.Add(new PickupTrackItem(pickupItem));
                        break;
                    case LevelCrossingItem levelCrossingItem:
                        result.Add(new LevelCrossingTrackItem(levelCrossingItem));
                        break;
                    case RoadLevelCrossingItem roadLevelCrossingItem: // road level crossings are not really useful and no route seems to contain them, but we'll just treat them as LevelCrossings
                        result.Add(new LevelCrossingTrackItem(roadLevelCrossingItem));
                        break;
                    case SoundRegionItem soundRegionItem:
                        result.Add(new SoundRegionTrackItem(soundRegionItem));
                        break;
                    case SignalItem signalItem:
                        bool normalSignal = (signalConfig.SignalTypes.TryGetValue(signalItem.SignalType, out SignalType signalType) && signalType.FunctionType == SignalFunction.Normal);
                        if (!signalsOnly || (signalsOnly && normalSignal))
                            result.Add(new SignalTrackItem(signalItem, trackItemNodes, normalSignal));
                        break;
                    case CrossoverItem crossOverItem:
                        result.Add(new CrossOverTrackItem(crossOverItem));
                        break;
                    case RoadCarSpawner carSpawner:
                        result.Add(new CarSpawnerTrackItem(carSpawner));
                        break;
                    case EmptyItem emptyItem:
                        result.Add(new EmptyTrackItem(emptyItem));
                        break;
                    default:
                        Trace.TraceWarning($"{trackItem.GetType().Name} not supported for Track Items");
                        break;
                }
            }

            result.AddRange(SidingTrackItem.LinkSidingItems(sidingItems));

            return result;
        }

        internal virtual bool ShouldDraw(TrackViewerViewSettings setting)
        {
            return true;
        }
    }
    #endregion

    #region CrossOverTrackItem
    internal class CrossOverTrackItem : TrackItemBase
    {
        public CrossOverTrackItem(CrossoverItem source) : base(source)
        {
            Size = 5f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<CrossOverTrackItem>(colorVariation);
            BasicShapes.DrawTexture(BasicTextureType.Ring, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
        }

        internal override bool ShouldDraw(TrackViewerViewSettings setting)
        {
            return (setting & TrackViewerViewSettings.CrossOvers) == TrackViewerViewSettings.CrossOvers;
        }

    }

    #endregion

    #region CarSpawnerTrackItem
    internal class CarSpawnerTrackItem : TrackItemBase
    {
        public CarSpawnerTrackItem(RoadCarSpawner source) : base(source)
        {
            Size = 5f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            BasicShapes.DrawTexture(BasicTextureType.CarSpawner, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }

        internal override bool ShouldDraw(TrackViewerViewSettings setting)
        {
            return (setting & TrackViewerViewSettings.CarSpawners) == TrackViewerViewSettings.CarSpawners;
        }
    }

    #endregion

    #region EmptyTrackItem
    internal class EmptyTrackItem : TrackItemBase
    {
        public EmptyTrackItem(EmptyItem source) : base(source)
        {
            Size = 5f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = Color.Red;
            BasicShapes.DrawTexture(BasicTextureType.RingCrossed, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
        }

        internal override bool ShouldDraw(TrackViewerViewSettings setting)
        {
            return false;
        }

    }

    #endregion

    #region SidingTrackItem
    internal class SidingTrackItem : TrackItemBase
    {
        private readonly string sidingName;
        internal readonly int Id;
        private readonly int linkedId;
        private bool drawName;
        private bool shouldDrawName;

        public SidingTrackItem(SidingItem source) : base(source)
        {
            sidingName = source.ItemName;
            Id = (int)source.TrackItemId;
            linkedId = source.LinkedSidingId;
            Size = 5f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor = GetColor<SidingTrackItem>(colorVariation);
            Color drawColor = GetColor<SidingTrackItem>(colorVariation.Next().Next());
            BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
            if (drawName && shouldDrawName)
                TextShape.DrawString(contentArea.WorldToScreenCoordinates(in location), fontColor, sidingName, font, Vector2.One, HorizontalAlignment.Left, VerticalAlignment.Top, SpriteEffects.None, contentArea.SpriteBatch);
        }

        internal override bool ShouldDraw(TrackViewerViewSettings setting)
        {
            shouldDrawName = (setting & TrackViewerViewSettings.SidingNames) == TrackViewerViewSettings.SidingNames;
            return (setting & TrackViewerViewSettings.Sidings) == TrackViewerViewSettings.Sidings;
        }

        /// <summary>
        /// Link siding items which belong together pairwise so text only appears once (otherwise text is mostly overlapping since siding items are too close to each other
        /// </summary>
        internal static List<SidingTrackItem> LinkSidingItems(Dictionary<int, SidingTrackItem> sidingItems)
        {
            List<SidingTrackItem> result = new List<SidingTrackItem>();

            while (sidingItems.Count > 0)
            {
                int sourceId = sidingItems.Keys.First();
                SidingTrackItem source = sidingItems[sourceId];
                sidingItems.Remove(sourceId);
                if (sidingItems.TryGetValue(source.linkedId, out SidingTrackItem target))
                {
                    if (target.linkedId != source.Id)
                    {
                        Trace.TraceWarning($"Siding Item Pair has inconsistent linking from Source Id {source.Id} to target {source.linkedId} vs Target id {target.Id} to source {target.linkedId}.");
                    }
                    sidingItems.Remove(target.Id);
                    result.Add(target);
                    source.drawName = true;
                    // TODO 20210115 maybe resulting location of text should be in the middle between the two linked siding items
                }
                else
                {
                    Trace.TraceWarning($"Linked Siding Item {source.linkedId} for Siding Item {source.Id} not found.");
                }
                result.Add(source);
            }

            return result;
        }
    }
    #endregion

    #region PlatformTrackItem
    internal class PlatformTrackItem : TrackItemBase
    {
        private readonly string platformName;
        private readonly string stationName;
        private bool shouldDrawName;
        private bool shouldDrawStationName;

        public PlatformTrackItem(PlatformItem source) :
            base(source)
        {
            platformName = source.ItemName;
            stationName = source.Station;
            Size = 7f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor = GetColor<PlatformTrackItem>(colorVariation);
            BasicShapes.DrawTexture(BasicTextureType.Platform, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
            if (shouldDrawName)
                TextShape.DrawString(contentArea.WorldToScreenCoordinates(in location), fontColor, platformName, font, Vector2.One, HorizontalAlignment.Left, VerticalAlignment.Top, SpriteEffects.None, contentArea.SpriteBatch);
            if (shouldDrawStationName)
                TextShape.DrawString(contentArea.WorldToScreenCoordinates(in location), fontColor, stationName, font, Vector2.One, HorizontalAlignment.Left, VerticalAlignment.Bottom, SpriteEffects.None, contentArea.SpriteBatch);
        }

        internal override bool ShouldDraw(TrackViewerViewSettings setting)
        {
            shouldDrawName = (setting & TrackViewerViewSettings.PlatformNames) == TrackViewerViewSettings.PlatformNames;
            shouldDrawStationName = (setting & TrackViewerViewSettings.PlatformStations) == TrackViewerViewSettings.PlatformStations;
            return (setting & TrackViewerViewSettings.Platforms) == TrackViewerViewSettings.Platforms;
        }
    }
    #endregion

    #region SpeedPostTrackItem
    internal class SpeedPostTrackItem : TrackItemBase
    {
        private readonly string distance;
        private readonly bool milePost;

        public SpeedPostTrackItem(SpeedPostItem source) : base(source)
        {
            distance = source.Distance.ToString(CultureInfo.CurrentCulture);
            milePost = source.IsMilePost;
            Size = 5f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor;
            Color drawColor;
            if (milePost)
            {
                fontColor = GetColor<SpeedPostTrackItem>(colorVariation.Next());
                drawColor = GetColor<SpeedPostTrackItem>(colorVariation);
            }
            else
            {
                fontColor = GetColor<SpeedPostTrackItem>(colorVariation);
                drawColor = GetColor<SpeedPostTrackItem>(colorVariation.Next().Next());
            }
            // TODO 20210117 show more of the SpeedPostItem properties (direction, number/dot)
            BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
            TextShape.DrawString(contentArea.WorldToScreenCoordinates(in location), fontColor, distance, font, Vector2.One, HorizontalAlignment.Center, VerticalAlignment.Center, SpriteEffects.None, contentArea.SpriteBatch);
        }

        internal override bool ShouldDraw(TrackViewerViewSettings setting)
        {
            return milePost ? (setting & TrackViewerViewSettings.MilePosts) == TrackViewerViewSettings.MilePosts : (setting & TrackViewerViewSettings.SpeedPosts) == TrackViewerViewSettings.SpeedPosts;
        }

    }
    #endregion

    #region HazardTrackItem
    internal class HazardTrackItem : TrackItemBase
    {
        public HazardTrackItem(HazardItem source) : base(source)
        {
            Size = 7f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            BasicShapes.DrawTexture(BasicTextureType.Hazard, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }

        internal override bool ShouldDraw(TrackViewerViewSettings setting)
        {
            return (setting & TrackViewerViewSettings.Hazards) == TrackViewerViewSettings.Hazards;
        }
    }
    #endregion

    #region PickupTrackItem
    internal class PickupTrackItem : TrackItemBase
    {
        public PickupTrackItem(PickupItem source) : base(source)
        {
            Size = 7f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            BasicShapes.DrawTexture(BasicTextureType.Pickup, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }

        internal override bool ShouldDraw(TrackViewerViewSettings setting)
        {
            return (setting & TrackViewerViewSettings.Pickups) == TrackViewerViewSettings.Pickups;
        }
    }
    #endregion

    #region LevelCrossingTrackItem
    internal class LevelCrossingTrackItem : TrackItemBase
    {
        private readonly bool roadLevelCrossing;

        public LevelCrossingTrackItem(LevelCrossingItem source) : base(source)
        {
            Size = 5f;
        }

        public LevelCrossingTrackItem(RoadLevelCrossingItem source) : base(source)
        {
            roadLevelCrossing = true;
            Size = 5f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            BasicShapes.DrawTexture(BasicTextureType.LevelCrossing, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }

        internal override bool ShouldDraw(TrackViewerViewSettings setting)
        {
            return roadLevelCrossing ? (setting & TrackViewerViewSettings.RoadCrossings) == TrackViewerViewSettings.RoadCrossings : (setting & TrackViewerViewSettings.LevelCrossings) == TrackViewerViewSettings.LevelCrossings;
        }

    }
    #endregion

    #region SoundRegionTrackItem
    internal class SoundRegionTrackItem : TrackItemBase
    {
        public SoundRegionTrackItem(SoundRegionItem source) : base(source)
        {
            Size = 5f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            BasicShapes.DrawTexture(BasicTextureType.Sound, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }

        internal override bool ShouldDraw(TrackViewerViewSettings setting)
        {
            return (setting & TrackViewerViewSettings.SoundRegions) == TrackViewerViewSettings.SoundRegions;
        }

    }
    #endregion

    #region SignalTrackItem
    internal class SignalTrackItem : TrackItemBase
    {
        private readonly float angle;
        private readonly bool normal = true;

        public ISignal Signal { get; }

        public SignalTrackItem(SignalItem source, TrackVectorNode[] trackItemNodes, bool normalSignal) : base(source)
        {
            if (source.SignalObject > -1)
            {
                Signal = RuntimeData.Instance.RuntimeReferenceResolver?.SignalById(source.SignalObject);
            }
            Size = 2f;

            normal = normalSignal;
            TrackVectorNode vectorNode = trackItemNodes[source.TrackItemId];
            angle = new Traveller(vectorNode, source.Location, (Direction)source.Direction).RotY;

            Vector3 shiftedLocation = source.Location.Location +
                    0.1f * new Vector3((float)Math.Cos(angle), 0f, -(float)Math.Sin(angle));
            WorldLocation location = new WorldLocation(source.Location.TileX, source.Location.TileZ, shiftedLocation);
            base.location = PointD.FromWorldLocation(location);
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            BasicTextureType signalState =
                contentArea.Scale switch
                {
                    double scale when scale < 3 => Signal?.State switch
                    {
                        SignalState.Clear => BasicTextureType.SignalDotGreen,
                        SignalState.Approach => BasicTextureType.SignalDotYellow,
                        SignalState.Lock => BasicTextureType.SignalDotRed,
                        _ => BasicTextureType.SignalSmall
                    },
                    double scale when scale < 10 => Signal?.State switch
                    {
                        SignalState.Clear => BasicTextureType.SignalSmallGreen,
                        SignalState.Approach => BasicTextureType.SignalSmallYellow,
                        SignalState.Lock => BasicTextureType.SignalSmallRed,
                        _ => BasicTextureType.SignalSmall
                    },
                    _ => Signal?.State switch
                    {
                        SignalState.Clear => BasicTextureType.SignalGreen,
                        SignalState.Approach => BasicTextureType.SignalYellow,
                        SignalState.Lock => BasicTextureType.SignalRed,
                        _ => BasicTextureType.Signal
                    },
                };

            Size = contentArea.Scale switch
            {
                double i when i < 0.5 => 30,
                double i when i < 0.75 => 15,
                double i when i < 1 => 10,
                double i when i < 3 => 7,
                double i when i < 5 => 5,
                double i when i < 8 => 4,
                _ => 3,
            };

            BasicShapes.DrawTexture(signalState, contentArea.WorldToScreenCoordinates(in Location), angle, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }

        internal override bool ShouldDraw(TrackViewerViewSettings setting)
        {
            return normal ? (setting & TrackViewerViewSettings.Signals) == TrackViewerViewSettings.Signals : (setting & TrackViewerViewSettings.OtherSignals) == TrackViewerViewSettings.OtherSignals;
        }
    }
    #endregion
}
