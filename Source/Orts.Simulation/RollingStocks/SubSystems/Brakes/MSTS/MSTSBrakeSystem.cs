// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

using Orts.Formats.Msts;
using Orts.Formats.Msts.Parsers;

namespace Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS
{
    public abstract class MSTSBrakeSystem : BrakeSystem
    {
        protected MSTSBrakeSystem(TrainCar car) : base(car)
        {
        }

        public static BrakeSystem Create(BrakeSystemType brakeSystem, TrainCar car)
        {
            return brakeSystem switch
            {
                BrakeSystemType.ManualBraking => new ManualBraking(car),
                BrakeSystemType.StraightVacuumSinglePipe => new StraightVacuumSinglePipe(car),
                BrakeSystemType.VacuumTwinPipe => new VacuumSinglePipe(car),
                BrakeSystemType.VacuumSinglePipe => new VacuumSinglePipe(car),
                BrakeSystemType.AirTwinPipe => new AirTwinPipe(car),
                BrakeSystemType.AirSinglePipe => new AirSinglePipe(car),
                BrakeSystemType.Ecp => new EPBrakeSystem(car),
                BrakeSystemType.Ep => new EPBrakeSystem(car),
                BrakeSystemType.Sme => new SMEBrakeSystem(car),
                BrakeSystemType.AirPiped => new SingleTransferPipe(car),
                BrakeSystemType.VacuumPiped => new SingleTransferPipe(car),
                _ => new SingleTransferPipe(car),
            };
        }

        public abstract void Parse(string lowercasetoken, STFReader stf);

        public abstract void Update(double elapsedClockSeconds);

        public abstract void InitializeFrom(BrakeSystem source);
    }
}
