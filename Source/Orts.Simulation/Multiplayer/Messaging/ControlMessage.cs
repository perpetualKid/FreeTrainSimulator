using System;
using System.Diagnostics;

using FreeTrainSimulator.Common;

using MemoryPack;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class ControlMessage : MultiPlayerMessageContent
    {
        public ControlMessageType MessageType { get; set; }
        public string Text { get; set; }
        public string Recipient { get; set; }

        [MemoryPackConstructor]
        public ControlMessage() { }

        public ControlMessage(ControlMessageType messageType, string text)
        {
            MessageType = messageType;
            Text = text;
        }

        public ControlMessage(string recipient, ControlMessageType messageType, string text) :
            this(messageType, text)
        {
            Recipient = recipient;
        }

        public override void HandleMessage()
        {
            if (string.IsNullOrEmpty(Recipient) || multiPlayerManager.UserName.Equals(Recipient, StringComparison.OrdinalIgnoreCase))
            {
                Trace.WriteLine($"{MessageType}: {Text}");
                switch (MessageType)
                {
                    case ControlMessageType.Error:
                        Simulator.Instance.Confirmer?.Message(ConfirmLevel.Error, Text);
                        if (!multiPlayerManager.IsDispatcher)//if is a client, fatal error, will close the connection, and get into single mode
                        {
                            throw new MultiPlayerException();//this is a fatal error, thus the client will be stopped in ClientComm
                        }
                        break;
                    case ControlMessageType.SameNameError:
                        if (!multiPlayerManager.IsDispatcher)//someone with my name but I have been admitted into the game, will ignore it, otherwise, will quit
                        {
                            Trace.WriteLine(MultiPlayerManager.OnlineTrains.Players.Count);

                            if (MultiPlayerManager.OnlineTrains.Players.Count < 1)
                            {
                                Simulator.Instance.Confirmer?.Message(ConfirmLevel.Error, MultiPlayerManager.Catalog.GetString("Name conflicted with people in the game, will play in single mode"));
                                throw new SameNameException();//this is a fatal error, thus the client will be stopped in ClientComm
                            }
                        }
                        break;
                    case ControlMessageType.SwitchWarning:
                        multiPlayerManager.TrySwitch = false;
                        break;
                    case ControlMessageType.SwitchOK:
                        multiPlayerManager.TrySwitch = true;
                        break;
                    case ControlMessageType.OverspeedOK:
                        multiPlayerManager.CheckSpad = false;
                        break;
                    case ControlMessageType.NoOverspeed:
                        multiPlayerManager.CheckSpad = true;
                        break;
                    default:
                        Simulator.Instance.Confirmer?.Message(MessageType
                            switch
                        {
                            ControlMessageType.Warning => ConfirmLevel.Warning,
                            ControlMessageType.Information => ConfirmLevel.Information,
                            _ => ConfirmLevel.None
                        }, Text);
                        break;
                }
            }
        }
    }
}
