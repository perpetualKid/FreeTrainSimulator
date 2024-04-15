using System;
using System.Collections.Generic;
using System.Linq;

using MemoryPack;

namespace Orts.Simulation.MultiPlayer.Messaging
{
    [MemoryPackable]
    public partial class ChatMessage : MultiPlayerMessageContent
    {
        public string Text { get; set; }
        public IEnumerable<string> Receipients { get; private set; }

        [MemoryPackConstructor]
        private ChatMessage() { }

        public ChatMessage(string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message, nameof(message));

            int index = message.IndexOf(':', StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                Receipients = message[..index].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (++index <= message.Length)
                    Text = message[index..];
            }
            else
            {
                Text = message;
            }
        }

        public override void HandleMessage()
        {
            if (Receipients == null || !Receipients.Any() || Receipients.Contains(multiPlayerManager.UserName))
            {
                Simulator.Instance.Confirmer?.Message(MultiPlayerManager.Catalog.GetString(" From {0}: {1}", User, Text));
            }
        }
    }
}
