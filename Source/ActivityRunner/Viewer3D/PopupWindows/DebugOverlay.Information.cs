using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Orts.ActivityRunner.Processes;
using Orts.Common.DebugInfo;
using Orts.Common;
using Orts.Graphics.Window;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal partial class DebugOverlay: OverlayBase
    {

        private enum DetailInfoType
        {
            GraphicDetails,
        }

        private readonly EnumArray<INameValueInformationProvider, DetailInfoType> detailInfo = new EnumArray<INameValueInformationProvider, DetailInfoType>();

        private class GraphicInformation : DebugInfoBase
        {
            private readonly Viewer viewer;

            public GraphicInformation(Viewer viewer)
            {
                this.viewer = viewer;
            }

            public override void Update(GameTime gameTime)
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
                this["Render Primitives Seq"] = string.Join('\t', new string[] { "Sum" }.Concat(EnumExtension.GetNames<RenderPrimitiveSequence>()).Select(s => s.Max(12)));
                this["Render Primitives"] = string.Join('\t', new string[] { $"{viewer.RenderProcess.PrimitivePerFrame.Sum():0}" }.Concat(viewer.RenderProcess.PrimitivePerFrame.Select(p => $"{p:F0}")));
                this["Camera"] = $"{viewer.Camera.TileX:F0}\t{viewer.Camera.TileZ:F0}\t{viewer.Camera.Location.X:F2} \t{viewer.Camera.Location.Y:F2}\t{viewer.Camera.Location.Z:F2}\t{viewer.Tiles.GetElevation(viewer.Camera.CameraWorldLocation):F1} {FormatStrings.m}\t{viewer.Settings.LODBias} %\t{viewer.Settings.ViewingDistance} {FormatStrings.m}\t{(viewer.Settings.DistantMountains ? $"{viewer.Settings.DistantMountainsViewingDistance * 1e-3f:F0} {FormatStrings.km}" : "")}";
                base.Update(gameTime);
            }
        }

    }
}
