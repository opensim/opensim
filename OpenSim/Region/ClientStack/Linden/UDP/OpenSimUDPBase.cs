/*
 * Copyright (c) 2006, Clutch, Inc.
 * Original Author: Jeff Cesnik
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.org nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Net;
using System.Net.Sockets;
using log4net;
using OpenSim.Framework;
using OpenMetaverse;
using OpenMetaverse.Packets;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public readonly struct IncomingPacket(LLClientView client, Packet packet)
    {
        /// <summary>Client this packet came from</summary>
        public readonly LLClientView Client = client;

        /// <summary>Packet data that has been received</summary>
        public readonly Packet Packet = packet;
    }

    /// <summary>
    /// Base UDP server
    /// </summary>
    public abstract class OpenSimUDPBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// This method is called when an incoming packet is received
        /// </summary>
        /// <param name="buffer">Incoming packet buffer</param>
        public abstract void PacketReceived(UDPPacketBuffer buffer);

        /// <summary>UDP port to bind to in server mode</summary>
        protected int m_udpPort;

        /// <summary>Local IP address to bind to in server mode</summary>
        protected IPAddress m_localBindAddress;

        /// <summary>UDP socket, used in either client or server mode</summary>
        private Socket m_udpSocket;

        public static Object m_udpBuffersPoolLock = new();
        public static UDPPacketBuffer[] m_udpBuffersPool = new UDPPacketBuffer[1000];
        public static int m_udpBuffersPoolPtr = -1;

        /// <summary>Returns true if the server is currently listening for inbound packets, otherwise false</summary>
        internal bool m_IsRunningInbound;
        public bool IsRunningInbound
        {
            get { return m_IsRunningInbound; }
            private set { m_IsRunningInbound = value; }
        }

        public CancellationTokenSource InboundCancellationSource = new();

        /// <summary>Returns true if the server is currently sending outbound packets, otherwise false</summary>
        /// <remarks>If IsRunningOut = false, then any request to send a packet is simply dropped.</remarks>
        internal bool m_IsRunningOutbound;
        public bool IsRunningOutbound
        {
            get { return m_IsRunningOutbound; }
            private set { m_IsRunningOutbound = value; }
        }

        /// <summary>
        /// Number of UDP receives.
        /// </summary>
        public int UdpReceives { get; private set; }

        /// <summary>
        /// Number of UDP sends
        /// </summary>
        public int UdpSends { get; private set; }

        /// <summary>
        /// Number of receives over which to establish a receive time average.
        /// </summary>
        private readonly static int s_receiveTimeSamples = 500;

        /// <summary>
        /// Current number of samples taken to establish a receive time average.
        /// </summary>
        private int m_currentReceiveTimeSamples;

        /// <summary>
        /// Cumulative receive time for the sample so far.
        /// </summary>
        private int m_receiveTicksInCurrentSamplePeriod;

        /// <summary>
        /// The average time taken for each require receive in the last sample.
        /// </summary>
        public float AverageReceiveTicksForLastSamplePeriod { get; private set; }

        public int Port
        {
            get { return m_udpPort; }
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="bindAddress">Local IP address to bind the server to</param>
        /// <param name="port">Port to listening for incoming UDP packets on</param>
        /// /// <param name="usePool">Are we to use an object pool to get objects for handing inbound data?</param>
        public OpenSimUDPBase(IPAddress bindAddress, int port)
        {
            m_localBindAddress = bindAddress;
            m_udpPort = port;

            // for debugging purposes only, initializes the random number generator
            // used for simulating packet loss
            // m_dropRandomGenerator = new Random();
        }

        ~OpenSimUDPBase()
        {
            if(m_udpSocket is not null)
                try { m_udpSocket.Close(); } catch { }
        }

        public static UDPPacketBuffer GetNewUDPBuffer(IPEndPoint remoteEndpoint)
        {
            lock (m_udpBuffersPoolLock)
            {
                if (m_udpBuffersPoolPtr >= 0)
                {
                    UDPPacketBuffer buf = m_udpBuffersPool[m_udpBuffersPoolPtr];
                    m_udpBuffersPool[m_udpBuffersPoolPtr] = null;
                    m_udpBuffersPoolPtr--;
                    buf.RemoteEndPoint = remoteEndpoint;
                    buf.DataLength = 0;
                    return buf;
                }
            }
            return new UDPPacketBuffer(remoteEndpoint);
        }

        public static void FreeUDPBuffer(UDPPacketBuffer buf)
        {
            lock (m_udpBuffersPoolLock)
            {
                if(buf.DataLength < 0)
                    return; // avoid duplicated free that may still happen

                if (m_udpBuffersPoolPtr < 999)
                {
                    buf.RemoteEndPoint = null;
                    buf.DataLength = -1;
                    m_udpBuffersPoolPtr++;
                    m_udpBuffersPool[m_udpBuffersPoolPtr] = buf;
                }
            }
        }

        /// <summary>
        /// Start inbound UDP packet handling.
        /// </summary>
        /// <param name="recvBufferSize">The size of the receive buffer for
        /// the UDP socket. This value is passed up to the operating system
        /// and used in the system networking stack. Use zero to leave this
        /// value as the default</param>


        public virtual void StartInbound(int recvBufferSize)
        {
            if (!m_IsRunningInbound)
            {
                m_log.DebugFormat("[UDPBASE]: Starting inbound UDP loop");

                const int SIO_UDP_CONNRESET = -1744830452;

                m_udpSocket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                try
                {
                    if (m_udpSocket.Ttl < 128)
                    {
                        m_udpSocket.Ttl = 128;
                    }
                }
                catch (SocketException)
                {
                    m_log.Debug("[UDPBASE]: Failed to increase default TTL");
                }

                try
                {
                    m_udpSocket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null);
                }
                catch
                {
                    m_log.Debug("[UDPBASE]: SIO_UDP_CONNRESET flag not supported on this platform, ignoring");
                }

                // On at least Mono 3.2.8, multiple UDP sockets can bind to the same port by default.  At the moment
                // we never want two regions to listen on the same port as they cannot demultiplex each other's messages,
                // leading to a confusing bug.
                // By default, Windows does not allow two sockets to bind to the same port.
                //
                // Unfortunately, this also causes a crashed sim to leave the socket in a state
                // where it appears to be in use but is really just hung from the old process
                // crashing rather than closing it. While this protects agains misconfiguration,
                // allowing crashed sims to be started up again right away, rather than having to
                // wait 2 minutes for the socket to clear is more valuable. Commented 12/13/2016
                // m_udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);

                if (recvBufferSize != 0)
                    m_udpSocket.ReceiveBufferSize = recvBufferSize;

                IPEndPoint ipep = new(m_localBindAddress, m_udpPort);
                m_udpSocket.Bind(ipep);

                if (m_udpPort == 0)
                    m_udpPort = ((IPEndPoint)m_udpSocket.LocalEndPoint).Port;

                m_IsRunningInbound = true;

                // kick start the receiver tasks dance.
                Task.Run(AsyncBeginReceive).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Start outbound UDP packet handling.
        /// </summary>
        public virtual void StartOutbound()
        {
            m_log.DebugFormat("[UDPBASE]: Starting outbound UDP loop");

            m_IsRunningOutbound = true;
        }

        public virtual void StopInbound()
        {
            if (m_IsRunningInbound)
            {
                m_log.DebugFormat("[UDPBASE]: Stopping inbound UDP loop");

                m_IsRunningInbound = false;
                InboundCancellationSource.Cancel();
                m_udpSocket.Close();
                m_udpSocket = null;
            }
        }

        public virtual void StopOutbound()
        {
            m_log.DebugFormat("[UDPBASE]: Stopping outbound UDP loop");

            m_IsRunningOutbound = false;
        }

        private async void AsyncBeginReceive()
        {
            SocketAddress workSktAddress = new(m_udpSocket.AddressFamily);
            while (m_IsRunningInbound)
            {
                UDPPacketBuffer buf = GetNewUDPBuffer(null); // we need a fresh one here, for now at least
                try
                {
                    int nbytes = 
                        await m_udpSocket.ReceiveFromAsync(buf.Data.AsMemory(), SocketFlags.None, workSktAddress, InboundCancellationSource.Token).ConfigureAwait(false);
                    if (!m_IsRunningInbound || InboundCancellationSource.IsCancellationRequested)
                    {
                        FreeUDPBuffer(buf);
                        return;
                    }

                    if (nbytes > 0)
                    {
                        int startTick = Util.EnvironmentTickCount();

                        buf.RemoteEndPoint = Util.GetEndPoint(workSktAddress);
                        buf.DataLength = nbytes;
                        UdpReceives++;

                        PacketReceived(buf);

                        if (m_currentReceiveTimeSamples >= s_receiveTimeSamples)
                        {
                            AverageReceiveTicksForLastSamplePeriod
                                = (float)m_receiveTicksInCurrentSamplePeriod / s_receiveTimeSamples;

                            m_receiveTicksInCurrentSamplePeriod = 0;
                            m_currentReceiveTimeSamples = 0;
                        }
                        else
                        {
                            m_receiveTicksInCurrentSamplePeriod += Util.EnvironmentTickCountSubtract(startTick);
                            m_currentReceiveTimeSamples++;
                        }
                    }
                    else
                        FreeUDPBuffer(buf);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    m_log.Error($"[UDPBASE]: Error processing UDP receiveFrom. Exception ", e);
                }
            }
        }

        public void SyncSend(UDPPacketBuffer buf)
        {
            if(buf.RemoteEndPoint is null)
                return; // already expired
            try
            {
                m_udpSocket.SendTo(
                    buf.Data,
                    0,
                    buf.DataLength,
                    SocketFlags.None,
                    buf.RemoteEndPoint
                    );
                 UdpSends++;
            }
            catch (SocketException e)
            {
                m_log.WarnFormat("[UDPBASE]: sync send SocketException {0} {1}", buf.RemoteEndPoint, e.Message);
            }
            catch (ObjectDisposedException) { }
        }
    }
}
