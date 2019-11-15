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

#define GEARBOX_DEBUG_LOG

#define DEBUG_DUMP_STEAM_POWER_CURVE
// Uses the DataLogger to record power curve data for steam locos when no other option is chosen.
// To use this, on the Menu, check the Logging box and cancel all Options > DataLogger.
// The data logger records data in the file "Program\dump.csv".
// For steam locomotives only this replaces the default data with a record for each speed increment (mph).
// Collect the data by starting from rest and accelerating the loco to maximum speed.
// Only horsepower and mph available currently.
// Analyse the data using a spreadsheet and graph with an XY chart.


using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.Input;
using Orts.Common.Logging;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D
{
    /// <summary>
    /// Displays Viewer frame rate and Viewer.Text debug messages in the upper left corner of the screen.
    /// </summary>
    public class InfoDisplay
    {
        private readonly Viewer viewer;
        private readonly DataLogger dataLog;
        private readonly int ProcessorCount = Environment.ProcessorCount;

        private int frameNumber;
        private double lastUpdateRealTime;   // update text message only 4 times per second

        float previousLoggedSteamSpeedMpH = -5.0f;
        private bool recordSteamPerformance;
        private bool recordSteamPowerCurve;

#if DEBUG_DUMP_STEAM_POWER_CURVE
        float previousLoggedSpeedMpH = -1.0f;
#endif

        public InfoDisplay(Viewer viewer)
        {
            this.viewer = viewer;
            dataLog = new DataLogger(Path.Combine(viewer.Settings.LoggingPath, "OpenRailsDump.csv"));
            if (!Enum.TryParse(viewer.Settings.DataLoggerSeparator, true, out dataLog.Separator))
                throw new ArgumentException($"Separator char \"{viewer.Settings.DataLoggerSeparator}\" is not one of allowed values");

            if (viewer.Settings.DataLogger)
                DataLoggerStart();
        }

        internal void Terminate()
        {
            if (viewer.Settings.DataLogger)
                DataLoggerStop();
        }

        public void HandleUserInput(in ElapsedTime elapsedTime)
        {
            if (UserInput.IsPressed(UserCommand.DebugLogger))
            {
                viewer.Settings.DataLogger = !viewer.Settings.DataLogger;
                if (viewer.Settings.DataLogger)
                    DataLoggerStart();
                else
                    DataLoggerStop();
            }
        }

        private void RecordSteamPerformance()
        {
            MSTSSteamLocomotive steamloco = viewer.PlayerLocomotive as MSTSSteamLocomotive;

            float SteamspeedMpH = (float)Speed.MeterPerSecond.ToMpH(steamloco.SpeedMpS);
            if (SteamspeedMpH >= previousLoggedSteamSpeedMpH + 5) // Add a new record every time speed increases by 5 mph
            {
                previousLoggedSteamSpeedMpH = (int)SteamspeedMpH; // Keep speed records close to whole numbers

                dataLog.Data(Speed.MeterPerSecond.FromMpS(steamloco.SpeedMpS, false).ToString("F0"));
                dataLog.Data(Time.Second.ToM(steamloco.SteamPerformanceTimeS).ToString("F1"));
                dataLog.Data(steamloco.ThrottlePercent.ToString("F0"));
                dataLog.Data(steamloco.Train.MUReverserPercent.ToString("F0"));
                dataLog.Data(Dynamics.Force.ToLbf(steamloco.MotiveForceN).ToString("F0"));
                dataLog.Data(steamloco.IndicatedHorsePowerHP.ToString("F0"));
                dataLog.Data(steamloco.DrawBarPullLbsF.ToString("F0"));
                dataLog.Data(steamloco.DrawbarHorsePowerHP.ToString("F0"));
                dataLog.Data(Dynamics.Force.ToLbf(steamloco.LocomotiveCouplerForceN).ToString("F0"));
                dataLog.Data(Dynamics.Force.ToLbf(steamloco.LocoTenderFrictionForceN).ToString("F0"));
                dataLog.Data(Dynamics.Force.ToLbf(steamloco.TotalFrictionForceN).ToString("F0"));
                dataLog.Data(Mass.Kilogram.ToTonsUK(steamloco.TrainLoadKg).ToString("F0"));
                dataLog.Data(steamloco.BoilerPressurePSI.ToString("F0"));
                dataLog.Data(steamloco.LogSteamChestPressurePSI.ToString("F0"));
                dataLog.Data(steamloco.LogInitialPressurePSI.ToString("F0"));
                dataLog.Data(steamloco.LogCutoffPressurePSI.ToString("F0"));
                dataLog.Data(steamloco.LogReleasePressurePSI.ToString("F0"));
                dataLog.Data(steamloco.LogBackPressurePSI.ToString("F0"));

                dataLog.Data(steamloco.MeanEffectivePressurePSI.ToString("F0"));

                dataLog.Data(steamloco.CurrentSuperheatTempF.ToString("F0"));

                dataLog.Data(Frequency.Periodic.ToHours(steamloco.CylinderSteamUsageLBpS).ToString("F0"));
                dataLog.Data(Frequency.Periodic.ToHours(steamloco.WaterConsumptionLbpS).ToString("F0"));
                dataLog.Data(Mass.Kilogram.ToLb(Frequency.Periodic.ToHours(steamloco.FuelBurnRateSmoothedKGpS)).ToString("F0"));

                dataLog.Data(steamloco.SuperheaterSteamUsageFactor.ToString("F2"));
                dataLog.Data(steamloco.CumulativeCylinderSteamConsumptionLbs.ToString("F0"));
                dataLog.Data(steamloco.CumulativeWaterConsumptionLbs.ToString("F0"));

                dataLog.Data(steamloco.CutoffPressureDropRatio.ToString("F2"));

                dataLog.Data(steamloco.HPCylinderMEPPSI.ToString("F0"));
                dataLog.Data(steamloco.LogLPInitialPressurePSI.ToString("F0"));
                dataLog.Data(steamloco.LogLPCutoffPressurePSI.ToString("F0"));
                dataLog.Data(steamloco.LogLPReleasePressurePSI.ToString("F0"));
                dataLog.Data(steamloco.LogLPBackPressurePSI.ToString("F0"));
                dataLog.Data(steamloco.CutoffPressureDropRatio.ToString("F2"));
                dataLog.Data(steamloco.LPCylinderMEPPSI.ToString("F0"));

                dataLog.EndLine();
            }
        }

        private void RecordSteamPowerCurve()
        {
            MSTSSteamLocomotive steamloco = viewer.PlayerLocomotive as MSTSSteamLocomotive;
            double speedMpH = Speed.MeterPerSecond.ToMpH(steamloco.SpeedMpS);
            if (speedMpH >= previousLoggedSpeedMpH + 1) // Add a new record every time speed increases by 1 mph
            {
                previousLoggedSpeedMpH = (int)speedMpH; // Keep speed records close to whole numbers
                dataLog.Data(speedMpH.ToString("F1"));
                double power = Dynamics.Power.ToHp(steamloco.MotiveForceN * steamloco.SpeedMpS);
                dataLog.Data(power.ToString("F1"));
                dataLog.Data(steamloco.ThrottlePercent.ToString("F0"));
                dataLog.Data(steamloco.Train.MUReverserPercent.ToString("F0"));
                dataLog.EndLine();
            }
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            frameNumber++;

            double elapsedRealSeconds = viewer.RealTime - lastUpdateRealTime;
            if (elapsedRealSeconds >= 0.25)
            {
                lastUpdateRealTime = viewer.RealTime;
                Profile(elapsedRealSeconds);
            }
                        
#if DEBUG_DUMP_STEAM_POWER_CURVE
            if (recordSteamPowerCurve && viewer.PlayerLocomotive is MSTSSteamLocomotive)
            {
                RecordSteamPowerCurve();
            }
            else
            {
#endif
                if (recordSteamPerformance && viewer.PlayerLocomotive is MSTSSteamLocomotive)
                {
                    RecordSteamPerformance();
                }
                else


            //Here's where the logger stores the data from each frame
                if (viewer.Settings.DataLogger)
                {
                    if (viewer.Settings.DataLogPerformance)
                    {
                        viewer.CurrentProcess.Refresh();
                        dataLog.Data(VersionInfo.Version);
                        dataLog.Data(frameNumber.ToString("F0"));
                        dataLog.Data(viewer.CurrentProcess.WorkingSet64.ToString("F0"));
                        dataLog.Data(GC.GetTotalMemory(false).ToString("F0"));
                        dataLog.Data(GC.CollectionCount(0).ToString("F0"));
                        dataLog.Data(GC.CollectionCount(1).ToString("F0"));
                        dataLog.Data(GC.CollectionCount(2).ToString("F0"));
                        dataLog.Data(ProcessorCount.ToString("F0"));
                        dataLog.Data(viewer.RenderProcess.FrameRate.Value.ToString("F0"));
                        dataLog.Data(viewer.RenderProcess.FrameTime.Value.ToString("F6"));
                        dataLog.Data(viewer.RenderProcess.ShadowPrimitivePerFrame.Sum().ToString("F0"));
                        dataLog.Data(viewer.RenderProcess.PrimitivePerFrame.Sum().ToString("F0"));
                        dataLog.Data(viewer.RenderProcess.Profiler.Wall.Value.ToString("F0"));
                        dataLog.Data(viewer.UpdaterProcess.Profiler.Wall.Value.ToString("F0"));
                        dataLog.Data(viewer.LoaderProcess.Profiler.Wall.Value.ToString("F0"));
                        dataLog.Data(viewer.SoundProcess.Profiler.Wall.Value.ToString("F0"));
                    }
                    if (viewer.Settings.DataLogPhysics)
                    {
                        dataLog.Data(FormatStrings.FormatPreciseTime(viewer.Simulator.ClockTime));
                        dataLog.Data(viewer.PlayerLocomotive.Direction.ToString());
                        dataLog.Data(viewer.PlayerTrain.MUReverserPercent.ToString("F0"));
                        dataLog.Data(viewer.PlayerLocomotive.ThrottlePercent.ToString("F0"));
                        dataLog.Data(viewer.PlayerLocomotive.MotiveForceN.ToString("F0"));
                        dataLog.Data(viewer.PlayerLocomotive.BrakeForceN.ToString("F0"));
                        dataLog.Data((viewer.PlayerLocomotive as MSTSLocomotive).LocomotiveAxle.AxleForceN.ToString("F2"));
                        dataLog.Data((viewer.PlayerLocomotive as MSTSLocomotive).LocomotiveAxle.SlipSpeedPercent.ToString("F1"));

                        switch (viewer.Settings.DataLogSpeedUnits)
                        {
                            case "route":
                                dataLog.Data(FormatStrings.FormatSpeed(viewer.PlayerLocomotive.SpeedMpS, viewer.MilepostUnitsMetric));
                                break;
                            case "mps":
                                dataLog.Data(viewer.PlayerLocomotive.SpeedMpS.ToString("F1"));
                                break;
                            case "mph":
                                dataLog.Data(Speed.MeterPerSecond.FromMpS(viewer.PlayerLocomotive.SpeedMpS, false).ToString("F1"));
                                break;
                            case "kmph":
                                dataLog.Data(Speed.MeterPerSecond.FromMpS(viewer.PlayerLocomotive.SpeedMpS, true).ToString("F1"));
                                break;
                            default:
                                dataLog.Data(FormatStrings.FormatSpeed(viewer.PlayerLocomotive.SpeedMpS, viewer.MilepostUnitsMetric));
                                break;
                        }

                        dataLog.Data((viewer.PlayerLocomotive.DistanceM.ToString("F0")));
                        dataLog.Data((viewer.PlayerLocomotive.GravityForceN.ToString("F0")));

                        if ((viewer.PlayerLocomotive as MSTSLocomotive).TrainBrakeController != null)
                            dataLog.Data((viewer.PlayerLocomotive as MSTSLocomotive).TrainBrakeController.CurrentValue.ToString("F2"));
                        else
                            dataLog.Data("null");

                        if ((viewer.PlayerLocomotive as MSTSLocomotive).EngineBrakeController != null)
                            dataLog.Data((viewer.PlayerLocomotive as MSTSLocomotive).EngineBrakeController.CurrentValue.ToString("F2"));
                        else
                            dataLog.Data("null");

                        dataLog.Data(viewer.PlayerLocomotive.BrakeSystem.GetCylPressurePSI().ToString("F0"));
                        dataLog.Data((viewer.PlayerLocomotive as MSTSLocomotive).MainResPressurePSI.ToString("F0"));
                        dataLog.Data((viewer.PlayerLocomotive as MSTSLocomotive).CompressorIsOn.ToString());
#if GEARBOX_DEBUG_LOG
                        if (viewer.PlayerLocomotive is MSTSDieselLocomotive dieselLoco)
                        {
                            dataLog.Data(dieselLoco.DieselEngines[0].RealRPM.ToString("F0"));
                            dataLog.Data(dieselLoco.DieselEngines[0].DemandedRPM.ToString("F0"));
                            dataLog.Data(dieselLoco.DieselEngines[0].LoadPercent.ToString("F0"));
                            if (dieselLoco.DieselEngines.HasGearBox)
                            {
                                dataLog.Data(dieselLoco.DieselEngines[0].GearBox.CurrentGearIndex.ToString());
                                dataLog.Data(dieselLoco.DieselEngines[0].GearBox.NextGearIndex.ToString());
                                dataLog.Data(dieselLoco.DieselEngines[0].GearBox.ClutchPercent.ToString());
                            }
                            else
                            {
                                dataLog.Data("null");
                                dataLog.Data("null");
                                dataLog.Data("null");
                            }
                            dataLog.Data(dieselLoco.DieselFlowLps.ToString("F2"));
                            dataLog.Data(dieselLoco.DieselLevelL.ToString("F0"));
                            dataLog.Data("null");
                            dataLog.Data("null");
                            dataLog.Data("null");
                        }
                        else if (viewer.PlayerLocomotive is MSTSElectricLocomotive electricLoco)
                        {
                            dataLog.Data(electricLoco.Pantographs[1].CommandUp.ToString());
                            dataLog.Data(electricLoco.Pantographs[2].CommandUp.ToString());
                            dataLog.Data(electricLoco.Pantographs.List.Count > 2 ?
                                electricLoco.Pantographs[3].CommandUp.ToString() : null);
                            dataLog.Data(electricLoco.Pantographs.List.Count > 3 ?
                                electricLoco.Pantographs[4].CommandUp.ToString() : null);
                            dataLog.Data("null");
                            dataLog.Data("null");
                            dataLog.Data("null");
                            dataLog.Data("null");
                            dataLog.Data("null");
                            dataLog.Data("null");
                            dataLog.Data("null");
                            dataLog.Data("null");
                            dataLog.Data("null");
                        }
                        else if (viewer.PlayerLocomotive is MSTSSteamLocomotive steamLoco)
                        {
                            dataLog.Data(steamLoco.BlowerSteamUsageLBpS.ToString("F0"));
                            dataLog.Data(steamLoco.BoilerPressurePSI.ToString("F0"));
                            dataLog.Data(steamLoco.CylinderCocksAreOpen.ToString());
                            dataLog.Data(steamLoco.CylinderCompoundOn.ToString());
                            dataLog.Data(steamLoco.EvaporationLBpS.ToString("F0"));
                            dataLog.Data(steamLoco.FireMassKG.ToString("F0"));
                            dataLog.Data(steamLoco.CylinderSteamUsageLBpS.ToString("F0"));
                            if (steamLoco.BlowerController != null)
                                dataLog.Data(steamLoco.BlowerController.CurrentValue.ToString("F0"));
                            else
                                dataLog.Data("null");

                            if (steamLoco.DamperController != null)
                                dataLog.Data(steamLoco.DamperController.CurrentValue.ToString("F0"));
                            else
                                dataLog.Data("null");
                            if (steamLoco.FiringRateController != null)
                                dataLog.Data(steamLoco.FiringRateController.CurrentValue.ToString("F0"));
                            else
                                dataLog.Data("null");
                            if (steamLoco.Injector1Controller != null)
                                dataLog.Data(steamLoco.Injector1Controller.CurrentValue.ToString("F0"));
                            else
                                dataLog.Data("null");
                            if (steamLoco.Injector2Controller != null)
                                dataLog.Data(steamLoco.Injector2Controller.CurrentValue.ToString("F0"));
                            else
                                dataLog.Data("null");
                        }
#endif
                    }
                dataLog.EndLine();
#if DEBUG_DUMP_STEAM_POWER_CURVE
                }
#endif
            }
        }

        private void DataLoggerStart()
        {
            StringBuilder headline = new StringBuilder();

            recordSteamPerformance = false;
            recordSteamPowerCurve = false;
            if (viewer.Settings.DataLogPerformance)
            {
                headline.Append(string.Join(((char)dataLog.Separator).ToString(), new string[] {
                    "SVN",
                    "Frame",
                    "Memory",
                    "Memory (Managed)",
                    "Gen 0 GC",
                    "Gen 1 GC",
                    "Gen 2 GC",
                    "Processors",
                    "Frame Rate",
                    "Frame Time",
                    "Shadow Primitives",
                    "Render Primitives",
                    "Render Process",
                    "Updater Process",
                    "Loader Process",
                    "Sound Process" } ));
            }
            if (viewer.Settings.DataLogPhysics)
            {
                if (headline.Length > 0)
                    headline.Append(((char)dataLog.Separator).ToString());

                headline.Append(string.Join(((char)dataLog.Separator).ToString(), new string[] {
                    "Time",
                    "Player Direction",
                    "Player Reverser [%]",
                    "Player Throttle [%]",
                    "Player Motive Force [N]",
                    "Player Brake Force [N]",
                    "Player Axle Force [N]",
                    "Player Wheelslip",
                    $"Player Speed [{viewer.Settings.DataLogSpeedUnits}]",
                    "Distance [m]",
                    "Player Gravity Force [N]",
                    "Train Brake",
                    "Engine Brake",
                    "Player Cylinder PSI",
                    "Player Main Res PSI",
                    "Player Compressor On",
                    "D:Real RPM / E:panto 1 / S:Blower usage LBpS",
                    "D:Demanded RPM / E:panto 2 / S:Boiler PSI",
                    "D:Load % / E:panto 3 / S:Cylinder Cocks open",
                    "D:Gearbox Current Gear / E:panto 4 / S:Evaporation LBpS",
                    "D:Gearbox Next Gear / E:null / S:Fire Mass KG",
                    "D:Clutch % / E:null / S:Steam usage LBpS",
                    "D:Fuel Flow Lps / E:null / S:Blower",
                    "D:Fuel level L / E:null / S:Damper",
                    "D:null / E:null / S:Firing Rate",
                    "D:null / E:null / S:Injector 1",
                    "D:null / E:null / S:Injector 2" } ));
            }
            if (viewer.Settings.DataLogSteamPerformance)
            {
                recordSteamPerformance = true;
                if (headline.Length > 0)
                    headline.Append(((char)dataLog.Separator).ToString());

                headline.Append(string.Join(((char)dataLog.Separator).ToString(), new string[] {
                    "Speed (mph)",
                    "Time (M)",
                    "Throttle (%)",
                    "Cut-off (%)",
                    "ITE (MotiveForce - lbf)",
                    "IHP (hp)",
                    "Drawbar TE (lbf)",
                    "Drawbar HP (hp)",
                    "Coupler Force (lbf)",
                    "Loco & Tender Resistance (lbf)",
                    "Train Resistance (lbf)",
                    "Train Load (t-uk)",
                    "Boiler Pressure (psi)",
                    "Steam Chest Pressure (psi)",
                    "Initial Pressure (psi)",
                    "Cutoff Pressure (psi)",
                    "Release Pressure (psi)",
                    "Back Pressure (psi)",
                    "MEP (psi)",
                    "Superheat Temp (F)",
                    "Steam consumption (lbs/h)",
                    "Water consumption (lbs/h)",
                    "Coal consumption (lbs/h)",
                    "Cylinder Thermal Efficiency",
                    "Cumulative Steam (lbs)",
                    "Cumulative Water (lbs)",
                    "Cutoff pressure Ratio",
                    "HP MEP (psi)",
                    "LPInitial Pressure (psi)",
                    "LPCutoff Pressure (psi)",
                    "LPRelease Pressure (psi)",
                    "LPBack Pressure (psi)",
                    "LPCutoff pressure Ratio",
                    "LP MEP (psi)" } ));
            }

#if DEBUG_DUMP_STEAM_POWER_CURVE
            if (!viewer.Settings.DataLogPerformance && !viewer.Settings.DataLogPhysics && !viewer.Settings.DataLogMisc && !viewer.Settings.DataLogSteamPerformance)
            {
                recordSteamPowerCurve = true;
                headline.Append(string.Join(((char)dataLog.Separator).ToString(), new string[] {
                    "speed (mph)",
                    "power (hp)",
                    "throttle (%)",
                    "cut-off (%)" } ));
            }
#endif
            dataLog.AddHeadline(headline.ToString());

        }

        private void DataLoggerStop()
        {
            dataLog.Flush();
        }

        public void Profile(double elapsedRealSeconds) // should be called every 100mS
        {
            if (elapsedRealSeconds < 0.01)  // just in case
                return;

            viewer.RenderProcess.Profiler.Mark();
            viewer.UpdaterProcess.Profiler.Mark();
            viewer.LoaderProcess.Profiler.Mark();
            viewer.SoundProcess.Profiler.Mark();
        }
    }
}
