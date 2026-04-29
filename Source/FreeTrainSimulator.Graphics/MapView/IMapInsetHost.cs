using System.Collections.Generic;

using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapInsetHost
    {
        void UpdateColor(Color color);

        void SetTrackSegments(IEnumerable<TrackSegmentBase> trackSegments);
    }
}
