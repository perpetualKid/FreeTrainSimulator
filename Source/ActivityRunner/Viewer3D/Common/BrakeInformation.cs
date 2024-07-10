using System.ComponentModel;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;

using Microsoft.Xna.Framework;

using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS;

namespace Orts.ActivityRunner.Viewer3D.Common
{
    internal sealed class BrakeInformation : DetailInfoBase
    {
        private enum BrakeDetailColumn
        {
            [Description("Car Id")] Car,
            [Description("Type")] BrakeType,
            [Description("BC")] BC,
            [Description("BP")] BP,
            [Description("Srv Pipe")] SrvPipe,
            [Description("Str Pipe")] StrPipe,
            [Description("Vacuum Res")] VacuumReservoir,
            [Description("Aux Res")] AuxReservoir,
            [Description("Emergency Res")] EmergencyReservoir,
            [Description("Main Res Pipe")] MainReservoirPipe,
            [Description("Retainer Valve")] RetainerValve,
            [Description("Triple Valve")] TripleValve,
            [Description("Handbrake")] Handbrake,
            [Description("Brakehose Connected")] BrakehoseConnected,
            [Description("Angle Cock")] AngleCock,
            [Description("BleedOff Valve")] BleedOff,
        }

        private readonly EnumArray<DetailInfoBase, BrakeDetailColumn> columns = new EnumArray<DetailInfoBase, BrakeDetailColumn>(() => new DetailInfoBase());
        private Train train;
        private int numberCars;
        private BrakeDetailColumn[] brakeDetails;

        private static readonly BrakeDetailColumn[] vacSingleNoAutoBrake = new BrakeDetailColumn[]
        {
            BrakeDetailColumn.Car,
            BrakeDetailColumn.BrakeType,
            BrakeDetailColumn.BC,
            BrakeDetailColumn.BP,
            BrakeDetailColumn.Handbrake,
            BrakeDetailColumn.BrakehoseConnected,
            BrakeDetailColumn.AngleCock
        };
        private static readonly BrakeDetailColumn[] vacSingleAutoBrake = new BrakeDetailColumn[]
        {
            BrakeDetailColumn.Car,
            BrakeDetailColumn.BrakeType,
            BrakeDetailColumn.BC,
            BrakeDetailColumn.BP,
            BrakeDetailColumn.VacuumReservoir,
            BrakeDetailColumn.Handbrake,
            BrakeDetailColumn.BrakehoseConnected,
            BrakeDetailColumn.AngleCock
        };
        private static readonly BrakeDetailColumn[] manualBrake = new BrakeDetailColumn[]
        {
            BrakeDetailColumn.Car,
            BrakeDetailColumn.BrakeType,
            BrakeDetailColumn.BP,
            BrakeDetailColumn.Handbrake
        };
        private static readonly BrakeDetailColumn[] smeBrake = new BrakeDetailColumn[]
        {
            BrakeDetailColumn.Car,
            BrakeDetailColumn.BrakeType,
            BrakeDetailColumn.BC,
            BrakeDetailColumn.SrvPipe,
            BrakeDetailColumn.AuxReservoir,
            BrakeDetailColumn.EmergencyReservoir,
            BrakeDetailColumn.StrPipe,
            BrakeDetailColumn.RetainerValve,
            BrakeDetailColumn.TripleValve,
            BrakeDetailColumn.Handbrake,
            BrakeDetailColumn.BrakehoseConnected,
            BrakeDetailColumn.AngleCock,
            BrakeDetailColumn.BleedOff,
        };
        private static readonly BrakeDetailColumn[] airBraked = new BrakeDetailColumn[]
        {
            BrakeDetailColumn.Car,
            BrakeDetailColumn.BrakeType,
            BrakeDetailColumn.BC,
            BrakeDetailColumn.BP,
            BrakeDetailColumn.AuxReservoir,
            BrakeDetailColumn.EmergencyReservoir,
            BrakeDetailColumn.MainReservoirPipe,
            BrakeDetailColumn.RetainerValve,
            BrakeDetailColumn.TripleValve,
            BrakeDetailColumn.Handbrake,
            BrakeDetailColumn.BrakehoseConnected,
            BrakeDetailColumn.AngleCock,
            BrakeDetailColumn.BleedOff,
        };

        public BrakeInformation() : base(true)
        {
            columns[0] = this;
        }

        public override void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                if (train != (train = Simulator.Instance.PlayerLocomotive.Train) | numberCars != (numberCars = train.Cars.Count))
                {
                    AddHeader();
                }
                for (int i = 0; i < train.Cars.Count; i++)
                {
                    TrainCar car = train.Cars[i];
                    string key = $"{i + 1}";
                    foreach (BrakeDetailColumn detailColumn in brakeDetails)
                    {
                        columns[detailColumn][key] = car.BrakeSystem.BrakeInfo.DetailInfo[detailColumn.ToString()];
                    }
                }
                base.Update(gameTime);
            }
        }

        private void AddHeader()
        {
            MultiColumnCount = 0;
            foreach (DetailInfoBase item in columns)
            {
                NextColumn = null;
                item.Clear();
            }

            brakeDetails = train.LeadLocomotive.BrakeSystem switch
            {
                VacuumSinglePipe => train.LeadLocomotive.NonAutoBrakePresent ? vacSingleNoAutoBrake : vacSingleAutoBrake,
                ManualBraking => manualBrake,
                SMEBrakeSystem => smeBrake,
                _ => airBraked,
            };
            for (int i = 0; i < brakeDetails.Length - 1; i++)
            {
                columns[brakeDetails[i]].NextColumn = columns[brakeDetails[i + 1]];
            }
            MultiColumnCount = brakeDetails.Length;

            foreach (BrakeDetailColumn column in brakeDetails)
            {
                columns[column]["#"] = column.GetLocalizedDescription();
            }
        }

    }
}
