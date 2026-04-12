// COPYRIGHT 2014, 2018 by the Open Rails project.
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

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;

using TrackTraveller = FreeTrainSimulator.Runtime.Track.TrackTraveller;

namespace ORTS.TrackViewer.Drawing
{
    #region DrawableTrackItem (base class)
    /// <summary>
    /// This class represents all track items that are defined in the TrackDatabase (also for road) in a way that allows
    /// us to draw them in trackviewer. This is the base class, all real track items are supposed to be subclasses (because
    /// each has its own texture or symbol to draw, and also its own name.
    /// </summary>
    internal abstract class DrawableTrackItem
    {
        /// <summary>WorldLocation of the track item</summary>
        public WorldLocation WorldLocation { get; set; }
        /// <summary>Short description, name of the type of item</summary>
        public string Description { get; set; }
        /// <summary>Index of the original item (TrItemId)</summary>
        public int Index { get; private set; }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="originalTrItem">The original track item that we are representing for drawing</param>
        protected DrawableTrackItem(TrackItemBase originalTrItem)
        {
            Index = originalTrItem.TrackItemIndex;
            WorldLocation = originalTrItem.Location;
            Description = "unknown";
        }

        /// <summary>
        /// Factory method. This will return a proper subclass of DrawableTrackItem that represents the correct type of track item.
        /// </summary>
        /// <param name="originalTrItem">The original track item that needs to be represented while drawing</param>
        /// <returns>A drawable trackitem, with proper subclass</returns>
        public static DrawableTrackItem CreateDrawableTrItem(TrackItemBase originalTrItem)
        {
            if (originalTrItem is SignalTrackItem)          { return new DrawableSignalItem(originalTrItem); }
            if (originalTrItem is PlatformTrackItem)        { return new DrawablePlatformItem(originalTrItem); }
            if (originalTrItem is SidingTrackItem)          { return new DrawableSidingItem(originalTrItem); }
            if (originalTrItem is SpeedpostTrackItem)       { return new DrawableSpeedPostItem(originalTrItem); }
            if (originalTrItem is MilepostTrackItem)        { return new DrawableSpeedPostItem(originalTrItem); }
            if (originalTrItem is HazardTrackItem)          { return new DrawableHazardItem(originalTrItem); }
            if (originalTrItem is PickupTrackItem)          { return new DrawablePickupItem(originalTrItem); }
            if (originalTrItem is LevelCrossingTrackItem)   { return new DrawableLevelCrItem(originalTrItem); }
            if (originalTrItem is SoundRegionTrackItem)     { return new DrawableSoundRegionItem(originalTrItem); }
            if (originalTrItem is RoadLevelCrossingTrackItem) { return new DrawableRoadLevelCrItem(originalTrItem); }
            if (originalTrItem is CarSpawnerTrackItem)      { return new DrawableCarSpawnerItem(originalTrItem); }
            if (originalTrItem is CrossoverTrackItem)       { return new DrawableCrossoverItem(originalTrItem); }
            return new DrawableEmptyItem(originalTrItem);
        }

        /// <summary>
        /// Draw a single track item
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="colors">The colorscheme to use</param>
        /// <param name="drawAlways">Do we need to draw anyway, independent of settings?</param>
        /// <returns>true if the item has been drawn</returns>
        internal abstract bool Draw(DrawArea drawArea, ColorScheme colors, bool drawAlways);
    }
    #endregion

    #region DrawableSignalItem
    /// <summary>
    /// Represents a drawable signal
    /// </summary>
    internal sealed class DrawableSignalItem : DrawableTrackItem
    {
        /// <summary>direction (forward or backward the signal relative to the direction of the track</summary>
        private readonly TrackDirection direction;

        /// <summary>angle to draw the signal at</summary>
        private float angle;

        /// <summary>Is it a normal signal</summary>
        private bool isNormal;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="originalTrItem">The original track item that we are representing for drawing</param>
        public DrawableSignalItem(TrackItemBase originalTrItem)
            : base(originalTrItem)
        {
            Description = "signal";
            isNormal = true; // default value
            SignalTrackItem originalSignalItem = originalTrItem as SignalTrackItem;
            direction = originalSignalItem.Direction;
        }

        /// <summary>
        /// Sets whether this signal is a normal signal based on precomputed model data.
        /// </summary>
        public void SetNormalSignal(bool normalSignal)
        {
            isNormal = normalSignal;
        }

