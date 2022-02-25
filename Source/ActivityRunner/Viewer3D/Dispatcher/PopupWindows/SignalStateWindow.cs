using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.ActivityRunner.Viewer3D.Dispatcher.PopupWindows
{
    public class SignalStateWindow : WindowBase
    {
        private readonly Point offset;
        private ISignal signal;
        private RadioButton rbtnApproach;
        private RadioButton rbtnProceed;
        private RadioButton rbtnStop;
        private RadioButton rbtnSystem;
        private RadioButton rbtnCallon;

        public SignalStateWindow(WindowManager owner, Point relativeLocation) : 
            base(owner ?? throw new ArgumentNullException(nameof(owner)), 
                CatalogManager.Catalog.GetString("Signal State"), relativeLocation, new Point(owner.DefaultFontSize * 12, (int)(owner.DefaultFontSize * 7 + 20)))
        {
            Modal = true;
            offset = new Point((int)((Borders.Width / -3) * owner.DpiScaling), (int)(10 * owner.DpiScaling));
        }

        public void OpenAt(Point point, ISignal signal)
        { 
            this.signal = signal;
            Relocate(point + offset);
            Open();
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            ControlLayout rbLayout = layout.AddLayoutVertical();
            ControlLayoutHorizontal line = rbLayout.AddLayoutHorizontalLineOfText();
            RadioButtonGroup group  = new RadioButtonGroup();
#pragma warning disable CA2000 // Dispose objects before losing scope
            line.Add(rbtnSystem = new RadioButton(this, group) { TextColor = Color.White, State = true });
            line.Add(new Label(this, line.RemainingWidth, Owner.TextFontDefault.Height, CatalogManager.Catalog.GetString("System Controlled")));
            line = rbLayout.AddLayoutHorizontalLineOfText();
            line.Add(rbtnStop = new RadioButton(this, group) { TextColor = Color.Red });
            line.Add(new Label(this, line.RemainingWidth, Owner.TextFontDefault.Height, CatalogManager.Catalog.GetString("Stop")));
            line = rbLayout.AddLayoutHorizontalLineOfText();
            line.Add(rbtnApproach = new RadioButton(this, group) { TextColor = Color.Yellow });
            line.Add(new Label(this, line.RemainingWidth, Owner.TextFontDefault.Height, CatalogManager.Catalog.GetString("Approach")));
            line = rbLayout.AddLayoutHorizontalLineOfText();
            line.Add(rbtnProceed = new RadioButton(this, group) { TextColor = Color.LimeGreen });
            line.Add(new Label(this, line.RemainingWidth, Owner.TextFontDefault.Height, CatalogManager.Catalog.GetString("Proceed")));
            line = rbLayout.AddLayoutHorizontalLineOfText();
            line.Add(rbtnCallon = new RadioButton(this, group) { TextColor = Color.White });
            line.Add(new Label(this, line.RemainingWidth, Owner.TextFontDefault.Height, CatalogManager.Catalog.GetString("Call On")));
#pragma warning restore CA2000 // Dispose objects before losing scope

            return layout;
        }

        protected override void FocusLost()
        {
            Close();
            base.FocusLost();
        }
    }
}
