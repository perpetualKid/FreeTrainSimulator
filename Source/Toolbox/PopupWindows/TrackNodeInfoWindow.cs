
using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;
using FreeTrainSimulator.Models.Track;

using GetText;

using Microsoft.Xna.Framework;

namespace Orts.Toolbox.PopupWindows
{
    internal class TrackNodeInfoWindow : WindowBase
    {
        private enum SearchType
        {
            Track = 1,
            Road = 2,
        }

        private ContentArea contentArea;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private NameValueTextGrid trackNodeInfoGrid;
        private ControlLayout searchBoxLine;
        private TextInput searchBox;
        private Label headerLabel;
        private RadioButtonGroup searchTypeButtons;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public TrackNodeInfoWindow(WindowManager owner, ContentArea contentArea, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Track Node Information"), relativeLocation, new Point(260, 204), catalog)
        {
            this.contentArea = contentArea;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            RadioButton radioButton;
            layout = base.Layout(layout, headerScaling);
            ControlLayoutHorizontal line = layout.AddLayoutHorizontalLineOfText();
            searchBoxLine = line.AddLayoutHorizontalLineOfText();
            searchBoxLine.Visible = false;
            int columnWidth = searchBoxLine.RemainingWidth / 4;
            searchBoxLine.Add(searchBox = new TextInput(this, searchBoxLine.RemainingWidth / 3, searchBoxLine.RemainingHeight));
            searchBox.TextChanged += SearchBox_TextChanged;
            searchBox.OnEscapeKey += SearchBox_OnEscapeKey;
            searchBox.OnEnterKey += SearchBox_OnEnterKey;
            searchTypeButtons = new RadioButtonGroup();
            searchBoxLine.Add(radioButton = new RadioButton(this, searchTypeButtons) { State = true, Tag = SearchType.Track });
            searchBoxLine.Add(new Label(this, columnWidth, searchBoxLine.RemainingHeight, Catalog.GetString("Tracks")));
            searchBoxLine.Add(radioButton = new RadioButton(this, searchTypeButtons) { State = false, Tag = SearchType.Road });
            searchBoxLine.Add(new Label(this, columnWidth, searchBoxLine.RemainingHeight, Catalog.GetString("Roads")));

            line.Add(headerLabel = new Label(this, -line.Bounds.Width, 0, line.Bounds.Width, line.RemainingHeight, TextInput.SearchIcon + " " + Catalog.GetString("Find Track Node by Index"), HorizontalAlignment.Center, Owner.TextFontDefault, Color.White));
            headerLabel.OnClick += HeaderLabel_OnClick;

            layout.AddHorizontalSeparator();
            layout = layout.AddLayoutVertical();
            columnWidth = (int)(layout.RemainingWidth / Owner.DpiScaling / 2);
            trackNodeInfoGrid = new NameValueTextGrid(this, 0, 0, layout.RemainingWidth, layout.RemainingHeight)
            {
                InformationProvider = (contentArea?.Content as ToolboxContent)?.TrackNodeInfo,
                ColumnWidth = new int[] { columnWidth },
            };
            layout.Add(trackNodeInfoGrid);
            return layout;
        }

        private void SearchBox_OnEnterKey(object sender, EventArgs e)
        {
            if (int.TryParse((sender as TextInput).Text, out int nodeIndex))
            {
                if (searchTypeButtons.Selected != null)
                {
                    switch ((SearchType)searchTypeButtons.Selected.Tag)
                    {
                        case SearchType.Track:
                            IIndexedElement node = TrackModel.Instance(Owner.Game).TrackNodeByIndex(nodeIndex, TrackElementType.RailTrack);
                            if (node is TrackSegmentSection segmentSection)
                            {
                                contentArea?.UpdateScaleToFit(segmentSection.TopLeftBound, segmentSection.BottomRightBound);
                                contentArea?.SetTrackingPosition(segmentSection.MidPoint);
                                contentArea.Content.HighlightItem(MapContentType.Tracks, segmentSection.SectionSegments[0]);
                            }
                            break;
                        case SearchType.Road:
                            IIndexedElement roadNode = TrackModel.Instance(Owner.Game).TrackNodeByIndex(nodeIndex, TrackElementType.RoadTrack);
                            if (roadNode is TrackSegmentSection roadSegmentSection)
                            {
                                contentArea?.UpdateScaleToFit(roadSegmentSection.TopLeftBound, roadSegmentSection.BottomRightBound);
                                contentArea?.SetTrackingPosition(roadSegmentSection.MidPoint);
                                contentArea.Content.HighlightItem(MapContentType.Roads, roadSegmentSection.SectionSegments[0]);
                            }
                            break;
                    }
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

        internal void GameWindow_OnContentAreaChanged(object sender, ContentAreaChangedEventArgs e)
        {
            contentArea = e.ContentArea;
            trackNodeInfoGrid.InformationProvider = (contentArea?.Content as ToolboxContent)?.TrackNodeInfo;
        }
    }
}
