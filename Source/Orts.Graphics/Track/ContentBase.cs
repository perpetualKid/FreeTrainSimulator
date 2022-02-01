using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using Orts.Common.DebugInfo;
using Orts.Common.Position;

namespace Orts.Graphics.Track
{
    public abstract class ContentBase : INameValueInformationProvider
    {
        private protected readonly Game game;

        public ContentArea ContentArea { get; }

        public string RouteName { get; }

        public bool UseMetricUnits { get; }

        public Rectangle Bounds { get; protected set; }

        public NameValueCollection DebugInfo { get; } = new NameValueCollection();

        public Dictionary<string, FormatOption> FormattingOptions { get; }

        protected ContentBase(Game game, string routeName, bool metricUnits)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            RouteName = routeName;
            UseMetricUnits = metricUnits;
            ContentArea = new ContentArea(game, this);
        }

        public abstract Task Initialize();

        internal abstract void Draw(ITile bottomLeft, ITile topRight);

        internal abstract void UpdatePointerLocation(in PointD position, ITile bottomLeft, ITile topRight);
    }
}