        /// <summary>
        /// Find the angle that the signal needs to be drawn at
        /// </summary>
        /// <param name="nodeIndex">Index of the TrackNode on which the signal actually is</param>
        public void FindAngle(int nodeIndex)
        {
            angle = 0;
            try
            {
                TrackTraveller? ttInit = TrackTraveller.InitializeTraveller(WorldLocation, nodeIndex, (TrackDirection)direction);
                if (ttInit is TrackTraveller signalTraveller)
                {
                    angle = signalTraveller.Heading;

                    // Shift signal a little bit to be able to distinguish backfacing from normal facing
                    Microsoft.Xna.Framework.Vector3 shiftedLocation = WorldLocation.Location + 
                        0.0001f * new Microsoft.Xna.Framework.Vector3((float) Math.Cos(angle), 0f, -(float) Math.Sin(angle));
                    WorldLocation = new WorldLocation(WorldLocation.Tile, shiftedLocation );
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch { }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        /// <summary>
        /// Draw a single track item
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="colors">The colorscheme to use</param>
        /// <param name="drawAlways">Do we need to draw anyway, independent of settings?</param>
        internal override bool Draw(DrawArea drawArea, ColorScheme colors, bool drawAlways)
        {
            if (   drawAlways 
                || Properties.Settings.Default.showAllSignals
                || (Properties.Settings.Default.showSignals && isNormal)
                )
            {
                float size = 7f; // in meters
                int minPixelSize = 5;
                drawArea.DrawTexture(WorldLocation, "signal" + colors.NameExtension, size, minPixelSize, ColorScheme.None, angle);
                return true;
            }
            return false;
        }
    }
    #endregion

    #region DrawableLevelCrItem
    /// <summary>
    /// Represents a drawable level crossing
    /// </summary>
    internal sealed class DrawableLevelCrItem : DrawableTrackItem
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="originalTrItem">The original track item that we are representing for drawing</param>
        public DrawableLevelCrItem(TrackItemBase originalTrItem)
            : base(originalTrItem)
        {
            Description = "crossing";
        }

        /// <summary>
        /// Draw a single track item
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="colors">The colorscheme to use</param>
        /// <param name="drawAlways">Do we need to draw anyway, independent of settings?</param>
        internal override bool Draw(DrawArea drawArea, ColorScheme colors, bool drawAlways)
        {
            if (Properties.Settings.Default.showCrossings || drawAlways)
            {
                drawArea.DrawTexture(WorldLocation, "disc", 6f, 0, colors.Crossing);
                return true;
            }
            return false;
        }
    }
    #endregion

    #region DrawableRoadLevelCrItem
    /// <summary>
    /// Represents a drawable level crossing on a road
    /// </summary>
    internal sealed class DrawableRoadLevelCrItem : DrawableTrackItem
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="originalTrItem">The original track item that we are representing for drawing</param>
        public DrawableRoadLevelCrItem(TrackItemBase originalTrItem)
            : base(originalTrItem)
        {
            Description = "crossing (road)";
        }

        /// <summary>
        /// Draw a single track item
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="colors">The colorscheme to use</param>
        /// <param name="drawAlways">Do we need to draw anyway, independent of settings?</param>
        internal override bool Draw(DrawArea drawArea, ColorScheme colors, bool drawAlways)
        {
            if (Properties.Settings.Default.showRoadCrossings || drawAlways)
            {
                drawArea.DrawTexture(WorldLocation, "disc", 4f, 0, colors.RoadCrossing);
                return true;
            }
            return false;
        }
    }
    #endregion

    #region DrawableSidingItem
    /// <summary>
    /// Represents a drawable siding item
    /// </summary>
    internal sealed class DrawableSidingItem : DrawableTrackItem
    {
        private readonly string itemName;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="originalTrItem">The original track item that we are representing for drawing</param>
        public DrawableSidingItem(TrackItemBase originalTrItem)
            : base(originalTrItem)
        {
            Description = "siding";
            itemName = (originalTrItem as SidingTrackItem).SidingName;
        }

        /// <summary>
        /// Draw a single track item
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="colors">The colorscheme to use</param>
        /// <param name="drawAlways">Do we need to draw anyway, independent of settings?</param>
        internal override bool Draw(DrawArea drawArea, ColorScheme colors, bool drawAlways)
        {
            bool returnValue;
            returnValue = false;
            if (Properties.Settings.Default.showSidingMarkers || drawAlways)
            {
                drawArea.DrawTexture(WorldLocation, "disc", 6f, 0, colors.Siding);
                returnValue = true;
            }
            if (Properties.Settings.Default.showSidingNames || drawAlways)
            {
                drawArea.DrawExpandingString(WorldLocation, itemName);
                returnValue = true;
            }
            return returnValue;
        }
    }
    #endregion

