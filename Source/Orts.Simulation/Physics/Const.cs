namespace Orts.Simulation.Physics
{
    internal static class Const
    {
        public const double DensityAir = 1.247;   // Density of air - use a av value
        public const double SpecificHeatCapacityAir = 1.006; // Specific Heat Capacity of Air
        public const double AirDensityBySpecificHeatCapacity = DensityAir * SpecificHeatCapacityAir; // Product Specific Heat Capacity of Air * Density of Air
        public const double OneAtmospherePSI = 14.5037738;      // Atmospheric Pressure
        public const double BoltzmanConstPipeWpM2 = 0.00000005657302466; // Boltzman's Constant
        public const double EmissivityFactor = 0.79; // Oxidised steel
        public const double PipeHeatTransCoeffWpM2K = 22.0;    // heat transmission coefficient for a steel pipe.
    }
}
