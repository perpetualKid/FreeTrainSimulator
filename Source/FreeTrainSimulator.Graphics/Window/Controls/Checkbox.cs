using System;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.Window.Controls
{
    public enum CheckMarkStyle
    {
        Marks,
        Circles,
        Ballot,
        Check,
    }

    public class Checkbox : Label
    {
        private bool? state;
        private readonly bool tristate;
        private readonly CheckMarkStyle checkMarkStyle;
        private readonly bool useColors;

        public bool ReadOnly { get; set; }

        public bool? State
        {
            get => state;
            set
            {
                state = tristate ? value : value.GetValueOrDefault();
                TextColor = useColors ? state.HasValue ? state.Value ? Color.LimeGreen : Color.OrangeRed : Color.LightGray : Color.White;
                //                Text = state.HasValue ? state.Value ? "✔" : "" : "■";
                Text = checkMarkStyle switch
                {
                    CheckMarkStyle.Marks => state.HasValue ? state.Value ? "\u2705" : "\u274C" : "\u2753",
                    CheckMarkStyle.Circles => state.HasValue ? state.Value ? "\u25C9" : "\u25EF" : "\u25CE",
                    CheckMarkStyle.Ballot => state.HasValue ? state.Value ? "\u2611" : "\u2610" : "\u2612",
                    CheckMarkStyle.Check => state.HasValue ? state.Value ? "\u2714" : "\u25A1" : "\u2754",
                    _ => throw new NotImplementedException(),
                };
            }
        }

        public Checkbox(FormBase window, bool threeStates = false, CheckMarkStyle checkMarkStyle = CheckMarkStyle.Check, bool useColors = true) :
            base(window ?? throw new ArgumentNullException(nameof(window)), window.Owner.TextFontDefault.Height, window.Owner.TextFontDefault.Height, "", HorizontalAlignment.Center)
        {
            tristate = threeStates;
            State = null;
            this.checkMarkStyle = checkMarkStyle;
            this.useColors = useColors;
        }

        internal override bool RaiseMouseClick(WindowMouseEvent e)
        {
            if (ReadOnly)
                return false;
            State = tristate ? !State.HasValue ? false : State.Value ? null : true : State = !State;
            _ = base.RaiseMouseClick(e);
            return true;
        }
    }
}