    #region DrawablePlatformItem
    /// <summary>
    /// Represents a drawable platform item
    /// </summary>
    internal sealed class DrawablePlatformItem : DrawableTrackItem
    {
        private readonly string itemName;
        private readonly string stationName;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="originalTrItem">The original track item that we are representing for drawing</param>
        public DrawablePlatformItem(TrackItemBase originalTrItem)
            : base(originalTrItem)
        {
            Description = "platform";
            PlatformTrackItem platform = originalTrItem as PlatformTrackItem;
            itemName = platform.PlatformName;
            stationName = platform.StationName;
        }

        /// <summary>
        /// Draw a single track item
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="colors">The colorscheme to use</param>
        /// <param name="drawAlways">Do we need to draw anyway, independent of settings?</param>
        internal override bool Draw(DrawArea drawArea, ColorScheme colors, bool drawAlways)
        {
            bool returnValue;
            returnValue = false;
            if (Properties.Settings.Default.showPlatformMarkers || drawAlways)
            {
                float size = 9f; // in meters
                int minPixelSize = 7;
                drawArea.DrawTexture(WorldLocation, "platform" + colors.NameExtension, size, minPixelSize);
                returnValue = true;
            }
            if (Properties.Settings.Default.showPlatformNames)
            {
                drawArea.DrawExpandingString(WorldLocation, itemName);
                returnValue = true;
            }
            if (Properties.Settings.Default.showStationNames || 
                (drawAlways && !Properties.Settings.Default.showPlatformNames) )
            {   // if drawAlways and no station nor platform name requested, then also show station
                drawArea.DrawExpandingString(WorldLocation, stationName);
                returnValue = true;
            }
            
            return returnValue;
        }
    }
    #endregion

    #region DrawblePickupItem
    /// <summary>
    /// Represents a drawable pickup item
    /// </summary>
    internal sealed class DrawablePickupItem : DrawableTrackItem
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="originalTrItem">The original track item that we are representing for drawing</param>
        public DrawablePickupItem(TrackItemBase originalTrItem)
            : base(originalTrItem)
        {
            Description = "pickup";
        }

        /// <summary>
        /// Draw a single track item
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="colors">The colorscheme to use</param>
        /// <param name="drawAlways">Do we need to draw anyway, independent of settings?</param>
        internal override bool Draw(DrawArea drawArea, ColorScheme colors, bool drawAlways)
        {
            if (Properties.Settings.Default.showPickups || drawAlways)
            {
                float size = 9f; // in meters
                int minPixelSize = 5;
                drawArea.DrawTexture(WorldLocation, "pickup" + colors.NameExtension, size, minPixelSize);
                return true;
            }
            return false;
        }
    }
    #endregion

    #region DrawableHazardItem
    /// <summary>
    /// Represents a drawable hazard item
    /// </summary>
    internal sealed class DrawableHazardItem : DrawableTrackItem
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="originalTrItem">The original track item that we are representing for drawing</param>
        public DrawableHazardItem(TrackItemBase originalTrItem)
            : base(originalTrItem)
        {
            Description = "hazard";
        }

