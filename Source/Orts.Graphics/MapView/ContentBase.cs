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
        private protected readonly Game game;
        private protected EnumArray<bool, MapViewItemSettings> viewSettings = new EnumArray<bool, MapViewItemSettings>(true);

        private protected readonly EnumArray<ITileIndexedList<ITileCoordinate<Tile>, Tile>, MapViewItemSettings> contentItems = new EnumArray<ITileIndexedList<ITileCoordinate<Tile>, Tile>, MapViewItemSettings>();
        private protected readonly EnumArray<ITileCoordinate<Tile>, MapViewItemSettings> nearestItems = new EnumArray<ITileCoordinate<Tile>, MapViewItemSettings>();

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

        public void UpdateItemVisiblity(MapViewItemSettings setting, bool value)
        {
            this.viewSettings[setting] = value;
        }

        public void InitializeItemVisiblity(EnumArray<bool, MapViewItemSettings> settings)
        {
            this.viewSettings = settings;
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
