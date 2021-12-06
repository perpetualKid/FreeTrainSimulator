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

using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.MultiPlayer
{
    public class OnlinePlayer
	{
		public Decoder decoder;
		public OnlinePlayer() 
        { 
            decoder = new Decoder(); 
            CreatedTime = Simulator.Instance.GameTime; 
            url = "NA";
        }

		public TcpClient Client;
		public string Username = "";
		public string LeadingLocomotiveID = "";
		public Train Train;
		public string con;
		public string path; //pat and consist files
		public Thread thread;
		public double CreatedTime;
		private object lockObj = new object();
		public string url = ""; //avatar location
		public double quitTime = -100f;
		public enum Status {Valid, Quit, Removed};
		public Status status = Status.Valid;//is this player removed by the dispatcher
        public bool protect; //when in true, will not force this player out, to protect the one that others uses the same name

        // Used to restore
        public OnlinePlayer(BinaryReader inf)
        {
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));

            Username = inf.ReadString();
            LeadingLocomotiveID = inf.ReadString();
            int trainNo = inf.ReadInt32();
            Train = Simulator.Instance.Trains.GetTrainByNumber(trainNo);
            con = inf.ReadString();
            path = inf.ReadString();
            CreatedTime = inf.ReadDouble();
            url = inf.ReadString();
            quitTime = inf.ReadDouble();
            status = (Status)inf.ReadInt32();
            protect = inf.ReadBoolean();
            status = Status.Quit;
            Train.SpeedMpS = 0;
            quitTime = Simulator.Instance.GameTime; // allow a total of 10 minutes to reenter game.
            for (int i = 0; i < Train.Cars.Count; i++)
            {
                TrainCar car = Train.Cars[i];
                if (car is MSTSLocomotive && MultiPlayerManager.IsServer())
                    MultiPlayerManager.Instance().AddOrRemoveLocomotive(Username, Train.Number, i, true);
            }

            if (!MultiPlayerManager.Instance().lostPlayer.ContainsKey(Username))
            {
                MultiPlayerManager.Instance().lostPlayer.Add(Username, this);
                MultiPlayerManager.Instance().AddRemovedPlayer(this);//add this player to be removed
            }
        }

		public void Send(string msg)
		{
			if (msg == null) return;
			try
			{
				NetworkStream clientStream = Client.GetStream();

				lock (lockObj)//lock the buffer in case two threads want to write at once
				{
					byte[] buffer = Encoding.Unicode.GetBytes(msg);//encoder.GetBytes(msg);
					clientStream.Write(buffer, 0, buffer.Length);
					clientStream.Flush();
				}
			}
			catch
			{
			}
		}

        public void Save(BinaryWriter outf)
        {
            if (null == outf)
                throw new ArgumentNullException(nameof(outf));

            outf.Write(Username);
            outf.Write(LeadingLocomotiveID);
            outf.Write(Train.Number);
            outf.Write(con);
            outf.Write(path);
            outf.Write(CreatedTime);
            outf.Write(url);
            outf.Write(quitTime);
            outf.Write((int)status);
            outf.Write(protect);
        }
	}
}
