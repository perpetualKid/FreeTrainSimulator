// COPYRIGHT 2011 by the Open Rails project.
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

using System;

using FreeTrainSimulator.Common.Calc;

namespace Orts.Simulation.RollingStocks.SubSystems.PowerTransmissions
{
    public class ElectricMotor
    {
        private protected float powerLossesW;

        public float Inertia { get; }

        public float TemperatureK { get; private set; }

        private readonly Integrator temperatureIntegrator = new Integrator();

        public float ThermalCoeff { get; }
        public float SpecificHeatCapacity { get; }
        public float Surface { get; }
        public float Weight { get; }
        public float CoolingPower { set; get; }

        public Axle AxleConnected { get; }

        public ElectricMotor(Axle axle)
        {
            Inertia = 1.0f;
            TemperatureK = 273.0f;
            ThermalCoeff = 50.0f;
            SpecificHeatCapacity = 40.0f;
            Surface = 2.0f;
            Weight = 5.0f;
            AxleConnected = axle;
            AxleConnected.Motor = this;
            AxleConnected.TransmissionRatio = 1;
        }

        public virtual float GetDevelopedTorqueNm(float motorSpeed)
        {
            return 0;
        }

        public virtual void Update(double timeSpan)
        {
            TemperatureK = (float)temperatureIntegrator.Integrate(timeSpan, (temperatureK) => 1.0f/(SpecificHeatCapacity * Weight)*((powerLossesW - CoolingPower) / (ThermalCoeff * Surface) - temperatureK));
        }

        public virtual void Reset()
        {
        }
    }
}
