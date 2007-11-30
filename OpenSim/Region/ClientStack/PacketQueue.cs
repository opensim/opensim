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

        // All throttle times and number of bytes are calculated by dividing by this value
        // This value also determines how many times per throttletimems the timer will run
        // If throttleimems is 1000 ms, then the timer will fire every 1000/7 milliseconds

        private int throttleTimeDivisor = 7;

        private int throttletimems = 1000;

        private PacketThrottle ResendThrottle;
        private PacketThrottle LandThrottle;
        private PacketThrottle WindThrottle;
        private PacketThrottle CloudThrottle;
        private PacketThrottle TaskThrottle;
        private PacketThrottle AssetThrottle;
        private PacketThrottle TextureThrottle;
        private PacketThrottle TotalThrottle;

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
        

            // Set up the throttle classes (min, max, current) in bytes
            ResendThrottle = new PacketThrottle(5000, 100000, 50000);
            LandThrottle = new PacketThrottle(1000, 100000, 100000);
            WindThrottle = new PacketThrottle(1000, 100000, 10000);
            CloudThrottle = new PacketThrottle(1000, 100000, 50000);
            TaskThrottle = new PacketThrottle(1000, 800000, 100000);
            AssetThrottle = new PacketThrottle(1000, 800000, 80000);
            TextureThrottle = new PacketThrottle(1000, 800000, 100000);
            // Total Throttle trumps all
            // Number of bytes allowed to go out per second. (256kbps per client) 
            TotalThrottle = new PacketThrottle(0, 162144, 1536000);
            
            // TIMERS needed for this
            throttleTimer = new Timer((int)(throttletimems/throttleTimeDivisor));
            throttleTimer.Elapsed += new ElapsedEventHandler(throttleTimer_Elapsed);
            throttleTimer.Start();
        }

        private void ResetCounters()
        {
            ResendThrottle.Reset();
            LandThrottle.Reset();
            WindThrottle.Reset();
            CloudThrottle.Reset();
            TaskThrottle.Reset();
            AssetThrottle.Reset();
            TextureThrottle.Reset();
            TotalThrottle.Reset();
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
            while (TotalThrottle.UnderLimit() && PacketsWaiting() && 
                   (throttleLoops <= MaxThrottleLoops))
            {
                throttleLoops++;
                //Now comes the fun part..   we dump all our elements into PacketQueue that we've saved up.
                if (ResendThrottle.UnderLimit() && ResendOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = ResendOutgoingPacketQueue.Dequeue();

                    SendQueue.Enqueue(qpack);
                    TotalThrottle.Add(qpack.Packet.ToBytes().Length);
                    ResendThrottle.Add(qpack.Packet.ToBytes().Length);
                }
                if (LandThrottle.UnderLimit() && LandOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = LandOutgoingPacketQueue.Dequeue();

                    SendQueue.Enqueue(qpack);
                    TotalThrottle.Add(qpack.Packet.ToBytes().Length);
                    LandThrottle.Add(qpack.Packet.ToBytes().Length);
                }
                if (WindThrottle.UnderLimit() && WindOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = WindOutgoingPacketQueue.Dequeue();

                    SendQueue.Enqueue(qpack);
                    TotalThrottle.Add(qpack.Packet.ToBytes().Length);
                    WindThrottle.Add(qpack.Packet.ToBytes().Length);
                }
                if (CloudThrottle.UnderLimit() && CloudOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = CloudOutgoingPacketQueue.Dequeue();

                    SendQueue.Enqueue(qpack);
                    TotalThrottle.Add(qpack.Packet.ToBytes().Length);
                    CloudThrottle.Add(qpack.Packet.ToBytes().Length);
                }
                if (TaskThrottle.UnderLimit() && TaskOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = TaskOutgoingPacketQueue.Dequeue();

                    SendQueue.Enqueue(qpack);
                    TotalThrottle.Add(qpack.Packet.ToBytes().Length);
                    TaskThrottle.Add(qpack.Packet.ToBytes().Length);
                }
                if (TextureThrottle.UnderLimit() && TextureOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = TextureOutgoingPacketQueue.Dequeue();

                    SendQueue.Enqueue(qpack);
                    TotalThrottle.Add(qpack.Packet.ToBytes().Length);
                    TextureThrottle.Add(qpack.Packet.ToBytes().Length);
                }
                if (AssetThrottle.UnderLimit() && AssetOutgoingPacketQueue.Count > 0)
                {
                    QueItem qpack = AssetOutgoingPacketQueue.Dequeue();

                    SendQueue.Enqueue(qpack);
                    TotalThrottle.Add(qpack.Packet.ToBytes().Length);
                    AssetThrottle.Add(qpack.Packet.ToBytes().Length);
                }

            }

        }


    }

    
    
}