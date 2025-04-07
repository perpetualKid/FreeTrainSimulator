using System;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Track;

using GetText;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Toolbox.PopupWindows
{
    public class TrainPathSaveEventArgs : EventArgs
    {
        public PathModelHeader PathDetails { get; }

        public TrainPathSaveEventArgs(PathModelHeader path)
        {
            PathDetails = path;
        }
    }

    public class TrainPathSaveWindow : WindowBase
    {
        private readonly UserCommandController<UserCommand> userCommandController;

#pragma warning disable CA2213 // Disposable fields should be disposed
        private Label saveButton;
        private Label cancelButton;
        private TextInput pathNameInput;
        private TextInput pathIdInput;
        private TextInput pathStartInput;
        private TextInput pathEndInput;
        private Checkbox playerPathInput;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public event EventHandler<TrainPathSaveEventArgs> OnSavePath;
        public event EventHandler OnSaveCancel;

        public TrainPathSaveWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Train Path Meta Data"), relativeLocation, new Point(360, 300), catalog)
        {
            Modal = true;
            ZOrder = 50;
            userCommandController = Owner.UserCommandController as UserCommandController<UserCommand>;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            int columnWidth = layout.RemainingWidth / 4;
            int rowHeight = (int)(Owner.TextFontDefault.Height * 1.5);

            ControlLayoutHorizontal line;

            line = layout.AddLayoutHorizontal(rowHeight);
            line.VerticalChildAlignment = VerticalAlignment.Center;
            line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Path Name")));
            line.Add(pathNameInput = new TextInput(this, columnWidth * 3, Owner.TextFontDefault.Height));

            line = layout.AddLayoutHorizontal(rowHeight);
            line.VerticalChildAlignment = VerticalAlignment.Center;
            line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Path ID")));
            line.Add(pathIdInput = new TextInput(this, columnWidth * 3, Owner.TextFontDefault.Height));

            line = layout.AddLayoutHorizontal(rowHeight);
            line.VerticalChildAlignment = VerticalAlignment.Center;
            line.Add(new Label(this, columnWidth, Owner.TextFontDefault.Height, Catalog.GetString("Path Start")));
            line.Add(pathStartInput = new TextInput(this, columnWidth * 3, Owner.TextFontDefault.Height));

            line = layout.AddLayoutHorizontal(rowHeight);
            line.VerticalChildAlignment = VerticalAlignment.Center;
            line.Add(new Label(this, columnWidth, Owner.TextFontDefault.Height, Catalog.GetString("Path End")));
            line.Add(pathEndInput = new TextInput(this, columnWidth * 3, Owner.TextFontDefault.Height));

            line = layout.AddLayoutHorizontal(rowHeight);
            line.VerticalChildAlignment = VerticalAlignment.Center;
            line.Add(new Label(this, columnWidth, Owner.TextFontDefault.Height, Catalog.GetString("Player Path")));
            line.Add(playerPathInput = new Checkbox(this, false, CheckMarkStyle.Check, true));

            layout.AddHorizontalSeparator(true);
            saveButton = new Label(this, layout.RemainingWidth / 2, Owner.TextFontDefault.Height, Catalog.GetString($"Save"), HorizontalAlignment.Center);
            saveButton.OnClick += SaveButton_OnClick;
            cancelButton = new Label(this, layout.RemainingWidth / 2, Owner.TextFontDefault.Height, Catalog.GetString($"Cancel"), HorizontalAlignment.Center);
            cancelButton.OnClick += CancelButton_OnClick;
            ControlLayout buttonLine = layout.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.25));
            buttonLine.Add(saveButton);
            buttonLine.AddVerticalSeparator();
            buttonLine.Add(cancelButton);
            layout.AddHorizontalSeparator(true);

            return layout;
        }

        private void CancelButton_OnClick(object sender, MouseClickEventArgs e)
        {
            Close();
        }

        private void SaveButton_OnClick(object sender, MouseClickEventArgs e)
        {
            PathModelHeader pathDetails = new PathModelHeader()
            {
                Id = pathIdInput.Text.Trim(),
                Name = pathNameInput.Text.Trim(),
                Start = pathStartInput.Text.Trim(),
                End = pathEndInput.Text.Trim(),
                PlayerPath = playerPathInput.State.GetValueOrDefault(),
            };
            OnSavePath?.Invoke(this, new TrainPathSaveEventArgs(pathDetails));
            base.Close();
        }

        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.Cancel, KeyEventType.KeyPressed, CancelSaving, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.Cancel, KeyEventType.KeyPressed, CancelSaving);
            return base.Close();
        }

        private void CancelSaving(UserCommandArgs args)
        {
            args.Handled = true;
            OnSaveCancel?.Invoke(this, EventArgs.Empty);
            Close();
        }

    }
}
