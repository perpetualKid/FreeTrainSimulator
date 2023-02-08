﻿using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Graphics.Window.Controls;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    //passing Camera view/projection and location to Orts.Graphics primitives
    internal class CameraViewProjectionHolder : IViewProjection
    {
        public ref readonly Matrix Projection => ref viewer.Camera.XnaProjection;

        public ref readonly Matrix View => ref viewer.Camera.XnaView;

        public ref readonly WorldLocation Location => ref viewer.Camera.CameraWorldLocation;

        private readonly Viewer viewer;

        public CameraViewProjectionHolder(Viewer viewer)
        {
            this.viewer = viewer;
        }
    }
}


