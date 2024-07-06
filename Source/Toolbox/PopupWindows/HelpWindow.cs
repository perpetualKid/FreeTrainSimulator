using System;
using System.Collections.Generic;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Toolbox.Settings;

namespace Orts.Toolbox.PopupWindows
{
    public class HelpWindow : WindowBase
    {
        private enum SearchColumn
        {
            Command = 1,
            Key = 2,
        }

        private readonly List<WindowControl> controls = new List<WindowControl>();
#pragma warning disable CA2213 // Disposable fields should be disposed
        private VerticalScrollboxControlLayout scrollbox;
        private TextInput searchBox;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private SearchColumn searchMode;

        public HelpWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Help"), relativeLocation, new Point(360, 134), catalog)
        {
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            
            ControlLayoutHorizontal line = layout.AddLayoutHorizontalLineOfText();
            int width = line.RemainingWidth / 2;
            Label headerLabel;
            line.Add(headerLabel = new Label(this, width, line.RemainingHeight, Catalog.GetString("Command") + TextInput.SearchIcon, HorizontalAlignment.Center));
            headerLabel.Tag = SearchColumn.Command;
            headerLabel.OnClick += HeaderLabel_OnClick;
            line.Add(headerLabel = new Label(this, width, line.RemainingHeight, Catalog.GetString("Key") + TextInput.SearchIcon, HorizontalAlignment.Center));
            headerLabel.Tag = SearchColumn.Key;
            headerLabel.OnClick += HeaderLabel_OnClick;
            line.Add(searchBox = new TextInput(this, -line.Bounds.Width, 0, layout.RemainingWidth, (int)(Owner.TextFontDefault.Height * 1.2)));
            searchBox.Visible = false;
            searchBox.TextChanged += SearchBox_TextChanged;
            searchBox.OnEscapeKey += SearchBox_OnEscapeKey;
            layout.AddHorizontalSeparator();
            scrollbox = new VerticalScrollboxControlLayout(this, layout.RemainingWidth, layout.RemainingHeight);
            layout.Add(scrollbox);
            controls.Clear();

            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
            {
                line = scrollbox.Client.AddLayoutHorizontalLineOfText();
                width = line.RemainingWidth / 2;
                line.Add(new Label(this, width, line.RemainingHeight, command.GetLocalizedDescription()));
                line.Add(new Label(this, width, line.RemainingHeight, InputSettings.UserCommands[command]?.ToString()));
                controls.Add(line);
            }
            return layout;
        }

        private void HeaderLabel_OnClick(object sender, MouseClickEventArgs e)
        {
            searchMode = (SearchColumn)((sender as Label).Tag);
            searchBox.Container.Controls[0].Visible = false;
            searchBox.Container.Controls[1].Visible = false;
            searchBox.Visible = true;
        }

        private void SearchBox_OnEscapeKey(object sender, EventArgs e)
        {
            searchBox.Text = null;
            searchBox.Visible = false;
            searchBox.Container.Controls[0].Visible = true;
            searchBox.Container.Controls[1].Visible = true;
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            Filter((sender as TextInput).Text, searchMode);
        }

        private void Filter(string searchText, SearchColumn searchFlags)
        {
            scrollbox.Clear();

            if (string.IsNullOrEmpty(searchText))
            {
                foreach (ControlLayoutHorizontal line in controls)
                    scrollbox.Client.Add(line);
            }
            else
            {
                foreach (ControlLayoutHorizontal line in controls)
                {
                    switch (searchMode)
                    {

                        case SearchColumn.Command:
                            if ((line.Controls[0] as Label)?.Text?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                                scrollbox.Client.Add(line);
                            break;
                        case SearchColumn.Key:
                            if ((line.Controls[1] as Label)?.Text?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                                scrollbox.Client.Add(line);
                            break;
                    }
                }
            }
            scrollbox.UpdateContent();
        }
    }
}
