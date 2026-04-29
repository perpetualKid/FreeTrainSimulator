using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public sealed class XnaMapInsetHost : IMapInsetHost
    {
        private readonly InsetComponent insetComponent;

        public XnaMapInsetHost(InsetComponent insetComponent)
        {
            this.insetComponent = insetComponent;
        }

        public void UpdateColor(Color color)
        {
            insetComponent?.UpdateColor(color);
        }

        public void SetTrackSegments(IEnumerable<TrackSegmentBase> trackSegments)
        {
            insetComponent?.SetTrackSegments(trackSegments?.OfType<TrackSegment>());
        }
    }
}
