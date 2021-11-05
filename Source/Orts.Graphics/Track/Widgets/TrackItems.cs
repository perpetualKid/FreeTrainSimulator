
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
using Orts.Graphics.Track.Shapes;

namespace Orts.Graphics.Track.Widgets
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

        public static List<TrackItemBase> Create(TrackItem[] trackItems)
        {
            List<TrackItemBase> result = new List<TrackItemBase>();
            if (trackItems == null)
                return result;
            Dictionary<uint, SidingTrackItem> sidingItems = new Dictionary<uint, SidingTrackItem>();

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

        public static List<TrackItemBase> Create(TrackItem[] trackItems, SignalConfigurationFile signalConfig, TrackDB trackDb, Dictionary<uint, List<TrackSegment>> trackNodeSegments)
        {
            List<TrackItemBase> result = new List<TrackItemBase>();
            if (trackItems == null)
                return result;
            Dictionary<uint, SidingTrackItem> sidingItems = new Dictionary<uint, SidingTrackItem>();

            TrackVectorNode[] trackItemNodes = new TrackVectorNode[trackItems.Length];

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
            foreach (TrackItem trackItem in trackItems)
            {
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
                        result.Add(new SignalTrackItem(signalItem, signalConfig, trackItemNodes, trackNodeSegments));
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

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            Color drawColor = GetColor<CrossOverTrackItem>(colorVariation);
            BasicShapes.DrawTexture(BasicTextureType.Ring, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), drawColor, contentArea.SpriteBatch);
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

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawTexture(BasicTextureType.CarSpawner, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
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

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            Color drawColor = Color.Red;
            BasicShapes.DrawTexture(BasicTextureType.RingCrossed, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), drawColor, contentArea.SpriteBatch);
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
        internal readonly uint Id;
        private readonly uint linkedId;
        private bool drawName;
        private bool shouldDrawName;

        public SidingTrackItem(SidingItem source) : base(source)
        {
            sidingName = source.ItemName;
            Id = source.TrackItemId;
            linkedId = source.LinkedSidingId;
            Size = 5f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            Color fontColor = GetColor<SidingTrackItem>(colorVariation);
            Color drawColor = GetColor<SidingTrackItem>(colorVariation.Next().Next());
            BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), drawColor, contentArea.SpriteBatch);
            if (drawName && shouldDrawName)
                TextShape.DrawString(contentArea.WorldToScreenCoordinates(in location), fontColor, sidingName, font, Vector2.One, TextHorizontalAlignment.Left, TextVerticalAlignment.Top, SpriteEffects.None, contentArea.SpriteBatch);
        }

        internal override bool ShouldDraw(TrackViewerViewSettings setting)
        {
            shouldDrawName = (setting & TrackViewerViewSettings.SidingNames) == TrackViewerViewSettings.SidingNames;
            return (setting & TrackViewerViewSettings.Sidings) == TrackViewerViewSettings.Sidings;
        }

        /// <summary>
        /// Link siding items which belong together pairwise so text only appears once (otherwise text is mostly overlapping since siding items are too close to each other
        /// </summary>
        internal static List<SidingTrackItem> LinkSidingItems(Dictionary<uint, SidingTrackItem> sidingItems)
        {
            List<SidingTrackItem> result = new List<SidingTrackItem>();

            while (sidingItems.Count > 0)
            {
                uint sourceId = sidingItems.Keys.First();
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

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            Color fontColor = GetColor<PlatformTrackItem>(colorVariation);
            BasicShapes.DrawTexture(BasicTextureType.Platform, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
            if (shouldDrawName)
                TextShape.DrawString(contentArea.WorldToScreenCoordinates(in location), fontColor, platformName, font, Vector2.One, TextHorizontalAlignment.Left, TextVerticalAlignment.Top, SpriteEffects.None, contentArea.SpriteBatch);
            if (shouldDrawStationName)
                TextShape.DrawString(contentArea.WorldToScreenCoordinates(in location), fontColor, stationName, font, Vector2.One, TextHorizontalAlignment.Left, TextVerticalAlignment.Bottom, SpriteEffects.None, contentArea.SpriteBatch);
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

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
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
            BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), drawColor, contentArea.SpriteBatch);
            TextShape.DrawString(contentArea.WorldToScreenCoordinates(in location), fontColor, distance, font, Vector2.One, TextHorizontalAlignment.Center, TextVerticalAlignment.Center, SpriteEffects.None, contentArea.SpriteBatch);
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

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawTexture(BasicTextureType.Hazard, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
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

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawTexture(BasicTextureType.Pickup, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
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

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawTexture(BasicTextureType.LevelCrossing, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
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

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawTexture(BasicTextureType.Sound, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
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

        public SignalTrackItem(SignalItem source, SignalConfigurationFile signalConfig, TrackVectorNode[] trackItemNodes, Dictionary<uint, List<TrackSegment>> trackNodeSegments) : base(source)
        {
            Size = 7f;
            if (signalConfig.SignalTypes.ContainsKey(source.SignalType))
            {
                normal = signalConfig.SignalTypes[source.SignalType].FunctionType == SignalFunction.Normal;
            }

            PointD sourcelocation = PointD.FromWorldLocation(source.Location);
            double closest = double.MaxValue;
            TrackSegment closestSegment = null;

            TrackVectorNode vectorNode = trackItemNodes[source.TrackItemId];
            if (vectorNode != null)
            {
                foreach (TrackSegment segment in trackNodeSegments[vectorNode.Index])
                {
                    double currentDistance = sourcelocation.DistanceToLineSegmentSquared(segment.Location, segment.Vector);
                    if (currentDistance < closest)
                    {
                        closest = currentDistance;
                        closestSegment = segment;
                    }

                }
            }

            angle = MathHelper.WrapAngle((closestSegment?.Direction ?? 0) + MathHelper.PiOver2 + (source.Direction == Common.TrackDirection.Reverse ? MathHelper.TwoPi : MathHelper.Pi));

            Vector3 shiftedLocation = source.Location.Location +
                    0.1f * new Vector3((float)Math.Cos(angle), 0f, -(float)Math.Sin(angle));
            WorldLocation location = new WorldLocation(source.Location.TileX, source.Location.TileZ, shiftedLocation);
            base.location = PointD.FromWorldLocation(location);

        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawTexture(BasicTextureType.Signal, contentArea.WorldToScreenCoordinates(in Location), angle, contentArea.WorldToScreenSize(Size), false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }

        internal override bool ShouldDraw(TrackViewerViewSettings setting)
        {
            return normal ? (setting & TrackViewerViewSettings.Signals) == TrackViewerViewSettings.Signals : (setting & TrackViewerViewSettings.OtherSignals) == TrackViewerViewSettings.OtherSignals;
        }
    }
    #endregion
}
