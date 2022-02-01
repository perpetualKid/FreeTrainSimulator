using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using Orts.Common.DebugInfo;
using Orts.Common.Position;
using Orts.Formats.Msts;

namespace Orts.Graphics.Track
{
    public abstract class ContentBase : INameValueInformationProvider
    {
        private protected readonly Game game;

        public bool UseMetricUnits { get; } = RuntimeData.Instance.UseMetricUnits;

        public string RouteName { get; } = RuntimeData.Instance.RouteName;

        public ContentArea ContentArea { get; }

        public Rectangle Bounds { get; protected set; }

        public NameValueCollection DebugInfo { get; } = new NameValueCollection();

        public Dictionary<string, FormatOption> FormattingOptions { get; }

        protected ContentBase(Game game)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            if (null == RuntimeData.Instance)
                throw new InvalidOperationException("RuntimeData not initialized!");
            ContentArea = new ContentArea(game, this);
        }

        public abstract Task Initialize();

        internal abstract void Draw(ITile bottomLeft, ITile topRight);

        internal abstract void UpdatePointerLocation(in PointD position, ITile bottomLeft, ITile topRight);
    }
}
