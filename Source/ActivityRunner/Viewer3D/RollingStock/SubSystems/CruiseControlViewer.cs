// COPYRIGHT 2015 by the Open Rails project.
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

using FreeTrainSimulator.Common.Input;

using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.SubSystems
{
    public class CruiseControlViewer
    {
        private readonly MSTSLocomotive locomotive;
        private readonly CruiseControl cruiseControl;
        private readonly Viewer viewer;

        public CruiseControlViewer(Viewer viewer, MSTSLocomotive locomotive)
        {
            ArgumentNullException.ThrowIfNull(locomotive);
            this.viewer = viewer;
            this.locomotive = locomotive;
            this.cruiseControl = locomotive.CruiseControl;
        }

        public void RegisterUserCommandHandling()
        {
            viewer.UserCommandController.AddEvent(UserCommand.ControlSpeedRegulatorMaxAccelerationDecrease, KeyEventType.KeyPressed, cruiseControl.SpeedRegulatorMaxForceStartDecrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlSpeedRegulatorMaxAccelerationDecrease, KeyEventType.KeyReleased, cruiseControl.SpeedRegulatorMaxForceStopDecrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlSpeedRegulatorMaxAccelerationIncrease, KeyEventType.KeyPressed, cruiseControl.SpeedRegulatorMaxForceStartIncrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlSpeedRegulatorMaxAccelerationIncrease, KeyEventType.KeyReleased, cruiseControl.SpeedRegulatorMaxForceStopIncrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlSpeedRegulatorModeDecrease, KeyEventType.KeyPressed, cruiseControl.SpeedRegulatorModeDecrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlSpeedRegulatorModeIncrease, KeyEventType.KeyPressed, cruiseControl.SpeedRegulatorModeIncrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlSpeedRegulatorSelectedSpeedDecrease, KeyEventType.KeyPressed, cruiseControl.SpeedRegulatorSelectedSpeedStartDecrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlSpeedRegulatorSelectedSpeedDecrease, KeyEventType.KeyReleased, cruiseControl.SpeedRegulatorSelectedSpeedStopDecrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlSpeedRegulatorSelectedSpeedIncrease, KeyEventType.KeyPressed, cruiseControl.SpeedRegulatorSelectedSpeedStartIncrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlSpeedRegulatorSelectedSpeedIncrease, KeyEventType.KeyReleased, cruiseControl.SpeedRegulatorSelectedSpeedStopIncrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlNumberOfAxlesDecrease, KeyEventType.KeyPressed, cruiseControl.NumberOfAxlesDecrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlNumberOfAxlesDecrease, KeyEventType.KeyPressed, cruiseControl.NumerOfAxlesIncrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlRestrictedSpeedZoneActive, KeyEventType.KeyPressed, cruiseControl.ActivateRestrictedSpeedZone, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlCruiseControlModeIncrease, KeyEventType.KeyPressed, cruiseControl.SpeedSelectorModeStartIncrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlCruiseControlModeIncrease, KeyEventType.KeyReleased, cruiseControl.SpeedSelectorModeStopIncrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlCruiseControlModeDecrease, KeyEventType.KeyPressed, cruiseControl.SpeedSelectorModeDecrease, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlTrainTypePaxCargo, KeyEventType.KeyReleased, locomotive.ChangeTrainTypePaxCargo, true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlSpeedRegulatorSelectedSpeedToZero, KeyEventType.KeyReleased, ControlSpeedRegulatorSelectedSpeedToZero, true);
        }

        public void UnregisterUserCommandHandling()
        {
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlSpeedRegulatorMaxAccelerationDecrease, KeyEventType.KeyPressed, cruiseControl.SpeedRegulatorMaxForceStartDecrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlSpeedRegulatorMaxAccelerationDecrease, KeyEventType.KeyReleased, cruiseControl.SpeedRegulatorMaxForceStopDecrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlSpeedRegulatorMaxAccelerationIncrease, KeyEventType.KeyPressed, cruiseControl.SpeedRegulatorMaxForceStartIncrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlSpeedRegulatorMaxAccelerationIncrease, KeyEventType.KeyReleased, cruiseControl.SpeedRegulatorMaxForceStopIncrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlSpeedRegulatorModeDecrease, KeyEventType.KeyPressed, cruiseControl.SpeedRegulatorModeDecrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlSpeedRegulatorModeIncrease, KeyEventType.KeyPressed, cruiseControl.SpeedRegulatorModeIncrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlSpeedRegulatorSelectedSpeedDecrease, KeyEventType.KeyPressed, cruiseControl.SpeedRegulatorSelectedSpeedStartDecrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlSpeedRegulatorSelectedSpeedDecrease, KeyEventType.KeyReleased, cruiseControl.SpeedRegulatorSelectedSpeedStopDecrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlSpeedRegulatorSelectedSpeedIncrease, KeyEventType.KeyPressed, cruiseControl.SpeedRegulatorSelectedSpeedStartIncrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlSpeedRegulatorSelectedSpeedIncrease, KeyEventType.KeyReleased, cruiseControl.SpeedRegulatorSelectedSpeedStopIncrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlNumberOfAxlesDecrease, KeyEventType.KeyPressed, cruiseControl.NumberOfAxlesDecrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlNumberOfAxlesDecrease, KeyEventType.KeyPressed, cruiseControl.NumerOfAxlesIncrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlRestrictedSpeedZoneActive, KeyEventType.KeyPressed, cruiseControl.ActivateRestrictedSpeedZone);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlCruiseControlModeIncrease, KeyEventType.KeyPressed, cruiseControl.SpeedSelectorModeStartIncrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlCruiseControlModeIncrease, KeyEventType.KeyReleased, cruiseControl.SpeedSelectorModeStopIncrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlCruiseControlModeDecrease, KeyEventType.KeyPressed, cruiseControl.SpeedSelectorModeDecrease);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlTrainTypePaxCargo, KeyEventType.KeyReleased, locomotive.ChangeTrainTypePaxCargo);
            viewer.UserCommandController.RemoveEvent(UserCommand.ControlSpeedRegulatorSelectedSpeedToZero, KeyEventType.KeyReleased, ControlSpeedRegulatorSelectedSpeedToZero);
        }

        private void ControlSpeedRegulatorSelectedSpeedToZero() => cruiseControl.SetSpeed(0);
    }
}
