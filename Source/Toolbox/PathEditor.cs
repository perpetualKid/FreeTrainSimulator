using System;
using System.Xml.XPath;

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

    internal class PathEditor
    {
        private long lastPathClickTick;

        private readonly ToolboxContent toolboxContent;
        private Path path;

        internal TrainPathBase TrainPath { get; private set; }

        public string FilePath => path?.FilePath;

        internal event EventHandler<PathEditorChangedEventArgs> OnEditorPathChanged;

        public PathEditor(ToolboxContent content, GameWindow gameWindow)
        {
            ArgumentNullException.ThrowIfNull(content);

            toolboxContent = content;
        }

        public PathEditor(ContentArea contentArea)
        {
            toolboxContent = contentArea?.Content as ToolboxContent ?? throw new ArgumentNullException(nameof(contentArea));
        }

        public bool InitializePath(Path path)
        {
            try
            {
                if (path != null)
                {
                    this.path = path;
                    PathFile patFile = new PathFile(path.FilePath);
                    TrainPath = toolboxContent.InitializePath(patFile, path.FilePath);
                }
                else
                {
                    TrainPath = toolboxContent.InitializePath(null, null);
                }
                OnEditorPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
                return true;
            }
            catch (Exception ex) when (ex is Exception)
            {
                return false;
            }
        }

        internal void InitializeNewPath()
        {
            TrainPath = toolboxContent.InitializeNewPath();
            OnEditorPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        internal void HighlightPathItem(int index)
        {
            toolboxContent.HighlightPathItem(index);

            //TrainPath.SelectedNodeIndex = index;
            //TrainPathPointBase item = currentPath.SelectedNode;
            //if (item != null)
            //    ContentArea.SetTrackingPosition(item.Location);

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
