using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.Window.Controls
{
    public class RadioButton : WindowTextureControl
    {
        private bool state;
        private readonly int size;
        private readonly Point centerOffset;
        private readonly RadioButtonGroup group;

        public bool State
        {
            get => state;
            set
            {
                state = value;
                if (value)
                {
                    foreach (RadioButton button in group.Group)
                    {
                        if (button != this)
                            button.State = false;
                    }
                }
            }
        }

        public RadioButton(FormBase window, RadioButtonGroup group) :
            base(window ?? throw new ArgumentNullException(nameof(window)), 0, 0,
                window.Owner.TextFontDefault.Height, window.Owner.TextFontDefault.Height)
        {
            size = window.Owner.TextFontDefault.Height * 3 / 4;
            centerOffset = new Point(window.Owner.TextFontDefault.Height / 2);
            this.group = group ?? throw new ArgumentNullException(nameof(group));
            group.Group.Add(this);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            Window.Owner.BasicShapes.DrawTexture(state ? BasicTextureType.Disc : BasicTextureType.Ring, (Bounds.Location + offset + centerOffset).ToVector2(), 0, size, TextColor, spriteBatch);
            base.Draw(spriteBatch, offset);
        }

        internal override bool RaiseMouseClick(WindowMouseEvent e)
        {
            State = true;
            _ = base.RaiseMouseClick(e);
            return true;
        }
    }

    public class RadioButtonGroup
    {
        internal List<RadioButton> Group { get; } = new List<RadioButton>();

        public RadioButton Selected => Group.Where(rb => rb.State).FirstOrDefault();
    }
}
