using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Models.Imported.Track;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Graphics.MapView
{
    public abstract class ContentBase : INameValueInformationProvider
    {
        private protected readonly Game game;
        private protected EnumArray<bool, MapContentType> viewSettings = new EnumArray<bool, MapContentType>(true);

        private protected readonly EnumArray<ITileCoordinate, MapContentType> nearestItems = new EnumArray<ITileCoordinate, MapContentType>();

        private protected TrackModel trackModel;

        public bool UseMetricUnits { get; }

        public string RouteName { get; }

        public ContentArea ContentArea { get; }

        public Rectangle Bounds { get; protected set; }

        public InformationDictionary DetailInfo { get; } = new InformationDictionary();

        public Dictionary<string, FormatOption> FormattingOptions { get; } = new Dictionary<string, FormatOption>();

        protected ContentBase(Game game)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            if (null == RuntimeData.GameInstance(game))
                throw new InvalidOperationException("RuntimeData not initialized!");
            ContentArea = new ContentArea(game, this);
            RouteName = RuntimeData.GameInstance(game).RouteData.Name;
            UseMetricUnits = RuntimeData.GameInstance(game).MetricUnits;
        }

        public abstract Task Initialize();

        public void UpdateItemVisiblity(MapContentType setting, bool value)
        {
            viewSettings[setting] = value;
        }

        public void InitializeItemVisiblity(EnumArray<bool, MapContentType> settings)
        {
            viewSettings = settings;
        }

        public void HighlightItem(MapContentType mapviewItem, ITileCoordinate item)
        {
            nearestItems[mapviewItem] = item;
        }

        internal abstract void Draw(in Tile bottomLeft, in Tile topRight);

        internal abstract void UpdatePointerLocation(in PointD position, in Tile bottomLeft, in Tile topRight);

        private protected void InitializeBounds()
        {
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

            // if there is only one tile, limit the dimensions to the extend of the track within that tile
            if (trackModel.ContentByTile[MapContentType.Grid].Count == 1)
            {
                if (trackModel.ContentByTile[MapContentType.EndNodes].ItemCount > 0)
                {
                    foreach (EndNode endNode in trackModel.ContentByTile[MapContentType.EndNodes])
                    {
                        minX = Math.Min(minX, endNode.Location.X);
                        minY = Math.Min(minY, endNode.Location.Y);
                        maxX = Math.Max(maxX, endNode.Location.X);
                        maxY = Math.Max(maxY, endNode.Location.Y);
                    }
                }
                else
                {
                    foreach (TrackSegment trackSegment in trackModel.ContentByTile[MapContentType.Tracks])
                    {
                        minX = Math.Min(minX, trackSegment.Location.X);
                        minY = Math.Min(minY, trackSegment.Location.Y);
                        maxX = Math.Max(maxX, trackSegment.Location.X);
                        maxY = Math.Max(maxY, trackSegment.Location.Y);
                    }
                }
            }
            else
            {
                minX = Math.Min(minX, (trackModel.ContentByTile[MapContentType.Grid] as TileIndexedList<GridTile>)[0][0].Tile.X);
                maxX = Math.Max(maxX, (trackModel.ContentByTile[MapContentType.Grid] as TileIndexedList<GridTile>)[^1][0].Tile.X);
                foreach (GridTile tile in trackModel.ContentByTile[MapContentType.Grid])
                {
                    minY = Math.Min(minY, tile.Tile.Z);
                    maxY = Math.Max(maxY, tile.Tile.Z);
                }
                minX = (minX * WorldLocation.TileSize) - (WorldLocation.TileSize / 2);
                maxX = (maxX * WorldLocation.TileSize) + (WorldLocation.TileSize / 2);
                minY = (minY * WorldLocation.TileSize) - (WorldLocation.TileSize / 2);
                maxY = (maxY * WorldLocation.TileSize) + (WorldLocation.TileSize / 2);
            }
            Bounds = new Rectangle((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
        }

        private protected abstract class DetailInfoProxyBase : INameValueInformationProvider
        {
            public abstract InformationDictionary DetailInfo { get; }

            public abstract Dictionary<string, FormatOption> FormattingOptions { get; }
        }
    }
}
