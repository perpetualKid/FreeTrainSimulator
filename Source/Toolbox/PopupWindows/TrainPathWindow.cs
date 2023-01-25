using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using GetText;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Common.Input;
using Orts.Formats.Msts.Files;
using Orts.Graphics.MapView;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Models.Track;
using Orts.Toolbox.Settings;

using SharpDX.Direct3D9;

namespace Orts.Toolbox.PopupWindows
{
    internal class TrainPathWindow : WindowBase
    {
        private enum TabSettings
        {
            [Description("Path Nodes")]
            PathNodes,
            [Description("Path Data")]
            MetaData,
            [Description("Paths")]
            Paths,
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

        private bool contentUpdated;
        private ContentArea contentArea;
        private readonly UserCommandController<UserCommand> userCommandController;
        private TrainPath currentPath;
        private VerticalScrollboxControlLayout pathNodeScrollbox;
        private VerticalScrollboxControlLayout pathScrollbox;
        private readonly List<WindowControl> pathControls = new List<WindowControl>();
        private TextInput searchBox;
        private int selectedPathNodeLine;
        private long keyRepeatTick;
        private int columnWidth;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private TabControl<TabSettings> tabControl;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private NameValueTextGrid metadataGrid;
        private readonly ToolboxSettings toolboxSettings;
        private readonly TrainPathMetadataInformation metadataInformationProvider = new TrainPathMetadataInformation();

