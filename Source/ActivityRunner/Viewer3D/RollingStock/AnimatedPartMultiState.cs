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

using Orts.ActivityRunner.Viewer3D.RollingStock.CabView;
using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

namespace Orts.ActivityRunner.Viewer3D.RollingStock
{
    // This supports animation of Pantographs, Mirrors and Doors - any up/down on/off 2 state types
    // It is initialized with a list of indexes for the matrices related to this part
    // On Update( position ) it slowly moves the parts towards the specified position
    public class AnimatedPartMultiState : AnimatedPart
    {
        public CabViewControlType Type { get; }
        public (ControlType, int) Key { get; }
        /// <summary>
        /// Construct with a link to the shape that contains the animated parts 
        /// </summary>
        public AnimatedPartMultiState(PoseableShape poseableShape, (ControlType, int) k)
            : base(poseableShape)
        {
            Type = k.Item1.CabViewControlType;
            Key = k;
        }

        /// <summary>
        /// Transition the part toward the specified state. 
        /// </summary>
        public void Update(MSTSLocomotiveViewer locoViewer, in ElapsedTime elapsedTime)
        {
            if (MatrixIndexes.Count == 0 || !locoViewer.Has3DCabRenderer)
                return;

            if (locoViewer.CabRenderer3D.ControlMap.TryGetValue(Key, out CabViewControlRenderer cvfr))
            {
                float index = cvfr is CabViewDiscreteRenderer renderer ? renderer.GetDrawIndex() : cvfr.GetRangeFraction() * FrameCount;
                SetFrameClamp(index);
            }
        }
    }
}
