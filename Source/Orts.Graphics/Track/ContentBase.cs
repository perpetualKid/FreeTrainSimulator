using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using Orts.Common.DebugInfo;
using Orts.Common.Position;

namespace Orts.Graphics.Track
{
    public abstract class ContentBase: INameValueInformationProvider
    {
        private protected ContentArea contentArea;

        public string RouteName { get; }

        public bool UseMetricUnits { get; }

        public Rectangle Bounds { get; protected set; }

        public NameValueCollection DebugInfo { get; } = new NameValueCollection();

        public Dictionary<string, FormatOption> FormattingOptions { get; }

        protected ContentBase(string routeName, bool metricUnits) 
        { 
            RouteName = routeName;
            UseMetricUnits = metricUnits;
        }

        public abstract Task Initialize();

        internal void SetContentArea(ContentArea contentArea) { this.contentArea = contentArea; }

        internal abstract void Draw(ITile bottomLeft, ITile topRight);

        internal abstract void UpdateNearestItems(in PointD position, ITile bottomLeft, ITile topRight);

    }
}
