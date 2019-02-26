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
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;

namespace OpenMetaverse
{
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

        public static Object m_udpBuffersPoolLock = new Object();
        public static UDPPacketBuffer[] m_udpBuffersPool = new UDPPacketBuffer[1000];
        public static int m_udpBuffersPoolPtr = -1;

        /// <summary>Returns true if the server is currently listening for inbound packets, otherwise false</summary>
        public bool IsRunningInbound { get; private set; }

        /// <summary>Returns true if the server is currently sending outbound packets, otherwise false</summary>
        /// <remarks>If IsRunningOut = false, then any request to send a packet is simply dropped.</remarks>
        public bool IsRunningOutbound { get; private set; }

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

        #region PacketDropDebugging
        /// <summary>
        /// For debugging purposes only... random number generator for dropping
        /// outbound packets.
        /// </summary>
        private Random m_dropRandomGenerator = new Random();

        /// <summary>
        /// For debugging purposes only... parameters for a simplified
        /// model of packet loss with bursts, overall drop rate should
        /// be roughly 1 - m_dropLengthProbability / (m_dropProbabiliy + m_dropLengthProbability)
        /// which is about 1% for parameters 0.0015 and 0.15
        /// </summary>
        private double m_dropProbability = 0.0030;
        private double m_dropLengthProbability = 0.15;
        private bool m_dropState = false;

        /// <summary>
        /// For debugging purposes only... parameters to control the time
        /// duration over which packet loss bursts can occur, if no packets
        /// have been sent for m_dropResetTicks milliseconds, then reset the
        /// state of the packet dropper to its default.
        /// </summary>
        private int m_dropLastTick = 0;
        private int m_dropResetTicks = 500;

