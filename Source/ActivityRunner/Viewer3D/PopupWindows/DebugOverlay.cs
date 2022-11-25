using System;
using System.ComponentModel;

using GetText;

using Microsoft.CodeAnalysis.Operations;
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
using Orts.Simulation;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.PowerTransmissions;

using SharpDX.Direct2D1.Effects;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class DebugOverlay : OverlayBase
    {
        private enum TabSettings
        {
            [Description("System Information")] Common,
            [Description("Game Performance Details")] Performance,
            [Description("Consist Information")] Consist,
            [Description("Locomotive Information")] Locomotive,
            [Description("Force Information")] Force,
            [Description("Distributed Power Information")] DistributedPower,
            [Description("Game Information")] GameDetails,
            [Description("Dispatcher Information")] Dispatcher,
        }

        private readonly UserCommandController<UserCommand> userCommandController;
        private readonly Viewer viewer;
        private readonly UserSettings settings;

        private TabLayout<TabSettings> tabLayout;
        private readonly System.Drawing.Font textFont = FontManager.Exact("Arial", System.Drawing.FontStyle.Regular)[13];

        private GraphControl graphFrameTime;
        private GraphControl graphFrameRate;
        private GraphControl graphRenderProcess;
        private GraphControl graphUpdateProcess;
        private GraphControl graphSoundProcess;
        private GraphControl graphLoaderProcess;
        private GraphControl graphThrottle;
        private GraphControl graphPowerInput;
        private GraphControl graphPowerOutput;
        private GraphControl graphMotiveForce;
        private GraphControl graphDynamicForce;
        private GraphControl graphForceSubsteps;

        private NameValueTextGrid consistTableGrid;
        private NameValueTextGrid distributedPowerTableGrid;
        private NameValueTextGrid dispatcherGrid;
        private NameValueTextGrid locomotiveGrid;
        private NameValueTextGrid forceTableGrid;
        private NameValueTextGrid scrollableGrid;

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
                layoutContainer.Add(new NameValueTextGrid(this, 0, 0, textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    ColumnWidth = new int[] { 240, -1 },
                    InformationProvider = (Owner.Game as GameHost).SystemInfo[DiagnosticInfo.System],
                });
            };
            tabLayout.TabLayouts[TabSettings.Performance] = (layoutContainer) =>
            {
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Left;
                layoutContainer.Add(new NameValueTextGrid(this, 0, 0, textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    ColumnWidth = new int[] { 240, -1 },
                    InformationProvider = (Owner.Game as GameHost).SystemInfo[DiagnosticInfo.ProcessMetric]
                });
                layoutContainer.Add(new NameValueTextGrid(this, layoutContainer.RemainingWidth / 2, 0, textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    ColumnWidth = new int[] { 240, -1 },
                    InformationProvider = (Owner.Game as GameHost).SystemInfo[DiagnosticInfo.Clr]
                });
                layoutContainer.Add(new NameValueTextGrid(this, 0, (int)(180 * Owner.DpiScaling), textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    ColumnWidth = new int[] { 240, -1 },
                    InformationProvider = (Owner.Game as GameHost).SystemInfo[DiagnosticInfo.GpuMetric],
                });
                layoutContainer.Add(new NameValueTextGrid(this, 0, (int)(180 * Owner.DpiScaling), textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    ColumnWidth = new int[] { 240, 80 },
                    InformationProvider = viewer.DetailInfo[DetailInfoType.GraphicDetails],
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
            tabLayout.TabLayouts[TabSettings.Consist] = (layoutContainer) =>
            {
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Left;
                layoutContainer.Add(new NameValueTextGrid(this, 0, 0, textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    ColumnWidth = new int[] { 240, -1 },
                    InformationProvider = viewer.DetailInfo[DetailInfoType.TrainDetails],
                });
                int y = (int)(160 * Owner.DpiScaling);
                layoutContainer.Add(consistTableGrid = new NameValueTextGrid(this, 0, y, layoutContainer.RemainingWidth, layoutContainer.RemainingHeight - y, textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    InformationProvider = viewer.DetailInfo[DetailInfoType.ConsistDetails],
                    ColumnWidth = new int[] { 40, 64, 64, 80, 64 },
                });

            };
            tabLayout.TabLayouts[TabSettings.Locomotive] = (layoutContainer) =>
            {
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Left;
                layoutContainer.Add(locomotiveGrid = new NameValueTextGrid(this, 0, 0, textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    ColumnWidth = new int[] { 180, 140 },
                    InformationProvider = viewer.DetailInfo[DetailInfoType.LocomotiveDetails],
                });
                int graphWidth = Math.Min((int)(layoutContainer.RemainingWidth * 2.0 / 3.0), 768);
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Right;
                layoutContainer.Add(graphThrottle = new GraphControl(this, 0, layoutContainer.RemainingHeight - (int)(160 * Owner.DpiScaling), graphWidth, 40, "0", "100 %", Catalog.GetString("Throttle"), graphWidth / 2) { GraphColor = Color.Blue });
                layoutContainer.Add(graphPowerInput = new GraphControl(this, 0, 15, graphWidth, 40, "0", "100 %", Catalog.GetString("Power Input"), graphWidth / 2) { GraphColor = Color.Yellow });
                layoutContainer.Add(graphPowerOutput = new GraphControl(this, 0, 15, graphWidth, 40, "0", "100 %", Catalog.GetString("Power Output"), graphWidth / 2) { GraphColor = Color.Green });

            };
            tabLayout.TabLayouts[TabSettings.Force] = (layoutContainer) =>
            {
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Left;
                layoutContainer.Add(forceTableGrid = new NameValueTextGrid(this, 0, 0, textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    ColumnWidth = new int[] { 40, 64, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 100, },
                    InformationProvider = viewer.DetailInfo[DetailInfoType.ForceDetails],
                });
                int graphWidth = Math.Min((int)(layoutContainer.RemainingWidth * 2.0 / 3.0), 768);
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Right;
                layoutContainer.Add(graphMotiveForce = new GraphControl(this, 0, layoutContainer.RemainingHeight - (int)(160 * Owner.DpiScaling), graphWidth, 40, "0", "100 %", Catalog.GetString("Motive Force"), graphWidth / 2) { GraphColor = Color.LimeGreen });
                layoutContainer.Add(graphDynamicForce = new GraphControl(this, 0, 15, graphWidth, 40, "0", "100 %", Catalog.GetString("Dynamic Force"), graphWidth / 2) { GraphColor = Color.MediumVioletRed });
                layoutContainer.Add(graphForceSubsteps = new GraphControl(this, 0, 15, graphWidth, 40, "0", "300 %", Catalog.GetString("Number of Substeps"), graphWidth / 2) { GraphColor = Color.DeepSkyBlue });
            };
            tabLayout.TabLayouts[TabSettings.DistributedPower] = (layoutContainer) =>
            {
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Left;
                layoutContainer.Add(distributedPowerTableGrid = new NameValueTextGrid(this, 0, 0, textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    ColumnWidth = new int[] { 140, 120 },
                    InformationProvider = viewer.DetailInfo[DetailInfoType.DistributedPowerDetails],
                });
            };
            tabLayout.TabLayouts[TabSettings.GameDetails] = (layoutContainer) =>
            {
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Left;
                layoutContainer.Add(new NameValueTextGrid(this, 0, 0, textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    ColumnWidth = new int[] { 240, -1 },
                    InformationProvider = viewer.DetailInfo[DetailInfoType.GameDetails],
                });
                int y = (int)(160 * Owner.DpiScaling);
                layoutContainer.Add(new NameValueTextGrid(this, 0, y, textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    ColumnWidth = new int[] { 240, -1 },
                    InformationProvider = viewer.DetailInfo[DetailInfoType.WeatherDetails],
                });
            };
            tabLayout.TabLayouts[TabSettings.Dispatcher] = (layoutContainer) =>
            {
                layoutContainer.HorizontalChildAlignment = HorizontalAlignment.Left;
                layoutContainer.Add(dispatcherGrid = new NameValueTextGrid(this, 0, 0, textFont)
                {
                    OutlineRenderOptions = OutlineRenderOptions.Default,
                    ColumnWidth = new int[] { 40, 240, 64, 64, 64, 64, 80, 80, 80, 120, 64, 120, 64, -1 },
                    InformationProvider = viewer.DetailInfo[DetailInfoType.DispatcherDetails],
                });
            };
            layout.Add(tabLayout);
            return layout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            if (shouldUpdate)
            {
                MSTSLocomotive locomotive = Simulator.Instance.PlayerLocomotive;
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
                    case TabSettings.Locomotive:
                        graphThrottle.AddSample(locomotive.ThrottlePercent * 0.01f);
                        switch (locomotive)
                        {
                            case MSTSDieselLocomotive dieselLocomotive:
                                graphPowerInput.AddSample(dieselLocomotive.DieselEngines.MaxOutputPowerW / dieselLocomotive.DieselEngines.MaxPowerW);
                                graphPowerOutput.AddSample(dieselLocomotive.DieselEngines.PowerW / dieselLocomotive.DieselEngines.MaxPowerW);
                                break;
                            case MSTSSteamLocomotive steamLocomotive:
                                graphPowerInput.AddSample(steamLocomotive.ThrottlePercent * 0.01f);
                                graphPowerOutput.AddSample(steamLocomotive.MotiveForceN / steamLocomotive.MaxPowerW * steamLocomotive.SpeedMpS);
                                break;
                            case MSTSElectricLocomotive electricLocomotive:
                                graphPowerInput.AddSample(electricLocomotive.ThrottlePercent * 0.01f);
                                graphPowerOutput.AddSample(electricLocomotive.MotiveForceN / electricLocomotive.MaxPowerW * electricLocomotive.SpeedMpS);
                                break;
                        }
                        break;
                    case TabSettings.Force:
                        if ((locomotive as MSTSDieselLocomotive)?.DieselEngines.GearBox is GearBox gearBox && (locomotive as MSTSDieselLocomotive)?.DieselTransmissionType == DieselTransmissionType.Mechanic)
                        {
                            // For geared locomotives the Max Force base value changes for each gear.
                            graphMotiveForce.AddSample(locomotive.MotiveForceN / gearBox.CurrentGear.MaxTractiveForceN);
                        }
                        else
                        {
                            graphMotiveForce.AddSample(locomotive.MotiveForceN / locomotive.MaxForceN);
                        }
                        graphForceSubsteps.AddSample(locomotive.LocomotiveAxle.AxleRevolutionsInt.NumOfSubstepsPS / (float)locomotive.LocomotiveAxle.AxleRevolutionsInt.MaxSubsteps);
                        break;
                }
                base.Update(gameTime, true);
            }
        }
        public override bool Open()
        {
            userCommandController.AddEvent(UserCommand.DisplayHUD, KeyEventType.KeyPressed, TabAction, true);
            userCommandController.AddEvent(UserCommand.DisplayHUDScrollUp, KeyEventType.KeyPressed, ScrollUp, true);
            userCommandController.AddEvent(UserCommand.DisplayHUDScrollDown, KeyEventType.KeyPressed, ScrollDown, true);
            userCommandController.AddEvent(UserCommand.DisplayHUDScrollLeft, KeyEventType.KeyPressed, ScrollLeft, true);
            userCommandController.AddEvent(UserCommand.DisplayHUDScrollRight, KeyEventType.KeyPressed, ScrollRight, true);
            return base.Open();
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DisplayHUD, KeyEventType.KeyPressed, TabAction);
            userCommandController.RemoveEvent(UserCommand.DisplayHUDScrollUp, KeyEventType.KeyPressed, ScrollUp);
            userCommandController.RemoveEvent(UserCommand.DisplayHUDScrollDown, KeyEventType.KeyPressed, ScrollDown);
            userCommandController.RemoveEvent(UserCommand.DisplayHUDScrollLeft, KeyEventType.KeyPressed, ScrollLeft);
            userCommandController.RemoveEvent(UserCommand.DisplayHUDScrollRight, KeyEventType.KeyPressed, ScrollRight);
            return base.Close();
        }

        protected override void Initialize()
        {
            base.Initialize();
            if (EnumExtension.GetValue(settings.PopupSettings[ViewerWindowType.DebugOverlay], out TabSettings tab))
                tabLayout.TabAction(tab);
            SetScrollableGridTarget();
        }

        private void TabAction(UserCommandArgs args)
        {
            if (args is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
            {
                tabLayout.TabAction();
                settings.PopupSettings[ViewerWindowType.DebugOverlay] = tabLayout.CurrentTab.ToString();
                SetScrollableGridTarget();
            }
        }

        private void SetScrollableGridTarget()
        {
            scrollableGrid = tabLayout.CurrentTab switch
            {
                TabSettings.Consist => consistTableGrid,
                TabSettings.Locomotive => locomotiveGrid,
                TabSettings.Force => forceTableGrid,
                TabSettings.DistributedPower => distributedPowerTableGrid,
                TabSettings.Dispatcher => dispatcherGrid,
                _ => null,
            };
        }

        private void ScrollUp()
        {
            if (null != scrollableGrid)
                scrollableGrid.Row--;
        }

        private void ScrollDown()
        {
            if (null != scrollableGrid)
                scrollableGrid.Row++;
        }

        private void ScrollLeft()
        {
            if (null != scrollableGrid)
                scrollableGrid.Column++;
        }

        private void ScrollRight()
        {
            if (null != scrollableGrid)
                scrollableGrid.Column--;
        }

    }
}
