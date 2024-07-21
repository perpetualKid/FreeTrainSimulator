using System;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;
using FreeTrainSimulator.Models.Track;

using GetText;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Toolbox.PopupWindows
{
    internal sealed class TrackItemInfoWindow : WindowBase
    {
        private ContentArea contentArea;
        private readonly UserCommandController<UserCommand> userCommandController;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private NameValueTextGrid trackItemInfoGrid;
        private ControlLayout searchBoxLine;
        private TextInput searchBox;
        private Label headerLabel;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public TrackItemInfoWindow(WindowManager owner, ContentArea contentArea, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Track Item Information"), relativeLocation, new Point(240, 202), catalog)
        {
            this.contentArea = contentArea;
            userCommandController = Owner.UserCommandController as UserCommandController<UserCommand>;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);

            ControlLayoutHorizontal line = layout.AddLayoutHorizontalLineOfText();
            searchBoxLine = line.AddLayoutHorizontalLineOfText();
            searchBoxLine.Visible = false;
            int columnWidth = searchBoxLine.RemainingWidth / 4;
            searchBoxLine.Add(searchBox = new TextInput(this, searchBoxLine.RemainingWidth, searchBoxLine.RemainingHeight));
            searchBox.TextChanged += SearchBox_TextChanged;
            searchBox.OnEscapeKey += SearchBox_OnEscapeKey;
            searchBox.OnEnterKey += SearchBox_OnEnterKey;

            line.Add(headerLabel = new Label(this, -line.Bounds.Width, 0, line.Bounds.Width, line.RemainingHeight, TextInput.SearchIcon + " " + Catalog.GetString("Find Track Item by Index"), HorizontalAlignment.Center, Owner.TextFontDefault, Color.White));
            headerLabel.OnClick += HeaderLabel_OnClick;

            layout.AddHorizontalSeparator();

            layout = layout.AddLayoutVertical();
            columnWidth = (int)(layout.RemainingWidth / Owner.DpiScaling / 3);
            trackItemInfoGrid = new NameValueTextGrid(this, 0, 0, layout.RemainingWidth, layout.RemainingHeight)
            {
                InformationProvider = (contentArea?.Content as ToolboxContent)?.TrackItemInfo,
                ColumnWidth = new int[] { columnWidth, columnWidth * 2 },
            };
            layout.Add(trackItemInfoGrid);
            return layout;

        }
        internal void GameWindow_OnContentAreaChanged(object sender, ContentAreaChangedEventArgs e)
        {
            contentArea = e.ContentArea;
            trackItemInfoGrid.InformationProvider = (contentArea?.Content as ToolboxContent)?.TrackItemInfo;
        }

        private void SearchBox_OnEnterKey(object sender, EventArgs e)
        {
            if (int.TryParse((sender as TextInput).Text, out int nodeIndex))
            {
                IIndexedElement item = TrackModel.Instance(Owner.Game).TrackItemByIndex(nodeIndex);
                if (item is TrackItemBase trackItem)
                {
                    //                    contentArea?.UpdateScaleToFit(segmentSection.TopLeftBound, segmentSection.BottomRightBound);
                    contentArea?.SetTrackingPosition(trackItem.Location);
                    //                    contentArea.Content.HighlightItem(Common.MapViewItemSettings.Tracks, segmentSection.SectionSegments[0]);
                }
            }
            SearchBox_OnEscapeKey(sender, e);
        }

        private void HeaderLabel_OnClick(object sender, MouseClickEventArgs e)
        {
            headerLabel.Visible = false;
            searchBoxLine.Visible = true;
        }

        private void SearchBox_OnEscapeKey(object sender, EventArgs e)
        {
            searchBox.Text = null;
            searchBoxLine.Visible = false;
            headerLabel.Visible = true;
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            string searchText = (sender as TextInput).Text;
            if (!string.IsNullOrEmpty(searchText) && !char.IsDigit(searchText[^1]))
                (sender as TextInput).Text = searchText[..^1];
        }


    }
}
