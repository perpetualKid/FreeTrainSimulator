using System;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Track;
using FreeTrainSimulator.Models.Shim;

namespace FreeTrainSimulator.Toolbox
{
    public class PathEditorChangedEventArgs : EventArgs
    {
        public TrainPathBase Path { get; }

        public PathEditorChangedEventArgs(TrainPathBase path)
        {
            Path = path;
        }
    }

    internal sealed class PathEditor : PathEditorBase
    {
        private readonly UserCommandController<UserCommand> userCommandController;
        private PathModelHeader path;
        private long lastPathClickTick;
        private bool validPointAdded;

        public string PathId => path?.Id;

        internal event EventHandler<PathEditorChangedEventArgs> OnPathChanged;

        internal event EventHandler<PathEditorChangedEventArgs> OnPathUpdated;

        public PathEditor(ContentArea contentArea) : base(contentArea) { }

        public PathEditor(ContentArea contentArea, UserCommandController<UserCommand> userCommandController) : base(contentArea) 
        { 
            this.userCommandController = userCommandController;
            userCommandController.AddEvent(CommonUserCommand.PointerPressed, MousePressedLeft);
            userCommandController.AddEvent(CommonUserCommand.PointerReleased, MouseReleasedLeft);
            userCommandController.AddEvent(CommonUserCommand.AlternatePointerPressed, MousePressedRight);
            userCommandController.AddEvent(CommonUserCommand.PointerDragged, MouseDragged);
        }

        protected override void Dispose(bool disposing)
        {
            userCommandController.RemoveEvent(CommonUserCommand.PointerPressed, MousePressedLeft);
            userCommandController.RemoveEvent(CommonUserCommand.PointerReleased, MouseReleasedLeft);
            userCommandController.RemoveEvent(CommonUserCommand.AlternatePointerPressed, MousePressedRight);
            userCommandController.RemoveEvent(CommonUserCommand.PointerDragged, MouseDragged);

            base.Dispose(disposing);
        }

        public bool InitializePath(PathModelHeader path)
        {
            try
            {
                this.path = path;
                InitializePathModel(path);
                OnPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
                return true;
            }
            catch (Exception ex) when (ex is Exception)
            {
                return false;
            }
        }

        public void InitializeNewPath()
        {
            this.path = new PathModelHeader()
            {
                Id = "<New Path>",
                Name = "<New Path>",
                Start = "Start",
                End = "End",
                PlayerPath = true,
            };
            InitializePath();
            OnPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        public async Task SavePath()
        {
            PathModel pathModel = ConvertTrainPath();
            pathModel = await TrackData.Instance.RouteData.Save(pathModel).ConfigureAwait(false);
            OnPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        public void MousePressedLeft(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (EditMode)
            {
                if (Environment.TickCount64 - lastPathClickTick < 500 && validPointAdded) //considered as double click
                {
                    _ = AddPathEndPoint();
                }
                else
                {
                    validPointAdded = AddPathPoint();
                }
                lastPathClickTick = Environment.TickCount64;
                OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
                userCommandArgs.Handled = true;
            }
        }

        public void MouseDragged(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {

        }

        public void MouseReleasedLeft(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
        }

        public void MousePressedRight(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            RemovePathPoint();
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
            userCommandArgs.Handled = true;
        }
    }
}
