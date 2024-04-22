﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer.Shared
{
    public enum MessageType
    {
        Unknown = 0,
        Legacy,
        Server,
        Lost,
        Chat,
        Aider,
        Quit,
        TimeCheck,
        TrainEvent,
        Weather,
        Control,
        TrainControl,
        SignalReset,
        Exhaust,
        Move,
        RemoveTrain,
        SwitchStates,
        SwitchChange,
        SignalStates,
        SignalChange,
        LocomotiveInfo,
        LocomotiveChange,
        MovingTable,
        PlayerTrainChange,
        PlayerState,
        TrainState,
        TrainFlip,
        TrainRequest,
        TrainUpdate,
        TrainCouple,
        TrainUncouple,
    }
}
