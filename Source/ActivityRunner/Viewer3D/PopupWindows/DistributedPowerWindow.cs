using System;
using System.Collections.Generic;
using System.Linq;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.DebugInfo;
using Orts.Common.Input;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Settings;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Brakes;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class DistributedPowerWindow : WindowBase
    {
        private const int monoColumnWidth = 40;
        private const int normalColumnWidth = 64;

        private enum WindowMode
        {
            Normal,
            NormalMono,
            Short,
            ShortMono,
        }

        private enum GroupDetail
        {
            GroupId,
            LocomotivesNumber,
            Throttle,
            Load,
            BrakePipe,
            Remote,
            EqualizerReservoir,
            BrakeCylinder,
            MainReservoir,
        }

        private readonly UserSettings settings;
        private readonly UserCommandController<UserCommand> userCommandController;
        private WindowMode windowMode;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Label labelExpandMono;
        private Label labelExpandDetails;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly EnumArray<ControlLayout, GroupDetail> groupDetails = new EnumArray<ControlLayout, GroupDetail>();
        private int groupCount;

        public DistributedPowerWindow(WindowManager owner, Point relativeLocation, UserSettings settings, Viewer viewer, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Distributed Power"), relativeLocation, new Point(160, 200), catalog)
        {
            userCommandController = viewer.UserCommandController;
            this.settings = settings;
            _ = EnumExtension.GetValue(settings.PopupSettings[ViewerWindowType.DistributedPowerWindow], out windowMode);
            UpdatePowerInformation();
            Resize();
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling).AddLayoutOffset(0);
            ControlLayout line = layout.AddLayoutHorizontal();
            line.HorizontalChildAlignment = HorizontalAlignment.Right;
            line.VerticalChildAlignment = VerticalAlignment.Top;
            line.Add(labelExpandMono = new Label(this, Owner.TextFontDefault.Height, Owner.TextFontDefault.Height, windowMode is WindowMode.ShortMono or WindowMode.NormalMono ? FormatStrings.Markers.ArrowRight : FormatStrings.Markers.ArrowLeft, HorizontalAlignment.Center, Color.Yellow));
            labelExpandMono.OnClick += LabelExpandMono_OnClick;
            line.Add(labelExpandDetails = new Label(this, Owner.TextFontDefault.Height, Owner.TextFontDefault.Height, windowMode is WindowMode.Normal or WindowMode.NormalMono ? FormatStrings.Markers.ArrowUp : FormatStrings.Markers.ArrowDown, HorizontalAlignment.Center, Color.Yellow));
            labelExpandDetails.OnClick += LabelExpandDetails_OnClick;
            labelExpandDetails.Visible = labelExpandMono.Visible = groupCount > 0;
            layout = layout.AddLayoutVertical();
            if (groupCount == 0)
            {
                layout.VerticalChildAlignment = VerticalAlignment.Center;
                layout.Add(new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, Catalog.GetString("Distributed power management not available with this player train."), HorizontalAlignment.Center));
                Caption = Catalog.GetString("Distributed Power Info");
            }
            else
            {
                Caption = Catalog.GetString("DPU Info");

                void AddDetailLine(GroupDetail groupDetail, int width, string labelText, System.Drawing.Font font, HorizontalAlignment alignment = HorizontalAlignment.Right)
                {
                    line = layout.AddLayoutHorizontalLineOfText();
                    line.Add(new Label(this, width, font.Height, labelText, font));
                    for (int i = 0; i < groupCount; i++)
                    {
                        line.Add(new Label(this, 0, 0, width, font.Height, null, alignment, font, Color.White));
                        line.Add(new Label(this, 0, 0, (int)(Owner.DpiScaling * 12), font.Height, null, HorizontalAlignment.Center, Owner.TextFontDefault, Color.Green));
                    }
                    groupDetails[groupDetail] = line;
                }

                if (windowMode is WindowMode.ShortMono or WindowMode.NormalMono)
                {
                    int columnWidth = (int)(Owner.DpiScaling * monoColumnWidth);
                    AddDetailLine(GroupDetail.GroupId, columnWidth, FourCharAcronym.LocoGroup.GetLocalizedDescription(), Owner.TextFontMonoDefaultBold, HorizontalAlignment.Center);
                    layout.AddHorizontalSeparator(true);
                    AddDetailLine(GroupDetail.LocomotivesNumber, columnWidth, FourCharAcronym.Locomotives.GetLocalizedDescription(), Owner.TextFontMonoDefault);
                    AddDetailLine(GroupDetail.Throttle, columnWidth, FourCharAcronym.Throttle.GetLocalizedDescription(), Owner.TextFontMonoDefault);
                    AddDetailLine(GroupDetail.Load, columnWidth, FourCharAcronym.TractiveEffort.GetLocalizedDescription(), Owner.TextFontMonoDefault);
                    AddDetailLine(GroupDetail.BrakePipe, columnWidth, FourCharAcronym.BrakePressure.GetLocalizedDescription(), Owner.TextFontMonoDefault);
                    AddDetailLine(GroupDetail.Remote, columnWidth, FourCharAcronym.Remote.GetLocalizedDescription(), Owner.TextFontMonoDefault);

                    if (windowMode == WindowMode.NormalMono)
                    {
                        AddDetailLine(GroupDetail.EqualizerReservoir, columnWidth, FourCharAcronym.EQReservoir.GetLocalizedDescription(), Owner.TextFontMonoDefault);
                        AddDetailLine(GroupDetail.BrakeCylinder, columnWidth, FourCharAcronym.BrakeCylinder.GetLocalizedDescription(), Owner.TextFontMonoDefault);
                        AddDetailLine(GroupDetail.MainReservoir, columnWidth, FourCharAcronym.MainReservoir.GetLocalizedDescription(), Owner.TextFontMonoDefault);
                    }
                }
                else
                {
                    int columnWidth = (int)(Owner.DpiScaling * normalColumnWidth);
                    AddDetailLine(GroupDetail.GroupId, columnWidth, Catalog.GetString("Group"), Owner.TextFontDefault, HorizontalAlignment.Center);
                    layout.AddHorizontalSeparator(true);
                    AddDetailLine(GroupDetail.LocomotivesNumber, columnWidth, Catalog.GetString("Locos"), Owner.TextFontDefault);
                    AddDetailLine(GroupDetail.Throttle, columnWidth, Catalog.GetString("Throttle"), Owner.TextFontDefault);
                    AddDetailLine(GroupDetail.Load, columnWidth, Catalog.GetString("Trac Eff"), Owner.TextFontDefault);
                    AddDetailLine(GroupDetail.BrakePipe, columnWidth, Catalog.GetString("Brk Pipe"), Owner.TextFontDefault);
                    AddDetailLine(GroupDetail.Remote, columnWidth, Catalog.GetString("Remote"), Owner.TextFontDefault);

                    if (windowMode == WindowMode.Normal)
                    {
                        AddDetailLine(GroupDetail.EqualizerReservoir, columnWidth, Catalog.GetString("EQ Res"), Owner.TextFontDefault);
                        AddDetailLine(GroupDetail.BrakeCylinder, columnWidth, Catalog.GetString("Brk Cyl"), Owner.TextFontDefault);
                        AddDetailLine(GroupDetail.MainReservoir, columnWidth, Catalog.GetString("Main Res"), Owner.TextFontDefault);
                    }
                }
            }
            return layout;
        }

        private void LabelExpandDetails_OnClick(object sender, MouseClickEventArgs e)
        {
            windowMode = windowMode.Next().Next();
            Resize();
        }

        private void LabelExpandMono_OnClick(object sender, MouseClickEventArgs e)
        {
            windowMode = windowMode is WindowMode.Normal or WindowMode.Short ? windowMode.Next() : windowMode.Previous();
            Resize();
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
            if (shouldUpdate)
            {
                UpdatePowerInformation();
            }
        }

        public override bool Open()
        {
            bool result = base.Open();
            if (result)
            {
                userCommandController.AddEvent(UserCommand.DisplayDistributedPowerWindow, KeyEventType.KeyPressed, TabAction, true);
            }
            return result;
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.DisplayDistributedPowerWindow, KeyEventType.KeyPressed, TabAction);
            return base.Close();
        }

        private void TabAction(UserCommandArgs args)
        {
            if (groupCount > 0 && args is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & settings.Input.WindowTabCommandModifier) == settings.Input.WindowTabCommandModifier)
            {
                windowMode = windowMode.Next();
                Resize();
            }
        }

        private void Resize()
        {
            if (groupCount == 0)
            {
                Resize(new Point(420, 60));
            }
            else
            {
                Point size = windowMode switch
                {
                    WindowMode.Normal => new Point((groupCount + 1) * (normalColumnWidth + 10), 170),
                    WindowMode.NormalMono => new Point((groupCount + 1) * (monoColumnWidth + 10), 170),
                    WindowMode.Short => new Point((groupCount + 1) * (normalColumnWidth + 10), 130),
                    WindowMode.ShortMono => new Point((groupCount + 1) * (monoColumnWidth + 10), 130),
                    _ => throw new InvalidOperationException(),
                };

                Resize(size);
            }

            settings.PopupSettings[ViewerWindowType.DistributedPowerWindow] = windowMode.ToString();
        }

        private void UpdatePowerInformation()
        {
            IEnumerable<IGrouping<int, MSTSDieselLocomotive>> distributedLocomotives = Simulator.Instance.PlayerLocomotive.Train.Cars.OfType<MSTSDieselLocomotive>().GroupBy((dieselLocomotive) => dieselLocomotive.DistributedPowerUnitId);
            int groups = distributedLocomotives.Count();

            if (groups != groupCount)
            {
                groupCount = groups;
                Resize();
            }

            int i = 1;
            RemoteControlGroup remoteControlGroup = RemoteControlGroup.FrontGroupSync;

            foreach (IGrouping<int, MSTSDieselLocomotive> item in distributedLocomotives)
            {
                MSTSDieselLocomotive groupLead = item.FirstOrDefault();
                bool fence = remoteControlGroup != (remoteControlGroup = groupLead.RemoteControlGroup);

                if (groupDetails[GroupDetail.GroupId]?.Controls[i] is Label groupLabel)
                    groupLabel.Text = $"{groupLead?.DistributedPowerUnitId}";
                if (i > 1) //fence is before the current group
                {
                    foreach (GroupDetail groupDetail in EnumExtension.GetValues<GroupDetail>())
                    {
                        if (groupDetails[groupDetail]?.Controls[i - 1] is Label label)
                        {
                            label.Text = fence ? FormatStrings.Markers.Fence : null;
                            if (groupDetail == GroupDetail.GroupId)
                            {
                                if (!fence)
                                    label.Text = FormatStrings.Markers.Dash;
                                label.TextColor = fence ? Color.Green : Color.White;
                            }
                        }
                    }
                }
                if (groupDetails[GroupDetail.LocomotivesNumber]?.Controls[i] is Label locoLabel)
                    locoLabel.Text = $"{item.Count()}";
                if (groupDetails[GroupDetail.Throttle]?.Controls[i] is Label throttleLabel)
                {
                    throttleLabel.Text = $"{groupLead.DistributedPowerThrottleInfo()}";
                    throttleLabel.TextColor = groupLead.DynamicBrakePercent >= 0 ? Color.Yellow : Color.White;
                }
                if (groupDetails[GroupDetail.BrakePipe]?.Controls[i] is Label brakeLabel)
                {
                    string brakePipe = groupLead.BrakeSystem.BrakeInfo.DetailInfo["BP"];
                    if (windowMode != WindowMode.Normal)
                        brakePipe = brakePipe?.Split(' ')[0];
                    brakeLabel.Text = brakePipe;
                }
                if (groupDetails[GroupDetail.Load]?.Controls[i] is Label loadLabel)
                {
                    loadLabel.Text = $"{groupLead.DistributedPowerForceInfo():F0}{(windowMode is WindowMode.Normal or WindowMode.Short ? $" {(Simulator.Instance.Route.MilepostUnitsMetric ? " A" : " K")}" : "")}";
                }
                if (groupDetails[GroupDetail.Remote]?.Controls[i] is Label remoteLabel)
                {
                    remoteLabel.Text = $"{(groupLead.IsLeadLocomotive()  ? RemoteControlGroup.Unconnected.GetLocalizedDescription() : groupLead.RemoteControlGroup.GetLocalizedDescription())}";
                }
                if (windowMode is WindowMode.Normal or WindowMode.NormalMono)
                {
                    TrainCar lastCar = groupLead.Train.Cars[^1];
                    if (lastCar == groupLead)
                        lastCar = groupLead.Train.Cars[0];

                    // EQ
                    if (groupDetails[GroupDetail.EqualizerReservoir]?.Controls[i] is Label eqLabel)
                    {
                        string eqReservoir = groupLead.BrakeSystem.BrakeInfo.DetailInfo["EQ"];
                        if (windowMode != WindowMode.Normal)
                            eqReservoir = eqReservoir?.Split(' ')[0];
                        eqLabel.Text = eqReservoir ?? "———";
                    }

                    // BC
                    if (groupDetails[GroupDetail.BrakeCylinder]?.Controls[i] is Label bcLabel)
                    {
                        string bcPressure = groupLead.BrakeSystem.BrakeInfo.DetailInfo["BC"];
                        if (windowMode != WindowMode.Normal)
                            bcPressure = bcPressure?.Split(' ')[0];
                        bcLabel.Text = bcPressure;
                    }

                    // MR
                    if (groupDetails[GroupDetail.MainReservoir]?.Controls[i] is Label mrLabel)
                        mrLabel.Text = $"{FormatStrings.FormatPressure(groupLead.MainResPressurePSI, Pressure.Unit.PSI, groupLead.BrakeSystemPressureUnits[BrakeSystemComponent.MainReservoir], windowMode == WindowMode.Normal)}";
                }
                i += 2;
            }

        }
    }
}
