
using System;
using System.Diagnostics;

using Orts.Common;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS;

namespace Orts.Simulation.Physics
{
    public partial class Train
    {
        /// Initialize brakes
        internal virtual void InitializeBrakes()
        {
            if (Math.Abs(SpeedMpS) > 0.1)
            {
                simulator.Confirmer?.Warning(CabControl.InitializeBrakes, CabSetting.Warn1);// As Confirmer may not be created until after a restore.
                return;
            }
            UnconditionalInitializeBrakes();
            return;
        }

        /// Initializes brakes also if Speed != 0; directly used by keyboard command
        internal void UnconditionalInitializeBrakes()
        {
            if (simulator.Settings.SimpleControlPhysics && LeadLocomotiveIndex >= 0) // If brake and control set to simple, and a locomotive present, then set all cars to same brake system as the locomotive
            {
                MSTSLocomotive lead = (MSTSLocomotive)Cars[LeadLocomotiveIndex];
                if (lead.TrainBrakeController != null)
                {
                    foreach (MSTSWagon car in Cars)
                    {
                        if (lead.CarBrakeSystemType != car.CarBrakeSystemType) // Test to see if car brake system is the same as the locomotive
                        {
                            // If not, change so that they are compatible
                            car.CarBrakeSystemType = lead.CarBrakeSystemType;
                            if (lead.BrakeSystem is VacuumSinglePipe)
                                car.MSTSBrakeSystem = new VacuumSinglePipe(car);
                            else if (lead.BrakeSystem is AirTwinPipe)
                                car.MSTSBrakeSystem = new AirTwinPipe(car);
                            else if (lead.BrakeSystem is AirSinglePipe)
                            {
                                car.MSTSBrakeSystem = new AirSinglePipe(car);
                                // if emergency reservoir has been set on lead locomotive then also set on trailing cars
                                if (lead.EmergencyReservoirPresent)
                                {
                                    car.EmergencyReservoirPresent = lead.EmergencyReservoirPresent;
                                }
                            }
                            else if (lead.BrakeSystem is EPBrakeSystem)
                                car.MSTSBrakeSystem = new EPBrakeSystem(car);
                            else if (lead.BrakeSystem is SingleTransferPipe)
                                car.MSTSBrakeSystem = new SingleTransferPipe(car);
                            else
                                throw new Exception("Unknown brake type");

                            car.MSTSBrakeSystem.InitializeFromCopy(lead.BrakeSystem);
                            Trace.TraceInformation("Car and Locomotive Brake System Types Incompatible on Car {0} - Car brakesystem type changed to {1}", car.CarID, car.CarBrakeSystemType);
                        }
                    }
                }
            }

            simulator.Confirmer?.Confirm(CabControl.InitializeBrakes, CabSetting.Off);

            float maxPressurePSI = 90;
            float fullServPressurePSI = 64;
            if (FirstCar?.BrakeSystem is VacuumSinglePipe)
            {
                maxPressurePSI = 21;
                fullServPressurePSI = 16;
            }

            if (LeadLocomotiveIndex >= 0)
            {
                MSTSLocomotive lead = (MSTSLocomotive)Cars[LeadLocomotiveIndex];
                if (lead.TrainBrakeController != null)
                {
                    maxPressurePSI = lead.TrainBrakeController.MaxPressurePSI;
                    fullServPressurePSI = lead.BrakeSystem is VacuumSinglePipe ? 16 : maxPressurePSI - lead.TrainBrakeController.FullServReductionPSI;
                    EqualReservoirPressurePSIorInHg = Math.Min(maxPressurePSI, EqualReservoirPressurePSIorInHg);
                    (double pressurePSI, double epControllerState) = lead.TrainBrakeController.UpdatePressure(EqualReservoirPressurePSIorInHg, BrakeLine4, 1000);
                    BrakeLine4 = (float)epControllerState;
                    EqualReservoirPressurePSIorInHg = (float)Math.Max(pressurePSI, fullServPressurePSI);
                }
                if (lead.EngineBrakeController != null)
                    BrakeLine3PressurePSI = (float)lead.EngineBrakeController.UpdateEngineBrakePressure(BrakeLine3PressurePSI, 1000);
                if (lead.DynamicBrakeController != null)
                {
                    MUDynamicBrakePercent = lead.DynamicBrakeController.Update(1000) * 100;
                    if (MUDynamicBrakePercent == 0)
                        MUDynamicBrakePercent = -1;
                }
                BrakeLine2PressurePSI = maxPressurePSI;
                ConnectBrakeHoses();
            }
            else
            {
                EqualReservoirPressurePSIorInHg = BrakeLine2PressurePSI = BrakeLine3PressurePSI = 0;
                // Initialize static consists airless for allowing proper shunting operations,
                // but set AI trains pumped up with air.
                if (TrainType == TrainType.Static)
                    maxPressurePSI = 0;
                BrakeLine4 = -1;
            }
            foreach (TrainCar car in Cars)
                car.BrakeSystem.Initialize(LeadLocomotiveIndex < 0, maxPressurePSI, fullServPressurePSI, false);
        }

