using System.Globalization;
using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common.Calc;
using Orts.Common.DebugInfo;
using Orts.Common.Info;

namespace Orts.ActivityRunner.Processes.State
{
    internal class CommonInfo: DebugInfoBase
    {
        private readonly SmoothedData frameRate = new SmoothedData();
        private readonly GameHost gameHost;
        private readonly Catalog catalog = CatalogManager.Catalog;

        public CommonInfo(GameHost game) : base(true)
        {
            gameHost = game;
            this["Version"] = VersionInfo.FullVersion;
            this["Time"] = null;
            this["Adapter"] = catalog.GetString($"{gameHost.GraphicsDevice.Adapter.Description} ({SystemInfo.GraphicAdapterMemoryInformation}) ({gameHost.GraphicsDevice.Viewport.Bounds.Size.X:F0} pixels x {gameHost.GraphicsDevice.Viewport.Bounds.Size.Y:F0} pixels)");
        }

        public override void Update(GameTime gameTime)
        {
            this["Time"] = DateTime.Now.ToString(CultureInfo.CurrentCulture);
            frameRate.Update(gameTime.ElapsedGameTime.TotalSeconds, 1.0 / gameTime.ElapsedGameTime.TotalSeconds);
            this["FPS"] = $"{frameRate.SmoothedValue:0}";
            FormattingOptions["FPS"] = gameTime.IsRunningSlowly ? FormatOption.RegularRed : null;
        }

    }
}
