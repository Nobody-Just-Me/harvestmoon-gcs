using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MavLinkNet
{

    public abstract class MavLinkGenericTransport : IDisposable
    {
        public byte MavlinkSystemId = 200;
        public byte MavlinkComponentId = 1;
        public byte PacketSignalByte = byte.MinValue;
        public MavLinkState UavState = new MavLinkState();

        public event PacketReceivedDelegate OnPacketReceived;

        public event OtherPacketReceivedDelegate OtherPacketReceived;

        public event EventHandler OnReceptionEnded;

        public abstract void Initialize();
        public abstract void Dispose();
        public abstract void SendMessage(UasMessage msg);

        public void InitializeProtocolVersion(WireProtocolVersion wireProtocolVersion)
        {
            switch (wireProtocolVersion)
            {
                case WireProtocolVersion.v10:
                    MavLinkAsyncWalker.PacketSignalByte = 0xFE;
                    break;
                case WireProtocolVersion.v20:
                    MavLinkAsyncWalker.PacketSignalByte = 0xFD;
                    break;
                default:
                    break;
            }
        }

        // __ MavLink events __________________________________________________

        protected void HandlePacketReceived(object sender, MavLinkPacketBase e)
        {
            Console.WriteLine($"[MavLinkGenericTransport] HandlePacketReceived called! MessageId={e.MessageId}, Message={e.Message?.GetType().Name}");
            
            if (OnPacketReceived != null)
            {
                Console.WriteLine($"[MavLinkGenericTransport] Invoking OnPacketReceived event...");
                OnPacketReceived(sender, e);
                Console.WriteLine($"[MavLinkGenericTransport] OnPacketReceived event invoked successfully");
            }
            else
            {
                Console.WriteLine($"[MavLinkGenericTransport] WARNING: OnPacketReceived has no subscribers!");
            }
        }

        protected void HandleReceptionEnded(object sender)
        {
            if (OnReceptionEnded != null) OnReceptionEnded(sender, EventArgs.Empty);
        }

        protected void HandleOtherPacketReceived(object sender, byte[] Buffer)
        {
            OtherPacketReceived(this, Buffer);
        }

        public virtual void ProcessReceivedBytes(byte[] buffer, int offset, int count)
        {
        }
    }
}