        /// Set handbrakes
        internal void SetHandbrakePercent(float percent)
        {
            if (Math.Abs(SpeedMpS) > 0.1)
                return;
            foreach (TrainCar car in Cars)
                car.BrakeSystem.SetHandbrakePercent(percent);
        }

        /// Connect brake hoses when train is initialised
        internal void ConnectBrakeHoses()
        {
            for (int i = 0; i < Cars.Count; i++)
            {
                Cars[i].BrakeSystem.FrontBrakeHoseConnected = i > 0;
                Cars[i].BrakeSystem.AngleCockAOpen = i > 0;
                Cars[i].BrakeSystem.AngleCockBOpen = i < Cars.Count - 1;
                // If end of train is not reached yet, then test the attached following car. If it is a manual braked car then set the brake cock on this car to closed.
                // Hence automatic brakes will operate to this point in the train.
                if (i < Cars.Count - 1)
                {
                    if (Cars[i + 1].CarBrakeSystemType == "manual_braking") //TODO 20201125 convert to enum
                    {
                        Cars[i].BrakeSystem.AngleCockBOpen = false;
                    }
                }
                Cars[i].BrakeSystem.BleedOffValveOpen = false;
            }
        }

        /// Disconnect brakes
        internal void DisconnectBrakes()
        {
            if (Math.Abs(SpeedMpS) > 0.1)
                return;
            (int first, int last) = FindLeadLocomotives();
            for (int i = 0; i < Cars.Count; i++)
            {
                Cars[i].BrakeSystem.FrontBrakeHoseConnected = first < i && i <= last;
                Cars[i].BrakeSystem.AngleCockAOpen = i != first;
                Cars[i].BrakeSystem.AngleCockBOpen = i != last;
            }
        }

        /// Propagate brake pressure
        protected void PropagateBrakePressure(double elapsedClockSeconds)
        {
            if (LeadLocomotiveIndex >= 0)
            {
                MSTSLocomotive lead = (MSTSLocomotive)Cars[LeadLocomotiveIndex];
                if (lead.TrainBrakeController != null)
                {
                    (double pressurePSI, double epControllerState) = lead.TrainBrakeController.UpdatePressure(EqualReservoirPressurePSIorInHg, BrakeLine4, elapsedClockSeconds);
                    EqualReservoirPressurePSIorInHg = (float)pressurePSI;
                    BrakeLine4 = (float)epControllerState;
                }
                if (lead.EngineBrakeController != null)
                    BrakeLine3PressurePSI = (float)lead.EngineBrakeController.UpdateEngineBrakePressure(BrakeLine3PressurePSI, elapsedClockSeconds);
                lead.BrakeSystem.PropagateBrakePressure(elapsedClockSeconds);
            }
            else if (TrainType == TrainType.Static)
            {
                // Propagate brake pressure of locomotiveless static consists in the advanced way,
                // to allow proper shunting operations.
                Cars[0].BrakeSystem.PropagateBrakePressure(elapsedClockSeconds);
            }
            else
            {
                // Propagate brake pressure of AI trains simplified
                /// AI trains simplyfied brake control is done by setting their Train.BrakeLine1PressurePSIorInHg,
                /// that is propagated promptly to each car directly.
                foreach (TrainCar car in Cars)
                {
                    car.BrakeSystem.BrakeLine1PressurePSI = car.BrakeSystem.InternalPressure(EqualReservoirPressurePSIorInHg);
                    car.BrakeSystem.BrakeLine2PressurePSI = BrakeLine2PressurePSI;
                    car.BrakeSystem.BrakeLine3PressurePSI = 0;
                }
            }
        }

    }
}
