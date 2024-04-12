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

using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.MultiPlayer
{
    public enum OnlinePlayerStatus 
    { 
        Valid, 
        Quit, 
        Removed 
    };

    public class OnlinePlayer
	{
		public OnlinePlayer(string userName, string consistFile, string pathFile) 
        { 
            Username = userName;
            CreatedTime = Simulator.Instance.GameTime; 
            Consist = consistFile;
            Path = pathFile;
        }

		public string Username { get; }
		public string LeadingLocomotiveID { get; set; } = string.Empty;
		public Train Train { get; set; }
		public string Consist { get; }
		public string Path { get; } //pat and consist files
		public double CreatedTime { get; set; }
		public double QuitTime { get; set; } = -100f;
		public OnlinePlayerStatus Status { get; set; } = OnlinePlayerStatus.Valid;//is this player removed by the dispatcher
        public bool Protected { get; set; } //when in true, will not force this player out, to protect the one that others uses the same name

        // Used to restore
        public OnlinePlayer(BinaryReader inf)
        {
            ArgumentNullException.ThrowIfNull(inf);

            Username = inf.ReadString();
            LeadingLocomotiveID = inf.ReadString();
            int trainNo = inf.ReadInt32();
            Train = Simulator.Instance.Trains.GetTrainByNumber(trainNo);
            Consist = inf.ReadString();
            Path = inf.ReadString();
            CreatedTime = inf.ReadDouble();
            QuitTime = inf.ReadDouble();
            Status = (OnlinePlayerStatus)inf.ReadInt32();
            Protected = inf.ReadBoolean();
            Status = OnlinePlayerStatus.Quit;
            Train.SpeedMpS = 0;
            QuitTime = Simulator.Instance.GameTime; // allow a total of 10 minutes to reenter game.
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

        public void Save(BinaryWriter outf)
        {
            ArgumentNullException.ThrowIfNull(outf);

            outf.Write(Username);
            outf.Write(LeadingLocomotiveID);
            outf.Write(Train.Number);
            outf.Write(Consist);
            outf.Write(Path);
            outf.Write(CreatedTime);
            outf.Write(QuitTime);
            outf.Write((int)Status);
            outf.Write(Protected);
        }
	}
}
