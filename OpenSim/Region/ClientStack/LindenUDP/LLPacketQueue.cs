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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Reflection;
using System.Threading;
using System.Timers;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Statistics;
using OpenSim.Framework.Statistics.Interfaces;
using Timer=System.Timers.Timer;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public class LLPacketQueue : IPullStatsProvider
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Is queueing enabled at all?
        /// </summary>
        private bool m_enabled = true;

        private OpenSim.Framework.BlockingQueue<LLQueItem> SendQueue;

        private Queue<LLQueItem> IncomingPacketQueue;
        private Queue<LLQueItem> OutgoingPacketQueue;
        private Queue<LLQueItem> ResendOutgoingPacketQueue;
        private Queue<LLQueItem> LandOutgoingPacketQueue;
        private Queue<LLQueItem> WindOutgoingPacketQueue;
        private Queue<LLQueItem> CloudOutgoingPacketQueue;
        private Queue<LLQueItem> TaskOutgoingPacketQueue;
        private Queue<LLQueItem> TaskLowpriorityPacketQueue;
        private Queue<LLQueItem> TextureOutgoingPacketQueue;
        private Queue<LLQueItem> AssetOutgoingPacketQueue;

        // private Dictionary<uint, uint> PendingAcks = new Dictionary<uint, uint>();
        // private Dictionary<uint, Packet> NeedAck = new Dictionary<uint, Packet>();

        // All throttle times and number of bytes are calculated by dividing by this value
        // This value also determines how many times per throttletimems the timer will run
        // If throttleimems is 1000 ms, then the timer will fire every 1000/7 milliseconds

        private float throttleMultiplier = 2.0f; // Default value really doesn't matter.
        private int throttleTimeDivisor = 7;

        private int throttletimems = 1000;

        internal LLPacketThrottle ResendThrottle;
        internal LLPacketThrottle LandThrottle;
        internal LLPacketThrottle WindThrottle;
        internal LLPacketThrottle CloudThrottle;
        internal LLPacketThrottle TaskThrottle;
        internal LLPacketThrottle AssetThrottle;
        internal LLPacketThrottle TextureThrottle;
        internal LLPacketThrottle TotalThrottle;
        
        private Dictionary<uint,int> contents = new Dictionary<uint, int>();

        /// <summary>
        /// The number of packets in the OutgoingPacketQueue
        /// 
        /// </summary>
        internal int TextureOutgoingPacketQueueCount
        {
            get 
            { 
                if (TextureOutgoingPacketQueue == null)
                    return 0;
                return TextureOutgoingPacketQueue.Count;
            }
        }

        // private long LastThrottle;
        // private long ThrottleInterval;
        private Timer throttleTimer;

        private UUID m_agentId;

        public LLPacketQueue(UUID agentId, ClientStackUserSettings userSettings)
        {
            // While working on this, the BlockingQueue had me fooled for a bit.
            // The Blocking queue causes the thread to stop until there's something
            // in it to process.  it's an on-purpose threadlock though because
            // without it, the clientloop will suck up all sim resources.

            SendQueue = new OpenSim.Framework.BlockingQueue<LLQueItem>();

            IncomingPacketQueue = new Queue<LLQueItem>();
            OutgoingPacketQueue = new Queue<LLQueItem>();
            ResendOutgoingPacketQueue = new Queue<LLQueItem>();
            LandOutgoingPacketQueue = new Queue<LLQueItem>();
            WindOutgoingPacketQueue = new Queue<LLQueItem>();
            CloudOutgoingPacketQueue = new Queue<LLQueItem>();
            TaskOutgoingPacketQueue = new Queue<LLQueItem>();
            TaskLowpriorityPacketQueue = new Queue<LLQueItem>();
            TextureOutgoingPacketQueue = new Queue<LLQueItem>();
            AssetOutgoingPacketQueue = new Queue<LLQueItem>();

            // Store the throttle multiplier for posterity.
            throttleMultiplier = userSettings.ClientThrottleMultipler;


            int throttleMaxBPS = 1500000;
            if (userSettings.TotalThrottleSettings != null)
                throttleMaxBPS = userSettings.TotalThrottleSettings.Max;

            // Set up the throttle classes (min, max, current) in bits per second
            ResendThrottle = new LLPacketThrottle(5000, throttleMaxBPS / 15, 16000, userSettings.ClientThrottleMultipler);
            LandThrottle = new LLPacketThrottle(1000, throttleMaxBPS / 15, 2000, userSettings.ClientThrottleMultipler);
            WindThrottle = new LLPacketThrottle(0, throttleMaxBPS / 15, 0, userSettings.ClientThrottleMultipler);
            CloudThrottle = new LLPacketThrottle(0, throttleMaxBPS / 15, 0, userSettings.ClientThrottleMultipler);
            TaskThrottle = new LLPacketThrottle(1000, throttleMaxBPS / 2, 3000, userSettings.ClientThrottleMultipler);
            AssetThrottle = new LLPacketThrottle(1000, throttleMaxBPS / 2, 1000, userSettings.ClientThrottleMultipler);
            TextureThrottle = new LLPacketThrottle(1000, throttleMaxBPS / 2, 4000, userSettings.ClientThrottleMultipler);


            // Total Throttle trumps all - it is the number of bits in total that are allowed to go out per second.


            ThrottleSettings totalThrottleSettings = userSettings.TotalThrottleSettings;
            if (null == totalThrottleSettings)
            {
                totalThrottleSettings = new ThrottleSettings(0, throttleMaxBPS, 28000);
            }

            TotalThrottle
                = new LLPacketThrottle(
                    totalThrottleSettings.Min, totalThrottleSettings.Max, totalThrottleSettings.Current,
                    userSettings.ClientThrottleMultipler);

            throttleTimer = new Timer((int)(throttletimems / throttleTimeDivisor));
            throttleTimer.Elapsed += ThrottleTimerElapsed;
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

        public void Enqueue(LLQueItem item)
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

            if (item.Incoming)
            {
                SendQueue.PriorityEnqueue(item);
                return;
            }

            if (item.Sequence != 0)
                lock (contents)
                {
                    if (contents.ContainsKey(item.Sequence))
                        contents[item.Sequence] += 1;
                    else
                        contents.Add(item.Sequence, 1);
                }

            lock (this)
            {
                switch (item.throttleType & ThrottleOutPacketType.TypeMask)
                {
                    case ThrottleOutPacketType.Resend:
                        ThrottleCheck(ref ResendThrottle, ref ResendOutgoingPacketQueue, item);
                        break;
                    case ThrottleOutPacketType.Texture:
                        ThrottleCheck(ref TextureThrottle, ref TextureOutgoingPacketQueue, item);
                        break;
                    case ThrottleOutPacketType.Task:
                        if ((item.throttleType & ThrottleOutPacketType.LowPriority) != 0)
                            ThrottleCheck(ref TaskThrottle, ref TaskLowpriorityPacketQueue, item);
                        else
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
                        SendQueue.PriorityEnqueue(item);
                        break;
                }
            }
        }

        public LLQueItem Dequeue()
        {
            while (true)
            {
                LLQueItem item = SendQueue.Dequeue();
                if (item == null)
                    return null;
                if (item.Incoming)
                    return item;
                item.TickCount = System.Environment.TickCount;
                if (item.Sequence == 0)
                    return item;
                lock (contents)
                {
                    if (contents.ContainsKey(item.Sequence))
                    {
                        if (contents[item.Sequence] == 1)
                            contents.Remove(item.Sequence);
                        else
                            contents[item.Sequence] -= 1;
                        return item;
                    }
                }
            }
        }

        public void Cancel(uint sequence)
        {
            lock (contents) contents.Remove(sequence);
        }

        public bool Contains(uint sequence)
        {
            lock (contents) return contents.ContainsKey(sequence);
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
                        SendQueue.PriorityEnqueue(TaskOutgoingPacketQueue.Dequeue());
                    }
                    if (TaskLowpriorityPacketQueue.Count > 0)
                    {
                        SendQueue.Enqueue(TaskLowpriorityPacketQueue.Dequeue());
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

        public void WipeClean()
        {
            lock (this)
            {
                ResendOutgoingPacketQueue.Clear();
                LandOutgoingPacketQueue.Clear();
                WindOutgoingPacketQueue.Clear();
                CloudOutgoingPacketQueue.Clear();
                TaskOutgoingPacketQueue.Clear();
                TaskLowpriorityPacketQueue.Clear();
                TextureOutgoingPacketQueue.Clear();
                AssetOutgoingPacketQueue.Clear();
                SendQueue.Clear();
                lock (contents) contents.Clear();
            }
        }

        public void Close()
        {
            Flush();
            WipeClean(); // I'm sure there's a dirty joke in here somewhere. -AFrisby

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
                    TaskLowpriorityPacketQueue.Count > 0 ||
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
                // this variable will be true if there was work done in the last execution of the
                // loop, since each pass through the loop checks the queue length, we no longer 
                // need the check on entering the loop
                bool qchanged = true;
                
                ResetCounters();
                // m_log.Info("[THROTTLE]: Entering Throttle");
                while (TotalThrottle.UnderLimit() && qchanged && throttleLoops <= MaxThrottleLoops)
                {
                    qchanged = false; // We will break out of the loop if no work was accomplished

                    throttleLoops++;
                    //Now comes the fun part..   we dump all our elements into m_packetQueue that we've saved up.
                    if ((ResendOutgoingPacketQueue.Count > 0) && ResendThrottle.UnderLimit())
                    {
                        LLQueItem qpack = ResendOutgoingPacketQueue.Dequeue();

                        SendQueue.Enqueue(qpack);
                        TotalThrottle.AddBytes(qpack.Length);
                        ResendThrottle.AddBytes(qpack.Length);
                        
                        qchanged = true;
                    }
                    
                    if ((LandOutgoingPacketQueue.Count > 0) && LandThrottle.UnderLimit())
                    {
                        LLQueItem qpack = LandOutgoingPacketQueue.Dequeue();

                        SendQueue.Enqueue(qpack);
                        TotalThrottle.AddBytes(qpack.Length);
                        LandThrottle.AddBytes(qpack.Length);
                        qchanged = true;
                    }
                    
                    if ((WindOutgoingPacketQueue.Count > 0) && WindThrottle.UnderLimit())
                    {
                        LLQueItem qpack = WindOutgoingPacketQueue.Dequeue();

                        SendQueue.Enqueue(qpack);
                        TotalThrottle.AddBytes(qpack.Length);
                        WindThrottle.AddBytes(qpack.Length);
                        qchanged = true;
                    }
                    
                    if ((CloudOutgoingPacketQueue.Count > 0) && CloudThrottle.UnderLimit())
                    {
                        LLQueItem qpack = CloudOutgoingPacketQueue.Dequeue();

                        SendQueue.Enqueue(qpack);
                        TotalThrottle.AddBytes(qpack.Length);
                        CloudThrottle.AddBytes(qpack.Length);
                        qchanged = true;
                    }
                    
                    if ((TaskOutgoingPacketQueue.Count > 0 || TaskLowpriorityPacketQueue.Count > 0) && TaskThrottle.UnderLimit())
                    {
                        LLQueItem qpack;
                        if (TaskOutgoingPacketQueue.Count > 0)
                        {
                            qpack = TaskOutgoingPacketQueue.Dequeue();
                            SendQueue.PriorityEnqueue(qpack);
                        }
                        else
                        {
                            qpack = TaskLowpriorityPacketQueue.Dequeue();
                            SendQueue.Enqueue(qpack);
                        }

                        TotalThrottle.AddBytes(qpack.Length);
                        TaskThrottle.AddBytes(qpack.Length);
                        qchanged = true;
                    }
                    
                    if ((TextureOutgoingPacketQueue.Count > 0) && TextureThrottle.UnderLimit())
                    {
                        LLQueItem qpack = TextureOutgoingPacketQueue.Dequeue();

                        SendQueue.Enqueue(qpack);
                        TotalThrottle.AddBytes(qpack.Length);
                        TextureThrottle.AddBytes(qpack.Length);
                        qchanged = true;
                    }
                    
                    if ((AssetOutgoingPacketQueue.Count > 0) && AssetThrottle.UnderLimit())
                    {
                        LLQueItem qpack = AssetOutgoingPacketQueue.Dequeue();

                        SendQueue.Enqueue(qpack);
                        TotalThrottle.AddBytes(qpack.Length);
                        AssetThrottle.AddBytes(qpack.Length);
                        qchanged = true;
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

        private void ThrottleCheck(ref LLPacketThrottle throttle, ref Queue<LLQueItem> q, LLQueItem item)
        {
            // The idea..  is if the packet throttle queues are empty
            // and the client is under throttle for the type.  Queue
            // it up directly.  This basically short cuts having to
            // wait for the timer to fire to put things into the
            // output queue

            if ((q.Count == 0) && (throttle.UnderLimit()))
            {
                try
                {
                    Monitor.Enter(this);
                    throttle.AddBytes(item.Length);
                    TotalThrottle.AddBytes(item.Length);
                    SendQueue.Enqueue(item);
                }
                catch (Exception e)
                {
                    // Probably a serialization exception
                    m_log.WarnFormat("ThrottleCheck: {0}", e.ToString());
                }
                finally
                {
                    Monitor.Pulse(this);
                    Monitor.Exit(this);
                }
            }
            else
            {
                q.Enqueue(item);
            }
        }

        private static int ScaleThrottle(int value, int curmax, int newmax)
        {
            return (int)((value / (float)curmax) * newmax);
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
            // From mantis http://opensimulator.org/mantis/view.php?id=1374
            // it appears that sometimes we are receiving empty throttle byte arrays.
            // TODO: Investigate this behaviour
            if (throttle.Length == 0)
            {
                m_log.Warn("[PACKET QUEUE]: SetThrottleFromClient unexpectedly received a throttle byte array containing no elements!");
                return;
            }

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

            // values gotten from OpenMetaverse.org/wiki/Throttle.  Thanks MW_
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
              m_log.Info("[CLIENT]: Client AgentThrottle - Got throttle:resendbits=" + tResend +
              " landbits=" + tLand +
              " windbits=" + tWind +
              " cloudbits=" + tCloud +
              " taskbits=" + tTask +
              " texturebits=" + tTexture +
              " Assetbits=" + tAsset +
              " Allbits=" + tall);
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
//            else if (tall < 1)
//            {
//                // client is stupid, penalize him by minning everything
//                ResendThrottle.Throttle = ResendThrottle.Min;
//                LandThrottle.Throttle = LandThrottle.Min;
//                WindThrottle.Throttle = WindThrottle.Min;
//                CloudThrottle.Throttle = CloudThrottle.Min;
//                TaskThrottle.Throttle = TaskThrottle.Min;
//                TextureThrottle.Throttle = TextureThrottle.Min;
//                AssetThrottle.Throttle = AssetThrottle.Min;
//                TotalThrottle.Throttle = TotalThrottle.Min;
//            }
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
//            ResetCounters(); // DO NOT reset, better to send less for one period than more
        }

        // See IPullStatsProvider
        public string GetStats()
        {
            return string.Format("{0,7} {1,7} {2,7} {3,7} {4,7} {5,7} {6,7} {7,7} {8,7} {9,7}",
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

        public LLQueItem[] GetQueueArray()
        {
            return SendQueue.GetQueueArray();
        }

        public float ThrottleMultiplier
        {
            get { return throttleMultiplier; }
        }
    }
}
