using System.Collections.Generic;
using System.Linq;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Input;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Settings;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class DistributedPowerWindow : WindowBase
    {
        private const string ArrowUp = "▲"; //\u25B2
        private const string ArrowDown = "▼"; //\u25BC
        private const string ArrowRight = "►"; //\u25BA
        private const string ArrowLeft = "◄"; //\u25C4

        private enum WindowMode
        {
            Normal,
            NormalMono,
            Short,
            ShortMono,
        }

        private readonly UserSettings settings;
        private readonly UserCommandController<UserCommand> userCommandController;
        private WindowMode windowMode;
        private Label labelExpandMono;
        private Label labelExpandDetails;
        private Label labelGroupStatus;
        private string groupStatus;

        public DistributedPowerWindow(WindowManager owner, Point relativeLocation, UserSettings settings, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Distributed Power"), relativeLocation, new Point(160, 200), catalog)
        {
            userCommandController = Owner.UserCommandController as UserCommandController<UserCommand>;
            this.settings = settings;
            _ = EnumExtension.GetValue(settings.PopupSettings[ViewerWindowType.DistributedPowerWindow], out windowMode);
            UpdatePowerInformation();
            Resize();
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            layout = layout.AddLayoutOffset(0);
            ControlLayout buttonLine = layout.AddLayoutHorizontal();
            buttonLine.HorizontalChildAlignment = HorizontalAlignment.Right;
            buttonLine.VerticalChildAlignment = VerticalAlignment.Top;
            buttonLine.Add(labelExpandMono = new Label(this, Owner.TextFontDefault.Height, Owner.TextFontDefault.Height, windowMode == WindowMode.ShortMono || windowMode == WindowMode.NormalMono ? ArrowRight : ArrowLeft));
            labelExpandMono.OnClick += LabelExpandMono_OnClick;
            buttonLine.Add(labelExpandDetails = new Label(this, Owner.TextFontDefault.Height, Owner.TextFontDefault.Height, windowMode == WindowMode.Normal || windowMode == WindowMode.NormalMono ? ArrowUp : ArrowDown));
            labelExpandDetails.OnClick += LabelExpandDetails_OnClick;
            labelExpandDetails.Visible = labelExpandMono.Visible = groupCount > 0;
            layout = layout.AddLayoutVertical();
            if (groupCount == 0)
            {
                layout.Add(labelGroupStatus = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, groupStatus, HorizontalAlignment.Center));
                Caption = Catalog.GetString("Distributed Power Info");
            }
            else
            {
                Caption = Catalog.GetString("DPU Info");
                layout.Add(labelGroupStatus = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, groupStatus, HorizontalAlignment.Left));
                layout.AddHorizontalSeparator(true);
            }
            return layout;
        }

        private void LabelExpandDetails_OnClick(object sender, MouseClickEventArgs e)
        {
            windowMode = windowMode.Next().Next();
            Resize();
        }

        private void LabelExpandMono_OnClick(object sender, MouseClickEventArgs e)
        {
            windowMode = windowMode == WindowMode.Normal || windowMode == WindowMode.Short ? windowMode.Next() : windowMode.Previous();
            Resize();
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
            if (shouldUpdate)
            {
                UpdatePowerInformation();
            }
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.DisplayDistributedPowerWindow, KeyEventType.KeyPressed, TabAction, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DisplayDistributedPowerWindow, KeyEventType.KeyPressed, TabAction);
            return base.Close();
        }

        private void TabAction(UserCommandArgs args)
        {
            if (groupCount > 0 && args is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & settings.Input.WindowTabCommandModifier) == settings.Input.WindowTabCommandModifier)
            {
                windowMode = windowMode.Next();
                Resize();
            }
        }

        private void Resize()
        {
            if (groupCount == 0)
            {
                Resize(new Point(420, 60));
            }
            else
            {
                int width = (groupCount + 1) * 50;
                Point size = windowMode switch
                {
                    WindowMode.Normal => new Point(width, 300),
                    WindowMode.NormalMono => new Point(width * 4 / 5, 300),
                    WindowMode.Short => new Point(width, 120),
                    WindowMode.ShortMono => new Point(width * 4 / 5, 120),
                    _ => throw new System.InvalidOperationException(),
                };

                Resize(size);
            }

            settings.PopupSettings[ViewerWindowType.DistributedPowerWindow] = windowMode.ToString();
        }

        private int groupCount;

        private void UpdatePowerInformation()
        {
            int groups;
            IEnumerable<IGrouping<int, MSTSDieselLocomotive>> distributedLocomotives = Simulator.Instance.PlayerLocomotive.Train.Cars.OfType<MSTSDieselLocomotive>().GroupBy((dieselLocomotive) => dieselLocomotive.DistributedPowerUnitId);
            groups = distributedLocomotives.Count();

            {
                foreach (IGrouping<int, MSTSDieselLocomotive> item in distributedLocomotives)
                {
                    int count = item.Count();
                }
            }

            if (groups != groupCount)
            {
                groupCount = groups;
                Resize();
            }
            if (groupCount == 0)
            {
                groupStatus = Catalog.GetString("Distributed power management not available with this player train.");
            }

        }
    }
}
