
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
    internal class TrackNodeInfoWindow : WindowBase
    {
        private enum SearchType
        {
            Track = 1,
            Road = 2,
        }

        private ContentArea contentArea;
        private readonly UserCommandController<UserCommand> userCommandController;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private NameValueTextGrid trackNodeInfoGrid;
        private ControlLayout searchBoxLine;
        private TextInput searchBox;
        private Label headerLabel;
        private RadioButtonGroup searchTypeButtons;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public TrackNodeInfoWindow(WindowManager owner, ContentArea contentArea, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Track Node Information"), relativeLocation, new Point(240, 202), catalog)
        {
            this.contentArea = contentArea;
            userCommandController = Owner.UserCommandController as UserCommandController<UserCommand>;
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

            line.Add(headerLabel = new Label(this, -line.Bounds.Width, 0, line.Bounds.Width, line.RemainingHeight, TextInput.SearchIcon + " " + Catalog.GetString("Find TrackNode by Index"), Graphics.HorizontalAlignment.Center, Owner.TextFontDefault, Color.White));
            headerLabel.OnClick += HeaderLabel_OnClick;

            layout.AddHorizontalSeparator();
            layout = layout.AddLayoutVertical();
            trackNodeInfoGrid = new NameValueTextGrid(this, 0, 0, layout.RemainingWidth, layout.RemainingHeight)
            {
                InformationProvider = contentArea?.Content.TrackNodeInfo,
                ColumnWidth = new int[] { 120 - 4 },
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
                            ITrackNode node = TrackModel.Instance<RailTrackModel>(Owner.Game).NodeByIndex(nodeIndex);
                            if (node is TrackSegmentSection segmentSection)
                            {
                                contentArea?.UpdateScaleToFit(segmentSection.TopLeftBound, segmentSection.BottomRightBound);
                                contentArea?.SetTrackingPosition(segmentSection.MidPoint);
                                contentArea.Content.HighlightItem(Common.MapViewItemSettings.Tracks, segmentSection.SectionSegments[0]);
                            }
                            break;
                        case SearchType.Road:
                            ITrackNode roadNode = TrackModel.Instance<RoadTrackModel>(Owner.Game).NodeByIndex(nodeIndex);
                            if (roadNode is TrackSegmentSection roadSegmentSection)
                            {
                                contentArea?.UpdateScaleToFit(roadSegmentSection.TopLeftBound, roadSegmentSection.BottomRightBound);
                                contentArea?.SetTrackingPosition(roadSegmentSection.MidPoint);
                                contentArea.Content.HighlightItem(Common.MapViewItemSettings.Roads, roadSegmentSection.SectionSegments[0]);
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
            trackNodeInfoGrid.InformationProvider = contentArea?.Content?.TrackNodeInfo;
        }

        private void TabAction(UserCommandArgs args)
        {
            if (args is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
            {
            }
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
    }
}
