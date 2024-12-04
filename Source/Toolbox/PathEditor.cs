using System;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Imported.Track;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Files;

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
        private PathModelCore path;
        private long lastPathClickTick;
        private bool validPointAdded;

        public string PathId => path?.Id;

        internal event EventHandler<PathEditorChangedEventArgs> OnPathChanged;

        internal event EventHandler<PathEditorChangedEventArgs> OnPathUpdated;

        public PathEditor(ContentArea contentArea) : base(contentArea) { }

        public bool InitializePath(PathModelCore path)
        {
            try
            {
                if (path != null)
                {
                    this.path = path;
                    PathFile patFile = new PathFile(path.SourceFile());
                    InitializePath(patFile, path.SourceFile());
                }
                else
                {
                    InitializePath(null, null);
                }
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

        public void MouseAction(Point screenLocation, KeyModifiers keyModifiers)
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
        }
    }
}