        /// <summary>
        /// Draw a single track item
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="colors">The colorscheme to use</param>
        /// <param name="drawAlways">Do we need to draw anyway, independent of settings?</param>
        internal override bool Draw(DrawArea drawArea, ColorScheme colors, bool drawAlways)
        {
            if (Properties.Settings.Default.showHazards || drawAlways)
            {
                float size = 9f; // in meters
                int minPixelSize = 7;
                drawArea.DrawTexture(WorldLocation, "hazard" + colors.NameExtension, size, minPixelSize);
                return true;
            }
            return false;
        }
    }
    #endregion

    #region DrawableCarSpawnerItem
    /// <summary>
    /// Represents a drawable car spawner
    /// </summary>
    internal sealed class DrawableCarSpawnerItem : DrawableTrackItem
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="originalTrItem">The original track item that we are representing for drawing</param>
        public DrawableCarSpawnerItem(TrackItemBase originalTrItem)
            : base(originalTrItem)
        {
            Description = "carspawner";
        }

        /// <summary>
        /// Draw a single track item
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="colors">The colorscheme to use</param>
        /// <param name="drawAlways">Do we need to draw anyway, independent of settings?</param>
        internal override bool Draw(DrawArea drawArea, ColorScheme colors, bool drawAlways)
        {
            if (Properties.Settings.Default.showCarSpawners || drawAlways)
            {
                float size = 9f; // in meters
                int minPixelSize = 5;
                drawArea.DrawTexture(WorldLocation, "carspawner" + colors.NameExtension, size, minPixelSize);
                return true;
            }
            return false;
        }
    }
    #endregion

    #region DrawableEmptyItem
    /// <summary>
    /// Represents a drawable empty item (so not much to draw then)
    /// </summary>
    internal sealed class DrawableEmptyItem : DrawableTrackItem
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="originalTrItem">The original track item that we are representing for drawing</param>
        public DrawableEmptyItem(TrackItemBase originalTrItem)
            : base(originalTrItem)
        {
            Description = "empty";
        }

        /// <summary>
        /// Draw a single track item
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="colors">The colorscheme to use</param>
        /// <param name="drawAlways">Do we need to draw anyway, independent of settings?</param>
        internal override bool Draw(DrawArea drawArea, ColorScheme colors, bool drawAlways)
        {
            // draw nothing, but do allow it to be mentioned if it exists.
            return true;
        }
    }
    #endregion

    #region DrawableCorssoverItem
    /// <summary>
    /// Represents a drawable cross-over
    /// </summary>
    internal sealed class DrawableCrossoverItem : DrawableTrackItem
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="originalTrItem">The original track item that we are representing for drawing</param>
        public DrawableCrossoverItem(TrackItemBase originalTrItem)
            : base(originalTrItem)
        {
            Description = "crossover";
        }

        /// <summary>
        /// Draw a single track item
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="colors">The colorscheme to use</param>
        /// <param name="drawAlways">Do we need to draw anyway, independent of settings?</param>
        internal override bool Draw(DrawArea drawArea, ColorScheme colors, bool drawAlways)
        {
            if (Properties.Settings.Default.showCrossovers || drawAlways)
            {
                drawArea.DrawTexture(WorldLocation, "disc", 3f, 0, colors.EndNode);
                return true;
            }
            return false;
        }
    }
    #endregion

    #region DrawableSpeedPostItem
    /// <summary>
    /// Represents a drawable speedpost (or milepost)
    /// </summary>
    internal sealed class DrawableSpeedPostItem : DrawableTrackItem
    {
        private readonly bool isLimit;
        private readonly bool isMilePost;
        private readonly float distance;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="originalTrItem">The original track item that we are representing for drawing</param>
        public DrawableSpeedPostItem(TrackItemBase originalTrItem)
            : base(originalTrItem)
        {
            Description = "speedpost";
            if (originalTrItem is SpeedpostTrackItem speedpost)
            {
                isLimit = speedpost.SpeedpostType.HasFlag(SpeedpostType.Limit);
                isMilePost = false;
                distance = speedpost.SpeedValue;
            }
            else if (originalTrItem is MilepostTrackItem milepost)
            {
                isLimit = false;
                isMilePost = true;
                distance = milepost.DistanceValue;
            }
        }

        /// <summary>
        /// Draw a single track item
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="colors">The colorscheme to use</param>
        /// <param name="drawAlways">Do we need to draw anyway, independent of settings?</param>
        internal override bool Draw(DrawArea drawArea, ColorScheme colors, bool drawAlways)
        {
            bool returnValue;
            returnValue = false;
            if (isLimit && (Properties.Settings.Default.showSpeedLimits || drawAlways))
            {
                drawArea.DrawTexture(WorldLocation, "disc", 6f, 0, colors.Speedpost);
                string speed = distance.ToString(System.Globalization.CultureInfo.CurrentCulture);
                drawArea.DrawExpandingString(WorldLocation, speed);
                returnValue = true;
            }
            if (isMilePost && (Properties.Settings.Default.showMileposts || drawAlways))
            {
                drawArea.DrawTexture(WorldLocation, "disc", 6f, 0, colors.Speedpost);
                string distanceStr = distance.ToString(System.Globalization.CultureInfo.CurrentCulture);
                drawArea.DrawExpandingString(WorldLocation, distanceStr);
                returnValue = true;
            }

            return returnValue;
        }
    }
    #endregion

    #region DrawableSoundRegionItem
    /// <summary>
    /// Represents a drawable sound region
    /// </summary>
    internal sealed class DrawableSoundRegionItem : DrawableTrackItem
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="originalTrItem">The original track item that we are representing for drawing</param>
        public DrawableSoundRegionItem(TrackItemBase originalTrItem)
            : base(originalTrItem)
        {
            Description = "soundregion";
        }

        /// <summary>
        /// Draw a single track item
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="colors">The colorscheme to use</param>
        /// <param name="drawAlways">Do we need to draw anyway, independent of settings?</param>
        internal override bool Draw(DrawArea drawArea, ColorScheme colors, bool drawAlways)
        {
            if (Properties.Settings.Default.showSoundRegions || drawAlways)
            {
                float size = 4f; // in meters
                int minPixelSize = 5;
                drawArea.DrawTexture(WorldLocation, "sound" + colors.NameExtension, size, minPixelSize);
                return true;
            }
            return false;
        }
    }
    #endregion
}
