// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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

// This file is the responsibility of the 3D & Environment Team. 

using FreeTrainSimulator.Common;

using Orts.Formats.Msts.Models;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.CabView
{
    /// <summary>
    /// Discrete renderer for animated controls, like external 2D wiper
    /// </summary>
    public class CabViewAnimationsRenderer : CabViewDiscreteRenderer
    {
        private double cumulativeTime;
        private readonly float cycleTimeS;
        private bool animationOn;

        public CabViewAnimationsRenderer(Viewer viewer, MSTSLocomotive locomotive, CabViewAnimatedDisplayControl control, CabShader shader)
            : base(viewer, locomotive, control, shader)
        {
            cycleTimeS = control.CycleTimeS;
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            float data = !IsPowered && control.ValueIfDisabled != null ? (float)control.ValueIfDisabled : locomotive.GetDataOf(control);

            var animate = data != 0;
            if (animate)
                animationOn = true;

            int index;
            var halfCycleS = cycleTimeS / 2f;
            if (animationOn)
            {
                cumulativeTime += elapsedTime.ClockSeconds;
                if (cumulativeTime > cycleTimeS && !animate)
                    animationOn = false;
                cumulativeTime %= cycleTimeS;

                if (cumulativeTime < halfCycleS)
                    index = PercentToIndex((float)(cumulativeTime / halfCycleS));
                else
                    index = PercentToIndex((float)(cycleTimeS - cumulativeTime) / halfCycleS);
            }
            else
            {
                index = 0;
            }

            PrepareFrameForIndex(frame, elapsedTime, index);
        }
    }
}
