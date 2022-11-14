using System;
using System.ComponentModel;

using GetText;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes;
using Orts.ActivityRunner.Processes.Diagnostics;
using Orts.Common;
using Orts.Common.Input;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Graphics.Xna;
using Orts.Settings;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class DebugOverlay : OverlayBase
    {
        private enum TabSettings
        {
            [Description("System Information")] Common,
            [Description("Weather Information")] Weather,
            [Description("Performance")] Performance,
        }

        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly Viewer viewer;
        private readonly UserSettings settings;
        private NameValueTextGrid systemInformation;
        private TabLayout<TabSettings> tabLayout;
        private readonly System.Drawing.Font textFont = FontManager.Exact("Arial", System.Drawing.FontStyle.Regular)[13];

        private GraphControl graphFrameTime;
        private GraphControl graphFrameRate;
        private GraphControl graphRenderProcess;
        private GraphControl graphUpdateProcess;
        private GraphControl graphSoundProcess;
        private GraphControl graphLoaderProcess;

        public DebugOverlay(WindowManager owner, UserSettings settings, Viewer viewer, Catalog catalog = null) : base(owner, catalog ?? CatalogManager.Catalog)
        {
            ArgumentNullException.ThrowIfNull(viewer);
            this.settings = settings;
            userCommandController = viewer.UserCommandController;
            this.viewer = viewer;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            tabLayout = new TabLayout<TabSettings>(this, 10, 10, layout.RemainingWidth - 20, layout.RemainingHeight - 20);
            tabLayout.TabLayouts[TabSettings.Common] = (layoutContainer) =>
            {
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Left;
                layoutContainer.Add(systemInformation = new NameValueTextGrid(this, 0, 0, textFont) 
                { 
                    OutlineRenderOptions = OutlineRenderOptions.Default, 
                    NameColumnWidth = 240,
                    InformationProvider = (Owner.Game as GameHost).SystemInfo[DiagnosticInfo.System],
                });
            };
            tabLayout.TabLayouts[TabSettings.Weather] = (layoutContainer) =>
            {
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Left;
                layoutContainer.Add(systemInformation = new NameValueTextGrid(this, 0, 0, textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    NameColumnWidth = 240,
                    InformationProvider = viewer.DetailInfo[DetailInfoType.WeatherDetails],
                });
            };
            tabLayout.TabLayouts[TabSettings.Performance] = (layoutContainer) =>
            {
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Left;
                layoutContainer.Add(new NameValueTextGrid(this, 0, 0, textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    NameColumnWidth = 240,
                    InformationProvider = (Owner.Game as GameHost).SystemInfo[DiagnosticInfo.ProcessMetric]
                });
                layoutContainer.Add(systemInformation = new NameValueTextGrid(this, layoutContainer.RemainingWidth / 2, 0, textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    NameColumnWidth = 240,
                    InformationProvider = (Owner.Game as GameHost).SystemInfo[DiagnosticInfo.Clr]
                });
                layoutContainer.Add(new NameValueTextGrid(this, 0, (int)(180 * Owner.DpiScaling), textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    NameColumnWidth = 240,
                    InformationProvider = (Owner.Game as GameHost).SystemInfo[DiagnosticInfo.GpuMetric],
                });
                layoutContainer.Add(new NameValueTextGrid(this, 0, (int)(180 * Owner.DpiScaling), textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    NameColumnWidth = 240,
                    MultiValueColumnWidth = 80,
                    InformationProvider = viewer.DetailInfo[DetailInfoType.GraphicDetails],
                    Column = 0
                });
                int graphWidth = Math.Min((int)(layoutContainer.RemainingWidth * 2.0 / 3.0), 768);
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Right;
                layoutContainer.Add(graphFrameRate = new GraphControl(this, 0, layoutContainer.RemainingHeight - (int)(260 * Owner.DpiScaling), graphWidth, 40, "0", "60", Catalog.GetString("Frame rate fps"), graphWidth) { GraphColor = Color.Purple });
                layoutContainer.Add(graphFrameTime = new GraphControl(this, 0, 15, graphWidth, 40, "0", "100", Catalog.GetString("Frame time ms"), graphWidth) { GraphColor = Color.LightGreen });
                layoutContainer.Add(graphRenderProcess = new GraphControl(this, 0, 15, graphWidth, 25, null, null, Catalog.GetString("Render Process %"), graphWidth) { GraphColor = Color.Red });
                layoutContainer.Add(graphUpdateProcess = new GraphControl(this, 0, 15, graphWidth, 25, null, null, Catalog.GetString("Update Process %"), graphWidth) { GraphColor = Color.Yellow });
                layoutContainer.Add(graphLoaderProcess = new GraphControl(this, 0, 15, graphWidth, 25, null, null, Catalog.GetString("Loader Process %"), graphWidth) { GraphColor = Color.Magenta });
                layoutContainer.Add(graphSoundProcess = new GraphControl(this, 0, 15, graphWidth, 25, null, null, Catalog.GetString("Sound Process %"), graphWidth) { GraphColor = Color.Cyan });
            };
            layout.Add(tabLayout);
            return layout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            if (shouldUpdate)
            {
                switch (tabLayout.CurrentTab)
                {
                    case TabSettings.Performance:
                        graphFrameRate.AddSample(MetricCollector.Instance.Metrics[SlidingMetric.FrameRate].Value / 60);
                        graphFrameTime.AddSample(MetricCollector.Instance.Metrics[SlidingMetric.FrameTime].Value * 10);
                        graphRenderProcess.AddSample(Profiler.ProfilingData[ProcessType.Render].Wall.Value / 100);
                        graphUpdateProcess.AddSample(Profiler.ProfilingData[ProcessType.Updater].Wall.Value / 100);
                        graphLoaderProcess.AddSample(Profiler.ProfilingData[ProcessType.Loader].Wall.Value / 100);
                        graphSoundProcess.AddSample(Profiler.ProfilingData[ProcessType.Sound].Wall.Value / 100);

                        break;
                }
                base.Update(gameTime, true);
            }
        }
        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.DisplayHUD, KeyEventType.KeyPressed, TabAction, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DisplayHUD, KeyEventType.KeyPressed, TabAction);
            return base.Close();
        }

        protected override void Initialize()
        {
            base.Initialize();
            if (EnumExtension.GetValue(settings.PopupSettings[ViewerWindowType.DebugOverlay], out TabSettings tab))
                tabLayout.TabAction(tab);
        }

        private void TabAction(UserCommandArgs args)
        {
            if (args is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
            {
                tabLayout.TabAction();
                settings.PopupSettings[ViewerWindowType.DebugOverlay] = tabLayout.CurrentTab.ToString();
            }
        }
    }
}
