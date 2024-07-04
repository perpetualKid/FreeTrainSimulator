using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class GraphicInformation : DetailInfoBase
    {
        private readonly Viewer viewer;
        internal static readonly string[] sumSeparator = new string[] { "Sum" };

        public GraphicInformation(Viewer viewer)
        {
            this.viewer = viewer;
            this["Graphics Information"] = null;
            this[".0"] = null;
        }

        public override void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                this["TextureManager"] = viewer.TextureManager.GetStatus();
                this["MaterialManager"] = viewer.MaterialManager.GetStatus();
                this["ShapeManager"] = viewer.ShapeManager.GetStatus();
                this["Terrain"] = viewer.World.Terrain.GetStatus();
                if (viewer.Settings.DynamicShadows)
                {
                    this["Shadow Maps"] = string.Join('\t', new string[] { $"({viewer.Settings.ShadowMapResolution}x{viewer.Settings.ShadowMapResolution})" }.Concat(Enumerable.Range(0, RenderProcess.ShadowMapCount).Select(i => $"{RenderProcess.ShadowMapDistance[i]}/{RenderProcess.ShadowMapDiameter[i]}")));
                    this["Shadow Primities"] = string.Join('\t', new string[] { $"{viewer.RenderProcess.ShadowPrimitivePerFrame.Sum()}" }.Concat(viewer.RenderProcess.ShadowPrimitivePerFrame.Select(p => $"{p}")));
                }
                this["Render Primitives Seq"] = string.Join('\t', sumSeparator.Concat(EnumExtension.GetNames<RenderPrimitiveSequence>()).Select(s => s.Max(12)));
                this["Render Primitives"] = string.Join('\t', new string[] { $"{viewer.RenderProcess.PrimitivePerFrame.Sum():0}" }.Concat(viewer.RenderProcess.PrimitivePerFrame.Select(p => $"{p:F0}")));
                this["Camera"] = $"{viewer.Camera.Tile.X:F0}\t{viewer.Camera.Tile.Z:F0}\t{viewer.Camera.Location.X:F2} \t{viewer.Camera.Location.Y:F2}\t{viewer.Camera.Location.Z:F2}\t{viewer.Tiles.GetElevation(viewer.Camera.CameraWorldLocation):F1} {FormatStrings.m}\t{viewer.Settings.LODBias} %\t{viewer.Settings.ViewingDistance} {FormatStrings.m}\t{(viewer.Settings.DistantMountains ? $"{viewer.Settings.DistantMountainsViewingDistance * 1e-3f:F0} {FormatStrings.km}" : "")}";
                base.Update(gameTime);
            }
        }
    }
}
