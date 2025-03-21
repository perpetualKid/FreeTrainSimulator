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


namespace Orts.Simulation.RollingStocks.SubSystems.PowerTransmissions
{
    public class SeriesMotor : ElectricMotor
    {
        private float armatureResistanceOhms;
        private float fieldResistanceOhms;
        private float shuntResistorOhms;
        private float shuntRatio;

        public float ArmatureResistanceOhms
        {
            set
            {
                armatureResistanceOhms = value;
            }
            get
            {
                return armatureResistanceOhms * (235.0f + TemperatureK) / (235.0f + 20.0f);
            }
        }

        public float ArmatureInductanceH { set; get; }

        public float FieldResistanceOhms
        {
            set
            {
                fieldResistanceOhms = value;
            }
            get
            {
                return fieldResistanceOhms * (235.0f + TemperatureK) / (235.0f + 20.0f);
            }
        }
        public float FieldInductance { set; get; }

        public bool Compensated { set; get; }

        public float ArmatureCurrentA { get; private set; }

        public float FieldCurrentA { get; private set; }

        public float TerminalVoltageV { set; get; }

        public float ArmatureVoltageV => ArmatureCurrentA * ArmatureResistanceOhms + BackEMFVoltage;
        
        public float StartingResistorOhms { set; get; }
        
        public float AdditionalResistanceOhms { set; get; }

        public float ShuntResistorOhms
        {
            set
            {
                if (value == 0.0f)
                {
                    shuntRatio = 0.0f;
                    shuntResistorOhms = 0.0f;
                }
                else
                    shuntResistorOhms = value;
            }
            get
            {
                return shuntResistorOhms == 0.0f ? float.PositiveInfinity : shuntResistorOhms;
            }
        }

        public float ShuntPercent
        {
            set
            {
                shuntRatio = value / 100.0f;
            }
            get
            {
                return shuntResistorOhms == 0.0f ? shuntRatio * 100.0f : 1.0f - shuntResistorOhms / (FieldResistanceOhms + shuntResistorOhms);
            }
        }
        public float BackEMFVoltage { get; private set; }

        public float MotorConstant { set; get; }

        private float fieldWb;

        private readonly float nominalRevolutionsRad;
        private readonly float nominalVoltageV;
        private readonly float nominalCurrentA;

        private float UpdateField()
        {
            float temp = (nominalVoltageV - (ArmatureResistanceOhms + FieldResistanceOhms) * nominalCurrentA) / nominalRevolutionsRad;
            fieldWb = FieldCurrentA <= nominalCurrentA ? temp * FieldCurrentA / nominalCurrentA : temp;
            temp *= (1.0f - shuntRatio);
            return temp;
        }

        public SeriesMotor(float nomCurrentA, float nomVoltageV, float nomRevolutionsRad, Axle axle) : base(axle)

        {
            nominalCurrentA = nomCurrentA;
            nominalVoltageV = nomVoltageV;
            nominalRevolutionsRad = nomRevolutionsRad;
        }

        public override float GetDevelopedTorqueNm(float motorSpeed)
        {
            BackEMFVoltage = motorSpeed * fieldWb;
            return fieldWb * ArmatureCurrentA/* - (frictionTorqueNm * revolutionsRad / NominalRevolutionsRad * revolutionsRad / NominalRevolutionsRad)*/;
        }

        public override void Update(double timeSpan)
        {
            ArmatureCurrentA = shuntResistorOhms == 0.0f
                ? FieldCurrentA / (1.0f - shuntRatio)
                : (FieldResistanceOhms + ShuntResistorOhms) / ShuntResistorOhms * FieldCurrentA;
            if ((BackEMFVoltage * FieldCurrentA) >= 0.0f)
            {
                FieldCurrentA += (float)timeSpan / FieldInductance *
                    (TerminalVoltageV
                        - BackEMFVoltage
                        - ArmatureResistanceOhms * ArmatureCurrentA
                        - FieldResistanceOhms * (1.0f - shuntRatio) * FieldCurrentA
                        - ArmatureCurrentA * StartingResistorOhms
                        - ArmatureCurrentA * AdditionalResistanceOhms
                    //- ((fieldCurrentA == 0.0) ? 0.0 : 2.0)            //voltage drop on brushes
                    );
            }
            else
            {
                FieldCurrentA = 0.0f;
            }

            UpdateField();

            powerLossesW = ArmatureResistanceOhms * ArmatureCurrentA * ArmatureCurrentA +
                           FieldResistanceOhms * FieldCurrentA * FieldCurrentA;

            //temperatureK += timeSpan * ThermalCoeffJ_m2sC * SurfaceM / (SpecificHeatCapacityJ_kg_C * WeightKg)
            //    * ((powerLossesW - CoolingPowerKW) / (SpecificHeatCapacityJ_kg_C * WeightKg) - temperatureK);

            base.Update(timeSpan);
        }
        public override void Reset()
        {
            FieldCurrentA = 0.0f;
            ArmatureCurrentA = 0.0f;
            fieldWb = 0.0f;
            base.Reset();
        }
    }
}
