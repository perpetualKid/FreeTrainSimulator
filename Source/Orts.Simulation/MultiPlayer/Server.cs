// COPYRIGHT 2012 by the Open Rails project.
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

// #define DEBUG_MULTIPLAYER
// DEBUG flag for debug prints

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Orts.MultiPlayer
{
    public class Server
    {
        public List<OnlinePlayer> Players;
        public string UserName;
        public string Code;
        private ClientComm Connection;
        public int ConnectionMode;

        public void Stop()
        {
            if (Connection != null) Connection.Stop();
        }
        public Server(string s, ClientComm c)
        {
            Players = new List<OnlinePlayer>();
            string[] tmp = s.Split(' ');
            UserName = tmp[0];
            Code = tmp[1];
            Connection = c;
            ConnectionMode = 0;
        }

        public void BroadCast(string msg)
        {
            Connection.Send(msg);
        }
    }
}
