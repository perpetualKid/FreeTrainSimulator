using System;

using Microsoft.Xna.Framework;

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

        internal event EventHandler<PathEditorChangedEventArgs> OnPathChanged;

        internal event EventHandler<PathEditorChangedEventArgs> OnPathUpdated;

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
            base.InitializePath();
            OnPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        public void AddPathPoint(Point screenLocation)
        {
            base.AddPathPoint();
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }
    }
}
