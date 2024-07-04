// COPYRIGHT 2015 by the Open Rails project.
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

using System;
using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Simulation;
using Orts.Simulation.World;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.SubSystems
{
    public class ContainersViewer
    {
        private Dictionary<Container, ContainerViewer> containers = new Dictionary<Container, ContainerViewer>();
        private List<Container> visibleContainers = new List<Container>();
        private readonly Viewer viewer;

        public ContainersViewer(Viewer viewer)
        {
            this.viewer = viewer;
        }

        public void LoadPrep()
        {
            List<Container> visibleContainers = new List<Container>();
            float removeDistance = viewer.Settings.ViewingDistance * 1.5f;
            foreach (Container container in Simulator.Instance.ContainerManager.Containers)
                if (WorldLocation.ApproximateDistance(viewer.Camera.CameraWorldLocation, container.WorldPosition.WorldLocation) < removeDistance && container.Visible)
                    visibleContainers.Add(container);
            this.visibleContainers = visibleContainers;
        }

        public void Mark()
        {
            foreach (ContainerViewer container in containers.Values)
                container.Mark();
        }

        public void Load()
        {
            System.Threading.CancellationToken cancellation = viewer.LoaderProcess.CancellationToken;
            List<Container> visibleContainers = this.visibleContainers;
            Dictionary<Container, ContainerViewer> containers = this.containers;
            if (visibleContainers.Any(c => !containers.ContainsKey(c)) || containers.Keys.Any(c => !visibleContainers.Contains(c)))
            {
                Dictionary<Container, ContainerViewer> newContainers = new Dictionary<Container, ContainerViewer>();
                foreach (Container container in visibleContainers)
                {
                    if (cancellation.IsCancellationRequested)
                        break;
                    if (containers.TryGetValue(container, out ContainerViewer value))
                        newContainers.Add(container, value);
                    else
                        newContainers.Add(container, new ContainerViewer(container));
                }
                this.containers = newContainers;
            }
        }

        public void PrepareFrame(RenderFrame frame, ElapsedTime elapsedTime)
        {
            foreach (var container in containers.Values)
                container.PrepareFrame(frame, elapsedTime);
        }
    }

    public class ContainerViewer
    {
        private readonly Container container;
        private readonly AnimatedShape containerShape;

        public ContainerViewer(ContainerHandlingStation containerHandlingItem, Container container)
        {
            ArgumentNullException.ThrowIfNull(container);

            this.container = container;
            containerShape = new AnimatedShape(container.BaseShapeFileFolderSlash + container.ShapeFileName + '\0' + container.BaseShapeFileFolderSlash, containerHandlingItem, ShapeFlags.ShadowCaster);
            if (containerShape.SharedShape.LodControls.Length > 0)
                foreach (var lodControl in containerShape.SharedShape.LodControls)
                    if (lodControl.DistanceLevels.Length > 0)
                        foreach (var distanceLevel in lodControl.DistanceLevels)
                            if (distanceLevel.SubObjects.Length > 0
                                && distanceLevel.SubObjects[0].ShapePrimitives.Length > 0
                                && distanceLevel.SubObjects[0].ShapePrimitives[0].Hierarchy.Length > 0)
                                distanceLevel.SubObjects[0].ShapePrimitives[0].Hierarchy[0] = distanceLevel.SubObjects[0].ShapePrimitives[0].Hierarchy.Length;
        }

        public ContainerViewer(Container container)
        {
            ArgumentNullException.ThrowIfNull(container);

            this.container = container;
            containerShape = new AnimatedShape(container.BaseShapeFileFolderSlash + container.ShapeFileName + '\0' + container.BaseShapeFileFolderSlash, container, ShapeFlags.ShadowCaster);
            if (containerShape.SharedShape.LodControls.Length > 0)
                foreach (var lodControl in containerShape.SharedShape.LodControls)
                    if (lodControl.DistanceLevels.Length > 0)
                        foreach (var distanceLevel in lodControl.DistanceLevels)
                            if (distanceLevel.SubObjects.Length > 0
                                && distanceLevel.SubObjects[0].ShapePrimitives.Length > 0
                                && distanceLevel.SubObjects[0].ShapePrimitives[0].Hierarchy.Length > 0)
                                distanceLevel.SubObjects[0].ShapePrimitives[0].Hierarchy[0] = distanceLevel.SubObjects[0].ShapePrimitives[0].Hierarchy.Length;
            /*            if (ContainerShape.XNAMatrices.Length > 0 && animation is FreightAnimationDiscrete && (animation as FreightAnimationDiscrete).Flipped)
                        {
                            var flipper = Matrix.Identity;
                            flipper.M11 = -1;
                            flipper.M33 = -1;
                            ContainerShape.XNAMatrices[0] *= flipper;
                        }*/
        }

        public void Mark()
        {
            containerShape.Mark();
        }

        public void PrepareFrame(RenderFrame frame, ElapsedTime elapsedTime)
        {
            containerShape.PrepareFrame(frame, elapsedTime);
        }
    }
}
