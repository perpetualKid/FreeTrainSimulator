// COPYRIGHT 2013, 2014 by the Open Rails project.
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

/// This module ...
/// 
/// Author: Stéfan Paitoni
/// Updates : 
/// 

using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Orts.Formats.OR;
using Orts.Common;


namespace Orts.ActivityEditor.Base.Formats
{
    #region Activity_def
    [Flags]
    enum EventType
    {
        ACTIVITY_EVENT = 0,
        ACTIVITY_START = 1,
        ACTIVITY_STOP = 2,
        ACTIVITY_WAIT = 3
    };
#endregion

    #region PathEventItem


    public class PathEventItem : GlobalItem
    {
        [JsonProperty("NameEvent")]
        public string NameEvent { get; set; }
        [JsonProperty("NameVisible")]
        public bool NameVisible;
        [JsonProperty("TypeEvent")]
        public int TypeEvent;


        public PathEventItem(TypeEditor interfaceType)
        {
            typeItem = (int)EventType.ACTIVITY_EVENT;
            alignEdition(interfaceType, null);
        }

        public override void alignEdition(TypeEditor interfaceType, GlobalItem ownParent)
        {
            if (interfaceType == TypeEditor.ACTIVITY)
            {
                setMovable();
                setLineSnap();
                setEditable();
                setActEdit();
            }
        }

        public void SetName(int info)
        {
        }

        public virtual Icon GetIcon() { return null; }
    }

    public class ActStartItem : PathEventItem
    {
        private readonly Icon StartIcon;
        public ActStartItem(TypeEditor interfaceType)
            : base(interfaceType)
        {
            Stream st;
            Assembly a = Assembly.GetExecutingAssembly();

            typeItem = (int)EventType.ACTIVITY_START;
            st = a.GetManifestResourceStream("LibAE.Icon.Start.ico");
            StartIcon = new System.Drawing.Icon(st);
 
        }

        public void SetNameStart(int info)
        {
            NameEvent = "start" + info;
        }

        public override void ConfigCoord(in MSTSCoord coord)
        {
            base.ConfigCoord(in coord);
            typeItem = (int)TypeItem.ACTIVITY_ITEM;
            NameVisible = false;
        }

        public override void Update(in MSTSCoord coord)
        {
            base.ConfigCoord(in coord);
        }

        public override Icon GetIcon() { return StartIcon; }

    }

    public class ActStopItem : PathEventItem
    {
        private readonly Icon StopIcon;

        public ActStopItem(TypeEditor interfaceType)
            : base(interfaceType)
        {
            Stream st;
            Assembly a = Assembly.GetExecutingAssembly();

            typeItem = (int)EventType.ACTIVITY_STOP;
            st = a.GetManifestResourceStream("LibAE.Icon.Stop.ico");
            StopIcon = new System.Drawing.Icon(st);

        }

        public void SetNameStop(int info)
        {
            NameEvent = "stop" + info;
        }

        public override void ConfigCoord(in MSTSCoord coord)
        {
            base.ConfigCoord(in coord);
            typeItem = (int)TypeItem.ACTIVITY_ITEM;
            NameVisible = false;
        }

        public override void Update(in MSTSCoord coord)
        {
            base.ConfigCoord(in coord);
        }

        public override Icon GetIcon() { return StopIcon; }

    }

    public class ActWaitItem : PathEventItem
    {
        private readonly Icon WaitIcon;

        public ActWaitItem(TypeEditor interfaceType)
            : base(interfaceType)
        {
            Stream st;
            Assembly a = Assembly.GetExecutingAssembly();

            typeItem = (int)EventType.ACTIVITY_WAIT;
            st = a.GetManifestResourceStream("LibAE.Icon.Wait.ico");
            WaitIcon = new System.Drawing.Icon(st);

        }

        public void SetNameWait(int info)
        {
            NameEvent = "wait" + info;
        }

        public override void ConfigCoord(in MSTSCoord coord)
        {
            base.ConfigCoord(in coord);
            typeItem = (int)TypeItem.ACTIVITY_ITEM;
            NameVisible = false;
        }

        public override void Update(in MSTSCoord coord)
        {
            base.ConfigCoord(in coord);
        }

        public override Icon GetIcon() { return WaitIcon; }

    }


    #endregion


}