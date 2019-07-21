using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Orts.ActivityRunner.Viewer3D.Shapes
{
    public class SharedShapeManager
    {
        private readonly Viewer viewer;

        private readonly Dictionary<string, SharedShape> sharedShapes = new Dictionary<string, SharedShape>();
        private Dictionary<string, bool> shapeMarks;

        internal SharedShapeManager(Viewer viewer)
        {
            this.viewer = viewer;
            SharedShape.Initialize(viewer);
            BaseShape.Initialize(viewer);
        }

        public SharedShape Get(string path)
        {
            if (Thread.CurrentThread.Name != "Loader Process")
                Trace.TraceError("SharedShapeManager.Get incorrectly called by {0}; must be Loader Process or crashes will occur.", Thread.CurrentThread.Name);

            if (path == null || path == SharedShape.Empty.FilePath)
                return SharedShape.Empty;

            path = path.ToLowerInvariant();
            if (!sharedShapes.ContainsKey(path))
            {
                try
                {
                    sharedShapes.Add(path, new SharedShape(path));
                }
                catch (Exception error)
                {
                    Trace.WriteLine(new FileLoadException(path, error));
                    sharedShapes.Add(path, SharedShape.Empty);
                }
            }
            return sharedShapes[path];
        }

        public void Mark()
        {
            shapeMarks = new Dictionary<string, bool>(sharedShapes.Count);
            foreach (var path in sharedShapes.Keys)
                shapeMarks.Add(path, false);
        }

        public void Mark(SharedShape shape)
        {
            if (sharedShapes.ContainsValue(shape))
                shapeMarks[sharedShapes.First(kvp => kvp.Value == shape).Key] = true;
        }

        public void Sweep()
        {
            foreach (var path in shapeMarks.Where(kvp => !kvp.Value).Select(kvp => kvp.Key))
                sharedShapes.Remove(path);
        }

        public string GetStatus()
        {
            return Viewer.Catalog.GetPluralStringFmt("{0:F0} shape", "{0:F0} shapes", sharedShapes.Keys.Count);
        }
    }
}
