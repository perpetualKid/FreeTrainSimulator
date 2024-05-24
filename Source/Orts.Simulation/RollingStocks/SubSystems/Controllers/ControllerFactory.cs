// COPYRIGHT 2010 by the Open Rails project.
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
using System.IO;
using System.Threading.Tasks;

using Orts.Common;
using Orts.Models.State;

namespace Orts.Simulation.RollingStocks.SubSystems.Controllers
{
    public static class ControllerFactory
    {
        public static void Save(IController controller, BinaryWriter outf)
        {
            outf.Write(controller != null);

            if (controller != null)
                controller.Save(outf);
        }

        public static async Task Restore(IController controller, ControllerSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(controller, nameof(controller));
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            switch (saveState.ControllerType)
            {
                case ControllerType.NotchController:
                    await (controller as MSTSNotchController).Restore(saveState).ConfigureAwait(false);
                    break;
                case ControllerType.BrakeController:
                    //(controller as ScriptedBrakeController).Restore()
                    break;
                default:
                    throw new InvalidDataException("Invalid controller type to restore");
            }
        }

		public static void Restore(IController controller, BinaryReader inf)
        {
            if (!inf.ReadBoolean())
                return;

            switch ((ControllerType)inf.ReadInt32())
            {                
                case ControllerType.NotchController:
                    if (controller == null)
                        controller = new MSTSNotchController();
//                    ((MSTSNotchController)controller).Restore(inf);
                    break;

                case ControllerType.BrakeController:
                    ((ScriptedBrakeController)controller).Restore(inf);
                    break;

                default:
                    throw new InvalidDataException("Invalid controller type");
            }
        }
    }
}
