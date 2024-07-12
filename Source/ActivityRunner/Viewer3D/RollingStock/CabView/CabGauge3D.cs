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

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Formats.Msts;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.CabView
{
    public class CabGauge3D
    {
        private readonly PoseableShape trainCarShape;
        private readonly short[] triangleListIndices;// Array of indices to vertices for triangles
        private readonly Viewer viewer;
        private readonly int matrixIndex;
        private readonly CabViewGaugeRenderer gaugeRenderer;
        private readonly Matrix xnaMatrix;
        private readonly float gaugeSize;

        public CabGauge3D(Viewer viewer, int iMatrix, float gaugeSize, PoseableShape trainCarShape, CabViewControlRenderer c)
        {
            gaugeRenderer = (CabViewGaugeRenderer)c;
            this.viewer = viewer;
            this.trainCarShape = trainCarShape;
            matrixIndex = iMatrix;
            xnaMatrix = this.trainCarShape.SharedShape.Matrices[iMatrix];
            this.gaugeSize = gaugeSize / 1000f; //how long is the scale 1? since OR cannot allow fraction number in part names, have to define it as mm
        }

        /// <summary>
        /// Transition the part toward the specified state. 
        /// </summary>
        public void Update(MSTSLocomotiveViewer locoViewer, in ElapsedTime elapsedTime)
        {
            if (!locoViewer.Has3DCabRenderer)
                return;

            var scale = gaugeRenderer.IsPowered || gaugeRenderer.control.ValueIfDisabled == null ? gaugeRenderer.GetRangeFraction() : (float)gaugeRenderer.control.ValueIfDisabled;

            trainCarShape.XNAMatrices[matrixIndex] = gaugeRenderer.Style == CabViewControlStyle.Pointer
                ? Matrix.CreateTranslation(scale * gaugeSize, 0, 0) * trainCarShape.SharedShape.Matrices[matrixIndex]
                : Matrix.CreateScale(scale * 10, 1, 1) * trainCarShape.SharedShape.Matrices[matrixIndex];
            //this.TrainCarShape.SharedShape.Matrices[matrixIndex] = XNAMatrix * mx * Matrix.CreateRotationX(10);
        }

    } // class ThreeDimCabGauge
}
