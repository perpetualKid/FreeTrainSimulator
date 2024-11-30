using System;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Models.Imported.State;

namespace Orts.Simulation.Physics
{
    /// <summary>
    /// Combines all Train brake information/status and control
    /// </summary>
    public class TrainBrakeSystem : ISaveStateApi<TrainBrakeSaveState>
    {
        public double EqualReservoirPressurePSIorInHg { get; internal set; } = 90;      // Pressure in equalising reservoir - set by player locomotive - train brake pipe use this as a reference to set brake pressure levels

        // Class AirSinglePipe etc. use this property for pressure in PSI, 
        // but Class VacuumSinglePipe uses it for vacuum in InHg.
        public double BrakeLine2Pressure { get; internal set; }              // extra line for dual line systems, main reservoir
        public double BrakeLine3Pressure { get; internal set; }              // extra line just in case, engine brake pressure
        public double BrakeLine4Pressure { get; internal set; } = -1;                    // extra line just in case, ep brake control line. -1: release/inactive, 0: hold, 0 < value <=1: apply
        public RetainerSetting RetainerSetting { get; internal set; } = RetainerSetting.Exhaust;
        public int RetainerPercent { get; internal set; } = 100;
        public double TotalTrainBrakePipeVolume { get; internal set; } // Total volume of train brake pipe
        public double TotalTrainBrakeCylinderVolume { get; internal set; } // Total volume of train brake cylinders
        public double TotalTrainBrakeSystemVolume { get; internal set; } // Total volume of train brake system
        public double TotalCurrentTrainBrakeSystemVolume { get; internal set; } // Total current volume of train brake system
        public bool VacuumBrakeEqualizerLocomotive { get; internal set; }          // Flag for locomotives fitted with vacuum brakes that have an Equalising reservoir fitted

        internal TrainBrakeSystem()
        {
        }

        public ValueTask<TrainBrakeSaveState> Snapshot()
        {
            return ValueTask.FromResult(new TrainBrakeSaveState()
            {
                EqualReservoirPressure = EqualReservoirPressurePSIorInHg,
                BrakeLine2Pressure = BrakeLine2Pressure,
                BrakeLine3Pressure = BrakeLine3Pressure,
                BrakeLine4Pressure = BrakeLine4Pressure,
                RetainerSetting = RetainerSetting,
                RetainerPercent = RetainerPercent,
                TrainBrakePipeVolume = TotalTrainBrakePipeVolume,
                TrainBrakeCylinderVolume = TotalTrainBrakeCylinderVolume,
                CurrentTrainBrakeSystemVolume = TotalCurrentTrainBrakeSystemVolume,
                TrainBrakeSystemVolume = TotalTrainBrakeSystemVolume,
                VacuumBrakeEqualizerLocomotive = VacuumBrakeEqualizerLocomotive,
            });
        }

        public ValueTask Restore(TrainBrakeSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            EqualReservoirPressurePSIorInHg = saveState.EqualReservoirPressure;
            BrakeLine2Pressure = saveState.BrakeLine2Pressure;
            BrakeLine3Pressure = saveState.BrakeLine3Pressure;
            BrakeLine4Pressure = saveState.BrakeLine4Pressure;
            RetainerSetting = saveState.RetainerSetting;
            RetainerPercent = saveState.RetainerPercent;
            TotalTrainBrakePipeVolume = saveState.TrainBrakePipeVolume;
            TotalTrainBrakeCylinderVolume = saveState.TrainBrakeCylinderVolume;
            TotalCurrentTrainBrakeSystemVolume = saveState.CurrentTrainBrakeSystemVolume;
            TotalTrainBrakeSystemVolume = saveState.TrainBrakeSystemVolume;
            VacuumBrakeEqualizerLocomotive = saveState.VacuumBrakeEqualizerLocomotive;

            return ValueTask.CompletedTask;
        }
    }
}
