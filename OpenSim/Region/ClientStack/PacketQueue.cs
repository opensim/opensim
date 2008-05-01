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
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Statistics;
using OpenSim.Framework.Statistics.Interfaces;
using Timer=System.Timers.Timer;

namespace OpenSim.Region.ClientStack
{
    public class PacketQueue : IPullStatsProvider
    {
        //private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_enabled = true;

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

        // private long LastThrottle;
        // private long ThrottleInterval;
        private Timer throttleTimer;
        
        private LLUUID m_agentId;

        public PacketQueue(LLUUID agentId)
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
            ResendThrottle = new PacketThrottle(5000, 100000, 16000);
            LandThrottle = new PacketThrottle(1000, 100000, 2000);
            WindThrottle = new PacketThrottle(1000, 100000, 1000);
            CloudThrottle = new PacketThrottle(1000, 100000, 1000);
            TaskThrottle = new PacketThrottle(1000, 800000, 3000);
            AssetThrottle = new PacketThrottle(1000, 800000, 1000);
            TextureThrottle = new PacketThrottle(1000, 800000, 4000);
            // Total Throttle trumps all
            // Number of bytes allowed to go out per second. (256kbps per client) 
            TotalThrottle = new PacketThrottle(0, 1500000, 28000);

            throttleTimer = new Timer((int) (throttletimems/throttleTimeDivisor));
            throttleTimer.Elapsed += new ElapsedEventHandler(ThrottleTimerElapsed);
            throttleTimer.Start();

            // TIMERS needed for this
            // LastThrottle = DateTime.Now.Ticks;
            // ThrottleInterval = (long)(throttletimems/throttleTimeDivisor);
            
            m_agentId = agentId;
            
            if (StatsManager.SimExtraStats != null)            
            {
                StatsManager.SimExtraStats.RegisterPacketQueueStatsProvider(m_agentId, this);
            }
        }

        /* STANDARD QUEUE MANIPULATION INTERFACES */


        public void Enqueue(QueItem item)
        {
            if (!m_enabled)
            {
                return;
            }
            // We could micro lock, but that will tend to actually
            // probably be worse than just synchronizing on SendQueue

            if (item == null)
            {
                SendQueue.Enqueue(item);
                return;
            }

            lock (this) {
                switch (item.throttleType)
                {
                    case ThrottleOutPacketType.Resend:
                        ThrottleCheck(ref ResendThrottle, ref ResendOutgoingPacketQueue, item);
                        break;
                    case ThrottleOutPacketType.Texture:
                        ThrottleCheck(ref TextureThrottle, ref TextureOutgoingPacketQueue, item);
                        break;
                    case ThrottleOutPacketType.Task:
                        ThrottleCheck(ref TaskThrottle, ref TaskOutgoingPacketQueue, item);
                        break;
                    case ThrottleOutPacketType.Land:
                        ThrottleCheck(ref LandThrottle, ref LandOutgoingPacketQueue, item);
                        break;
                    case ThrottleOutPacketType.Asset:
                        ThrottleCheck(ref AssetThrottle, ref AssetOutgoingPacketQueue, item);
                        break;
                    case ThrottleOutPacketType.Cloud:
                        ThrottleCheck(ref CloudThrottle, ref CloudOutgoingPacketQueue, item);
                        break;
                    case ThrottleOutPacketType.Wind:
                        ThrottleCheck(ref WindThrottle, ref WindOutgoingPacketQueue, item);
                        break;

                    default:
                        // Acknowledgements and other such stuff should go directly to the blocking Queue
                        // Throttling them may and likely 'will' be problematic
                        SendQueue.Enqueue(item);
                        break;
                }
            }
        }

        public QueItem Dequeue()
        {
            return SendQueue.Dequeue();
        }

        public void Flush()
        {
            lock (this)
            {
                while (PacketsWaiting())
                {
                    //Now comes the fun part..   we dump all our elements into m_packetQueue that we've saved up.
                    if (ResendOutgoingPacketQueue.Count > 0)
                    {
                        SendQueue.Enqueue(ResendOutgoingPacketQueue.Dequeue());
                    }
                    if (LandOutgoingPacketQueue.Count > 0)
                    {
                        SendQueue.Enqueue(LandOutgoingPacketQueue.Dequeue());
                    }
                    if (WindOutgoingPacketQueue.Count > 0)
                    {
                        SendQueue.Enqueue(WindOutgoingPacketQueue.Dequeue());
                    }
                    if (CloudOutgoingPacketQueue.Count > 0)
                    {
                        SendQueue.Enqueue(CloudOutgoingPacketQueue.Dequeue());
                    }
                    if (TaskOutgoingPacketQueue.Count > 0)
                    {
                        SendQueue.Enqueue(TaskOutgoingPacketQueue.Dequeue());
                    }
                    if (TextureOutgoingPacketQueue.Count > 0)
                    {
                        SendQueue.Enqueue(TextureOutgoingPacketQueue.Dequeue());
                    }
                    if (AssetOutgoingPacketQueue.Count > 0)
                    {
                        SendQueue.Enqueue(AssetOutgoingPacketQueue.Dequeue());
                    }
                }
                // m_log.Info("[THROTTLE]: Processed " + throttleLoops + " packets");
            }
        }

