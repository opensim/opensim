/*
* Copyright (c) Contributors, http://opensimulator.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using Timer=System.Timers.Timer;

namespace OpenSim.Region.ClientStack
{
    public class PacketQueue
    {
        private BlockingQueue<QueItem> SendQueue;
        
        private Queue<QueItem> IncomingPacketQueue;
        private Queue<QueItem> OutgoingPacketQueue;
        private Queue<QueItem> ResendOutgoingPacketQueue;
        private Queue<QueItem> LandOutgoingPacketQueue;
        private Queue<QueItem> WindOutgoingPacketQueue;
        private Queue<QueItem> CloudOutgoingPacketQueue;
        private Queue<QueItem> TaskOutgoingPacketQueue;
        private Queue<QueItem> TextureOutgoingPacketQueue;
        private Queue<QueItem> AssetOutgoingPacketQueue;
        
        private Dictionary<uint, uint> PendingAcks = new Dictionary<uint, uint>();
        private Dictionary<uint, Packet> NeedAck = new Dictionary<uint, Packet>();

        // 1536000
        private int throttleOutboundMax = 1536000; // Number of bytes allowed to go out per second. (256kbps per client) 
                                              // TODO: Make this variable. Lower throttle on un-ack. Raise over time?
        private int bytesSent = 0;   // Number of bytes sent this period

        private int throttleOutbound = 162144; // Number of bytes allowed to go out per second. (256kbps per client) 
        // TODO: Make this variable. Lower throttle on un-ack. Raise over time

        // All throttle times and number of bytes are calculated by dividing by this value
        // This value also determines how many times per throttletimems the timer will run
        // If throttleimems is 1000 ms, then the timer will fire every 1000/7 milliseconds

        private int throttleTimeDivisor = 7;

        private int throttletimems = 1000;

        // Maximum -per type- throttle
        private int ResendthrottleMAX = 100000;
        private int LandthrottleMax = 100000;
        private int WindthrottleMax = 100000;
        private int CloudthrottleMax = 100000;
        private int TaskthrottleMax = 800000;
        private int AssetthrottleMax = 800000;
        private int TexturethrottleMax = 800000;

        // Minimum -per type- throttle
        private int ResendthrottleMin = 5000; // setting resendmin to 0 results in mostly dropped packets
        private int LandthrottleMin = 1000;
        private int WindthrottleMin = 1000;
        private int CloudthrottleMin = 1000;
        private int TaskthrottleMin = 1000;
        private int AssetthrottleMin = 1000;
        private int TexturethrottleMin = 1000;

        // Sim default per-client settings.
        private int ResendthrottleOutbound = 50000;
        private int ResendBytesSent = 0;
        private int LandthrottleOutbound = 100000;
        private int LandBytesSent = 0;
        private int WindthrottleOutbound = 10000;
        private int WindBytesSent = 0;
        private int CloudthrottleOutbound = 5000;
        private int CloudBytesSent = 0;
        private int TaskthrottleOutbound = 100000;
        private int TaskBytesSent = 0;
        private int AssetthrottleOutbound = 80000;
        private int AssetBytesSent = 0;
        private int TexturethrottleOutbound = 100000;
        private int TextureBytesSent = 0;

        private Timer throttleTimer;


        public PacketQueue() 
        {
            // While working on this, the BlockingQueue had me fooled for a bit.
            // The Blocking queue causes the thread to stop until there's something 
            // in it to process.  it's an on-purpose threadlock though because 
            // without it, the clientloop will suck up all sim resources.
            
            SendQueue = new BlockingQueue<QueItem>();
            
            IncomingPacketQueue = new Queue<QueItem>();
            OutgoingPacketQueue = new Queue<QueItem>();
            ResendOutgoingPacketQueue = new Queue<QueItem>();
            LandOutgoingPacketQueue = new Queue<QueItem>();
            WindOutgoingPacketQueue = new Queue<QueItem>();
            CloudOutgoingPacketQueue = new Queue<QueItem>();
            TaskOutgoingPacketQueue = new Queue<QueItem>();
            TextureOutgoingPacketQueue = new Queue<QueItem>();
            AssetOutgoingPacketQueue = new Queue<QueItem>();
        
            // TIMERS needed for this
            ResetCounters();
            
            throttleTimer = new Timer((int)(throttletimems/throttleTimeDivisor));
            throttleTimer.Elapsed += new ElapsedEventHandler(throttleTimer_Elapsed);
            throttleTimer.Start();
        }

        private void ResetCounters()
        {
            bytesSent = 0;
            ResendBytesSent = 0;
            LandBytesSent = 0;
            WindBytesSent = 0;
            CloudBytesSent = 0;
            TaskBytesSent = 0;
            AssetBytesSent = 0;
            TextureBytesSent = 0; 
        }

        private bool PacketsWaiting()
        {
            return (ResendOutgoingPacketQueue.Count > 0 ||
                    LandOutgoingPacketQueue.Count > 0 ||
                    WindOutgoingPacketQueue.Count > 0 ||
                    CloudOutgoingPacketQueue.Count > 0 ||
                    TaskOutgoingPacketQueue.Count > 0 ||
                    AssetOutgoingPacketQueue.Count > 0 ||
                    TextureOutgoingPacketQueue.Count > 0);
        }
        
       
        private void throttleTimer_Elapsed(object sender, ElapsedEventArgs e)
        {   
            ResetCounters();
            
            // I was considering this..   Will an event fire if the thread it's on is blocked?

            // Then I figured out..  it doesn't really matter..  because this thread won't be blocked for long
            // The General overhead of the UDP protocol gets sent to the queue un-throttled by this
            // so This'll pick up about around the right time.

            int MaxThrottleLoops = 4550; // 50*7 packets can be dequeued at once.
            int throttleLoops = 0;

            // We're going to dequeue all of the saved up packets until 
            // we've hit the throttle limit or there's no more packets to send
            while ((bytesSent <= (int)(throttleOutbound/throttleTimeDivisor)) &&
                   PacketsWaiting() && (throttleLoops <= MaxThrottleLoops))
            {
                throttleLoops++;
                //Now comes the fun part..   we dump all our elements into PacketQueue that we've saved up.
                if (ResendBytesSent <= ((int)(ResendthrottleOutbound/throttleTimeDivisor)) && ResendOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = ResendOutgoingPacketQueue.Dequeue();

                    SendQueue.Enqueue(qpack);
                    bytesSent += qpack.Packet.ToBytes().Length;
                    ResendBytesSent += qpack.Packet.ToBytes().Length;
                }
                if (LandBytesSent <= ((int)(LandthrottleOutbound/throttleTimeDivisor)) && LandOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = LandOutgoingPacketQueue.Dequeue();

                    SendQueue.Enqueue(qpack);
                    bytesSent += qpack.Packet.ToBytes().Length;
                    LandBytesSent += qpack.Packet.ToBytes().Length;
                }
                if (WindBytesSent <= ((int)(WindthrottleOutbound/throttleTimeDivisor)) && WindOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = WindOutgoingPacketQueue.Dequeue();

                    SendQueue.Enqueue(qpack);
                    bytesSent += qpack.Packet.ToBytes().Length;
                    WindBytesSent += qpack.Packet.ToBytes().Length;
                }
                if (CloudBytesSent <= ((int)(CloudthrottleOutbound/throttleTimeDivisor)) && CloudOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = CloudOutgoingPacketQueue.Dequeue();

                    SendQueue.Enqueue(qpack);
                    bytesSent += qpack.Packet.ToBytes().Length;
                    CloudBytesSent += qpack.Packet.ToBytes().Length;
                }
                if (TaskBytesSent <= ((int)(TaskthrottleOutbound/throttleTimeDivisor)) && TaskOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = TaskOutgoingPacketQueue.Dequeue();

                    SendQueue.Enqueue(qpack);
                    bytesSent += qpack.Packet.ToBytes().Length;
                    TaskBytesSent += qpack.Packet.ToBytes().Length;
                }
                if (TextureBytesSent <= ((int)(TexturethrottleOutbound/throttleTimeDivisor)) && TextureOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = TextureOutgoingPacketQueue.Dequeue();

                    SendQueue.Enqueue(qpack);
                    bytesSent += qpack.Packet.ToBytes().Length;
                    TextureBytesSent += qpack.Packet.ToBytes().Length;
                }
                if (AssetBytesSent <= ((int)(AssetthrottleOutbound/throttleTimeDivisor)) && AssetOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = AssetOutgoingPacketQueue.Dequeue();

                    SendQueue.Enqueue(qpack);
                    bytesSent += qpack.Packet.ToBytes().Length;
                    AssetBytesSent += qpack.Packet.ToBytes().Length;
                }

            }

        }


    }

    
    
}