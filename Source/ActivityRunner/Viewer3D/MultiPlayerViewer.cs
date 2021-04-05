// COPYRIGHT 2012, 2013, 2014, 2015 by the Open Rails project.
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
using System.Linq;

using Orts.ActivityRunner.Viewer3D.Processes;
using Orts.Common.Input;
using Orts.MultiPlayer;
using Orts.View.Xna;

namespace Orts.ActivityRunner.Viewer3D
{
    public static class MultiPlayerViewer
    {
        //count how many times a key has been stroked, thus know if the panto should be up or down, etc. for example, stroke 11 times means up, thus send event with id 1
        private static int panto1;
        private static int panto2;
        private static int panto3;
        private static int panto4;
        private static int wiper;
        private static int headlight;
        private static int doorLeft;
        private static int doorRight;
        private static int mirrors;

        public static void RegisterInputEvents(Viewer viewer)
        {
            if (null == viewer)
                throw new ArgumentNullException(nameof(viewer));

            //In Multiplayer, I maybe the helper, but I can request to be the controller
            // Horn and bell are managed by UpdateHornAndBell in MSTSLocomotive.cs
            viewer.UserCommandController.AddEvent(UserCommand.GameRequestControl, KeyEventType.KeyPressed, MPManager.RequestControl);
            viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph1, KeyEventType.KeyPressed, () => MPManager.Notify(new MSGEvent(MPManager.GetUserName(), "PANTO1", (++panto1) % 2).ToString()));
            viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph2, KeyEventType.KeyPressed, () => MPManager.Notify(new MSGEvent(MPManager.GetUserName(), "PANTO2", (++panto2) % 2).ToString()));
            viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph3, KeyEventType.KeyPressed, () => MPManager.Notify(new MSGEvent(MPManager.GetUserName(), "PANTO3", (++panto3) % 2).ToString()));
            viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph4, KeyEventType.KeyPressed, () => MPManager.Notify(new MSGEvent(MPManager.GetUserName(), "PANTO4", (++panto4) % 2).ToString()));
            viewer.UserCommandController.AddEvent(UserCommand.ControlWiper, KeyEventType.KeyPressed, () => MPManager.Notify(new MSGEvent(MPManager.GetUserName(), "WIPER", (++wiper) % 2).ToString()));
            viewer.UserCommandController.AddEvent(UserCommand.ControlDoorLeft, KeyEventType.KeyPressed, () => MPManager.Notify(new MSGEvent(MPManager.GetUserName(), "DOORL", (++doorLeft) % 2).ToString()));
            viewer.UserCommandController.AddEvent(UserCommand.ControlDoorRight, KeyEventType.KeyPressed, () => MPManager.Notify(new MSGEvent(MPManager.GetUserName(), "DOORR", (++doorRight) % 2).ToString()));
            viewer.UserCommandController.AddEvent(UserCommand.ControlMirror, KeyEventType.KeyPressed, () => MPManager.Notify(new MSGEvent(MPManager.GetUserName(), "MIRRORS", (++mirrors) % 2).ToString()));

            viewer.UserCommandController.AddEvent(UserCommand.ControlHeadlightIncrease, KeyEventType.KeyPressed, () =>
            {
                headlight++;
                if (headlight >= 3)
                    headlight = 2;
                MPManager.Notify(new MSGEvent(MPManager.GetUserName(), "HEADLIGHT", headlight).ToString());
            });

            viewer.UserCommandController.AddEvent(UserCommand.ControlHeadlightDecrease, KeyEventType.KeyPressed, () =>
            {
                headlight--;
                if (headlight < 0)
                    headlight = 0;
                MPManager.Notify(new MSGEvent(MPManager.GetUserName(), "HEADLIGHT", headlight).ToString());
            });
        }
    }
}
