using System;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Track;

using Microsoft.Xna.Framework;

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
        private PathModelHeader path;
        private long lastPathClickTick;
        private bool validPointAdded;

        public string PathId => path?.Id;

        internal event EventHandler<PathEditorChangedEventArgs> OnPathChanged;

        internal event EventHandler<PathEditorChangedEventArgs> OnPathUpdated;

        public PathEditor(ContentArea contentArea) : base(contentArea) { }

        public PathEditor(ContentArea contentArea, UserCommandController<UserCommand> userCommandController) : base(contentArea) 
        { 
            userCommandController.AddEvent(CommonUserCommand.PointerPressed, MousePressedLeft);
            userCommandController.AddEvent(CommonUserCommand.PointerReleased, MouseReleasedLeft);
            userCommandController.AddEvent(CommonUserCommand.PointerDragged, MouseDragged);
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
            InitializePath();
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

    }
}
