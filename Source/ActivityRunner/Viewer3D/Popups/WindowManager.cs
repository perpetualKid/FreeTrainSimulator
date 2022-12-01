// COPYRIGHT 2010, 2011, 2012, 2013 by the Open Rails project.
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

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;

namespace Orts.ActivityRunner.Viewer3D.Popups
{
    public class WindowManager : RenderPrimitive
    {
        public static Texture2D WhiteTexture;

        public readonly Viewer Viewer;
        public readonly WindowTextManager TextManager;
        public readonly WindowTextFont TextFontDefault;
        public readonly WindowTextFont TextFontDefaultOutlined;

        private readonly Material WindowManagerMaterial;
        private readonly List<Window> Windows = new List<Window>();
        private SpriteBatch SpriteBatch;
        private Matrix Identity = Matrix.Identity;
        internal Point ScreenSize = new Point(10000, 10000); // Arbitrary but necessary.

        public WindowManager(Viewer viewer)
        {
            Viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));

            WindowManagerMaterial = new BasicBlendedMaterial(viewer, "WindowManager");
            TextManager = new WindowTextManager();
            TextFontDefault = TextManager.GetScaled("Arial", 10, System.Drawing.FontStyle.Regular);
            TextFontDefaultOutlined = TextManager.GetScaled("Arial", 10, System.Drawing.FontStyle.Regular, 1);

            SpriteBatch = new SpriteBatch(Viewer.Game.GraphicsDevice);

            if (WhiteTexture == null)
            {
                WhiteTexture = new Texture2D(Viewer.Game.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
                WhiteTexture.SetData(new[] { Color.White });
            }
        }


        public void Initialize()
        {
            ScreenChanged();

            foreach (var window in Windows)
            {
                window.Initialize();
                window.Layout();
            }
        }

        public void ScreenChanged()
        {
            var oldScreenSize = ScreenSize;
            ScreenSize = Viewer.DisplaySize;

            // Reposition all the windows.
            foreach (var window in Windows)
            {
                if (oldScreenSize.X - window.Location.Width > 0 && oldScreenSize.Y - window.Location.Height > 0)
                    window.MoveTo((ScreenSize.X - window.Location.Width) * window.Location.X / (oldScreenSize.X - window.Location.Width), (ScreenSize.Y - window.Location.Height) * window.Location.Y / (oldScreenSize.Y - window.Location.Height));
                window.ScreenChanged();
            }
        }

        private double LastPrepareRealTime;

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            var updateFull = false;
            if (Viewer.RealTime - LastPrepareRealTime >= 0.25)
            {
                updateFull = true;
                LastPrepareRealTime = Viewer.RealTime;
            }

            foreach (var window in VisibleWindows)
                window.PrepareFrame(frame, elapsedTime, updateFull);

            frame.AddPrimitive(WindowManagerMaterial, this, RenderPrimitiveGroup.Overlay, ref Identity);
        }

        public override void Draw()
        {
            // Nothing visible? Nothing more to do!
            if (!VisibleWindows.Any())
                return;

            foreach (var window in VisibleWindows)
            {
                SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, null, null, null, null);
                window.Draw(SpriteBatch);
                SpriteBatch.End();
            }
            // For performance, we call SpriteBatch.Begin() with SaveStateMode.None above, but we now need to restore
            // the state ourselves.
            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        internal void Add(Window window)
        {
            Windows.Add(window);
        }

        public IEnumerable<Window> VisibleWindows
        {
            get
            {
                return Windows.Where(w => w.Visible);
            }
        }

        public void Mark()
        {
            WindowManagerMaterial.Mark();
            foreach (Window window in Windows)
                window.Mark();
        }

        public void Load()
        {
            TextManager.Load(Viewer.Game.GraphicsDevice);
        }
    }
}
