using Orts.Simulation.RollingStocks.SubSystems.Brakes;

namespace Orts.Simulation.Physics
{
    /// <summary>
    /// Combines all Train brake information/status and control
    /// </summary>
    public class TrainBrakeSystem
    {
        private readonly Train train;

        public float EqualReservoirPressurePSIorInHg { get; internal set; } = 90;      // Pressure in equalising reservoir - set by player locomotive - train brake pipe use this as a reference to set brake pressure levels

        // Class AirSinglePipe etc. use this property for pressure in PSI, 
        // but Class VacuumSinglePipe uses it for vacuum in InHg.
        public float BrakeLine2Pressure { get; internal set; }              // extra line for dual line systems, main reservoir
        public float BrakeLine3Pressure { get; internal set; }              // extra line just in case, engine brake pressure
        public float BrakeLine4Pressure { get; internal set; } = -1;                    // extra line just in case, ep brake control line. -1: release/inactive, 0: hold, 0 < value <=1: apply
        public RetainerSetting RetainerSetting { get; internal set; } = RetainerSetting.Exhaust;
        public int RetainerPercent { get; internal set; } = 100;
        public float TotalTrainBrakePipeVolume { get; internal set; } // Total volume of train brake pipe
        public float TotalTrainBrakeCylinderVolume { get; internal set; } // Total volume of train brake cylinders
        public float TotalTrainBrakeSystemVolume { get; internal set; } // Total volume of train brake system
        public float TotalCurrentTrainBrakeSystemVolume { get; internal set; } // Total current volume of train brake system
        public bool EQEquippedVacLoco { get; internal set; }          // Flag for locomotives fitted with vacuum brakes that have an Equalising reservoir fitted

        internal TrainBrakeSystem(Train train)
        {
            this.train = train;
        }
    }
}
