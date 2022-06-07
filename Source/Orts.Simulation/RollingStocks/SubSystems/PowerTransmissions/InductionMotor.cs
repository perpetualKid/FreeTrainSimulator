// COPYRIGHT 2022 by the Open Rails project.
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

using Microsoft.Xna.Framework;

namespace Orts.Simulation.RollingStocks.SubSystems.PowerTransmissions
{
    public class InductionMotor : ElectricMotor
    {
        public float TargetForce { get; set; }
        public float EngineMaxSpeed { get; set; }
        public float OptimalAsyncSpeed { get; private set; } = 1;
        public bool SlipControl { get; set; }
        /// <summary>
        /// Motor drive frequency
        /// </summary>
        public float DriveSpeed { get; private set; }

        /// <summary>
        /// Maximum torque, as determined by throttle setting and force curves
        /// </summary>
        private float requiredTorqueNm;

        public InductionMotor(Axle axle, MSTSLocomotive locomotive) : base(axle)
        {
        }

        public override float GetDevelopedTorqueNm(float motorSpeedRadpS)
        {
            return requiredTorqueNm * MathHelper.Clamp((DriveSpeed - motorSpeedRadpS) / OptimalAsyncSpeed, -1, 1);
        }

        public override void Update(double timeSpan)
        {
            float linToAngFactor = AxleConnected.TransmissionRatio / AxleConnected.WheelRadiusM;
            if (SlipControl)
            {
                if (TargetForce > 0)
                    DriveSpeed = (AxleConnected.TrainSpeedMpS + AxleConnected.WheelSlipThresholdMpS * 0.99f) * linToAngFactor + OptimalAsyncSpeed;
                else if (TargetForce < 0)
                    DriveSpeed = (AxleConnected.TrainSpeedMpS - AxleConnected.WheelSlipThresholdMpS * 0.99f) * linToAngFactor - OptimalAsyncSpeed;
            }
            else
            {
                if (TargetForce > 0)
                    DriveSpeed = EngineMaxSpeed * linToAngFactor + OptimalAsyncSpeed;
                else if (TargetForce < 0)
                    DriveSpeed = -EngineMaxSpeed * linToAngFactor - OptimalAsyncSpeed;
            }
            requiredTorqueNm = Math.Abs(TargetForce) * AxleConnected.WheelRadiusM / AxleConnected.TransmissionRatio;
            base.Update(timeSpan);
        }
    }
}
