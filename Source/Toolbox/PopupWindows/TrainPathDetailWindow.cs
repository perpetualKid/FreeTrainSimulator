using System;
using System.ComponentModel;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Common.Input;
using Orts.Graphics.MapView;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Models.Track;
using Orts.Toolbox.Settings;

namespace Orts.Toolbox.PopupWindows
{
    internal class TrainPathDetailWindow : WindowBase
    {
        private enum TabSettings
        {
            [Description("Path Nodes")]
            PathNodes,
            [Description("Path Data")]
            MetaData,
        }

        private class TrainPathMetadataInformation : DetailInfoBase
        {
            public void Update(TrainPath path)
            {
                if (path == null)
                    this.Clear();
                else
                {
                    this["Path ID"] = path.PathFile.PathID;
                    this["Path Name"] = path.PathFile.Name;
                    this["Start"] = path.PathFile.Start;
                    this["End"] = path.PathFile.End;
                    this["Player Path"] = path.PathFile.PlayerPath ? "Yes" : "No";
                    this["Flags"] = path.PathFile.Flags.ToString();
                }
            }
        }

        private ContentArea contentArea;
        private readonly UserCommandController<UserCommand> userCommandController;
        private TrainPath path;
        private VerticalScrollboxControlLayout scrollbox;
        private int selectedLine;
        private long keyRepeatTick;
        private int columnWidth;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private TabControl<TabSettings> tabControl;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private NameValueTextGrid metadataGrid;
        private readonly ToolboxSettings toolboxSettings;
        private readonly TrainPathMetadataInformation metadataInformationProvider = new TrainPathMetadataInformation();

        public TrainPathDetailWindow(WindowManager owner, ToolboxSettings settings, ContentArea contentArea, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Train Path Details"), relativeLocation, new Point(360, 300), catalog)
        {
            toolboxSettings = settings;
            this.contentArea = contentArea;
            this.userCommandController = owner.UserCommandController as UserCommandController<UserCommand>;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            tabControl = new TabControl<TabSettings>(this, layout.RemainingWidth, layout.RemainingHeight);
            tabControl.TabLayouts[TabSettings.PathNodes] = (layoutContainer) =>
            {
                columnWidth = layoutContainer.RemainingWidth / 8;
                ControlLayout headerLine = layoutContainer.AddLayoutHorizontalLineOfText();
                headerLine.Add(new Label(this, columnWidth, headerLine.RemainingHeight, Catalog.GetString("Idx")));
                headerLine.Add(new Label(this, columnWidth * 2, headerLine.RemainingHeight, Catalog.GetString("Type")));
                headerLine.Add(new Label(this, columnWidth, headerLine.RemainingHeight, Catalog.GetString("Valid")));
                layoutContainer.AddHorizontalSeparator();
                layoutContainer = layoutContainer.AddLayoutHorizontal();

                layoutContainer.AddSpace(ControlLayout.SeparatorPadding, layoutContainer.RemainingHeight / 2);
                scrollbox = layoutContainer.AddLayoutScrollboxVertical(layoutContainer.RemainingWidth).Container as VerticalScrollboxControlLayout;
            };
            tabControl.TabLayouts[TabSettings.MetaData] = (layoutContainer) =>
            {
                columnWidth = layoutContainer.RemainingWidth / 4;
                ControlLayout headerLine = layoutContainer.AddLayoutHorizontalLineOfText();
                headerLine.Add(new Label(this, columnWidth, headerLine.RemainingHeight, Catalog.GetString("Setting")));
                headerLine.Add(new Label(this, columnWidth * 3, headerLine.RemainingHeight, Catalog.GetString("Value")));
                layoutContainer.AddHorizontalSeparator();
                metadataGrid = new NameValueTextGrid(this, 0, 0, layoutContainer.RemainingWidth, layoutContainer.RemainingHeight)
                {
                    InformationProvider = metadataInformationProvider,
                    ColumnWidth = new int[] { columnWidth, columnWidth * 3 },
                };
                layoutContainer.Add(metadataGrid);
            };

            layout.Add(tabControl);
            return layout;
        }

        protected override void Initialize()
        {
            base.Initialize();
            if (toolboxSettings.RestoreLastView && EnumExtension.GetValue(toolboxSettings.PopupSettings[ToolboxWindowType.TrainPathDetailWindow], out TabSettings tab))
                tabControl.TabAction(tab);
            tabControl.TabChanged += TabControl_TabChanged;
        }

        private void TabControl_TabChanged(object sender, TabChangedEventArgs<TabSettings> e)
        {
            toolboxSettings.PopupSettings[ToolboxWindowType.TrainPathDetailWindow] = e.Tab.ToString();
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
                tabControl?.TabAction();
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
                metadataInformationProvider.Update(path);
                for (int i = 0; i < path.PathItems.Count; i++)
                {
                    TrainPathItem item = path.PathItems[i];
                    line = scrollbox.Client.AddLayoutHorizontalLineOfText();
                    line.Add(new Label(this, columnWidth, Owner.TextFontDefault.Height, $"{i:D2}"));
                    line.Add(new TrainPathItemControl(this, item.PathNode.NodeType));
                    line.Add(new Label(this, columnWidth * 2, Owner.TextFontDefault.Height, item.PathNode.NodeType.ToString()));
                    line.Add(new Checkbox(this, false, CheckMarkStyle.Marks, true) { State = !item.Invalid, ReadOnly = true });
                    line.OnClick += Line_OnClick;
                    line.Tag = i;
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
