
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Graphics.MapView.Shapes;
using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{
    #region TrackItemBase
    internal abstract class TrackItemBase : PointPrimitive, IDrawable<PointPrimitive>
    {
        private protected static System.Drawing.Font font;
        internal protected readonly int TrackItemId;

        public abstract void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1);

        public TrackItemBase(TrackItem source): base(source.Location)
        {
            Size = 3;
            TrackItemId = source.TrackItemId;
        }

        internal static void SetFont(System.Drawing.Font font)
        {
            TrackItemBase.font = font;
        }

        public static List<TrackItemBase> CreateRoadItems(IList<TrackItem> trackItems)
        {
            List<TrackItemBase> result = new List<TrackItemBase>();
            if (trackItems == null)
                return result;

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

            return result;
        }

        public static List<TrackItemBase> CreateTrackItems(IList<TrackItem> trackItems, SignalConfigurationFile signalConfig, TrackDB trackDb, IList<TrackSegmentSection> trackNodeSegments)
        {
            List<TrackItemBase> result = new List<TrackItemBase>();
            if (trackItems == null)
                return result;
            TrackVectorNode[] trackItemNodes = new TrackVectorNode[trackItems.Count];

            //linking TrackItems to TrackNodes
            foreach (TrackVectorNode trackVectorNode in trackDb.TrackNodes.VectorNodes)
            {
                if (trackVectorNode.TrackItemIndices?.Length > 0)
                {
                    foreach (int trackItemIndex in trackVectorNode.TrackItemIndices)
                    {
                        trackItemNodes[trackItemIndex] = trackVectorNode;
                    }
                }
            }
            foreach (TrackItem trackItem in trackItems)
            {
                if (trackItem.Location == WorldLocation.None)
                    continue;

                switch (trackItem)
                {
                    case SidingItem sidingItem:
                        result.Add(new SidingTrackItem(sidingItem, trackItemNodes));
                        break;
                    case PlatformItem platformItem:
                        result.Add(new PlatformTrackItem(platformItem, trackItemNodes));
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
                        result.Add(new SignalTrackItem(signalItem, trackNodeSegments[trackItemNodes[signalItem.TrackItemId].Index], normalSignal));
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
            return result;
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

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = this.GetColor<CrossOverTrackItem>(colorVariation);
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Ring, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
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

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            contentArea.BasicShapes.DrawTexture(BasicTextureType.CarSpawner, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
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

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = Color.Red;
            contentArea.BasicShapes.DrawTexture(BasicTextureType.RingCrossed, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
        }
    }

    #endregion

    #region SidingTrackItem
    internal class SidingTrackItem : TrackItemBase
    {
        internal readonly string SidingName;
        internal readonly int LinkedId;

        internal TrackVectorNode TrackVectorNode;

        public SidingTrackItem(SidingItem source, TrackVectorNode[] trackItemNodes) : base(source)
        {
            TrackVectorNode = trackItemNodes[source.TrackItemId];
            SidingName = source.ItemName;
            LinkedId = source.LinkedSidingId;
            Size = 5f;
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = this.GetColor<SidingTrackItem>(colorVariation);
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
            contentArea.DrawText(in Location, drawColor, SidingName, font, Vector2.One, HorizontalAlignment.Left, VerticalAlignment.Top);
        }
    }
    #endregion

    #region PlatformTrackItem
    internal class PlatformTrackItem : TrackItemBase
    {
        internal readonly string PlatformName;
        internal readonly string StationName;
        internal readonly int LinkedId;

        internal TrackVectorNode TrackVectorNode;

        public PlatformTrackItem(PlatformItem source, TrackVectorNode[] trackItemNodes) :
            base(source)
        {
            TrackVectorNode = trackItemNodes[source.TrackItemId];
            PlatformName = source.ItemName;
            StationName = source.Station;
            LinkedId = source.LinkedPlatformItemId;
            Size = 7f;
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = this.GetColor<PlatformTrackItem>(colorVariation);
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Platform, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
            contentArea.DrawText(Location, drawColor, PlatformName, font, Vector2.One, HorizontalAlignment.Left, VerticalAlignment.Top);
            contentArea.DrawText(Location, drawColor, StationName, font, Vector2.One, HorizontalAlignment.Left, VerticalAlignment.Bottom);
        }
    }
    #endregion

    #region SpeedPostTrackItem
    internal class SpeedPostTrackItem : TrackItemBase
    {
        private readonly string distance;
        internal readonly bool MilePost;

        public SpeedPostTrackItem(SpeedPostItem source) : base(source)
        {
            distance = source.Distance.ToString(CultureInfo.CurrentCulture);
            MilePost = source.IsMilePost;
            Size = 5f;
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor;
            Color drawColor;
            if (MilePost)
            {
                fontColor = this.GetColor<SpeedPostTrackItem>(colorVariation.Next());
                drawColor = this.GetColor<SpeedPostTrackItem>(colorVariation);

            }
            else
            {
                fontColor = this.GetColor<SpeedPostTrackItem>(colorVariation.Next());
                drawColor = this.GetColor<SpeedPostTrackItem>(colorVariation);
            }
            // TODO 20210117 show more of the SpeedPostItem properties (direction, number/dot)
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
            contentArea.DrawText(Location, fontColor, distance, font, Vector2.One, HorizontalAlignment.Center, VerticalAlignment.Center);
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

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Hazard, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
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

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Pickup, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }
    }
    #endregion

    #region LevelCrossingTrackItem
    internal class LevelCrossingTrackItem : TrackItemBase
    {
        internal readonly bool RoadLevelCrossing;

        public LevelCrossingTrackItem(LevelCrossingItem source) : base(source)
        {
            Size = 5f;
        }

        public LevelCrossingTrackItem(RoadLevelCrossingItem source) : base(source)
        {
            RoadLevelCrossing = true;
            Size = 5f;
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            contentArea.BasicShapes.DrawTexture(BasicTextureType.LevelCrossing, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
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

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Sound, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }
    }
    #endregion

    #region PathJunctionTrackItem
    internal class PathJunctionTrackItem : TrackItemBase
    {
        public PathJunctionTrackItem(TrackItem source) : base(source)
        {
            Size = 7f;
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Circle, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }
    }
    #endregion

    #region PathReversalTrackItem
    internal class PathReversalTrackItem : TrackItemBase
    {
        public PathReversalTrackItem(TrackItem source) : base(source)
        {
            Size = 7f;
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Circle, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }
    }
    #endregion

    #region PathEndTrackItem
    internal class PathEndTrackItem : TrackItemBase
    {
        public PathEndTrackItem(TrackItem source) : base(source)
        {
            Size = 7f;
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Circle, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }
    }
    #endregion

    #region PathEndTrackItem
    internal class PathIntermediateTrackItem : TrackItemBase
    {
        public PathIntermediateTrackItem(TrackItem source) : base(source)
        {
            Size = 7f;
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Circle, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }
    }

    #endregion

    #region SignalTrackItem
    internal class SignalTrackItem : TrackItemBase
    {
        private readonly float angle;
        internal readonly bool Normal = true;

        public ISignal Signal { get; }

        public SignalTrackItem(SignalItem source, TrackSegmentSection segments, bool normalSignal) : base(source)
        {
            if (source.SignalObject > -1)
            {
                Signal = RuntimeData.Instance.RuntimeReferenceResolver?.SignalById(source.SignalObject);
            }
            Size = 2f;

            TrackSegmentBase segment = TrackSegmentBase.SegmentBaseAt(Location, segments.SectionSegments);
            angle = segment?.DirectionAt(Location) + (source.Direction == TrackDirection.Reverse ? -MathHelper.PiOver2 : MathHelper.PiOver2) ?? 0;

            Normal = normalSignal;
            Vector3 shiftedLocation = source.Location.Location +
                    0.1f * new Vector3((float)Math.Cos(angle), 0f, -(float)Math.Sin(angle));
            SetLocation(new WorldLocation(source.Location.TileX, source.Location.TileZ, shiftedLocation));
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
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

            contentArea.BasicShapes.DrawTexture(signalState, contentArea.WorldToScreenCoordinates(in Location), angle, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }
    }
    #endregion
}
