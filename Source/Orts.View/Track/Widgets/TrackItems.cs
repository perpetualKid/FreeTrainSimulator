
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Position;
using Orts.Formats.Msts.Models;
using Orts.View.DrawableComponents;
using Orts.View.Track.Shapes;

namespace Orts.View.Track.Widgets
{
    #region TrackItemBase
    internal abstract class TrackItemBase : PointWidget
    {
        [ThreadStatic]
        private protected static System.Drawing.Font font;

        public TrackItemBase(TrackItem source)
        {
            Size = 3;
            ref readonly WorldLocation location = ref source.Location;
            base.location = PointD.FromWorldLocation(location);
        }

        internal static void SetFont(System.Drawing.Font font)
        {
            TrackItemBase.font = font;
        }

        public static List<TrackItemBase> Create(IEnumerable<TrackItem> trackItems)
        {
            List<TrackItemBase> result = new List<TrackItemBase>();
            Dictionary<uint, SidingTrackItem> sidingItems = new Dictionary<uint, SidingTrackItem>();

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
                }
            }


            result.AddRange(SidingTrackItem.LinkSidingItems(sidingItems));

            return result;
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

        public SidingTrackItem(SidingItem source) : base(source)
        {
            sidingName = source.ItemName;
            Id = source.TrackItemId;
            linkedId = source.LinkedSidingId;
        }

        internal override void Draw(ContentArea contentArea)
        {
            BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), Color.CornflowerBlue, false, false, false, contentArea.SpriteBatch);
            if (drawName)
                TextDrawShape.DrawString(contentArea.WorldToScreenCoordinates(in location), Color.Red, sidingName, font, Vector2.One, TextAlignment.Left, SpriteEffects.None, contentArea.SpriteBatch);
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

        public PlatformTrackItem(PlatformItem source) : 
            base(source)
        {
            platformName = source.ItemName;
            Size = 9;
        }

        internal override void Draw(ContentArea contentArea)
        {
            BasicShapes.DrawTexture(BasicTextureType.Platform, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), Color.White, false, false, false, contentArea.SpriteBatch);
            TextDrawShape.DrawString(contentArea.WorldToScreenCoordinates(in location), Color.Blue, platformName, font, Vector2.One, TextAlignment.Left, SpriteEffects.None, contentArea.SpriteBatch);
        }
    }
    #endregion

}