        public void Close()
        {
            Flush();

            m_enabled = false;
            throttleTimer.Stop();
            
            if (StatsManager.SimExtraStats != null)            
            {
                StatsManager.SimExtraStats.DeregisterPacketQueueStatsProvider(m_agentId);
            }            
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

        public void ProcessThrottle()
        {
            // I was considering this..   Will an event fire if the thread it's on is blocked?

            // Then I figured out..  it doesn't really matter..  because this thread won't be blocked for long
            // The General overhead of the UDP protocol gets sent to the queue un-throttled by this
            // so This'll pick up about around the right time.

            int MaxThrottleLoops = 4550; // 50*7 packets can be dequeued at once.
            int throttleLoops = 0;

            // We're going to dequeue all of the saved up packets until 
            // we've hit the throttle limit or there's no more packets to send
            lock (this)
            {
                ResetCounters();
                // m_log.Info("[THROTTLE]: Entering Throttle");
                while (TotalThrottle.UnderLimit() && PacketsWaiting() &&
                       (throttleLoops <= MaxThrottleLoops))
                {
                    throttleLoops++;
                    //Now comes the fun part..   we dump all our elements into m_packetQueue that we've saved up.
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
                // m_log.Info("[THROTTLE]: Processed " + throttleLoops + " packets");
            }
        }

        private void ThrottleTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // just to change the signature, and that ProcessThrottle
            // will be used elsewhere possibly
            ProcessThrottle();
        }

        private void ThrottleCheck(ref PacketThrottle throttle, ref Queue<QueItem> q, QueItem item)
        {
            // The idea..  is if the packet throttle queues are empty
            // and the client is under throttle for the type.  Queue
            // it up directly.  This basically short cuts having to
            // wait for the timer to fire to put things into the
            // output queue

            if ((q.Count == 0) && (throttle.UnderLimit()))
            {
                Monitor.Enter(this);
                throttle.Add(item.Packet.ToBytes().Length);
                TotalThrottle.Add(item.Packet.ToBytes().Length);
                SendQueue.Enqueue(item);
                Monitor.Pulse(this);
                Monitor.Exit(this);
            }
            else
            {
                q.Enqueue(item);
            }
        }


        private static int ScaleThrottle(int value, int curmax, int newmax)
        {
            return (value / curmax) * newmax;
        }

        public byte[] GetThrottlesPacked(float multiplier)
        {
            int singlefloat = 4;
            float tResend = ResendThrottle.Throttle*multiplier;
            float tLand = LandThrottle.Throttle*multiplier;
            float tWind = WindThrottle.Throttle*multiplier;
            float tCloud = CloudThrottle.Throttle*multiplier;
            float tTask = TaskThrottle.Throttle*multiplier;
            float tTexture = TextureThrottle.Throttle*multiplier;
            float tAsset = AssetThrottle.Throttle*multiplier;

            byte[] throttles = new byte[singlefloat*7];
            int i = 0;
            Buffer.BlockCopy(BitConverter.GetBytes(tResend), 0, throttles, singlefloat*i, singlefloat);
            i++;
            Buffer.BlockCopy(BitConverter.GetBytes(tLand), 0, throttles, singlefloat*i, singlefloat);
            i++;
            Buffer.BlockCopy(BitConverter.GetBytes(tWind), 0, throttles, singlefloat*i, singlefloat);
            i++;
            Buffer.BlockCopy(BitConverter.GetBytes(tCloud), 0, throttles, singlefloat*i, singlefloat);
            i++;
            Buffer.BlockCopy(BitConverter.GetBytes(tTask), 0, throttles, singlefloat*i, singlefloat);
            i++;
            Buffer.BlockCopy(BitConverter.GetBytes(tTexture), 0, throttles, singlefloat*i, singlefloat);
            i++;
            Buffer.BlockCopy(BitConverter.GetBytes(tAsset), 0, throttles, singlefloat*i, singlefloat);

            return throttles;
        }

        public void SetThrottleFromClient(byte[] throttle)
        {
            int tResend = -1;
            int tLand = -1;
            int tWind = -1;
            int tCloud = -1;
            int tTask = -1;
            int tTexture = -1;
            int tAsset = -1;
            int tall = -1;
            int singlefloat = 4;

            //Agent Throttle Block contains 7 single floatingpoint values.
            int j = 0;

            // Some Systems may be big endian...  
            // it might be smart to do this check more often... 
            if (!BitConverter.IsLittleEndian)
                for (int i = 0; i < 7; i++)
                    Array.Reverse(throttle, j + i*singlefloat, singlefloat);

            // values gotten from libsecondlife.org/wiki/Throttle.  Thanks MW_
            // bytes
            // Convert to integer, since..   the full fp space isn't used.
            tResend = (int) BitConverter.ToSingle(throttle, j);
            j += singlefloat;
            tLand = (int) BitConverter.ToSingle(throttle, j);
            j += singlefloat;
            tWind = (int) BitConverter.ToSingle(throttle, j);
            j += singlefloat;
            tCloud = (int) BitConverter.ToSingle(throttle, j);
            j += singlefloat;
            tTask = (int) BitConverter.ToSingle(throttle, j);
            j += singlefloat;
            tTexture = (int) BitConverter.ToSingle(throttle, j);
            j += singlefloat;
            tAsset = (int) BitConverter.ToSingle(throttle, j);

            tall = tResend + tLand + tWind + tCloud + tTask + tTexture + tAsset;
            /*
              m_log.Info("[CLIENT]: Client AgentThrottle - Got throttle:resendbytes=" + tResend +
              " landbytes=" + tLand +
              " windbytes=" + tWind +
              " cloudbytes=" + tCloud +
              " taskbytes=" + tTask +
              " texturebytes=" + tTexture +
              " Assetbytes=" + tAsset +
              " Allbytes=" + tall);
            */

            // Total Sanity
            // Make sure that the client sent sane total values.

            // If the client didn't send acceptable values....
            // Scale the clients values down until they are acceptable.

            if (tall <= TotalThrottle.Max)
            {
                ResendThrottle.Throttle = tResend;
                LandThrottle.Throttle = tLand;
                WindThrottle.Throttle = tWind;
                CloudThrottle.Throttle = tCloud;
                TaskThrottle.Throttle = tTask;
                TextureThrottle.Throttle = tTexture;
                AssetThrottle.Throttle = tAsset;
                TotalThrottle.Throttle = tall;
            }
            else if (tall < 1)
            {
                // client is stupid, penalize him by minning everything
                ResendThrottle.Throttle = ResendThrottle.Min;
                LandThrottle.Throttle = LandThrottle.Min;
                WindThrottle.Throttle = WindThrottle.Min;
                CloudThrottle.Throttle = CloudThrottle.Min;
                TaskThrottle.Throttle = TaskThrottle.Min;
                TextureThrottle.Throttle = TextureThrottle.Min;
                AssetThrottle.Throttle = AssetThrottle.Min;
                TotalThrottle.Throttle = TotalThrottle.Min;
            }
            else
            {
                // we're over so figure out percentages and use those
                ResendThrottle.Throttle = tResend;

                LandThrottle.Throttle = ScaleThrottle(tLand, tall, TotalThrottle.Max);
                WindThrottle.Throttle = ScaleThrottle(tWind, tall, TotalThrottle.Max);
                CloudThrottle.Throttle = ScaleThrottle(tCloud, tall, TotalThrottle.Max);
                TaskThrottle.Throttle = ScaleThrottle(tTask, tall, TotalThrottle.Max);
                TextureThrottle.Throttle = ScaleThrottle(tTexture, tall, TotalThrottle.Max);
                AssetThrottle.Throttle = ScaleThrottle(tAsset, tall, TotalThrottle.Max);
                TotalThrottle.Throttle = TotalThrottle.Max;
            }
            // effectively wiggling the slider causes things reset
            ResetCounters();
        }
        
        // See IPullStatsProvider
        public string GetStats()
        {
            return string.Format("{0,7}  {1,7}  {2,7}  {3,7}  {4,7}  {5,7}  {6,7}  {7,7}  {8,7}  {9,7}",
                                 SendQueue.Count(),
                                 IncomingPacketQueue.Count,
                                 OutgoingPacketQueue.Count,
                                 ResendOutgoingPacketQueue.Count,
                                 LandOutgoingPacketQueue.Count,
                                 WindOutgoingPacketQueue.Count,
                                 CloudOutgoingPacketQueue.Count,
                                 TaskOutgoingPacketQueue.Count,
                                 TextureOutgoingPacketQueue.Count,
                                 AssetOutgoingPacketQueue.Count);                                     
        }

        public QueItem[] GetQueueArray()
        {
            return SendQueue.GetQueueArray();
        }
    }
}
