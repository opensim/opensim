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
using System.Threading;
using OpenMetaverse;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class OpenSimUDPBase
    {
        // these abstract methods must be implemented in a derived class to actually do
        // something with the packets that are sent and received.
        protected abstract void PacketReceived(UDPPacketBuffer buffer);
        protected abstract void PacketSent(UDPPacketBuffer buffer, int bytesSent);

        // the port to listen on
        internal int udpPort;

        // the UDP socket
        private Socket udpSocket;

        // the ReaderWriterLock is used solely for the purposes of shutdown (Stop()).
        // since there are potentially many "reader" threads in the internal .NET IOCP
        // thread pool, this is a cheaper synchronization primitive than using
        // a Mutex object.  This allows many UDP socket "reads" concurrently - when
        // Stop() is called, it attempts to obtain a writer lock which will then
        // wait until all outstanding operations are completed before shutting down.
        // this avoids the problem of closing the socket with outstanding operations
        // and trying to catch the inevitable ObjectDisposedException.
        private ReaderWriterLock rwLock = new ReaderWriterLock();

        // number of outstanding operations.  This is a reference count
        // which we use to ensure that the threads exit cleanly. Note that
        // we need this because the threads will potentially still need to process
        // data even after the socket is closed.
        private int rwOperationCount = 0;

        // the all important shutdownFlag.  This is synchronized through the ReaderWriterLock.
        private volatile bool shutdownFlag = true;

        // the remote endpoint to communicate with
        protected IPEndPoint remoteEndPoint = null;


        /// <summary>
        /// Initialize the UDP packet handler in server mode
        /// </summary>
        /// <param name="port">Port to listening for incoming UDP packets on</param>
        public OpenSimUDPBase(int port)
        {
            udpPort = port;
        }

        /// <summary>
        /// Initialize the UDP packet handler in client mode
        /// </summary>
        /// <param name="endPoint">Remote UDP server to connect to</param>
        public OpenSimUDPBase(IPEndPoint endPoint)
        {
            remoteEndPoint = endPoint;
            udpPort = 0;
        }

        /// <summary>
        /// 
        /// </summary>
        public void Start()
        {
            if (shutdownFlag)
            {
                if (remoteEndPoint == null)
                {
                    // Server mode

                    // create and bind the socket
                    IPEndPoint ipep = new IPEndPoint(Settings.BIND_ADDR, udpPort);
                    udpSocket = new Socket(
                        AddressFamily.InterNetwork,
                        SocketType.Dgram,
                        ProtocolType.Udp);
                    udpSocket.Bind(ipep);
                }
                else
                {
                    // Client mode
                    IPEndPoint ipep = new IPEndPoint(Settings.BIND_ADDR, udpPort);
                    udpSocket = new Socket(
                        AddressFamily.InterNetwork,
                        SocketType.Dgram,
                        ProtocolType.Udp);
                    udpSocket.Bind(ipep);
                    //udpSocket.Connect(remoteEndPoint);
                }

                // we're not shutting down, we're starting up
                shutdownFlag = false;

                // kick off an async receive.  The Start() method will return, the
                // actual receives will occur asynchronously and will be caught in
                // AsyncEndRecieve().
                AsyncBeginReceive();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Stop()
        {
            if (!shutdownFlag)
            {
                // wait indefinitely for a writer lock.  Once this is called, the .NET runtime
                // will deny any more reader locks, in effect blocking all other send/receive
                // threads.  Once we have the lock, we set shutdownFlag to inform the other
                // threads that the socket is closed.
                rwLock.AcquireWriterLock(-1);
                shutdownFlag = true;
                udpSocket.Close();
                rwLock.ReleaseWriterLock();

                // wait for any pending operations to complete on other
                // threads before exiting.
                const int FORCE_STOP = 100;
                int i = 0;
                while (rwOperationCount > 0 && i < FORCE_STOP)
                {
                    Thread.Sleep(10);
                    ++i;
                }

                if (i >= FORCE_STOP)
                {
                    Logger.Log("UDPBase.Stop() forced shutdown while waiting on pending operations",
                        Helpers.LogLevel.Warning);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool IsRunning
        {
            get { return !shutdownFlag; }
        }

        private void AsyncBeginReceive()
        {
            // this method actually kicks off the async read on the socket.
            // we aquire a reader lock here to ensure that no other thread
            // is trying to set shutdownFlag and close the socket.
            rwLock.AcquireReaderLock(-1);

            if (!shutdownFlag)
            {
                // increment the count of pending operations
                Interlocked.Increment(ref rwOperationCount);

                // allocate a packet buffer
                //WrappedObject<UDPPacketBuffer> wrappedBuffer = Pool.CheckOut();
                UDPPacketBuffer buf = new UDPPacketBuffer();

                try
                {
                    // kick off an async read
                    udpSocket.BeginReceiveFrom(
                        //wrappedBuffer.Instance.Data,
                        buf.Data,
                        0,
                        UDPPacketBuffer.BUFFER_SIZE,
                        SocketFlags.None,
                        //ref wrappedBuffer.Instance.RemoteEndPoint,
                        ref buf.RemoteEndPoint,
                        new AsyncCallback(AsyncEndReceive),
                        //wrappedBuffer);
                        buf);
                }
                catch (SocketException)
                {
                    // something bad happened
                    //Logger.Log(
                    //    "A SocketException occurred in UDPServer.AsyncBeginReceive()", 
                    //    Helpers.LogLevel.Error, se);

                    // an error occurred, therefore the operation is void.  Decrement the reference count.
                    Interlocked.Decrement(ref rwOperationCount);
                }
            }

            // we're done with the socket for now, release the reader lock.
            rwLock.ReleaseReaderLock();
        }

        private void AsyncEndReceive(IAsyncResult iar)
        {
            // Asynchronous receive operations will complete here through the call
            // to AsyncBeginReceive

            // aquire a reader lock
            rwLock.AcquireReaderLock(-1);

            if (!shutdownFlag)
            {
                // get the buffer that was created in AsyncBeginReceive
                // this is the received data
                //WrappedObject<UDPPacketBuffer> wrappedBuffer = (WrappedObject<UDPPacketBuffer>)iar.AsyncState;
                //UDPPacketBuffer buffer = wrappedBuffer.Instance;
                UDPPacketBuffer buffer = (UDPPacketBuffer)iar.AsyncState;

                try
                {
                    // get the length of data actually read from the socket, store it with the
                    // buffer
                    buffer.DataLength = udpSocket.EndReceiveFrom(iar, ref buffer.RemoteEndPoint);

                    // this operation is now complete, decrement the reference count
                    Interlocked.Decrement(ref rwOperationCount);

                    // we're done with the socket, release the reader lock
                    rwLock.ReleaseReaderLock();

                    // call the abstract method PacketReceived(), passing the buffer that
                    // has just been filled from the socket read.
                    PacketReceived(buffer);
                }
                catch (SocketException)
                {
                    // an error occurred, therefore the operation is void.  Decrement the reference count.
                    Interlocked.Decrement(ref rwOperationCount);

                    // we're done with the socket for now, release the reader lock.
                    rwLock.ReleaseReaderLock();
                }
                finally
                {
                    // start another receive - this keeps the server going!
                    AsyncBeginReceive();

                    //wrappedBuffer.Dispose();
                }
            }
            else
            {
                // nothing bad happened, but we are done with the operation
                // decrement the reference count and release the reader lock
                Interlocked.Decrement(ref rwOperationCount);
                rwLock.ReleaseReaderLock();
            }
        }

        public void AsyncBeginSend(UDPPacketBuffer buf)
        {
            rwLock.AcquireReaderLock(-1);

            if (!shutdownFlag)
            {
                try
                {
                    Interlocked.Increment(ref rwOperationCount);
                    udpSocket.BeginSendTo(
                        buf.Data,
                        0,
                        buf.DataLength,
                        SocketFlags.None,
                        buf.RemoteEndPoint,
                        new AsyncCallback(AsyncEndSend),
                        buf);
                }
                catch (SocketException)
                {
                    //Logger.Log(
                    //    "A SocketException occurred in UDPServer.AsyncBeginSend()",
                    //    Helpers.LogLevel.Error, se);
                }
            }

            rwLock.ReleaseReaderLock();
        }

        private void AsyncEndSend(IAsyncResult iar)
        {
            rwLock.AcquireReaderLock(-1);

            if (!shutdownFlag)
            {
                UDPPacketBuffer buffer = (UDPPacketBuffer)iar.AsyncState;

                try
                {
                    int bytesSent = udpSocket.EndSendTo(iar);

                    // note that call to the abstract PacketSent() method - we are passing the number
                    // of bytes sent in a separate parameter, since we can't use buffer.DataLength which
                    // is the number of bytes to send (or bytes received depending upon whether this
                    // buffer was part of a send or a receive).
                    PacketSent(buffer, bytesSent);
                }
                catch (SocketException)
                {
                    //Logger.Log(
                    //    "A SocketException occurred in UDPServer.AsyncEndSend()",
                    //    Helpers.LogLevel.Error, se);
                }
            }

            Interlocked.Decrement(ref rwOperationCount);
            rwLock.ReleaseReaderLock();
        }
    }
}