        /// <summary>
        /// Debugging code used to simulate dropped packets with bursts
        /// </summary>
        private bool DropOutgoingPacket()
        {
            double rnum = m_dropRandomGenerator.NextDouble();

            // if the connection has been idle for awhile (more than m_dropResetTicks) then
            // reset the state to the default state, don't continue a burst
            int curtick = Util.EnvironmentTickCount();
            if (Util.EnvironmentTickCountSubtract(curtick, m_dropLastTick) > m_dropResetTicks)
                m_dropState = false;

            m_dropLastTick = curtick;

            // if we are dropping packets, then the probability of dropping
            // this packet is the probability that we stay in the burst
            if (m_dropState)
            {
                m_dropState = (rnum < (1.0 - m_dropLengthProbability)) ? true : false;
            }
            else
            {
                m_dropState = (rnum < m_dropProbability) ? true : false;
            }

            return m_dropState;
        }
        #endregion PacketDropDebugging

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
            if(m_udpSocket !=null)
                try { m_udpSocket.Close(); } catch { }
        }

        public UDPPacketBuffer GetNewUDPBuffer(IPEndPoint remoteEndpoint)
        {
            lock (m_udpBuffersPoolLock)
            {
                if (m_udpBuffersPoolPtr >= 0)
                {
                    UDPPacketBuffer buf = m_udpBuffersPool[m_udpBuffersPoolPtr];
                    m_udpBuffersPool[m_udpBuffersPoolPtr] = null;
                    m_udpBuffersPoolPtr--;
                    buf.RemoteEndPoint = remoteEndpoint;
                    return buf;
                }
            }
            return new UDPPacketBuffer(remoteEndpoint);
        }

        public void FreeUDPBuffer(UDPPacketBuffer buf)
        {
            lock (m_udpBuffersPoolLock)
            {
                if (m_udpBuffersPoolPtr < 999)
                {
                    buf.RemoteEndPoint = null;
                    buf.DataLength = 0;
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
        /// <param name="asyncPacketHandling">Set this to true to start
        /// receiving more packets while current packet handler callbacks are
        /// still running. Setting this to false will complete each packet
        /// callback before the next packet is processed</param>
        /// <remarks>This method will attempt to set the SIO_UDP_CONNRESET flag
        /// on the socket to get newer versions of Windows to behave in a sane
        /// manner (not throwing an exception when the remote side resets the
        /// connection). This call is ignored on Mono where the flag is not
        /// necessary</remarks>

        public virtual void StartInbound(int recvBufferSize)
        {
            if (!IsRunningInbound)
            {
                m_log.DebugFormat("[UDPBASE]: Starting inbound UDP loop");

                const int SIO_UDP_CONNRESET = -1744830452;

                IPEndPoint ipep = new IPEndPoint(m_localBindAddress, m_udpPort);

                m_udpSocket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Dgram,
                    ProtocolType.Udp);

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
                    // This udp socket flag is not supported under mono,
                    // so we'll catch the exception and continue
                    // Try does not protect some mono versions on mac
                    if(Util.IsWindows())
                    {
                        m_udpSocket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null);
                        m_log.Debug("[UDPBASE]: SIO_UDP_CONNRESET flag set");
                    }               
                    else
                    {
                        m_log.Debug("[UDPBASE]: SIO_UDP_CONNRESET flag not supported on this platform, ignoring");
                    }
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

                m_udpSocket.Bind(ipep);

                if (m_udpPort == 0)
                    m_udpPort = ((IPEndPoint)m_udpSocket.LocalEndPoint).Port;

                IsRunningInbound = true;

                // kick off an async receive.  The Start() method will return, the
                // actual receives will occur asynchronously and will be caught in
                // AsyncEndRecieve().
                AsyncBeginReceive();
            }
        }

        /// <summary>
        /// Start outbound UDP packet handling.
        /// </summary>
        public virtual void StartOutbound()
        {
            m_log.DebugFormat("[UDPBASE]: Starting outbound UDP loop");

            IsRunningOutbound = true;
        }

        public virtual void StopInbound()
        {
            if (IsRunningInbound)
            {
                m_log.DebugFormat("[UDPBASE]: Stopping inbound UDP loop");

                IsRunningInbound = false;
                m_udpSocket.Close();
            }
        }

        public virtual void StopOutbound()
        {
            m_log.DebugFormat("[UDPBASE]: Stopping outbound UDP loop");

            IsRunningOutbound = false;
        }

        private void AsyncBeginReceive()
        {
            if (!IsRunningInbound)
                return;

            UDPPacketBuffer buf = GetNewUDPBuffer(new IPEndPoint(IPAddress.Any, 0)); // we need a fresh one here, for now at least
            try
            {
                // kick off an async read
                m_udpSocket.BeginReceiveFrom(
                    //wrappedBuffer.Instance.Data,
                    buf.Data,
                    0,
                    UDPPacketBuffer.BUFFER_SIZE,
                    SocketFlags.None,
                    ref buf.RemoteEndPoint,
                    AsyncEndReceive,
                    //wrappedBuffer);
                    buf);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.ConnectionReset)
                {
                    m_log.Warn("[UDPBASE]: SIO_UDP_CONNRESET was ignored, attempting to salvage the UDP listener on port " + m_udpPort);
                    bool salvaged = false;
                    while (!salvaged)
                    {
                        try
                        {
                            m_udpSocket.BeginReceiveFrom(
                                //wrappedBuffer.Instance.Data,
                                buf.Data,
                                0,
                                UDPPacketBuffer.BUFFER_SIZE,
                                SocketFlags.None,
                                ref buf.RemoteEndPoint,
                                AsyncEndReceive,
                                //wrappedBuffer);
                                buf);
                            salvaged = true;
                        }
                        catch (SocketException) { }
                        catch (ObjectDisposedException) { return; }
                    }

                    m_log.Warn("[UDPBASE]: Salvaged the UDP listener on port " + m_udpPort);
                }
            }
            catch (ObjectDisposedException e)
            {
                m_log.Error(
                    string.Format("[UDPBASE]: Error processing UDP begin receive {0}.  Exception  ", UdpReceives), e);
            }
            catch (Exception e)
            {
                m_log.Error(
                    string.Format("[UDPBASE]: Error processing UDP begin receive {0}.  Exception  ", UdpReceives), e);
            }
        }

        private void AsyncEndReceive(IAsyncResult iar)
        {
            // Asynchronous receive operations will complete here through the call
            // to AsyncBeginReceive
            if (IsRunningInbound)
            {
                UdpReceives++;
 
                try
                {
                    // get the buffer that was created in AsyncBeginReceive
                    // this is the received data
                    UDPPacketBuffer buffer = (UDPPacketBuffer)iar.AsyncState;

                    int startTick = Util.EnvironmentTickCount();

                    // get the length of data actually read from the socket, store it with the
                    // buffer
                    buffer.DataLength = m_udpSocket.EndReceiveFrom(iar, ref buffer.RemoteEndPoint);

                    // call the abstract method PacketReceived(), passing the buffer that
                    // has just been filled from the socket read.
                    PacketReceived(buffer);

                    // If more than one thread can be calling AsyncEndReceive() at once (e.g. if m_asyncPacketHandler)
                    // then a particular stat may be inaccurate due to a race condition.  We won't worry about this
                    // since this should be rare and  won't cause a runtime problem.
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
                catch (SocketException se)
                {
                    m_log.Error(
                        string.Format(
                            "[UDPBASE]: Error processing UDP end receive {0}, socket error code {1}.  Exception  ",
                            UdpReceives, se.ErrorCode),
                        se);
                }
                catch (ObjectDisposedException e)
                {
                    m_log.Error(
                        string.Format("[UDPBASE]: Error processing UDP end receive {0}.  Exception  ", UdpReceives), e);
                }
                catch (Exception e)
                {
                    m_log.Error(
                        string.Format("[UDPBASE]: Error processing UDP end receive {0}.  Exception  ", UdpReceives), e);
                }
                finally
                {
                    AsyncBeginReceive();
                }
            }
        }

/* not in use
        public void AsyncBeginSend(UDPPacketBuffer buf)
        {
//            if (IsRunningOutbound)
//            {

                // This is strictly for debugging purposes to simulate dropped
                // packets when testing throttles & retransmission code
                // if (DropOutgoingPacket())
                //     return;

                try
                {
                    m_udpSocket.BeginSendTo(
                        buf.Data,
                        0,
                        buf.DataLength,
                        SocketFlags.None,
                        buf.RemoteEndPoint,
                        AsyncEndSend,
                        buf);
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
 //           }
        }

        void AsyncEndSend(IAsyncResult result)
        {
            try
            {
//                UDPPacketBuffer buf = (UDPPacketBuffer)result.AsyncState;
                m_udpSocket.EndSendTo(result);

                UdpSends++;
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }
*/
        public void SyncSend(UDPPacketBuffer buf)
        {
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
                m_log.Warn("[UDPBASE]: sync send SocketException {0} " + e.Message);
            }
            catch (ObjectDisposedException) { }
        }
    }
}
