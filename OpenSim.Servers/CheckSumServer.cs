using System;
using System.Text;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Console;


namespace OpenSim.Servers
{
    public class CheckSumServer : UDPServerBase
    {
        protected ConsoleBase m_console;

        public CheckSumServer(int port, ConsoleBase console)
        {
            listenPort = port;
            this.m_console = console;
        }

        protected override void OnReceivedData(IAsyncResult result)
        {
            ipeSender = new IPEndPoint(IPAddress.Any, 0);
            epSender = (EndPoint)ipeSender;
            Packet packet = null;
            int numBytes = Server.EndReceiveFrom(result, ref epSender);
            int packetEnd = numBytes - 1;

            packet = Packet.BuildPacket(RecvBuffer, ref packetEnd, ZeroBuffer);

            // do we already have a circuit for this endpoint
            if (packet.Type == PacketType.SecuredTemplateChecksumRequest)
            {
               
                SecuredTemplateChecksumRequestPacket checksum = (SecuredTemplateChecksumRequestPacket)packet;
               TemplateChecksumReplyPacket checkreply = new TemplateChecksumReplyPacket();
                checkreply.DataBlock.Checksum = 180572585;
                checkreply.DataBlock.Flags = 0;
                checkreply.DataBlock.MajorVersion = 1;
                checkreply.DataBlock.MinorVersion = 15;
                checkreply.DataBlock.PatchVersion = 0;
                checkreply.DataBlock.ServerVersion = 0;
                checkreply.TokenBlock.Token = checksum.TokenBlock.Token;
                this.SendPacket(checkreply, epSender);

                /*
                //if we wanted to echo the the checksum/ version from the client (so that any client worked)
                SecuredTemplateChecksumRequestPacket checkrequest = new SecuredTemplateChecksumRequestPacket();
                checkrequest.TokenBlock.Token = checksum.TokenBlock.Token;
                this.SendPacket(checkrequest, epSender);
                 */
                 
            }
            else if (packet.Type == PacketType.TemplateChecksumReply)
            {
                TemplateChecksumReplyPacket checksum = (TemplateChecksumReplyPacket)packet;
                TemplateChecksumReplyPacket checkreply2 = new TemplateChecksumReplyPacket();
                checkreply2.DataBlock.Checksum = checksum.DataBlock.Checksum;
                checkreply2.DataBlock.Flags = checksum.DataBlock.Flags;
                checkreply2.DataBlock.MajorVersion = checksum.DataBlock.MajorVersion;
                checkreply2.DataBlock.MinorVersion = checksum.DataBlock.MinorVersion;
                checkreply2.DataBlock.PatchVersion = checksum.DataBlock.PatchVersion;
                checkreply2.DataBlock.ServerVersion = checksum.DataBlock.ServerVersion;
                checkreply2.TokenBlock.Token = checksum.TokenBlock.Token;
                this.SendPacket(checkreply2, epSender);
                
            }
            else
            { 
            }

            Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);
        }


        private void SendPacket(Packet Pack, EndPoint endp)
        {
            if (!Pack.Header.Resent)
            {
                // Set the sequence number

                Pack.Header.Sequence = 1;

            }
            byte[] ZeroOutBuffer = new byte[4096];
            byte[] sendbuffer;
            sendbuffer = Pack.ToBytes();

            try
            {
                if (Pack.Header.Zerocoded)
                {
                    int packetsize = Helpers.ZeroEncode(sendbuffer, sendbuffer.Length, ZeroOutBuffer);
                    this.SendPackTo(ZeroOutBuffer, packetsize, SocketFlags.None, endp);
                }
                else
                {
                    this.SendPackTo(sendbuffer, sendbuffer.Length, SocketFlags.None, endp);
                }
            }
            catch (Exception)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "OpenSimClient.cs:ProcessOutPacket() - WARNING: Socket exception occurred on connection ");

            }
        }

        private void SendPackTo(byte[] buffer, int size, SocketFlags flags, EndPoint endp)
        {
            this.Server.SendTo(buffer, size, flags, endp);
        }
    }
}