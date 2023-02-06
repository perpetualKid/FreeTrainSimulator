using System;

using Orts.Formats.Msts.Files;
using Orts.Graphics.MapView;
using Orts.Models.Simplified;
using Orts.Models.Track;

namespace Orts.Toolbox
{
    public class PathEditorChangedEventArgs : EventArgs
    {
        public TrainPathBase Path { get; }

        public PathEditorChangedEventArgs(TrainPathBase path)
        {
            Path = path;
        }
    }

    internal class PathEditor: PathEditorBase
    {
        private Path path;

        public string FilePath => path?.FilePath;

        internal event EventHandler<PathEditorChangedEventArgs> OnEditorPathChanged;

        public PathEditor(ContentArea contentArea): base(contentArea) { }

        public bool InitializePath(Path path)
        {
            try
            {
                if (path != null)
                {
                    this.path = path;
                    PathFile patFile = new PathFile(path.FilePath);
                    InitializePath(patFile, path.FilePath);
                }
                else
                {
                    InitializePath(null, null);
                }
                OnEditorPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
                return true;
            }
            catch (Exception ex) when (ex is Exception)
            {
                return false;
            }
        }

        public void InitializeNewPath()
        {
            base.InitializePath();
            OnEditorPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        //internal void AddTrainPathPoint(Point location)
        //{
        //    if (currentPath != null)
        //    {
        //        if (Environment.TickCount64 - lastPathClickTick < 200)
        //        {
        //            if (pathItem.ValidationResult == PathNodeInvalidReasons.None)
        //                currentPath.AddPathEndPoint(pathItem);
        //            pathItem = null;
        //        }
        //        else
        //        {
        //            lastPathClickTick = Environment.TickCount64;
        //            if (pathItem.ValidationResult == PathNodeInvalidReasons.None)
        //                pathItem = currentPath.AddPathPoint(pathItem);
        //        }
        //    }
        //}

    }
}
