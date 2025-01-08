using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Imported.Track;
using FreeTrainSimulator.Toolbox.Settings;

using GetText;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Toolbox.PopupWindows
{
    internal sealed class TrainPathWindow : WindowBase
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

        private sealed class TrainPathMetadataInformation : DetailInfoBase
        {
            public void Update(TrainPathBase path)
            {
                if (path == null || path.PathFile == null)
                    Clear();
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
        private readonly UserCommandController<UserCommand> userCommandController;
        private TrainPathBase currentPath;
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
        private readonly ProfileToolboxSettingsModel toolboxSettings;
        private readonly TrainPathMetadataInformation metadataInformationProvider = new TrainPathMetadataInformation();
        private PathEditor pathEditor;

        public TrainPathWindow(WindowManager owner, ProfileToolboxSettingsModel settings, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Train Path Details"), relativeLocation, new Point(360, 300), catalog)
        {
            toolboxSettings = settings;
            contentUpdated = true;
            pathEditor = (Owner.Game as GameWindow)?.PathEditor;
            pathEditor.OnPathUpdated += PathEditor_OnPathUpdated;
            pathEditor.OnPathChanged += PathEditor_OnPathChanged;
            userCommandController = owner.UserCommandController as UserCommandController<UserCommand>;
        }

        private void PathEditor_OnPathChanged(object sender, PathEditorChangedEventArgs e)
        {
            currentPath = e.Path;
            UpdateTrainPath();
        }

        private void PathEditor_OnPathUpdated(object sender, PathEditorChangedEventArgs e)
        {
            UpdateTrainPath();
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
                headerLine.Add(headerLabel = new Label(this, layoutContainer.RemainingWidth, headerLine.RemainingHeight, Catalog.GetString("Available Train Paths") + TextInput.SearchIcon) { Alignment = HorizontalAlignment.Center });
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
            if (e.ContentArea != null)
            {
                pathEditor = (Owner.Game as GameWindow)?.PathEditor;
                pathEditor.OnPathUpdated += PathEditor_OnPathUpdated;
                pathEditor.OnPathChanged += PathEditor_OnPathChanged;
            }
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
                if (currentPath != (currentPath = pathEditor?.TrainPath))
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

            if (null != pathEditor)
            {
                RadioButtonGroup group = new RadioButtonGroup();
                ControlLayout line;
                FrozenSet<PathModelCore> trainPaths = (Orts.Formats.Msts.RuntimeData.GameInstance(Owner.Game) as TrackData).TrainPaths;
                foreach (PathModelCore path in trainPaths)
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
                pathEditor.InitializePath(null);
                (line.Controls[0] as RadioButton).State = false;
            }
            else if (line?.Tag is PathModelCore path)
            {
                if (!((line.Controls[0] as RadioButton).State = pathEditor.InitializePath(path)))
                {
                    System.Windows.Forms.MessageBox.Show("Invalid path data");
                }
            }
        }

        private void PathSelectRadioButton_OnClick(object sender, MouseClickEventArgs e)
        {
            ControlLayout line = (sender as RadioButton)?.Container;
            if ((currentPath == null || line.BorderColor == Color.Transparent) && line?.Tag is PathModelCore path)
            {
                if (!((line.Controls[0] as RadioButton).State = pathEditor.InitializePath(path)))
                {
                    System.Windows.Forms.MessageBox.Show("Invalid path data");
                }
            }
            else
            {
                pathEditor.InitializePath(null);
                (sender as RadioButton).State = false;
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
                for (int i = 0; i < currentPath.PathPoints.Count; i++)
                {
                    TrainPathPointBase item = currentPath.PathPoints[i];
                    line = pathNodeScrollbox.Client.AddLayoutHorizontalLineOfText();
                    line.Add(new Label(this, columnWidth, Owner.TextFontDefault.Height, $"{i:D2}"));
                    line.Add(new TrainPathItemControl(this, item.NodeType));
                    line.Add(new Label(this, columnWidth * 2, Owner.TextFontDefault.Height, item.NodeType.ToString()));
                    line.Add(new Checkbox(this, false, CheckMarkStyle.Marks, true) { State = item.ValidationResult == PathNodeInvalidReasons.None, ReadOnly = true });
                    line.OnClick += PathNodeLine_OnClick;
                    line.Tag = i;
                }
            }
            else
                ClearPathSelection();
            metadataInformationProvider.Update(currentPath);
            pathNodeScrollbox.UpdateContent();
        }

        private void SelectActivePath()
        {
            if (null == pathScrollbox || null == currentPath)
                return;

            WindowControl pathLine = pathScrollbox.Client.Controls.Where(c => c.Tag is PathModelCore pathModel && pathModel.SourceFile() == currentPath.FilePath).FirstOrDefault();
            foreach (WindowControl control in pathScrollbox.Client.Controls)
            {
                if (control != pathLine)
                {
                    control.BorderColor = Color.Transparent;
                }
                else
                {
                    control.BorderColor = Color.Gray;
                    ((control as ControlLayout)?.Controls[0] as RadioButton).State = true;
                    pathScrollbox.SetFocusOn(control);
                }
            }
        }

        private void ClearPathSelection()
        {
            if (null == pathScrollbox || null == currentPath)
                return;
            foreach (WindowControl control in pathScrollbox.Client.Controls)
            {
                control.BorderColor = Color.Transparent;
                ((control as ControlLayout)?.Controls[0] as RadioButton).State = false;
            }
        }

        private void PathNodeLine_OnClick(object sender, MouseClickEventArgs e)
        {
            ControlLayout line = sender as ControlLayout;
            int lineNumber = (int)line.Tag;
            selectedPathNodeLine = lineNumber == selectedPathNodeLine ? -1 : lineNumber;
            SelectPathNodeLineByIndex();
        }

        private void SelectPathNodeLineByIndex()
        {
            if (selectedPathNodeLine < 0 || selectedPathNodeLine >= pathNodeScrollbox.Client.Count)
            {
                pathEditor.HighlightPathItem(-1);
                selectedPathNodeLine = Math.Clamp(selectedPathNodeLine, -1, pathNodeScrollbox.Client.Count);
            }
            foreach (WindowControl control in pathNodeScrollbox.Client.Controls)
            {
                if ((int)control.Tag != selectedPathNodeLine)
                    control.BorderColor = Color.Transparent;
                else
                {
                    control.BorderColor = Color.Gray;
                    pathEditor.HighlightPathItem(selectedPathNodeLine);
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
