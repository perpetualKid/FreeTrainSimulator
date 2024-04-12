// COPYRIGHT 2012, 2013 by the Open Rails project.
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

/* MPManager
 * 
 * Contains code to manager multiplayer sessions, especially to hide server/client mode from other code.
 * For example, the Notify method will check if it is server (then broadcast) or client (then send msg to server)
 * but the caller does not need to care.
 * 
 * 
 */

using System;

namespace Orts.Simulation.MultiPlayer
{
    public class ServerChangedEventArgs : EventArgs
    {
        public bool Server { get; }

        public ServerChangedEventArgs(bool server)
        {
            Server = server;
        }
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public double Timestamp { get; }
        public string Message { get; }

        public MessageReceivedEventArgs(double time, string message)
        {
            Timestamp = time;
            Message = message;
        }
    }


}
