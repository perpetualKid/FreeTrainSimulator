using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Common.Position;
using Orts.Formats.Msts;

namespace Orts.Graphics.MapView
{
    public abstract class ContentBase : INameValueInformationProvider
    {
        private protected enum ContentItemType
        {
            // types should be placed in the order they are drawn (overlap), top level last
            // and *MUST* be same name (string) as in Orts.Common.MapViewItemSettings
            Grid,
            Platforms,
            Sidings,
            MilePosts,
            SpeedPosts,
            Signals,
            OtherSignals,
            Tracks,
            EndNodes,
            JunctionNodes,
            CrossOvers,
            Roads,
            RoadEndNodes,
            RoadCrossings,
            LevelCrossings,
                Hazards,
            CarSpawners,
            Pickups,
            SoundRegions,
            SidingNames,
            PlatformNames,
            PlatformStations,
            Paths,
            PathEnds,
            PathIntermediates,
            PathJunctions,
            PathReversals,

        }

        private protected readonly Game game;
        private protected MapViewItemSettings viewSettings = MapViewItemSettings.All;

        private protected readonly EnumArray<bool, ContentItemType> contentItemsSettings = new EnumArray<bool, ContentItemType>(true);
        private protected readonly EnumArray<ITileIndexedList<ITileCoordinate<Tile>, Tile>, ContentItemType> contentItems = new EnumArray<ITileIndexedList<ITileCoordinate<Tile>, Tile>, ContentItemType>();
        private protected readonly EnumArray<ITileCoordinate<Tile>, ContentItemType> nearestItems = new EnumArray<ITileCoordinate<Tile>, ContentItemType>();

        public bool UseMetricUnits { get; } = RuntimeData.Instance.UseMetricUnits;

        public string RouteName { get; } = RuntimeData.Instance.RouteName;

        public ContentArea ContentArea { get; }

        public Rectangle Bounds { get; protected set; }

        public NameValueCollection DebugInfo { get; } = new NameValueCollection();

        public Dictionary<string, FormatOption> FormattingOptions { get; } = new Dictionary<string, FormatOption>();

        public INameValueInformationProvider TrackNodeInfo { get; private protected set; }

        protected ContentBase(Game game)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            if (null == RuntimeData.Instance)
                throw new InvalidOperationException("RuntimeData not initialized!");
            ContentArea = new ContentArea(game, this);
        }

        public abstract Task Initialize();

        public void UpdateItemVisiblity(MapViewItemSettings viewSettings)
        {
            this.viewSettings = viewSettings;

            foreach(ContentItemType itemType in EnumExtension.GetValues<ContentItemType>())
            {
                EnumExtension.GetValue(itemType.ToString(), out MapViewItemSettings setting);
                contentItemsSettings[itemType] = (viewSettings & setting) == setting;
            }
        }

        internal abstract void Draw(ITile bottomLeft, ITile topRight);

        internal abstract void UpdatePointerLocation(in PointD position, ITile bottomLeft, ITile topRight);

        private protected abstract class TrackNodeInfoProxyBase : INameValueInformationProvider
        {
            public abstract NameValueCollection DebugInfo { get; }

            public abstract Dictionary<string, FormatOption> FormattingOptions { get; }
        }
    }
}