        public TrainPathWindow(WindowManager owner, ToolboxSettings settings, ContentArea contentArea, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Train Path Details"), relativeLocation, new Point(360, 300), catalog)
        {
            toolboxSettings = settings;
            this.contentArea = contentArea;
            contentUpdated = true;
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
                pathNodeScrollbox = layoutContainer.AddLayoutScrollboxVertical(layoutContainer.RemainingWidth).Container as VerticalScrollboxControlLayout;
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
            tabControl.TabLayouts[TabSettings.Paths] = (layoutContainer) =>
            {
                ControlLayout headerLine = layoutContainer.AddLayoutHorizontalLineOfText();
                Label headerLabel;
                headerLine.Add(headerLabel = new Label(this, layoutContainer.RemainingWidth, headerLine.RemainingHeight, Catalog.GetString("Available Train Paths") + TextInput.SearchIcon) { Alignment = Graphics.HorizontalAlignment.Center });
                headerLabel.OnClick += PathSearchHeaderLabel_OnClick;
                headerLine.Add(searchBox = new TextInput(this, -headerLine.Bounds.Width, 0, layout.RemainingWidth, (int)(Owner.TextFontDefault.Height * 1.2)));
                searchBox.Visible = false;
                searchBox.TextChanged += SearchBox_TextChanged;
                searchBox.OnEscapeKey += SearchBox_OnEscapeKey;
                layoutContainer.AddHorizontalSeparator();
                pathScrollbox = new VerticalScrollboxControlLayout(this, layoutContainer.RemainingWidth, layoutContainer.RemainingHeight);
                layoutContainer.Add(pathScrollbox);
                UpdateAllTrainPaths();
                SelectActivePath();
            };
            layout.Add(tabControl);
            return layout;
        }

        private void PathSearchHeaderLabel_OnClick(object sender, MouseClickEventArgs e)
        {
            searchBox.Container.Controls[0].Visible = false;
            searchBox.Visible = true;
        }

        private void SearchBox_OnEscapeKey(object sender, EventArgs e)
        {
            searchBox.Text = null;
            searchBox.Visible = false;
            searchBox.Container.Controls[0].Visible = true;
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            FilterPaths((sender as TextInput).Text);
        }

        protected override void Initialize()
        {
            base.Initialize();
            if (toolboxSettings.RestoreLastView && EnumExtension.GetValue(toolboxSettings.PopupSettings[ToolboxWindowType.TrainPathWindow], out TabSettings tab))
                tabControl.TabAction(tab);
            tabControl.TabChanged += TabControl_TabChanged;
        }

        private void TabControl_TabChanged(object sender, TabChangedEventArgs<TabSettings> e)
        {
            toolboxSettings.PopupSettings[ToolboxWindowType.TrainPathWindow] = e.Tab.ToString();
        }

        internal void GameWindow_OnContentAreaChanged(object sender, ContentAreaChangedEventArgs e)
        {
            contentArea = e.ContentArea;
            contentUpdated = true;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            if (shouldUpdate)
            {
                if (contentUpdated)
                {
                    UpdateAllTrainPaths();
                }
                if (currentPath != (currentPath = (contentArea?.Content as ToolboxContent)?.TrainPath))
                {
                    UpdateTrainPath();
                }
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
                switch (tabControl.CurrentTab)
                {
                    case TabSettings.PathNodes:
                        selectedPathNodeLine++;
                        SelectPathNodeLineByIndex();
                        break;
                }
            }
            args.Handled = true;
        }

        private void MoveUp(UserCommandArgs args)
        {
            if (keyRepeatTick < Environment.TickCount64)
            {
                keyRepeatTick = Environment.TickCount64 + WindowManager.KeyRepeatDelay;
                switch (tabControl.CurrentTab)
                {
                    case TabSettings.PathNodes:
                        selectedPathNodeLine--;
                        SelectPathNodeLineByIndex();
                        break;
                }
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
            userCommandController.AddEvent(UserCommand.DisplayTrainPathWindow, KeyEventType.KeyPressed, TabAction, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DisplayTrainPathWindow, KeyEventType.KeyPressed, TabAction);
            return base.Close();
        }

        private void UpdateAllTrainPaths()
        {
            contentUpdated = false;
            if (null == pathScrollbox)
                return;

            pathScrollbox.Clear();
            pathControls.Clear();

            if (null != contentArea)
            {
                RadioButtonGroup group = new RadioButtonGroup();
                ControlLayout line;
                IEnumerable<Models.Simplified.Path> trainPaths = (Formats.Msts.RuntimeData.GameInstance(Owner.Game) as TrackData).TrainPaths;
                foreach (Models.Simplified.Path path in trainPaths)
                {
                    RadioButton radioButton;
                    line = pathScrollbox.Client.AddLayoutHorizontalLineOfText();
                    line.Add(radioButton = new RadioButton(this, group));
                    radioButton.OnClick += PathSelectRadioButton_OnClick;
                    line.Add(new Label(this, line.RemainingWidth, Owner.TextFontDefault.Height, path.Name));
                    line.OnClick += PathSelectLine_OnClick;
                    pathControls.Add(line);
                    line.Tag = path;
                }
                pathScrollbox.UpdateContent();
            }
        }

        private void PathSelectLine_OnClick(object sender, MouseClickEventArgs e)
        {
            ControlLayout line = sender as ControlLayout;
            if ((line.Controls[0] as RadioButton)?.State == true)
            {
                ((ToolboxContent)contentArea?.Content).InitializePath(null, null);
                (line.Controls[0] as RadioButton).State = false;
            }
            else if (line?.Tag is Models.Simplified.Path path)
            {
                try
                {
                    PathFile patFile = new PathFile(path.FilePath);
                    ((ToolboxContent)contentArea?.Content).InitializePath(patFile, path.FilePath);
                    (line.Controls[0] as RadioButton).State = true;
                }
                catch (Exception ex) when (ex is Exception)
                {
                    System.Windows.Forms.MessageBox.Show("Invalid path data");
                }
            }
        }

        private void PathSelectRadioButton_OnClick(object sender, MouseClickEventArgs e)
        {
            ControlLayout line = (sender as RadioButton)?.Container;
            if ((currentPath == null || line.BorderColor == Color.Transparent) && line?.Tag is Models.Simplified.Path path)
            {
                PathFile patFile = new PathFile(path.FilePath);
                ((ToolboxContent)contentArea?.Content).InitializePath(patFile, path.FilePath);
            }
            else
            {
                try
                {
                    ((ToolboxContent)contentArea?.Content).InitializePath(null, null);
                    (sender as RadioButton).State = false;
                }
                catch (Exception ex) when (ex is Exception)
                {
                    System.Windows.Forms.MessageBox.Show("Invalid path data");
                }
            }
        }

        private void UpdateTrainPath()
        {
            if (null == pathNodeScrollbox)
                return;

            foreach (WindowControl item in pathNodeScrollbox.Client.Controls)
                item.OnClick -= PathNodeLine_OnClick;
            pathNodeScrollbox.Clear();
            selectedPathNodeLine = -1;

            ControlLayout line;
            if (currentPath != null)
            {
                SelectActivePath();
                metadataInformationProvider.Update(currentPath);
                for (int i = 0; i < currentPath.PathItems.Count; i++)
                {
                    TrainPathPoint item = currentPath.PathItems[i];
                    line = pathNodeScrollbox.Client.AddLayoutHorizontalLineOfText();
                    line.Add(new Label(this, columnWidth, Owner.TextFontDefault.Height, $"{i:D2}"));
                    line.Add(new TrainPathItemControl(this, item.NodeType));
                    line.Add(new Label(this, columnWidth * 2, Owner.TextFontDefault.Height, item.NodeType.ToString()));
                    line.Add(new Checkbox(this, false, CheckMarkStyle.Marks, true) { State = item.ValidationResult == TrainPathNodeInvalidReasons.None, ReadOnly = true });
                    line.OnClick += PathNodeLine_OnClick;
                    line.Tag = i;
                }
            }
            pathNodeScrollbox.UpdateContent();
        }

        private void SelectActivePath()
        {
            if (null == pathScrollbox || null == currentPath)
                return;

            WindowControl pathLine = pathScrollbox.Client.Controls.Where(c => c.Tag is Models.Simplified.Path pathModel && pathModel.FilePath == currentPath.FilePath).FirstOrDefault();
            foreach (WindowControl control in pathScrollbox.Client.Controls)
            {
                if (control != pathLine)
                {
                    control.BorderColor = Color.Transparent;
                    //                    ((pathLine as ControlLayout)?.Controls[0] as RadioButton).State = false;
                }
                else
                {
                    control.BorderColor = Color.Gray;
                    pathScrollbox.SetFocusOn(control);
                }
            }

        }

        private void PathNodeLine_OnClick(object sender, MouseClickEventArgs e)
        {
            ControlLayout line = (sender as ControlLayout);
            int lineNumber = (int)line.Tag;
            selectedPathNodeLine = lineNumber == selectedPathNodeLine ? -1 : lineNumber;
            SelectPathNodeLineByIndex();
        }

        private void SelectPathNodeLineByIndex()
        {
            if (selectedPathNodeLine < 0 || selectedPathNodeLine >= pathNodeScrollbox.Client.Count)
            {
                (contentArea?.Content as ToolboxContent)?.HighlightPathItem(-1);
                selectedPathNodeLine = Math.Clamp(selectedPathNodeLine, -1, pathNodeScrollbox.Client.Count);
            }
            foreach (WindowControl control in pathNodeScrollbox.Client.Controls)
            {
                if ((int)control.Tag != selectedPathNodeLine)
                    control.BorderColor = Color.Transparent;
                else
                {
                    control.BorderColor = Color.Gray;
                    (contentArea?.Content as ToolboxContent)?.HighlightPathItem(selectedPathNodeLine);
                    pathNodeScrollbox.SetFocusOn(control);
                }
            }
        }

        private void FilterPaths(string searchText)
        {
            pathScrollbox.Clear();

            if (string.IsNullOrEmpty(searchText))
            {
                foreach (ControlLayoutHorizontal line in pathControls)
                    pathScrollbox.Client.Add(line);
            }
            else
            {
                foreach (ControlLayoutHorizontal line in pathControls)
                {
                    if ((line.Controls[1] as Label)?.Text?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                        pathScrollbox.Client.Add(line);
                }
            }
            pathScrollbox.UpdateContent();
            SelectActivePath();
        }
    }
}
