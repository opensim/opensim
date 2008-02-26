using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using OpenSim.Region.Environment.Scenes;
using libsecondlife;

namespace OpenSim.Region.Environment.Modules.VoiceChat
{
    /**
     * Represents a single voiceclient instance
     **/
    public class VoiceClient
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Socket m_socket;
        public LLUUID m_clientId;
        public bool m_authenticated = false;

        protected VoicePacketHeader m_header = null;
        protected int m_headerBytesReceived = 0;

        protected int m_offset = 0;
        protected int m_supportedCodecs = 0;

        protected byte[] m_buffer = null;
        protected byte[] m_headerBytes = new byte[5];

        protected bool m_enabled = true;

        protected VoiceChatServer m_server;

        public VoiceClient(Socket socket, VoiceChatServer server)
        {
            m_socket = socket;
            m_server = server;
        }

        public void OnDataReceived(byte[] data, int byteCount)
        {
            int offset = 0;
            while (offset < byteCount)
            {
                if (m_header == null)
                {
                    if (m_headerBytesReceived < 5)
                    {
                        m_headerBytes[m_headerBytesReceived++] = data[offset++];
                    }
                    else if (m_headerBytesReceived == 5)
                    {
                        m_header = new VoicePacketHeader();
                        m_header.Parse(m_headerBytes);
                        if (m_header.length > 65535)
                        {
                            throw new Exception("Packet size " + m_header.length + " > 65535");
                        }

                        m_buffer = new byte[m_header.length];
                        m_offset = 0;
                        m_headerBytesReceived = 0;
                    }
                }
                else
                {
                    int bytesToCopy = m_header.length-m_offset;
                    if (bytesToCopy > byteCount - offset)
                        bytesToCopy = byteCount - offset;

                    Buffer.BlockCopy(data, offset, m_buffer, m_offset, bytesToCopy);

                    offset += bytesToCopy;
                    m_offset += bytesToCopy;

                    if (m_offset == m_header.length)
                    {
                        ParsePacket(m_header.type, m_buffer);
                        m_header = null;
                    }
                }
            }
        }

        void ParsePacket(byte type, byte[] data)
        {
            switch (type)
            {
                case 0: //LOGIN
                    ParseLogin(data);
                    break;

                case 1: //AUDIODATA
                    if (m_authenticated)
                    {
                        VoicePacket packet = new VoicePacket(data);
                        packet.m_clientId = m_clientId;
                        m_server.BroadcastVoice(packet);
                    }
                    else
                    {
                        m_log.Warn("[VOICECHAT]: Got unauthorized audio data from " +
                                                           m_socket.RemoteEndPoint.ToString());
                        m_socket.Close();
                    }
                    break;

                case 3: //ENABLEVOIP
                    if (data[0] == 0)
                    {
                        m_log.Warn("[VOICECHAT]: VoiceChat has been disabled for " + m_clientId);
                        m_enabled = false;
                    }
                    else 
                    {
                        m_log.Warn("[VOICECHAT]: VoiceChat has been enabled for " + m_clientId);
                        m_enabled = true;
                    }
                    break;
                    

                default:
                    throw new Exception("Invalid packet received");
            }
        }

        void ParseLogin(byte[] data)
        {
            m_clientId = new LLUUID(data, 0);

            m_supportedCodecs = data[16];
            m_supportedCodecs |= data[17] << 8;
            m_supportedCodecs |= data[18] << 16;
            m_supportedCodecs |= data[19] << 24;

            if (m_server.AddClient(this, m_clientId))
            {
                m_log.Info("[VOICECHAT]: Client authenticated succesfully: " + m_clientId);
                m_authenticated = true;
            }
            else
            {
                throw new Exception("Unable to authenticate with id " + m_clientId);
            }
        }

        public bool IsEnabled()
        {
            return m_enabled;
        }

        public bool IsCodecSupported(int codec)
        {
            if ((m_supportedCodecs & codec) != 0)
                return true;

            return false;
        }

        public void SendTo(byte[] data)
        {
            if (m_authenticated)
            {
                //ServerStatus.ReportOutPacketTcp(m_socket.Send(data));
            }
        }
    }
}
