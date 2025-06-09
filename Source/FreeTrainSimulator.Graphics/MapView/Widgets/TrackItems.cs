
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Imported.Track;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    #region TrackItemBase
    internal abstract record TrackItemWidget : TrackItemBase, IDrawable<PointPrimitive>, INameValueInformationProvider
    {
        private protected static InformationDictionary debugInformation = new InformationDictionary() { ["Item Type"] = "Empty" };
        private protected static int debugInfoItemId;
        private protected static System.Drawing.Font font;
        internal protected readonly int TrackItemId;

        public virtual InformationDictionary DetailInfo
        {
            get
            {
                if (TrackItemId != debugInfoItemId)
                {
                    debugInformation.Clear();
                    debugInformation["Item Index"] = TrackItemId.ToString(CultureInfo.InvariantCulture);
                    AddInfoDetails(debugInformation);
                    debugInfoItemId = TrackItemId;
                }
                return debugInformation;
            }
        }

        protected abstract void AddInfoDetails(InformationDictionary infoHolder);

        public Dictionary<string, FormatOption> FormattingOptions { get; }

        public abstract void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1);

        public TrackItemWidget(TrackItem source) : base(source.Location)
        {
            Size = 3;
            TrackItemId = source.TrackItemId;
        }

        internal static void SetFont(System.Drawing.Font font)
        {
            TrackItemWidget.font = font;
        }

        public static List<TrackItemWidget> CreateRoadItems(IList<TrackItem> trackItems)
        {
            List<TrackItemWidget> result = new List<TrackItemWidget>();
            if (trackItems == null)
                return result;

            foreach (TrackItem trackItem in trackItems)
            {
                switch (trackItem)
                {
                    case RoadLevelCrossingItem roadLevelCrossingItem:
                        result.Add(new LevelCrossingTrackItem(roadLevelCrossingItem));
                        break;
                    case RoadCarSpawnerItem carSpawner:
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

        public static List<TrackItemWidget> CreateTrackItems(IReadOnlyList<TrackItem> trackItems, SignalConfigurationFile signalConfig, TrackDB trackDb, IReadOnlyList<TrackSegmentSection> trackNodeSegments)
        {
            List<TrackItemWidget> result = new List<TrackItemWidget>();
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
                        result.Add(speedPostItem.IsMilePost ? new MilePostTrackItem(speedPostItem, trackNodeSegments[trackItemNodes[speedPostItem.TrackItemId].Index]) : 
                            new SpeedPostTrackItem(speedPostItem, trackNodeSegments[trackItemNodes[speedPostItem.TrackItemId].Index]));
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
                        bool normalSignal = signalConfig.SignalTypes.TryGetValue(signalItem.SignalType, out SignalType signalType) && signalType.FunctionType == SignalFunction.Normal;
                        result.Add(new SignalTrackItem(signalItem, trackNodeSegments[trackItemNodes[signalItem.TrackItemId].Index], normalSignal));
                        break;
                    case CrossoverItem crossOverItem:
                        result.Add(new CrossOverTrackItem(crossOverItem));
                        break;
                    case RoadCarSpawnerItem carSpawner:
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
    internal record CrossOverTrackItem : TrackItemWidget
    {
        public CrossOverTrackItem(CrossoverItem source) : base(source)
        {
            Size = 4f;
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = WidgetDrawingOptions<CrossOverTrackItem>.Colors[colorVariation];
            scaleFactor *= WidgetDrawingOptions<JunctionNode>.ScaleFactor;

            contentArea.BasicShapes.DrawTexture(contentArea.Scale > 4 ? BasicTextureType.Ring : BasicTextureType.RingBold, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
        }

        protected override void AddInfoDetails(InformationDictionary infoHolder)
        {
            infoHolder["Item Type"] = "CrossOver";
        }
    }

    #endregion

    #region CarSpawnerTrackItem
    internal record CarSpawnerTrackItem : TrackItemWidget
    {
        public CarSpawnerTrackItem(RoadCarSpawnerItem source) : base(source)
        {
            Size = 5f;
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            contentArea.BasicShapes.DrawTexture(BasicTextureType.CarSpawner, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }
        protected override void AddInfoDetails(InformationDictionary infoHolder)
        {
            infoHolder["Item Type"] = "Car Spawner";
        }
    }

    #endregion

    #region EmptyTrackItem
    internal record EmptyTrackItem : TrackItemWidget
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

        protected override void AddInfoDetails(InformationDictionary infoHolder)
        {
            infoHolder["Item Type"] = "Empty";
        }
    }

    #endregion

    #region SidingTrackItem
    internal record SidingTrackItem : TrackItemWidget
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
            Color drawColor = WidgetDrawingOptions<SidingTrackItem>.Colors[colorVariation];
            OutlineRenderOptions outlineRenderOptions = WidgetDrawingOptions<SidingTrackItem>.OutlineRenderOptions;
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
            contentArea.DrawText(in Location, drawColor, SidingName, font, Vector2.One, 0, HorizontalAlignment.Left, VerticalAlignment.Top, outlineRenderOptions);
        }

        protected override void AddInfoDetails(InformationDictionary infoHolder)
        {
            infoHolder["Item Type"] = "Siding";
            infoHolder["Name"] = SidingName;
            infoHolder["Linked Id"] = LinkedId.ToString(CultureInfo.InvariantCulture);
        }
    }
    #endregion

    #region PlatformTrackItem
    internal record PlatformTrackItem : TrackItemWidget
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
            Color drawColor = WidgetDrawingOptions<PlatformTrackItem>.Colors[colorVariation];
            OutlineRenderOptions outlineRenderOptions = WidgetDrawingOptions<PlatformTrackItem>.OutlineRenderOptions;
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Platform, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
            contentArea.DrawText(Location, drawColor, PlatformName, font, Vector2.One, 0, HorizontalAlignment.Left, VerticalAlignment.Top, outlineRenderOptions);
            contentArea.DrawText(Location, drawColor, StationName, font, Vector2.One, 0, HorizontalAlignment.Left, VerticalAlignment.Bottom, outlineRenderOptions);
        }

        protected override void AddInfoDetails(InformationDictionary infoHolder)
        {
            infoHolder["Item Type"] = "Platform";
            infoHolder["Name"] = PlatformName;
            infoHolder["Station"] = StationName;
            infoHolder["Linked Id"] = LinkedId.ToString(CultureInfo.InvariantCulture);
        }
    }
    #endregion

    #region SpeedPostTrackItem
    internal record SpeedPostTrackItem : TrackItemWidget
    {
        private readonly string speed;
        private readonly float angle;
        private readonly PointD textLocation;

        public SpeedPostTrackItem(SpeedPostItem source, TrackSegmentSection segmentSection) : base(source)
        {
            speed = source.ToString();
            TrackSegmentBase segment = TrackSegmentBase.SegmentBaseAt(Location, segmentSection.SectionSegments);
            angle = segment.DirectionAt(Location);
            bool reverse = Math.Abs(angle + source.Angle) > MathHelper.PiOver2;

            angle += reverse ? -MathHelper.PiOver2 : MathHelper.PiOver2;
            textLocation = Location + (new PointD(1f* (float)Math.Cos(angle), -1*(float)Math.Sin(angle)));
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Size = contentArea.Scale switch
            {
                double i when i < 0.5 => 30,
                double i when i < 0.75 => 15,
                double i when i < 1 => 12,
                double i when i < 5 => 8,
                double i when i < 10 => 5,
                double i when i < 20 => 2,
                _ => 1f,
            };

            scaleFactor *= WidgetDrawingOptions<SpeedPostTrackItem>.ScaleFactor;

            Color drawColor = WidgetDrawingOptions<SpeedPostTrackItem>.Colors[colorVariation];
            OutlineRenderOptions outlineRenderOptions = WidgetDrawingOptions<SpeedPostTrackItem>.OutlineRenderOptions;
            contentArea.BasicShapes.DrawTexture(BasicTextureType.ArrowedIndicator, contentArea.WorldToScreenCoordinates(in Location), angle, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
            contentArea.DrawText(textLocation, drawColor, speed, font, Vector2.One, 0, HorizontalAlignment.Center, VerticalAlignment.Center, outlineRenderOptions);
        }

        protected override void AddInfoDetails(InformationDictionary infoHolder)
        {
            infoHolder["Item Type"] = "Speed Post";
            // TODO 20250603 show more of the SpeedPostItem properties (direction, number/dot)
            infoHolder["Speed"] = speed;
        }

        public static void UpdateTrackWidthRatio(bool downscale)
        {
            WidgetDrawingOptions<SpeedPostTrackItem>.ScaleFactor = downscale ? 1 : 2;
        }

    }
    #endregion

    #region MilePostTrackItem
    internal record MilePostTrackItem : TrackItemWidget
    {
        private readonly string distance;
        private readonly float angle;
        private readonly PointD textLocation;

        private static readonly Vector2 fontScale = new Vector2(0.9f, 0.9f);

        public MilePostTrackItem(SpeedPostItem source, TrackSegmentSection segmentSection) : base(source)
        {
            Size = 1f;
            distance = source.Distance.ToString(CultureInfo.CurrentCulture);
            TrackSegmentBase segment = TrackSegmentBase.SegmentBaseAt(Location, segmentSection.SectionSegments);
            angle = segment.DirectionAt(Location);

            if (Math.Abs(angle) > MathHelper.PiOver2)
                angle -= MathHelper.Pi;

            textLocation = Location + (new PointD(-1f * (float)Math.Cos(angle), -1 * (float)Math.Sin(angle)));
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {

            scaleFactor *= WidgetDrawingOptions<MilePostTrackItem>.ScaleFactor;

            Color drawColor = WidgetDrawingOptions<MilePostTrackItem>.Colors[colorVariation];
            OutlineRenderOptions outlineRenderOptions = WidgetDrawingOptions<MilePostTrackItem>.OutlineRenderOptions;

            contentArea.BasicShapes.DrawLine(4, drawColor, contentArea.WorldToScreenCoordinates(Location), contentArea.WorldToScreenSize(Size * scaleFactor), angle + MathHelper.PiOver2, contentArea.SpriteBatch);
            contentArea.BasicShapes.DrawLine(4, drawColor, contentArea.WorldToScreenCoordinates(Location), contentArea.WorldToScreenSize(Size * scaleFactor), angle + MathHelper.PiOver2 + MathHelper.Pi, contentArea.SpriteBatch);
            contentArea.DrawText(textLocation, drawColor, distance, font, fontScale, angle, HorizontalAlignment.Center, VerticalAlignment.Bottom, outlineRenderOptions);
        }

        protected override void AddInfoDetails(InformationDictionary infoHolder)
        {
            infoHolder["Item Type"] = "Mile Post";
            infoHolder["Distance"] = distance;
        }
    }
    #endregion

    #region HazardTrackItem
    internal record HazardTrackItem : TrackItemWidget
    {
        public HazardTrackItem(HazardItem source) : base(source)
        {
            Size = 7f;
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Hazard, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }

        protected override void AddInfoDetails(InformationDictionary infoHolder)
        {
            infoHolder["Item Type"] = "Hazard";
        }
    }
    #endregion

    #region PickupTrackItem
    internal record PickupTrackItem : TrackItemWidget
    {
        public PickupTrackItem(PickupItem source) : base(source)
        {
            Size = 7f;
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Pickup, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }

        protected override void AddInfoDetails(InformationDictionary infoHolder)
        {
            infoHolder["Item Type"] = "Pickup";
        }
    }
    #endregion

    #region LevelCrossingTrackItem
    internal record LevelCrossingTrackItem : TrackItemWidget
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

        protected override void AddInfoDetails(InformationDictionary infoHolder)
        {
            infoHolder["Item Type"] = "Level Crossing";
        }
    }
    #endregion

    #region SoundRegionTrackItem
    internal record SoundRegionTrackItem : TrackItemWidget
    {
        public SoundRegionTrackItem(SoundRegionItem source) : base(source)
        {
            Size = 5f;
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            contentArea.BasicShapes.DrawTexture(BasicTextureType.Sound, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }

        protected override void AddInfoDetails(InformationDictionary infoHolder)
        {
            infoHolder["Item Type"] = "Sound Region";
        }
    }
    #endregion

    #region SignalTrackItem
    internal record SignalTrackItem : TrackItemWidget
    {
        private readonly float angle;
        internal readonly bool Normal = true;
        private readonly string signalType;

        public ISignal Signal { get; }

        public SignalTrackItem(SignalItem source, TrackSegmentSection segments, bool normalSignal) : base(source)
        {
            if (source.SignalObject > -1)
                Signal = RuntimeData.Instance.RuntimeReferenceResolver?.SignalById(source.SignalObject);
            signalType = source.SignalType;
            Size = 2f;

            TrackSegmentBase segment = TrackSegmentBase.SegmentBaseAt(Location, segments.SectionSegments);
            angle = segment?.DirectionAt(Location) + (source.Direction == TrackDirection.Reverse ? -MathHelper.PiOver2 : MathHelper.PiOver2) ?? 0;

            Normal = normalSignal;
            Vector3 shiftedLocation = source.Location.Location +
                    (0.1f * new Vector3((float)Math.Cos(angle), 0f, -(float)Math.Sin(angle)));
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

        protected override void AddInfoDetails(InformationDictionary infoHolder)
        {
            infoHolder["Item Type"] = "Signal";
            infoHolder["Signal Type"] = Normal ? "Normal" : "Other";
            infoHolder["Signal Name"] = signalType;
        }
    }
    #endregion
}
