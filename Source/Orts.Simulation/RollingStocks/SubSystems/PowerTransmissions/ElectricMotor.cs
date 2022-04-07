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

using Orts.Common.Calc;

namespace Orts.Simulation.RollingStocks.SubSystems.PowerTransmissions
{
    public class ElectricMotor
    {
        private protected float powerLossesW;
        private float frictionTorqueNm;
        private float inertiaKgm2;
        private float axleDiameterM;
        private float transmissionRatio;

        public float DevelopedTorqueNm { get; set; }

        public float LoadTorqueNm { get; set; }

        public float FrictionTorqueNm 
        { 
            get => frictionTorqueNm; 
            set { frictionTorqueNm = Math.Abs(value); } 
        }

        public float InertiaKgm2
        {
            get => inertiaKgm2;
            set
            {
                if (value <= 0.0)
                    throw new NotSupportedException("Inertia must be greater than 0");
                inertiaKgm2 = value;
            }
        }

        public float RevolutionsRad { get; set; }

        public float TemperatureK { get; private set; }

        private readonly Integrator tempIntegrator = new Integrator();

        public float ThermalCoeffJ_m2sC { set; get; }
        public float SpecificHeatCapacityJ_kg_C { set; get; }
        public float SurfaceM { set; get; }
        public float WeightKg { set; get; }
        public float CoolingPowerW { set; get; }

        public float TransmissionRatio
        {
            get => transmissionRatio;
            set
            {
                if (value <= 0.0)
                    throw new NotSupportedException("Transmission ratio must be greater than zero");
                transmissionRatio = value;
            }
        }

        public float AxleDiameterM
        {
            set
            {
                if (value <= 0.0)
                    throw new NotSupportedException("Axle diameter must be greater than zero");
                axleDiameterM = value;
            }
            get => axleDiameterM;
        }

        public Axle AxleConnected { get; set; }

        public ElectricMotor()
        {
            DevelopedTorqueNm = 0.0f;
            LoadTorqueNm = 0.0f;
            InertiaKgm2 = 1.0f;
            RevolutionsRad = 0.0f;
            AxleDiameterM = 1.0f;
            TransmissionRatio = 1.0f;
            TemperatureK = 0.0f;
            ThermalCoeffJ_m2sC = 50.0f;
            SpecificHeatCapacityJ_kg_C = 40.0f;
            SurfaceM = 2.0f;
            WeightKg = 5.0f;
        }

        public virtual void Update(double timeSpan)
        {
            //revolutionsRad += timeSpan / inertiaKgm2 * (developedTorqueNm + loadTorqueNm + (revolutionsRad == 0.0 ? 0.0 : frictionTorqueNm));
            //if (revolutionsRad < 0.0)
            //    revolutionsRad = 0.0;
            TemperatureK = (float)tempIntegrator.Integrate(timeSpan, (temperatureK) => 1.0f/(SpecificHeatCapacityJ_kg_C * WeightKg)*((powerLossesW - CoolingPowerW) / (ThermalCoeffJ_m2sC * SurfaceM) - temperatureK));

        }

        public virtual void Reset()
        {
            RevolutionsRad = 0.0f;
        }
    }
}
