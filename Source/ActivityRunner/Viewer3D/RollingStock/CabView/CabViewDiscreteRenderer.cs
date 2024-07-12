// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

using System;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation.Commanding;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Controllers;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.CabView
{
    /// <summary>
    /// Discrete renderer for Lever, Twostate, Tristate, Multistate, Signal
    /// </summary>
    public class CabViewDiscreteRenderer : CabViewControlRenderer, ICabViewMouseControlRenderer
    {
        private protected readonly CabViewFramedControl ControlDiscrete;
        private readonly Rectangle SourceRectangle;
        private Rectangle DestinationRectangle;
        private readonly float CVCFlashTimeOn = 0.75f;
        private readonly float CVCFlashTimeTotal = 1.5f;
        private float CumulativeTime;
        private bool ButtonState;
        private int previousFrameIndex;

        /// <summary>
        /// Accumulated mouse movement. Used for controls with no assigned notch controllers, e.g. headlight and reverser.
        /// </summary>
        private float IntermediateValue;

        public CabViewDiscreteRenderer(Viewer viewer, MSTSLocomotive locomotive, CabViewFramedControl control, CabShader shader)
            : base(viewer, locomotive, control, shader)
        {
            ControlDiscrete = control;
            CABTextureManager.DisassembleTexture(viewer.Game.GraphicsDevice, base.control.AceFile, base.control.Bounds.Width, base.control.Bounds.Height, ControlDiscrete.FramesCount, ControlDiscrete.FramesX, ControlDiscrete.FramesY);
            texture = CABTextureManager.GetTextureByIndexes(base.control.AceFile, 0, false, false, out nightTexture, cabLightDirectory);
            SourceRectangle = new Rectangle(0, 0, texture.Width, texture.Height);
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            var index = GetDrawIndex();

            var mS = control as CabViewMultiStateDisplayControl;
            if (mS != null)
            {
                CumulativeTime += (float)elapsedTime.ClockSeconds;
                while (CumulativeTime > CVCFlashTimeTotal)
                    CumulativeTime -= CVCFlashTimeTotal;
                if (mS.Styles.Count > index && mS.Styles[index] == 1 && CumulativeTime > CVCFlashTimeOn)
                    return;
            }

            PrepareFrameForIndex(frame, elapsedTime, index);
        }

        protected void PrepareFrameForIndex(RenderFrame frame, ElapsedTime elapsedTime, int index)
        {
            var dark = viewer.MaterialManager.sunDirection.Y <= -0.085f || viewer.Camera.IsUnderground;

            texture = CABTextureManager.GetTextureByIndexes(control.AceFile, index, dark, locomotive.CabLightOn, out nightTexture, cabLightDirectory);
            if (texture == SharedMaterialManager.MissingTexture)
                return;

            base.PrepareFrame(frame, elapsedTime);

            // Cab view height and vertical position adjusted to allow for clip or stretch.
            var xratio = (float)viewer.CabWidthPixels / 640;
            var yratio = (float)viewer.CabHeightPixels / 480;
            // Cab view position adjusted to allow for letterboxing.
            DestinationRectangle.X = (int)(xratio * control.Bounds.X * 1.0001) - viewer.CabXOffsetPixels + viewer.CabXLetterboxPixels;
            DestinationRectangle.Y = (int)(yratio * control.Bounds.Y * 1.0001) + viewer.CabYOffsetPixels + viewer.CabYLetterboxPixels;
            DestinationRectangle.Width = (int)(xratio * Math.Min(control.Bounds.Width, texture.Width));  // Allow only downscaling of the texture, and not upscaling
            DestinationRectangle.Height = (int)(yratio * Math.Min(control.Bounds.Height, texture.Height));  // Allow only downscaling of the texture, and not upscaling
        }

        public override void Draw()
        {
            shader?.SetTextureData(DestinationRectangle.Left, DestinationRectangle.Top, DestinationRectangle.Width, DestinationRectangle.Height);
            controlView.SpriteBatch.Draw(texture, DestinationRectangle, SourceRectangle, Color.White);
        }

        /// <summary>
        /// Determines the index of the Texture to be drawn
        /// </summary>
        /// <returns>index of the Texture</returns>
        public int GetDrawIndex()
        {
            float data = !IsPowered && control.ValueIfDisabled != null ? (float)control.ValueIfDisabled : locomotive.GetDataOf(control);

            int index = previousFrameIndex;
            switch (ControlDiscrete.ControlType.CabViewControlType)
            {
                case CabViewControlType.Engine_Brake:
                case CabViewControlType.Brakeman_Brake:
                case CabViewControlType.Train_Brake:
                case CabViewControlType.Regulator:
                case CabViewControlType.CutOff:
                case CabViewControlType.Blower:
                case CabViewControlType.Dampers_Front:
                case CabViewControlType.Steam_Heat:
                case CabViewControlType.Orts_Water_Scoop:
                case CabViewControlType.Water_Injector1:
                case CabViewControlType.Water_Injector2:
                case CabViewControlType.Small_Ejector:
                case CabViewControlType.Orts_Large_Ejector:
                case CabViewControlType.FireHole:
                case CabViewControlType.Throttle:
                case CabViewControlType.Throttle_Display:
                    index = PercentToIndex(data);
                    break;
                case CabViewControlType.Friction_Braking:
                    index = data > 0.001 ? 1 : 0;
                    break;
                case CabViewControlType.Dynamic_Brake:
                case CabViewControlType.Dynamic_Brake_Display:
                    var dynBrakePercent = locomotive.Train.TrainType == TrainType.AiPlayerHosting ?
                        locomotive.DynamicBrakePercent : locomotive.LocalDynamicBrakePercent;
                    if (locomotive.DynamicBrakeController != null)
                    {
                        if (dynBrakePercent == -1)
                        {
                            index = 0;
                            break;
                        }
                        if (!locomotive.HasSmoothStruc)
                        {
                            index = locomotive.DynamicBrakeController.NotchIndex;
                        }
                        else
                        {
                            index = locomotive.CruiseControl != null &&
                                locomotive.CruiseControl.SpeedRegulatorMode == SpeedRegulatorMode.Auto && !locomotive.CruiseControl.DynamicBrakePriority ||
                                locomotive.DynamicBrakeIntervention > 0 ? 0 : PercentToIndex(dynBrakePercent);
                        }
                    }
                    else
                    {
                        index = PercentToIndex(dynBrakePercent);
                    }
                    break;
                case CabViewControlType.Cph_Display:
                    if (locomotive.CombinedControlType == MSTSLocomotive.CombinedControl.ThrottleDynamic && locomotive.DynamicBrakePercent >= 0)
                        // TODO <CSComment> This is a sort of hack to allow MSTS-compliant operation of Dynamic brake indications in the standard USA case with 8 steps (e.g. Dash9)
                        // This hack returns to code of previous OR versions (e.g. release 1.0).
                        // The clean solution for MSTS compliance would be not to increment the percentage of the dynamic brake at first dynamic brake key pression, so that
                        // subsequent steps become of 12.5% as in MSTS instead of 11.11% as in OR. This requires changes in the physics logic </CSComment>
                        index = (int)(ControlDiscrete.FramesCount * locomotive.GetCombinedHandleValue(false));
                    else
                        index = PercentToIndex(locomotive.GetCombinedHandleValue(false));
                    break;
                case CabViewControlType.Cp_Handle:
                    if (locomotive.CombinedControlType == MSTSLocomotive.CombinedControl.ThrottleDynamic && locomotive.DynamicBrakePercent >= 0
                        || locomotive.CombinedControlType == MSTSLocomotive.CombinedControl.ThrottleAir && locomotive.TrainBrakeController.CurrentValue > 0)
                        index = PercentToIndex(locomotive.GetCombinedHandleValue(false));
                    else
                        index = PercentToIndex(locomotive.GetCombinedHandleValue(false));
                    break;
                case CabViewControlType.Orts_Selected_Speed_Display:
                    if (locomotive.CruiseControl == null)
                    {
                        index = 0;
                        break;
                    }
                    index = (int)Speed.MeterPerSecond.ToKpH(locomotive.CruiseControl.SelectedSpeedMpS) / 10;
                    break;
                case CabViewControlType.Alerter_Display:
                case CabViewControlType.Reset:
                case CabViewControlType.Wipers:
                case CabViewControlType.ExternalWipers:
                case CabViewControlType.LeftDoor:
                case CabViewControlType.RightDoor:
                case CabViewControlType.Mirrors:
                case CabViewControlType.Horn:
                case CabViewControlType.Vacuum_Exhauster:
                case CabViewControlType.Whistle:
                case CabViewControlType.Bell:
                case CabViewControlType.Sanders:
                case CabViewControlType.Sanding:
                case CabViewControlType.WheelSlip:
                case CabViewControlType.Front_HLight:
                case CabViewControlType.Pantograph:
                case CabViewControlType.Pantograph2:
                case CabViewControlType.Orts_Pantograph3:
                case CabViewControlType.Orts_Pantograph4:
                case CabViewControlType.Pantographs_4:
                case CabViewControlType.Pantographs_4C:
                case CabViewControlType.Pantographs_5:
                case CabViewControlType.Panto_Display:
                case CabViewControlType.Orts_Circuit_Breaker_Driver_Closing_Order:
                case CabViewControlType.Orts_Circuit_Breaker_Driver_Opening_Order:
                case CabViewControlType.Orts_Circuit_Breaker_Driver_Closing_Authorization:
                case CabViewControlType.Orts_Circuit_Breaker_State:
                case CabViewControlType.Orts_Circuit_Breaker_Closed:
                case CabViewControlType.Orts_Circuit_Breaker_Open:
                case CabViewControlType.Orts_Circuit_Breaker_Authorized:
                case CabViewControlType.Orts_Circuit_Breaker_Open_And_Authorized:
                case CabViewControlType.Orts_Traction_CutOff_Relay_Driver_Closing_Order:
                case CabViewControlType.Orts_Traction_CutOff_Relay_Driver_Opening_Order:
                case CabViewControlType.Orts_Traction_CutOff_Relay_Driver_Closing_Authorization:
                case CabViewControlType.Orts_Traction_CutOff_Relay_State:
                case CabViewControlType.Orts_Traction_CutOff_Relay_Closed:
                case CabViewControlType.Orts_Traction_CutOff_Relay_Open:
                case CabViewControlType.Orts_Traction_CutOff_Relay_Authorized:
                case CabViewControlType.Orts_Traction_CutOff_Relay_Open_And_Authorized:
                case CabViewControlType.Direction:
                case CabViewControlType.Direction_Display:
                case CabViewControlType.Aspect_Display:
                case CabViewControlType.Gears:
                case CabViewControlType.OverSpeed:
                case CabViewControlType.Penalty_App:
                case CabViewControlType.Emergency_Brake:
                case CabViewControlType.Orts_Bailoff:
                case CabViewControlType.Orts_QuickRelease:
                case CabViewControlType.Orts_Overcharge:
                case CabViewControlType.Doors_Display:
                case CabViewControlType.Cyl_Cocks:
                case CabViewControlType.Orts_BlowDown_Valve:
                case CabViewControlType.Orts_Cyl_Comp:
                case CabViewControlType.Steam_Inj1:
                case CabViewControlType.Steam_Inj2:
                case CabViewControlType.Gears_Display:
                case CabViewControlType.Cab_Radio:
                case CabViewControlType.Orts_Player_Diesel_Engine:
                case CabViewControlType.Orts_Helpers_Diesel_Engines:
                case CabViewControlType.Orts_Player_Diesel_Engine_State:
                case CabViewControlType.Orts_Player_Diesel_Engine_Starter:
                case CabViewControlType.Orts_Player_Diesel_Engine_Stopper:
                case CabViewControlType.Orts_CabLight:
                case CabViewControlType.Orts_LeftDoor:
                case CabViewControlType.Orts_RightDoor:
                case CabViewControlType.Orts_Mirros:
                case CabViewControlType.Orts_Battery_Switch_Command_Switch:
                case CabViewControlType.Orts_Battery_Switch_Command_Button_Close:
                case CabViewControlType.Orts_Battery_Switch_Command_Button_Open:
                case CabViewControlType.Orts_Battery_Switch_On:
                case CabViewControlType.Orts_Master_Key:
                case CabViewControlType.Orts_Current_Cab_In_Use:
                case CabViewControlType.Orts_Other_Cab_In_Use:
                case CabViewControlType.Orts_Service_Retention_Button:
                case CabViewControlType.Orts_Service_Retention_Cancellation_Button:
                case CabViewControlType.Orts_Electric_Train_Supply_Command_Switch:
                case CabViewControlType.Orts_Electric_Train_Supply_On:
                case CabViewControlType.Orts_Odometer_Direction:
                case CabViewControlType.Orts_Odometer_Reset:
                case CabViewControlType.Orts_Generic_Item1:
                case CabViewControlType.Orts_Generic_Item2:
                case CabViewControlType.Orts_Eot_Emergency_Brake:
                    index = (int)data;
                    break;
                case CabViewControlType.Orts_Screen_Select:
                case CabViewControlType.Orts_DistributedPower_MoveToBack:
                case CabViewControlType.Orts_DistributedPower_MoveToFront:
                case CabViewControlType.Orts_DistributedPower_Traction:
                case CabViewControlType.Orts_DistributedPower_Idle:
                case CabViewControlType.Orts_DistributedPower_Brake:
                case CabViewControlType.Orts_DistributedPower_Increase:
                case CabViewControlType.Orts_DistributedPower_Decrease:
                case CabViewControlType.Orts_Eot_Comm_Test:
                case CabViewControlType.Orts_Eot_Disarm:
                case CabViewControlType.Orts_Eot_Arm_Two_Way:
                    index = ButtonState ? 1 : 0;
                    break;
                case CabViewControlType.Orts_Static_Display:
                    index = 0;
                    break;
                case CabViewControlType.Orts_Eot_State_Display:
                    index = ControlDiscrete.Values.FindIndex(ind => ind > (int)data) - 1;
                    if (index == -2)
                        index = ControlDiscrete.Values.Count - 1;
                    break;

                // Train Control System controls
                case CabViewControlType.Orts_TCS:
                // Jindrich
                case CabViewControlType.Orts_Restricted_Speed_Zone_Active:
                case CabViewControlType.Orts_Selected_Speed_Mode:
                case CabViewControlType.Orts_Selected_Speed_Regulator_Mode:
                case CabViewControlType.Orts_Selected_Speed_Maximum_Acceleration:
                case CabViewControlType.Orts_Number_Of_Axes_Display_Units:
                case CabViewControlType.Orts_Number_Of_Axes_Display_Tens:
                case CabViewControlType.Orts_Number_Of_Axes_Display_Hundreds:
                case CabViewControlType.Orts_Train_Length_Metres:
                case CabViewControlType.Orts_Remaining_Train_Length_Speed_Restricted:
                case CabViewControlType.Orts_Remaining_Train_Length_Percent:
                case CabViewControlType.Orts_Motive_Force:
                case CabViewControlType.Orts_Motive_Force_KiloNewton:
                case CabViewControlType.Orts_Maximum_Force:
                case CabViewControlType.Orts_Selected_Speed:
                case CabViewControlType.Orts_Force_In_Percent_Throttle_And_Dynamic_Brake:
                case CabViewControlType.Orts_Train_Type_Pax_Or_Cargo:
                case CabViewControlType.Orts_Controller_Voltage:
                case CabViewControlType.Orts_Ampers_By_Controller_Voltage:
                case CabViewControlType.Orts_CC_Selected_Speed:
                case CabViewControlType.Orts_Multi_Position_Controller:
                case CabViewControlType.Orts_Acceleration_In_Time:
                case CabViewControlType.Orts_CC_Speed_0:
                case CabViewControlType.Orts_CC_Speed_Delta:
                    index = (int)data;
                    break;
                case CabViewControlType.Orts_Selected_Speed_Selector:
                    float fraction = data / (float)ControlDiscrete.ScaleRangeMax;
                    index = MathHelper.Clamp(0, (int)(fraction * ((ControlDiscrete as CabViewDiscreteControl).FramesCount - 1)), (ControlDiscrete as CabViewDiscreteControl).FramesCount - 1);
                    break;
            }
            // If it is a control with NumPositions and NumValues, the index becomes the reference to the Positions entry, which in turn is the frame index within the .ace file
            if (ControlDiscrete is CabViewDiscreteControl && !(ControlDiscrete is CabViewSignalControl) && (ControlDiscrete as CabViewDiscreteControl).Positions.Count > index &&
                (ControlDiscrete as CabViewDiscreteControl).Positions.Count == ControlDiscrete.Values.Count && index >= 0)
                index = (ControlDiscrete as CabViewDiscreteControl).Positions[index];

            if (index >= ControlDiscrete.FramesCount)
                index = ControlDiscrete.FramesCount - 1;
            if (index < 0)
                index = 0;
            previousFrameIndex = index;
            return index;
        }

        public bool IsMouseWithin(Point mousePoint)
        {
            return ControlDiscrete.MouseControl & DestinationRectangle.Contains(mousePoint.X, mousePoint.Y);
        }

        private float UpdateCommandValue(float value, GenericButtonEventType buttonEventType, Vector2 delta)
        {
            switch (ControlDiscrete.ControlStyle)
            {
                case CabViewControlStyle.OnOff:
                    return buttonEventType == GenericButtonEventType.Pressed ? 1 - value : value;
                case CabViewControlStyle.While_Pressed:
                case CabViewControlStyle.Pressed:
                    return buttonEventType == GenericButtonEventType.Pressed ? 1 : 0;
                case CabViewControlStyle.None:
                    IntermediateValue %= 0.5f;
                    IntermediateValue += (ControlDiscrete.Orientation > 0 ? delta.Y / control.Bounds.Height : delta.X / control.Bounds.Width) * (ControlDiscrete.Direction > 0 ? -1 : 1);
                    return IntermediateValue > 0.5f ? 1 : IntermediateValue < -0.5f ? -1 : 0;
                default:
                    return value + (ControlDiscrete.Orientation > 0 ? delta.Y / control.Bounds.Height : delta.X / control.Bounds.Width) * (ControlDiscrete.Direction > 0 ? -1 : 1);
            }
        }

        public string GetControlName(Point mousePoint)
        {
            return ControlDiscrete.ControlType.CabViewControlType == CabViewControlType.Orts_TCS
                ? locomotive.TrainControlSystem.GetDisplayString(ControlDiscrete.ControlType.Id)
                : GetControlType().ToString();
        }

        public string ControlLabel => control.Label;

        /// <summary>
        /// Handles cabview mouse events, and changes the corresponding locomotive control values.
        /// </summary>
        public void HandleUserInput(GenericButtonEventType buttonEventType, Point position, Vector2 delta)
        {
            switch (control.ControlType.CabViewControlType)
            {
                case CabViewControlType.Regulator:
                case CabViewControlType.Throttle:
                    if (locomotive.CruiseControl?.SelectedMaxAccelerationPercent == 0 && locomotive.CruiseControl.SpeedRegulatorMode == SpeedRegulatorMode.Auto
                        && (locomotive.CruiseControl.DisableCruiseControlOnThrottleAndZeroForce || locomotive.CruiseControl.DisableCruiseControlOnThrottleAndZeroForceAndZeroSpeed &&
                        locomotive.CruiseControl.SelectedSpeedMpS == 0))
                    {
                        if (locomotive.CruiseControl.ZeroSelectedSpeedWhenPassingToThrottleMode)
                            locomotive.CruiseControl.SetSpeed(0);
                        if (locomotive.ThrottleController.CurrentValue == 0)
                        {
                            locomotive.CruiseControl.SpeedRegulatorMode = SpeedRegulatorMode.Manual;
                            locomotive.CruiseControl.DynamicBrakePriority = false;
                        }
                        locomotive.CruiseControl.SkipThrottleDisplay = false;
                    }
                    if (locomotive.CruiseControl?.SpeedRegulatorMode == SpeedRegulatorMode.Auto
                        && locomotive.CruiseControl.SelectedMaxAccelerationPercent != 0 && locomotive.CruiseControl.HasIndependentThrottleDynamicBrakeLever)
                        break;
                    locomotive.SetThrottleValue(UpdateCommandValue(locomotive.ThrottleController.IntermediateValue, buttonEventType, delta));
                    break;
                case CabViewControlType.Engine_Brake:
                    locomotive.SetEngineBrakeValue(UpdateCommandValue(locomotive.EngineBrakeController.IntermediateValue, buttonEventType, delta));
                    break;
                case CabViewControlType.Brakeman_Brake:
                    locomotive.SetBrakemanBrakeValue(UpdateCommandValue(locomotive.BrakemanBrakeController.IntermediateValue, buttonEventType, delta));
                    break;
                case CabViewControlType.Train_Brake:
                    locomotive.SetTrainBrakeValue(UpdateCommandValue(locomotive.TrainBrakeController.IntermediateValue, buttonEventType, delta));
                    break;
                case CabViewControlType.Dynamic_Brake:
                    locomotive.SetDynamicBrakeValue(UpdateCommandValue(locomotive.DynamicBrakeController.IntermediateValue, buttonEventType, delta));
                    break;
                case CabViewControlType.Gears:
                    locomotive.SetGearBoxValue(UpdateCommandValue(locomotive.GearBoxController.IntermediateValue, buttonEventType, delta));
                    break;
                case CabViewControlType.Direction:
                    float dir = UpdateCommandValue(0, buttonEventType, delta);
                    if (dir != 0)
                        _ = new ReverserCommand(viewer.Log, dir > 0);
                    break;
                case CabViewControlType.Front_HLight:
                    float hl = UpdateCommandValue(0, buttonEventType, delta);
                    if (hl != 0)
                        _ = new HeadlightCommand(viewer.Log, hl > 0);
                    break;
                case CabViewControlType.Whistle:
                case CabViewControlType.Horn:
                    _ = new HornCommand(viewer.Log, UpdateCommandValue(locomotive.Horn ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Vacuum_Exhauster:
                    _ = new VacuumExhausterCommand(viewer.Log, UpdateCommandValue(locomotive.VacuumExhausterPressed ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Bell:
                    _ = new BellCommand(viewer.Log, UpdateCommandValue(locomotive.Bell ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Sanders:
                case CabViewControlType.Sanding:
                    _ = new SanderCommand(viewer.Log, UpdateCommandValue(locomotive.Sander ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Pantograph:
                    _ = new PantographCommand(viewer.Log, 1, UpdateCommandValue(locomotive.Pantographs[1].CommandUp ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Pantograph2:
                    _ = new PantographCommand(viewer.Log, 2, UpdateCommandValue(locomotive.Pantographs[2].CommandUp ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Pantograph3:
                    _ = new PantographCommand(viewer.Log, 3, UpdateCommandValue(locomotive.Pantographs[3].CommandUp ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Pantograph4:
                    _ = new PantographCommand(viewer.Log, 4, UpdateCommandValue(locomotive.Pantographs[4].CommandUp ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Pantographs_4C:
                case CabViewControlType.Pantographs_4:
                    float pantos = UpdateCommandValue(0, buttonEventType, delta);
                    if (pantos != 0)
                    {
#pragma warning disable CA1508 // Avoid dead conditional code // 20230221 - false positive                        
                        if (locomotive.Pantographs[1].State == PantographState.Down && locomotive.Pantographs[2].State == PantographState.Down)
                        {
                            if (pantos > 0)
                                _ = new PantographCommand(viewer.Log, 1, true);
                            else if (control.ControlType.CabViewControlType == CabViewControlType.Pantographs_4C)
                                _ = new PantographCommand(viewer.Log, 2, true);
                        }
                        else if (locomotive.Pantographs[1].State == PantographState.Up && locomotive.Pantographs[2].State == PantographState.Down)
                        {
                            if (pantos > 0)
                                _ = new PantographCommand(viewer.Log, 2, true);
                            else
                                _ = new PantographCommand(viewer.Log, 1, false);
                        }
                        else if (locomotive.Pantographs[1].State == PantographState.Up && locomotive.Pantographs[2].State == PantographState.Up)
                        {
                            if (pantos > 0)
                                _ = new PantographCommand(viewer.Log, 1, false);
                            else
                                _ = new PantographCommand(viewer.Log, 2, false);
                        }
                        else if (locomotive.Pantographs[1].State == PantographState.Down && locomotive.Pantographs[2].State == PantographState.Up)
                        {
                            if (pantos < 0)
                                _ = new PantographCommand(viewer.Log, 1, true);
                            else if (control.ControlType.CabViewControlType == CabViewControlType.Pantographs_4C)
                                _ = new PantographCommand(viewer.Log, 2, false);
                        }
#pragma warning restore CA1508 // Avoid dead conditional code
                    }
                    break;
                case CabViewControlType.Steam_Heat:
                    locomotive.SetSteamHeatValue(UpdateCommandValue(locomotive.SteamHeatController.IntermediateValue, buttonEventType, delta));
                    break;
                case CabViewControlType.Orts_Water_Scoop:
                    if (((locomotive as MSTSSteamLocomotive).WaterScoopDown ? 1 : 0) != UpdateCommandValue(locomotive.WaterScoopDown ? 1 : 0, buttonEventType, delta))
                        _ = new ToggleWaterScoopCommand(viewer.Log);
                    break;
                case CabViewControlType.Orts_Circuit_Breaker_Driver_Closing_Order:
                    _ = new CircuitBreakerClosingOrderCommand(viewer.Log, UpdateCommandValue((locomotive as MSTSElectricLocomotive).ElectricPowerSupply.CircuitBreaker.DriverClosingOrder ? 1 : 0, buttonEventType, delta) > 0);
                    _ = new CircuitBreakerClosingOrderButtonCommand(viewer.Log, UpdateCommandValue(buttonEventType == GenericButtonEventType.Pressed ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Circuit_Breaker_Driver_Opening_Order:
                    _ = new CircuitBreakerOpeningOrderButtonCommand(viewer.Log, UpdateCommandValue(buttonEventType == GenericButtonEventType.Pressed ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Circuit_Breaker_Driver_Closing_Authorization:
                    _ = new CircuitBreakerClosingAuthorizationCommand(viewer.Log, UpdateCommandValue((locomotive as MSTSElectricLocomotive).ElectricPowerSupply.CircuitBreaker.DriverClosingAuthorization ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Traction_CutOff_Relay_Driver_Closing_Order:
                    _ = new TractionCutOffRelayClosingOrderCommand(viewer.Log, UpdateCommandValue((locomotive as MSTSDieselLocomotive).DieselPowerSupply.TractionCutOffRelay.DriverClosingOrder ? 1 : 0, buttonEventType, delta) > 0);
                    _ = new TractionCutOffRelayClosingOrderButtonCommand(viewer.Log, UpdateCommandValue(buttonEventType == GenericButtonEventType.Pressed ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Traction_CutOff_Relay_Driver_Opening_Order:
                    _ = new TractionCutOffRelayOpeningOrderButtonCommand(viewer.Log, UpdateCommandValue(buttonEventType == GenericButtonEventType.Pressed ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Traction_CutOff_Relay_Driver_Closing_Authorization:
                    _ = new TractionCutOffRelayClosingAuthorizationCommand(viewer.Log, UpdateCommandValue((locomotive as MSTSDieselLocomotive).DieselPowerSupply.TractionCutOffRelay.DriverClosingAuthorization ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Emergency_Brake:
                    if ((locomotive.EmergencyButtonPressed ? 1 : 0) != UpdateCommandValue(locomotive.EmergencyButtonPressed ? 1 : 0, buttonEventType, delta))
                        _ = new EmergencyPushButtonCommand(viewer.Log, !locomotive.EmergencyButtonPressed);
                    break;
                case CabViewControlType.Orts_Bailoff:
                    _ = new BailOffCommand(viewer.Log, UpdateCommandValue(locomotive.BailOff ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_QuickRelease:
                    _ = new QuickReleaseCommand(viewer.Log, UpdateCommandValue(locomotive.TrainBrakeController.QuickReleaseButtonPressed ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Overcharge:
                    _ = new BrakeOverchargeCommand(viewer.Log, UpdateCommandValue(locomotive.TrainBrakeController.OverchargeButtonPressed ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Reset:
                    _ = new AlerterCommand(viewer.Log, UpdateCommandValue(locomotive.TrainControlSystem.AlerterButtonPressed ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Cp_Handle:
                    if (locomotive.CruiseControl?.SelectedMaxAccelerationPercent == 0 && locomotive.CruiseControl.SpeedRegulatorMode == SpeedRegulatorMode.Auto
                         && (locomotive.CruiseControl.DisableCruiseControlOnThrottleAndZeroForce || locomotive.CruiseControl.DisableCruiseControlOnThrottleAndZeroForceAndZeroSpeed && locomotive.CruiseControl.SelectedSpeedMpS == 0))
                    {
                        if (locomotive.CruiseControl.ZeroSelectedSpeedWhenPassingToThrottleMode)
                            locomotive.CruiseControl.SetSpeed(0);
                        if (locomotive.ThrottleController.CurrentValue == 0)
                        {
                            locomotive.CruiseControl.SpeedRegulatorMode = SpeedRegulatorMode.Manual;
                            locomotive.CruiseControl.DynamicBrakePriority = false;
                        }
                        locomotive.CruiseControl.SkipThrottleDisplay = false;
                    }
                    if (locomotive.CruiseControl?.SpeedRegulatorMode == SpeedRegulatorMode.Auto
                        && locomotive.CruiseControl.SelectedMaxAccelerationPercent != 0 && locomotive.CruiseControl.HasIndependentThrottleDynamicBrakeLever)
                        break;
                    locomotive.SetCombinedHandleValue(UpdateCommandValue(locomotive.GetCombinedHandleValue(true), buttonEventType, delta));
                    break;
                // Steam locomotives only:
                case CabViewControlType.CutOff:
                    (locomotive as MSTSSteamLocomotive).SetCutoffValue(UpdateCommandValue((locomotive as MSTSSteamLocomotive).CutoffController.IntermediateValue, buttonEventType, delta));
                    break;
                case CabViewControlType.Blower:
                    (locomotive as MSTSSteamLocomotive).SetBlowerValue(UpdateCommandValue((locomotive as MSTSSteamLocomotive).BlowerController.IntermediateValue, buttonEventType, delta));
                    break;
                case CabViewControlType.Dampers_Front:
                    (locomotive as MSTSSteamLocomotive).SetDamperValue(UpdateCommandValue((locomotive as MSTSSteamLocomotive).DamperController.IntermediateValue, buttonEventType, delta));
                    break;
                case CabViewControlType.FireHole:
                    (locomotive as MSTSSteamLocomotive).SetFireboxDoorValue(UpdateCommandValue((locomotive as MSTSSteamLocomotive).FireboxDoorController.IntermediateValue, buttonEventType, delta));
                    break;
                case CabViewControlType.Water_Injector1:
                    (locomotive as MSTSSteamLocomotive).SetInjector1Value(UpdateCommandValue((locomotive as MSTSSteamLocomotive).Injector1Controller.IntermediateValue, buttonEventType, delta));
                    break;
                case CabViewControlType.Water_Injector2:
                    (locomotive as MSTSSteamLocomotive).SetInjector2Value(UpdateCommandValue((locomotive as MSTSSteamLocomotive).Injector2Controller.IntermediateValue, buttonEventType, delta));
                    break;
                case CabViewControlType.Cyl_Cocks:
                    if (((locomotive as MSTSSteamLocomotive).CylinderCocksAreOpen ? 1 : 0) != UpdateCommandValue((locomotive as MSTSSteamLocomotive).CylinderCocksAreOpen ? 1 : 0, buttonEventType, delta))
                        _ = new ToggleCylinderCocksCommand(viewer.Log);
                    break;
                case CabViewControlType.Orts_BlowDown_Valve:
                    if (((locomotive as MSTSSteamLocomotive).BlowdownValveOpen ? 1 : 0) != UpdateCommandValue((locomotive as MSTSSteamLocomotive).BlowdownValveOpen ? 1 : 0, buttonEventType, delta))
                        _ = new ToggleBlowdownValveCommand(viewer.Log);
                    break;
                case CabViewControlType.Orts_Cyl_Comp:
                    if (((locomotive as MSTSSteamLocomotive).CylinderCompoundOn ? 1 : 0) != UpdateCommandValue((locomotive as MSTSSteamLocomotive).CylinderCompoundOn ? 1 : 0, buttonEventType, delta))
                        _ = new ToggleCylinderCompoundCommand(viewer.Log);
                    break;
                case CabViewControlType.Steam_Inj1:
                    if (((locomotive as MSTSSteamLocomotive).Injector1IsOn ? 1 : 0) != UpdateCommandValue((locomotive as MSTSSteamLocomotive).Injector1IsOn ? 1 : 0, buttonEventType, delta))
                        _ = new ToggleInjectorCommand(viewer.Log, 1);
                    break;
                case CabViewControlType.Steam_Inj2:
                    if (((locomotive as MSTSSteamLocomotive).Injector2IsOn ? 1 : 0) != UpdateCommandValue((locomotive as MSTSSteamLocomotive).Injector2IsOn ? 1 : 0, buttonEventType, delta))
                        _ = new ToggleInjectorCommand(viewer.Log, 2);
                    break;
                case CabViewControlType.Small_Ejector:
                    (locomotive as MSTSSteamLocomotive).SetSmallEjectorValue(UpdateCommandValue((locomotive as MSTSSteamLocomotive).SmallEjectorController.IntermediateValue, buttonEventType, delta));
                    break;
                case CabViewControlType.Orts_Large_Ejector:
                    (locomotive as MSTSSteamLocomotive).SetLargeEjectorValue(UpdateCommandValue((locomotive as MSTSSteamLocomotive).LargeEjectorController.IntermediateValue, buttonEventType, delta));
                    break;
                //
                case CabViewControlType.Cab_Radio:
                    _ = new CabRadioCommand(viewer.Log, UpdateCommandValue(locomotive.CabRadioOn ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Wipers:
                    _ = new WipersCommand(viewer.Log, UpdateCommandValue(locomotive.Wiper ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Player_Diesel_Engine:
                    MSTSDieselLocomotive dieselLoco = locomotive as MSTSDieselLocomotive;
                    if ((dieselLoco.DieselEngines[0].State == DieselEngineState.Running ||
                                dieselLoco.DieselEngines[0].State == DieselEngineState.Stopped) &&
                                UpdateCommandValue(1, buttonEventType, delta) == 0)
                        _ = new TogglePlayerEngineCommand(viewer.Log);
                    break;
                case CabViewControlType.Orts_Helpers_Diesel_Engines:
                    foreach (TrainCar car in locomotive.Train.Cars)
                    {
                        dieselLoco = car as MSTSDieselLocomotive;
                        if (dieselLoco != null && dieselLoco.RemoteControlGroup != RemoteControlGroup.Unconnected)
                        {
                            if (car == viewer.Simulator.PlayerLocomotive && dieselLoco.DieselEngines.Count > 1)
                            {
                                if ((dieselLoco.DieselEngines[1].State == DieselEngineState.Running ||
                                            dieselLoco.DieselEngines[1].State == DieselEngineState.Stopped) &&
                                            UpdateCommandValue(1, buttonEventType, delta) == 0)
                                    _ = new ToggleHelpersEngineCommand(viewer.Log);
                                break;
                            }
                            else if (car != viewer.Simulator.PlayerLocomotive && dieselLoco.RemoteControlGroup >= 0)
                            {
                                if ((dieselLoco.DieselEngines[0].State == DieselEngineState.Running ||
                                            dieselLoco.DieselEngines[0].State == DieselEngineState.Stopped) &&
                                            UpdateCommandValue(1, buttonEventType, delta) == 0)
                                    _ = new ToggleHelpersEngineCommand(viewer.Log);
                                break;
                            }
                        }
                    }
                    break;
                case CabViewControlType.Orts_Player_Diesel_Engine_Starter:
                    dieselLoco = locomotive as MSTSDieselLocomotive;
                    if (dieselLoco.DieselEngines[0].State == DieselEngineState.Stopped &&
                                UpdateCommandValue(1, buttonEventType, delta) == 0)
                        _ = new TogglePlayerEngineCommand(viewer.Log);
                    break;
                case CabViewControlType.Orts_Player_Diesel_Engine_Stopper:
                    dieselLoco = locomotive as MSTSDieselLocomotive;
                    if (dieselLoco.DieselEngines[0].State == DieselEngineState.Running &&
                                UpdateCommandValue(1, buttonEventType, delta) == 0)
                        _ = new TogglePlayerEngineCommand(viewer.Log);
                    break;
                case CabViewControlType.Orts_CabLight:
                    if ((locomotive.CabLightOn ? 1 : 0) != UpdateCommandValue(locomotive.CabLightOn ? 1 : 0, buttonEventType, delta))
                        _ = new ToggleCabLightCommand(viewer.Log);
                    break;
                case CabViewControlType.Orts_LeftDoor:
                case CabViewControlType.Orts_RightDoor:
                    {
                        bool right = control.ControlType.CabViewControlType == CabViewControlType.Orts_RightDoor ^ locomotive.Flipped ^ locomotive.GetCabFlipped();
                        var state = locomotive.Train.DoorState(right ? DoorSide.Right : DoorSide.Left);
                        int open = state >= DoorState.Opening ? 1 : 0;
                        if (open != UpdateCommandValue(open, buttonEventType, delta))
                        {
                            if (right)
                                _ = new ToggleDoorsRightCommand(viewer.Log);
                            else
                                _ = new ToggleDoorsLeftCommand(viewer.Log);
                        }
                    }
                    break;
                case CabViewControlType.Orts_Mirros:
                    if ((locomotive.MirrorOpen ? 1 : 0) != UpdateCommandValue(locomotive.MirrorOpen ? 1 : 0, buttonEventType, delta))
                        _ = new ToggleMirrorsCommand(viewer.Log);
                    break;
                case CabViewControlType.Orts_Battery_Switch_Command_Switch:
                    _ = new BatterySwitchCommand(viewer.Log, UpdateCommandValue(locomotive.LocomotivePowerSupply.BatterySwitch.CommandSwitch ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Battery_Switch_Command_Button_Close:
                    _ = new BatterySwitchCloseButtonCommand(viewer.Log, UpdateCommandValue(buttonEventType == GenericButtonEventType.Pressed ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Battery_Switch_Command_Button_Open:
                    _ = new BatterySwitchOpenButtonCommand(viewer.Log, UpdateCommandValue(buttonEventType == GenericButtonEventType.Pressed ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Master_Key:
                    _ = new ToggleMasterKeyCommand(viewer.Log, UpdateCommandValue(locomotive.LocomotivePowerSupply.MasterKey.CommandSwitch ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Service_Retention_Button:
                    _ = new ServiceRetentionButtonCommand(viewer.Log, UpdateCommandValue(buttonEventType == GenericButtonEventType.Pressed ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Service_Retention_Cancellation_Button:
                    _ = new ServiceRetentionCancellationButtonCommand(viewer.Log, UpdateCommandValue(buttonEventType == GenericButtonEventType.Pressed ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Electric_Train_Supply_Command_Switch:
                    _ = new ElectricTrainSupplyCommand(viewer.Log, UpdateCommandValue(locomotive.LocomotivePowerSupply.ElectricTrainSupplySwitch.CommandSwitch ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Odometer_Direction:
                    if (UpdateCommandValue(1, buttonEventType, delta) == 0)
                        _ = new ToggleOdometerDirectionCommand(viewer.Log);
                    break;
                case CabViewControlType.Orts_Odometer_Reset:
                    _ = new ResetOdometerCommand(viewer.Log, UpdateCommandValue(locomotive.OdometerResetButtonPressed ? 1 : 0, buttonEventType, delta) > 0);
                    break;
                case CabViewControlType.Orts_Generic_Item1:
                    if ((locomotive.GenericItem1 ? 1 : 0) != UpdateCommandValue(locomotive.GenericItem1 ? 1 : 0, buttonEventType, delta))
                        _ = new ToggleGenericItem1Command(viewer.Log);
                    break;
                case CabViewControlType.Orts_Generic_Item2:
                    if ((locomotive.GenericItem2 ? 1 : 0) != UpdateCommandValue(locomotive.GenericItem2 ? 1 : 0, buttonEventType, delta))
                        _ = new ToggleGenericItem2Command(viewer.Log);
                    break;
                case CabViewControlType.Orts_Screen_Select:
                    bool buttonState = UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta) > 0;
                    if (((CabViewDiscreteControl)control).NewScreens != null)
                        foreach (var newScreen in ((CabViewDiscreteControl)control).NewScreens)
                        {
                            var newScreenDisplay = newScreen.NewScreenDisplay;
                            if (newScreen.NewScreenDisplay == -1)
                                newScreenDisplay = ((CabViewDiscreteControl)control).Display;
                            _ = new SelectScreenCommand(viewer.Log, buttonState, newScreen.NewScreen, newScreenDisplay);
                        }
                    ButtonState = buttonState;
                    break;
                // Train Control System controls
                case CabViewControlType.Orts_TCS:
                    int commandIndex = control.ControlType.Id - 1;
                    locomotive.TrainControlSystem.TCSCommandButtonDown.TryGetValue(commandIndex, out bool currentValue);
                    if (UpdateCommandValue(1, buttonEventType, delta) > 0 ^ currentValue)
                        _ = new TCSButtonCommand(viewer.Log, !currentValue, commandIndex);
                    locomotive.TrainControlSystem.TCSCommandSwitchOn.TryGetValue(commandIndex, out bool currentSwitchValue);
                    _ = new TCSSwitchCommand(viewer.Log, UpdateCommandValue(currentSwitchValue ? 1 : 0, buttonEventType, delta) > 0, commandIndex);
                    break;
                // Jindrich
                case CabViewControlType.Orts_CC_Selected_Speed:
                    if (locomotive.CruiseControl == null)
                        break;
                    float p = UpdateCommandValue(0, buttonEventType, delta);
                    if (p == 1)
                    {
                        locomotive.CruiseControl.SetSpeed(control.Parameter1);
                        locomotive.CruiseControl.SelectedSpeedPressed = true;
                    }
                    else if (p == 0)
                        locomotive.CruiseControl.SelectedSpeedPressed = false;
                    break;
                case CabViewControlType.Orts_Selected_Speed_Regulator_Mode:
                    p = UpdateCommandValue(0, buttonEventType, delta);
                    if (control.ControlStyle == CabViewControlStyle.OnOff)
                    {
                        if (p == 1)
                        {
                            if (locomotive.CruiseControl.SpeedRegulatorMode == SpeedRegulatorMode.Manual)
                            {
                                locomotive.CruiseControl.SpeedRegulatorModeIncrease();
                            }
                            else if (locomotive.CruiseControl.SpeedRegulatorMode == SpeedRegulatorMode.Auto)
                            {
                                locomotive.CruiseControl.SpeedRegulatorModeDecrease();
                            }
                        }
                    }
                    else
                    {
                        if (p == 1)
                        {
                            locomotive.CruiseControl.SpeedRegulatorModeIncrease();
                        }
                        else if (p == -1)
                        {
                            locomotive.CruiseControl.SpeedRegulatorModeDecrease();
                        }
                    }
                    break;
                case CabViewControlType.Orts_Selected_Speed_Mode:
                    p = UpdateCommandValue(0, buttonEventType, delta);
                    if (p == 1)
                    {
                        locomotive.CruiseControl.SpeedSelectorModeStartIncrease();
                    }
                    else if (locomotive.CruiseControl.SpeedSelectorMode == SpeedSelectorMode.Start)
                    {
                        if (buttonEventType == GenericButtonEventType.Released)
                        {
                            locomotive.CruiseControl.SpeedSelectorModeStopIncrease();
                        }
                    }
                    else if (p == -1)
                    {
                        locomotive.CruiseControl.SpeedSelectorModeDecrease();
                    }
                    break;
                case CabViewControlType.Orts_Restricted_Speed_Zone_Active:
                    if (UpdateCommandValue(0, buttonEventType, delta) == 1)
                    {
                        locomotive.CruiseControl.ActivateRestrictedSpeedZone();
                    }
                    break;
                case CabViewControlType.Orts_Number_Of_Axes_Increase:
                    if (UpdateCommandValue(0, buttonEventType, delta) == 1)
                    {
                        locomotive.CruiseControl.NumerOfAxlesIncrease();
                    }
                    break;
                case CabViewControlType.Orts_Number_Of_Axes_Decrease:
                    if (UpdateCommandValue(0, buttonEventType, delta) == 1)
                    {
                        locomotive.CruiseControl.NumberOfAxlesDecrease();
                    }
                    break;
                case CabViewControlType.Orts_Selected_Speed_Selector:
                    p = UpdateCommandValue(0, buttonEventType, delta);
                    locomotive.CruiseControl.SpeedRegulatorSelectedSpeedChangeByMouse(p, control.ControlUnit == CabViewControlUnit.Km_Per_Hour, (float)control.ScaleRangeMax);
                    break;
                case CabViewControlType.Orts_Selected_Speed_Maximum_Acceleration:
                    p = UpdateCommandValue(0, buttonEventType, delta);
                    locomotive.CruiseControl.SpeedRegulatorMaxForceChangeByMouse(p, (float)control.ScaleRangeMax);
                    break;
                case CabViewControlType.Orts_Multi_Position_Controller:
                    {
                        foreach (MultiPositionController mpc in locomotive.MultiPositionControllers)
                        {
                            if (mpc.ControllerId == control.ControlId)
                            {
                                p = UpdateCommandValue(0, buttonEventType, delta);
                                if (!mpc.StateChanged)
                                    mpc.StateChanged = true;
                                if (p != 0 && locomotive.CruiseControl.SelectedMaxAccelerationPercent == 0 && locomotive.CruiseControl.DisableCruiseControlOnThrottleAndZeroForce &&
                                    locomotive.CruiseControl.ForceRegulatorAutoWhenNonZeroSpeedSelectedAndThrottleAtZero && locomotive.ThrottleController.CurrentValue == 0 &&
                                    locomotive.DynamicBrakeController.CurrentValue == 0 && locomotive.CruiseControl.SpeedRegulatorMode == SpeedRegulatorMode.Manual)
                                    locomotive.CruiseControl.SpeedRegulatorMode = SpeedRegulatorMode.Auto;
                                if (p == 1)
                                {
                                    if (mpc.ControllerBinding == CruiseControllerBinding.SelectedSpeed && locomotive.CruiseControl.ForceRegulatorAutoWhenNonZeroSpeedSelected)
                                    {
                                        locomotive.CruiseControl.SpeedRegulatorMode = SpeedRegulatorMode.Auto;
                                        locomotive.CruiseControl.SpeedSelectorMode = SpeedSelectorMode.On;
                                    }
                                    mpc.DoMovement(Movement.Forward);
                                }
                                if (p == -1)
                                    mpc.DoMovement(Movement.Backward);
                                if (p == 0 && buttonEventType != GenericButtonEventType.Down)
                                {
                                    mpc.DoMovement(Movement.Neutral);
                                    mpc.StateChanged = false;
                                }
                            }
                        }
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_0:
                    {
                        locomotive.CruiseControl.SetSpeed(0, UpdateCommandValue(0, buttonEventType, delta) == 1, true);
                        break;
                    }
                case CabViewControlType.Orts_CC_Speed_Delta:
                    {
                        locomotive.CruiseControl.SetSpeed(control.Parameter1, UpdateCommandValue(0, buttonEventType, delta) == 1, false);
                        break;
                    }
                case CabViewControlType.Orts_DistributedPower_MoveToFront:
                    buttonState = UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta) > 0;
                    if (!ButtonState && (ButtonState ? 1 : 0) != UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta))
                        _ = new DistributedPowerMoveToFrontCommand(viewer.Log);
                    ButtonState = buttonState;
                    break;
                case CabViewControlType.Orts_DistributedPower_MoveToBack:
                    buttonState = UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta) > 0;
                    if (!ButtonState && (ButtonState ? 1 : 0) != UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta))
                        _ = new DistributedPowerMoveToBackCommand(viewer.Log);
                    ButtonState = buttonState;
                    break;
                case CabViewControlType.Orts_DistributedPower_Idle:
                    buttonState = UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta) > 0;
                    if (!ButtonState && (ButtonState ? 1 : 0) != UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta))
                        _ = new DistributedPowerIdleCommand(viewer.Log);
                    ButtonState = buttonState;
                    break;
                case CabViewControlType.Orts_DistributedPower_Traction:
                    buttonState = UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta) > 0;
                    if (!ButtonState && (ButtonState ? 1 : 0) != UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta))
                        _ = new DistributedPowerTractionCommand(viewer.Log);
                    ButtonState = buttonState;
                    break;
                case CabViewControlType.Orts_DistributedPower_Brake:
                    buttonState = UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta) > 0;
                    if (!ButtonState && (ButtonState ? 1 : 0) != UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta))
                        _ = new DistributedPowerDynamicBrakeCommand(viewer.Log);
                    ButtonState = buttonState;
                    break;
                case CabViewControlType.Orts_DistributedPower_Increase:
                    buttonState = UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta) > 0;
                    if (!ButtonState && (ButtonState ? 1 : 0) != UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta))
                        _ = new DistributedPowerIncreaseCommand(viewer.Log);
                    ButtonState = buttonState;
                    break;
                case CabViewControlType.Orts_DistributedPower_Decrease:
                    buttonState = UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta) > 0;
                    if (!ButtonState && (ButtonState ? 1 : 0) != UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta))
                        _ = new DistributedPowerDecreaseCommand(viewer.Log);
                    ButtonState = buttonState;
                    break;
                case CabViewControlType.Orts_Eot_Comm_Test:
                    buttonState = UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta) > 0;
                    if (!ButtonState && (ButtonState ? 1 : 0) != UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta))
                        _ = new EOTCommTestCommand(viewer.Log);
                    ButtonState = buttonState;
                    break;
                case CabViewControlType.Orts_Eot_Disarm:
                    buttonState = UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta) > 0;
                    if (!ButtonState && (ButtonState ? 1 : 0) != UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta))
                        _ = new EOTDisarmCommand(viewer.Log);
                    ButtonState = buttonState;
                    break;
                case CabViewControlType.Orts_Eot_Arm_Two_Way:
                    buttonState = UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta) > 0;
                    if (!ButtonState && (ButtonState ? 1 : 0) != UpdateCommandValue(ButtonState ? 1 : 0, buttonEventType, delta))
                        _ = new EOTArmTwoWayCommand(viewer.Log);
                    ButtonState = buttonState;
                    break;
                case CabViewControlType.Orts_Eot_Emergency_Brake:
                    if (UpdateCommandValue(0, buttonEventType, delta) == 1)
                    {
                        if (locomotive.Train?.EndOfTrainDevice != null)
                        {
                            _ = new ToggleEOTEmergencyBrakeCommand(viewer.Log);
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Translates a percent value to a display index
        /// </summary>
        /// <param name="percent">Percent to be translated</param>
        /// <returns>The calculated display index by the Control's Values</returns>
        protected int PercentToIndex(float percent)
        {
            var index = 0;

            if (percent > 1)
                percent /= 100f;

            if (ControlDiscrete.ScaleRangeMin != ControlDiscrete.ScaleRangeMax && !(ControlDiscrete.ScaleRangeMin == 0 && ControlDiscrete.ScaleRangeMax == 0))
                percent = MathHelper.Clamp(percent, (float)ControlDiscrete.ScaleRangeMin, (float)ControlDiscrete.ScaleRangeMax);

            if (ControlDiscrete.Values.Count > 1)
            {
                try
                {
                    var val = ControlDiscrete.Values[0] <= ControlDiscrete.Values[ControlDiscrete.Values.Count - 1] ?
                        ControlDiscrete.Values.Where(v => (float)v <= percent + 0.00001).Last() : ControlDiscrete.Values.Where(v => (float)v <= percent + 0.00001).First();
                    index = ControlDiscrete.Values.IndexOf(val);
                }
                catch
                {
                    var val = ControlDiscrete.Values.Min();
                    index = ControlDiscrete.Values.IndexOf(val);
                }
            }
            else if (ControlDiscrete.ScaleRangeMax != ControlDiscrete.ScaleRangeMin)
            {
                index = (int)(percent / (ControlDiscrete.ScaleRangeMax - ControlDiscrete.ScaleRangeMin) * ControlDiscrete.FramesCount);
            }

            return index;
        }
    }
}
