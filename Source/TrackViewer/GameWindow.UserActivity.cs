using System;
using System.Windows.Forms;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Info;
using Orts.Common.Input;

using Keys = Microsoft.Xna.Framework.Input.Keys;

namespace Orts.TrackViewer
{
    public partial class GameWindow : Game
    {
        private static readonly Vector2 moveLeft = new Vector2(1, 0);
        private static readonly Vector2 moveRight = new Vector2(-1, 0);
        private static readonly Vector2 moveUp = new Vector2(0, 1);
        private static readonly Vector2 moveDown = new Vector2(0, -1);
        private static readonly Vector2 moveLeftQuick = new Vector2(10, 0);
        private static readonly Vector2 moveRightQuick = new Vector2(-10, 0);
        private static readonly Vector2 moveUpQuick = new Vector2(0, 10);
        private static readonly Vector2 moveDownQuick = new Vector2(0, -10);
        
        public void ChangeScreenMode(Keys key, KeyModifiers modifiers)
        {
            SetScreenMode(currentScreenMode.Next());
        }

        public void CloseWindow(Keys key, KeyModifiers modifiers)
        {
            ExitApplication();
        }

        internal void ExitApplication()
        {
            if (MessageBox.Show(Catalog.GetString($"Do you want to quit {RuntimeInfo.ApplicationName} now?"), Catalog.GetString("Quit"), MessageBoxButtons.OKCancel) == DialogResult.OK)
                Exit();
        }

        public void MouseMove(Point position, Vector2 delta)
        {
        }

        public void MouseWheel(Point position, int delta)
        {
            contentArea?.UpdateScaleAt(position.ToVector2(), System.Math.Sign(delta));
        }

        public void MouseDragging(Point position, Vector2 delta)
        {
            contentArea?.UpdatePosition(delta);
        }

        public void MouseButtonUp(Point position)
        {
            System.Diagnostics.Debug.WriteLine($"Up {Window.Title} - {position}");
        }

        public void MouseButtonDown(Point position)
        {
            System.Diagnostics.Debug.WriteLine($"Down {Window.Title} - {position}");
        }

        private void MoveByKey(Keys key, KeyModifiers modifiers)
        {
            switch (key)
            {
                case Keys.Left:
                    contentArea?.UpdatePosition((modifiers & KeyModifiers.Control) == KeyModifiers.Control ? moveLeftQuick : moveLeft);
                    break;
                case Keys.Right:
                    contentArea?.UpdatePosition((modifiers & KeyModifiers.Control) == KeyModifiers.Control ? moveRightQuick : moveRight);
                    break;
                case Keys.Up:
                    contentArea?.UpdatePosition((modifiers & KeyModifiers.Control) == KeyModifiers.Control ? moveUpQuick : moveUp);
                    break;
                case Keys.Down:
                    contentArea?.UpdatePosition((modifiers & KeyModifiers.Control) == KeyModifiers.Control ? moveDownQuick : moveDown);
                    break;
            }
        }

        private DateTime nextUpdate;
        private void ZoomIn(Keys key, KeyModifiers modifiers)
        {
            if (DateTime.UtcNow > nextUpdate)
            {
                contentArea?.UpdateScale(1);
                nextUpdate = DateTime.UtcNow.AddMilliseconds(30);
            }
        }

        private void ZoomOut(Keys key, KeyModifiers modifiers)
        {
            if (DateTime.UtcNow > nextUpdate)
            {
                contentArea?.UpdateScale(-1);
                nextUpdate = DateTime.UtcNow.AddMilliseconds(30);
            }
        }

        private void ResetZoomAndLocation(Keys key, KeyModifiers modifiers)
        {
            contentArea?.ResetSize(Window.ClientBounds.Size, 60);
        }
    }
}
