using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common.Input;
using Orts.Graphics.MapView;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Models.Track;

namespace Orts.Toolbox.PopupWindows
{
    internal class TrainPathDetailWindow : WindowBase
    {
        private ContentArea contentArea;
        private readonly UserCommandController<UserCommand> userCommandController;
        private TrainPath path;
        private VerticalScrollboxControlLayout scrollbox;
        private int selectedLine;
        private long keyRepeatTick;

        public TrainPathDetailWindow(WindowManager owner, ContentArea contentArea, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Train Path Details"), relativeLocation, new Point(360, 300), catalog)
        {
            this.contentArea = contentArea;
            this.userCommandController = owner.UserCommandController as UserCommandController<UserCommand>;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            layout = layout.AddLayoutHorizontal();
            layout.AddSpace(ControlLayout.SeparatorPadding, layout.RemainingHeight / 2);
            scrollbox = layout.AddLayoutScrollboxVertical(layout.RemainingWidth).Container as VerticalScrollboxControlLayout;

            return layout;
        }

        internal void GameWindow_OnContentAreaChanged(object sender, ContentAreaChangedEventArgs e)
        {
            contentArea = e.ContentArea;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            if (shouldUpdate && (path != (path = (contentArea?.Content as ToolboxContent)?.TrainPath)))
            {
                UpdateTrainPath();
            }
            base.Update(gameTime, shouldUpdate);
        }

        private void TabAction(UserCommandArgs args)
        {
            if (args is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
            {
            }
        }

        private void MoveDown(UserCommandArgs args)
        {
            if (keyRepeatTick < Environment.TickCount64)
            {
                keyRepeatTick = Environment.TickCount64 + WindowManager.KeyRepeatDelay;
                selectedLine++;
                SelectLineByIndex();
            }
            args.Handled = true;
        }

        private void MoveUp(UserCommandArgs args)
        {
            if (keyRepeatTick < Environment.TickCount64)
            {
                keyRepeatTick = Environment.TickCount64 + WindowManager.KeyRepeatDelay;
                selectedLine--;
                SelectLineByIndex();
            }
            args.Handled = true;
        }

        protected override void FocusSet()
        {
            userCommandController.AddEvent(UserCommand.MoveUp, KeyEventType.KeyDown, MoveUp, true);
            userCommandController.AddEvent(UserCommand.MoveDown, KeyEventType.KeyDown, MoveDown, true);
            base.FocusSet();
        }

        protected override void FocusLost()
        {
            userCommandController.RemoveEvent(UserCommand.MoveUp, KeyEventType.KeyDown, MoveUp);
            userCommandController.RemoveEvent(UserCommand.MoveDown, KeyEventType.KeyDown, MoveDown);
            base.FocusLost();
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.DisplayLocationWindow, KeyEventType.KeyPressed, TabAction, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DisplayLocationWindow, KeyEventType.KeyPressed, TabAction);
            return base.Close();
        }

        private void UpdateTrainPath()
        {
            scrollbox.Clear();
            selectedLine = -1;

            ControlLayout line;
            if (path != null)
            {
                int i = 0;
                foreach (TrainPathItem item in path.PathItems)
                {
                    line = scrollbox.Client.AddLayoutHorizontalLineOfText();
                    line.Add(new TrainPathItemControl(this, item.PathNode.NodeType));
                    line.Add(new Label(this, 100, Owner.TextFontDefault.Height, item.PathNode.NodeType.ToString()));
                    line.OnClick += Line_OnClick;
                    line.Tag = i;
                    i++;
                }
            }

            scrollbox.UpdateContent();
        }

        private void Line_OnClick(object sender, MouseClickEventArgs e)
        {
            ControlLayout line = (sender as ControlLayout);
            int lineNumber = (int)line.Tag;
            selectedLine = lineNumber == selectedLine ? -1 : lineNumber;
            SelectLineByIndex();
        }

        private void SelectLineByIndex()
        {
            if (selectedLine < 0 || selectedLine >= scrollbox.Client.Count)
            {
                (contentArea?.Content as ToolboxContent)?.HighlightPathItem(-1);
                selectedLine = Math.Clamp(selectedLine, -1, scrollbox.Client.Count);
            }
            foreach (WindowControl control in scrollbox.Client.Controls)
            {
                if ((int)control.Tag != selectedLine)
                    control.BorderColor = Color.Transparent;
                else
                {
                    control.BorderColor = Color.Gray;
                    (contentArea?.Content as ToolboxContent)?.HighlightPathItem(selectedLine);
                    scrollbox.SetFocusOn(control);
                }
            }
        }
    }
}
