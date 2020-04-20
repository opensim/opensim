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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OSHttpServer
{
    /// <summary>
    /// Timeout Manager.   Checks for dead clients.  Clients with open connections that are not doing anything.   Closes sessions opened with keepalive.
    /// </summary>
    public static class ContextTimeoutManager
    {
        /// <summary>
        /// Use a Thread or a Timer to monitor the ugly
        /// </summary>
        private static Thread m_internalThread = null;
        private static object m_threadLock = new object();
        private static ConcurrentQueue<HttpClientContext> m_contexts = new ConcurrentQueue<HttpClientContext>();
        private static ConcurrentQueue<HttpClientContext> m_highPrio = new ConcurrentQueue<HttpClientContext>();
        private static ConcurrentQueue<HttpClientContext> m_midPrio = new ConcurrentQueue<HttpClientContext>();
        private static ConcurrentQueue<HttpClientContext> m_lowPrio = new ConcurrentQueue<HttpClientContext>();
        private static AutoResetEvent m_processWaitEven = new AutoResetEvent(false);
        private static bool m_shuttingDown;

        private static int m_ActiveSendingCount;
        private static double m_lastTimeOutCheckTime = 0;
        private static double m_lastSendCheckTime = 0;

        const int m_maxBandWidth = 10485760; //80Mbps
        const int m_maxConcurrenSend = 32;

        static ContextTimeoutManager()
        {
            TimeStampClockPeriod = 1.0 / (double)Stopwatch.Frequency;
            TimeStampClockPeriodMS = 1e3 / (double)Stopwatch.Frequency;
        }

        public static void Start()
        {
            lock (m_threadLock)
            {
                if (m_internalThread != null)
                    return;

                m_lastTimeOutCheckTime = GetTimeStampMS();
                m_internalThread = new Thread(ThreadRunProcess);
                m_internalThread.Priority = ThreadPriority.Normal;
                m_internalThread.IsBackground = true;
                m_internalThread.CurrentCulture = new CultureInfo("en-US", false);
                m_internalThread.Name = "HttpServerMain";
                m_internalThread.Start();
            }
        }

        public static void Stop()
        {
            m_shuttingDown = true;
            m_internalThread.Join();
            ProcessShutDown();
        }

        private static void ThreadRunProcess()
        {
            while (!m_shuttingDown)
            {
                m_processWaitEven.WaitOne(500);

                if(m_shuttingDown)
                    return;

                double now = GetTimeStamp();
                if(m_contexts.Count > 0)
                {
                    ProcessSendQueues(now);

                    if (now - m_lastTimeOutCheckTime > 1.0)
                    {
                        ProcessContextTimeouts();
                        m_lastTimeOutCheckTime = now;
                    }
                }
                else
                    m_lastTimeOutCheckTime = now;
            }
        }

        public static void ProcessShutDown()
        {
            try
            {
                SocketError disconnectError = SocketError.HostDown;
                for (int i = 0; i < m_contexts.Count; i++)
                {
                    HttpClientContext context = null;
                    if (m_contexts.TryDequeue(out context))
                    {
                        try
                        {
                            context.Disconnect(disconnectError);
                        }
                        catch { }
                    }
                }
                m_processWaitEven.Dispose();
                m_processWaitEven = null;
            }
            catch
            {
                // We can't let this crash.
            }
        }

        public static void ProcessSendQueues(double now)
        {
            int inqueues = m_highPrio.Count + m_midPrio.Count + m_lowPrio.Count;
            if(inqueues == 0)
                return;

            double dt = now - m_lastSendCheckTime;
            m_lastSendCheckTime = now;

            int totalSending = m_ActiveSendingCount;

            int curConcurrentLimit = m_maxConcurrenSend - totalSending;
            if(curConcurrentLimit <= 0)
                return;

            if(curConcurrentLimit > inqueues)
                curConcurrentLimit = inqueues;

            if (dt > 0.5)
                dt = 0.5;

            dt /= curConcurrentLimit;
            int curbytesLimit = (int)(m_maxBandWidth * dt);
            if(curbytesLimit < 8192)
                curbytesLimit = 8192;

            HttpClientContext ctx;
            int sent;
            while (curConcurrentLimit > 0)
            {
                sent = 0;
                while (m_highPrio.TryDequeue(out ctx))
                {
                    if(TrySend(ctx, curbytesLimit))
                        m_highPrio.Enqueue(ctx);

                    if (m_shuttingDown)
                        return;
                    --curConcurrentLimit;
                    if (++sent == 4)
                        break;
                }

                sent = 0;
                while(m_midPrio.TryDequeue(out ctx))
                {
                    if(TrySend(ctx, curbytesLimit))
                        m_midPrio.Enqueue(ctx);

                    if (m_shuttingDown)
                        return;
                    --curConcurrentLimit;
                    if (++sent >= 2)
                        break;
                }

                if (m_lowPrio.TryDequeue(out ctx))
                {
                    --curConcurrentLimit;
                    if(TrySend(ctx, curbytesLimit))
                        m_lowPrio.Enqueue(ctx);
                }

                if (m_shuttingDown)
                    return;
            }
        }

        private static bool TrySend(HttpClientContext ctx, int bytesLimit)
        {
            if(!ctx.CanSend())
                return false;

            return ctx.TrySendResponse(bytesLimit);
        }

        /// <summary>
        /// Causes the watcher to immediately check the connections. 
        /// </summary>
        public static void ProcessContextTimeouts()
        {
            try
            {
                for (int i = 0; i < m_contexts.Count; i++)
                {
                    if (m_shuttingDown)
                        return;
                    if (m_contexts.TryDequeue(out HttpClientContext context))
                    {
                        if (!ContextTimedOut(context, out SocketError disconnectError))
                            m_contexts.Enqueue(context);
                        else if(disconnectError != SocketError.InProgress)
                            context.Disconnect(disconnectError);
                    }
                }
            }
            catch
            {
                // We can't let this crash.
            }
        }

        private static bool ContextTimedOut(HttpClientContext context, out SocketError disconnectError)
        {
            disconnectError = SocketError.InProgress;

            // First our error conditions
            if (context.contextID < 0 || context.StopMonitoring || context.StreamPassedOff)
                return true;

            int nowMS = EnvironmentTickCount();

            // First we check first contact line
            if (!context.FirstRequestLineReceived)
            {
                if (EnvironmentTickCountAdd(context.TimeoutFirstLine, context.LastActivityTimeMS) < nowMS)
                {
                    disconnectError = SocketError.TimedOut;
                    return true;
                }
                return false;
            }

            // First we check first contact request
            if (!context.FullRequestReceived)
            {
                if (EnvironmentTickCountAdd(context.TimeoutRequestReceived, context.LastActivityTimeMS) < nowMS)
                {
                    disconnectError = SocketError.TimedOut;
                    return true;
                }
                return false;
            }

            if (context.TriggerKeepalive)
            {
                context.TriggerKeepalive = false;
                context.MonitorKeepaliveStartMS = nowMS + 1;
                return false;
            }

            if (context.MonitorKeepaliveStartMS != 0)
            {
                if (EnvironmentTickCountAdd(context.TimeoutKeepAlive, context.MonitorKeepaliveStartMS) < nowMS)
                {
                    if(context.IsClosing)
                        disconnectError = SocketError.Success;
                    else
                        disconnectError = SocketError.TimedOut;
                    context.MonitorKeepaliveStartMS = 0;
                    return true;
                }
                return false;
            }

            if (EnvironmentTickCountAdd(context.TimeoutMaxIdle, context.LastActivityTimeMS) < nowMS)
            {
                disconnectError = SocketError.TimedOut;
                context.MonitorKeepaliveStartMS = 0;
                return true;
            }
            return false;
        }

        public static void StartMonitoringContext(HttpClientContext context)
        {
            context.LastActivityTimeMS = EnvironmentTickCount();
            m_contexts.Enqueue(context);
        }

        public static void EnqueueSend(HttpClientContext context, int priority, bool notThrottled = true)
        {
            switch(priority)
            {
                case 0:
                    m_highPrio.Enqueue(context);
                    break;
                case 1:
                    m_midPrio.Enqueue(context);
                    break;
                case 2:
                    m_lowPrio.Enqueue(context);
                    break;
                default:
                    return;
            }
            if(notThrottled)
                m_processWaitEven.Set();
        }

        public static void ContextEnterActiveSend()
        {
            Interlocked.Increment(ref m_ActiveSendingCount);
        }

        public static void ContextLeaveActiveSend()
        {
            Interlocked.Decrement(ref m_ActiveSendingCount);
        }

        /// <summary>
        /// Environment.TickCount is an int but it counts all 32 bits so it goes positive
        /// and negative every 24.9 days. This trims down TickCount so it doesn't wrap
        /// for the callers. 
        /// This trims it to a 12 day interval so don't let your frame time get too long.
        /// </summary>
        /// <returns></returns>
        public static int EnvironmentTickCount()
        {
            return Environment.TickCount & EnvironmentTickCountMask;
        }
        const int EnvironmentTickCountMask = 0x3fffffff;

        /// <summary>
        /// Environment.TickCount is an int but it counts all 32 bits so it goes positive
        /// and negative every 24.9 days. Subtracts the passed value (previously fetched by
        /// 'EnvironmentTickCount()') and accounts for any wrapping.
        /// </summary>
        /// <param name="newValue"></param>
        /// <param name="prevValue"></param>
        /// <returns>subtraction of passed prevValue from current Environment.TickCount</returns>
        public static int EnvironmentTickCountSubtract(Int32 newValue, Int32 prevValue)
        {
            int diff = newValue - prevValue;
            return (diff >= 0) ? diff : (diff + EnvironmentTickCountMask + 1);
        }

        /// <summary>
        /// Environment.TickCount is an int but it counts all 32 bits so it goes positive
        /// and negative every 24.9 days. Subtracts the passed value (previously fetched by
        /// 'EnvironmentTickCount()') and accounts for any wrapping.
        /// </summary>
        /// <param name="newValue"></param>
        /// <param name="prevValue"></param>
        /// <returns>subtraction of passed prevValue from current Environment.TickCount</returns>
        public static int EnvironmentTickCountAdd(Int32 newValue, Int32 prevValue)
        {
            int ret = newValue + prevValue;
            return (ret >= 0) ? ret : (ret + EnvironmentTickCountMask + 1);
        }

        public static double TimeStampClockPeriodMS;
        public static double TimeStampClockPeriod;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static double GetTimeStamp()
        {
            return Stopwatch.GetTimestamp() * TimeStampClockPeriod;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static double GetTimeStampMS()
        {
            return Stopwatch.GetTimestamp() * TimeStampClockPeriodMS;
        }

        // doing math in ticks is usefull to avoid loss of resolution
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static long GetTimeStampTicks()
        {
            return Stopwatch.GetTimestamp();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static double TimeStampTicksToMS(long ticks)
        {
            return ticks * TimeStampClockPeriodMS;
        }

    }
}
