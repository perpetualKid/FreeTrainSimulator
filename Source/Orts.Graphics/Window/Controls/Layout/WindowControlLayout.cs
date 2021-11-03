
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Window.Controls.Layout
{
    internal class WindowControlLayout : ControlLayout
    {
        public readonly WindowBase Window;

        private bool capturedForDragging;

        public WindowControlLayout(WindowBase window, int width, int height)
            : base(0, 0, width, height)
        {
            Window = window;
        }

        /*
       internal override bool HandleMousePressed(WindowMouseEvent e)
       {
           if (base.HandleMousePressed(e))
               return true;

           capturedForDragging = true;
           // prevent from dragging when clicking on vertical scrollbar
           if (MathHelper.Distance(base.RemainingWidth, e.MousePosition.X) < 20)
               return false;

           // prevent from dragging when clicking on horizontal scrollbar
           if (MathHelper.Distance(base.RemainingHeight, e.MousePosition.Y) < 20)
               return false;

           return true;
       }

       internal override bool HandleMouseReleased(WindowMouseEvent e)
       {
           if (base.HandleMouseReleased(e))
               return true;
           capturedForDragging = false;
           return true;
       }

       internal override bool HandleMouseMove(WindowMouseEvent e)
       {
           if (base.HandleMouseMove(e))
               return true;

           if (capturedForDragging)
               Window.MoveTo(Window.Location.X + e.Movement.X, Window.Location.Y + e.Movement.Y);
           return true;
       }

       internal override bool HandleMouseScroll(WindowMouseEvent e)
       {
           if (base.HandleMouseScroll(e))
               return true;
           return true;
       }
*/
    }
}
