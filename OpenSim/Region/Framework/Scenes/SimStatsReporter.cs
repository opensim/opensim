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
using System.Timers;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// Collect statistics from the scene to send to the client and for access by other monitoring tools.
    /// </summary>
    /// <remarks>
    /// FIXME: This should be a monitoring region module
    /// </remarks>
    public class SimStatsReporter
    {
        private static readonly log4net.ILog m_log
            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public const string LastReportedObjectUpdateStatName = "LastReportedObjectUpdates";
        public const string SlowFramesStatName = "SlowFrames";

        public delegate void SendStatResult(SimStats stats);

        public delegate void YourStatsAreWrong();

        public event SendStatResult OnSendStatsResult;

        public event YourStatsAreWrong OnStatsIncorrect;

        private SendStatResult handlerSendStatResult;

        private YourStatsAreWrong handlerStatsIncorrect;

        /// <summary>
        /// These are the IDs of stats sent in the StatsPacket to the viewer.
        /// </summary>
        /// <remarks>
        /// Some of these are not relevant to OpenSimulator since it is architected differently to other simulators
        /// (e.g. script instructions aren't executed as part of the frame loop so 'script time' is tricky).
        /// </remarks>
        public enum Stats : uint
        {
            TimeDilation = 0,
            SimFPS = 1,
            PhysicsFPS = 2,
            AgentUpdates = 3,
            FrameMS = 4,
            NetMS = 5,
            OtherMS = 6,
            PhysicsMS = 7,
            AgentMS = 8,
            ImageMS = 9,
            ScriptMS = 10,
            TotalPrim = 11,
            ActivePrim = 12,
            Agents = 13,
            ChildAgents = 14,
            ActiveScripts = 15,
            ScriptLinesPerSecond = 16,
            InPacketsPerSecond = 17,
            OutPacketsPerSecond = 18,
            PendingDownloads = 19,
            PendingUploads = 20,
            VirtualSizeKb = 21,
            ResidentSizeKb = 22,
            PendingLocalUploads = 23,
            UnAckedBytes = 24,
            PhysicsPinnedTasks = 25,
            PhysicsLodTasks = 26,
            SimPhysicsStepMs = 27,
            SimPhysicsShapeMs = 28,
            SimPhysicsOtherMs = 29,
            SimPhysicsMemory = 30,
            ScriptEps = 31,
            SimSpareMs = 32,
            SimSleepMs = 33,
            SimIoPumpTime = 34
        }

        /// <summary>
        /// This is for llGetRegionFPS
        /// </summary>
        public float LastReportedSimFPS
        {
            get { return lastReportedSimFPS; }
        }

        /// <summary>
        /// Number of object updates performed in the last stats cycle
        /// </summary>
        /// <remarks>
        /// This isn't sent out to the client but it is very useful data to detect whether viewers are being sent a
        /// large number of object updates.
        /// </remarks>
        public float LastReportedObjectUpdates { get; private set; }

        public float[] LastReportedSimStats
        {
            get { return lastReportedSimStats; }
        }

        /// <summary>
        /// Number of frames that have taken longer to process than Scene.MIN_FRAME_TIME
        /// </summary>
        public Stat SlowFramesStat { get; private set; }

        /// <summary>
        /// The threshold at which we log a slow frame.
        /// </summary>
        public int SlowFramesStatReportThreshold { get; private set; }

        /// <summary>
        /// Extra sim statistics that are used by monitors but not sent to the client.
        /// </summary>
        /// <value>
        /// The keys are the stat names.
        /// </value>
        private Dictionary<string, float> m_lastReportedExtraSimStats = new Dictionary<string, float>();

        // Sending a stats update every 3 seconds-
        private int m_statsUpdatesEveryMS = 3000;
        private float m_statsUpdateFactor;
        private float m_timeDilation;
        private int m_fps;

        /// <summary>
        /// Number of the last frame on which we processed a stats udpate.
        /// </summary>
        private uint m_lastUpdateFrame;

        /// <summary>
        /// Our nominal fps target, as expected in fps stats when a sim is running normally.
        /// </summary>
        private float m_nominalReportedFps = 55;

        /// <summary>
        /// Parameter to adjust reported scene fps
        /// </summary>
        /// <remarks>
        /// Our scene loop runs slower than other server implementations, apparantly because we work somewhat differently.
        /// However, we will still report an FPS that's closer to what people are used to seeing.  A lower FPS might
        /// affect clients and monitoring scripts/software.
        /// </remarks>
        private float m_reportedFpsCorrectionFactor = 5;

        // saved last reported value so there is something available for llGetRegionFPS 
        private float lastReportedSimFPS;
        private float[] lastReportedSimStats = new float[22];
        private float m_pfps;

        /// <summary>
        /// Number of agent updates requested in this stats cycle
        /// </summary>
        private int m_agentUpdates;

        /// <summary>
        /// Number of object updates requested in this stats cycle
        /// </summary>
        private int m_objectUpdates;

        private int m_frameMS;
        private int m_spareMS;
        private int m_netMS;
        private int m_agentMS;
        private int m_physicsMS;
        private int m_imageMS;
        private int m_otherMS;

//Ckrinke: (3-21-08) Comment out to remove a compiler warning. Bring back into play when needed.
//Ckrinke        private int m_scriptMS = 0;

        private int m_rootAgents;
        private int m_childAgents;
        private int m_numPrim;
        private int m_inPacketsPerSecond;
        private int m_outPacketsPerSecond;
        private int m_activePrim;
        private int m_unAckedBytes;
        private int m_pendingDownloads;
        private int m_pendingUploads = 0;  // FIXME: Not currently filled in
        private int m_activeScripts;
        private int m_scriptLinesPerSecond;

        private int m_objectCapacity = 45000;

        private Scene m_scene;

        private RegionInfo ReportingRegion;

        private Timer m_report = new Timer();

        private IEstateModule estateModule;

        public SimStatsReporter(Scene scene)
        {
            m_scene = scene;
            m_reportedFpsCorrectionFactor = scene.MinFrameTime * m_nominalReportedFps;
            m_statsUpdateFactor = (float)(m_statsUpdatesEveryMS / 1000);
            ReportingRegion = scene.RegionInfo;

            m_objectCapacity = scene.RegionInfo.ObjectCapacity;
            m_report.AutoReset = true;
            m_report.Interval = m_statsUpdatesEveryMS;
            m_report.Elapsed += TriggerStatsHeartbeat;
            m_report.Enabled = true;

            if (StatsManager.SimExtraStats != null)
                OnSendStatsResult += StatsManager.SimExtraStats.ReceiveClassicSimStatsPacket;

            /// At the moment, we'll only report if a frame is over 120% of target, since commonly frames are a bit
            /// longer than ideal (which in itself is a concern).
            SlowFramesStatReportThreshold = (int)Math.Ceiling(m_scene.MinFrameTime * 1000 * 1.2);

            SlowFramesStat
                = new Stat(
                    "SlowFrames",
                    "Slow Frames",
                    " frames",
                    "scene",
                    m_scene.Name,
                    StatVerbosity.Info,
                    "Number of frames where frame time has been significantly longer than the desired frame time.");

            StatsManager.RegisterStat(SlowFramesStat);
        }

        public void Close()
        {
            m_report.Elapsed -= TriggerStatsHeartbeat;
            m_report.Close();
        }

        /// <summary>
        /// Sets the number of milliseconds between stat updates.
        /// </summary>
        /// <param name='ms'></param>
        public void SetUpdateMS(int ms)
        {
            m_statsUpdatesEveryMS = ms;
            m_statsUpdateFactor = (float)(m_statsUpdatesEveryMS / 1000);
            m_report.Interval = m_statsUpdatesEveryMS;
        }

        private void TriggerStatsHeartbeat(object sender, EventArgs args)
        {
            try
            {
                statsHeartBeat(sender, args);
            }
            catch (Exception e)
            {
                m_log.Warn(string.Format(
                    "[SIM STATS REPORTER] Update for {0} failed with exception ",
                    m_scene.RegionInfo.RegionName), e);
            }
        }

        private void statsHeartBeat(object sender, EventArgs e)
        {
            SimStatsPacket.StatBlock[] sb = new SimStatsPacket.StatBlock[22];
            SimStatsPacket.RegionBlock rb = new SimStatsPacket.RegionBlock();
            
            // Know what's not thread safe in Mono... modifying timers.
            // m_log.Debug("Firing Stats Heart Beat");
            lock (m_report)
            {
                uint regionFlags = 0;
                
                try
                {
                    if (estateModule == null)
                        estateModule = m_scene.RequestModuleInterface<IEstateModule>();
                    regionFlags = estateModule != null ? estateModule.GetRegionFlags() : (uint) 0;
                }
                catch (Exception)
                {
                    // leave region flags at 0
                }

#region various statistic googly moogly

                // We're going to lie about the FPS because we've been lying since 2008.  The actual FPS is currently
                // locked at a maximum of 11.  Maybe at some point this can change so that we're not lying.
                int reportedFPS = (int)(m_fps * m_reportedFpsCorrectionFactor);

                // save the reported value so there is something available for llGetRegionFPS 
                lastReportedSimFPS = reportedFPS / m_statsUpdateFactor;

                float physfps = ((m_pfps / 1000));

                //if (physfps > 600)
                //physfps = physfps - (physfps - 600);

                if (physfps < 0)
                    physfps = 0;

#endregion

                m_rootAgents = m_scene.SceneGraph.GetRootAgentCount();
                m_childAgents = m_scene.SceneGraph.GetChildAgentCount();
                m_numPrim = m_scene.SceneGraph.GetTotalObjectsCount();
                m_activePrim = m_scene.SceneGraph.GetActiveObjectsCount();
                m_activeScripts = m_scene.SceneGraph.GetActiveScriptsCount();

                // FIXME: Checking for stat sanity is a complex approach.  What we really need to do is fix the code
                // so that stat numbers are always consistent.
                CheckStatSanity();
                
                //Our time dilation is 0.91 when we're running a full speed,
                // therefore to make sure we get an appropriate range,
                // we have to factor in our error.   (0.10f * statsUpdateFactor)
                // multiplies the fix for the error times the amount of times it'll occur a second
                // / 10 divides the value by the number of times the sim heartbeat runs (10fps)
                // Then we divide the whole amount by the amount of seconds pass in between stats updates.

                // 'statsUpdateFactor' is how often stats packets are sent in seconds. Used below to change
                // values to X-per-second values.

                uint thisFrame = m_scene.Frame;
                float framesUpdated = (float)(thisFrame - m_lastUpdateFrame) * m_reportedFpsCorrectionFactor;
                m_lastUpdateFrame = thisFrame;

                // Avoid div-by-zero if somehow we've not updated any frames.
                if (framesUpdated == 0)
                    framesUpdated = 1;

                for (int i = 0; i < 22; i++)
                {
                    sb[i] = new SimStatsPacket.StatBlock();
                }
                
                sb[0].StatID = (uint) Stats.TimeDilation;
                sb[0].StatValue = (Single.IsNaN(m_timeDilation)) ? 0.1f : m_timeDilation ; //((((m_timeDilation + (0.10f * statsUpdateFactor)) /10)  / statsUpdateFactor));

                sb[1].StatID = (uint) Stats.SimFPS;
                sb[1].StatValue = reportedFPS / m_statsUpdateFactor;

                sb[2].StatID = (uint) Stats.PhysicsFPS;
                sb[2].StatValue = physfps / m_statsUpdateFactor;

                sb[3].StatID = (uint) Stats.AgentUpdates;
                sb[3].StatValue = (m_agentUpdates / m_statsUpdateFactor);

                sb[4].StatID = (uint) Stats.Agents;
                sb[4].StatValue = m_rootAgents;

                sb[5].StatID = (uint) Stats.ChildAgents;
                sb[5].StatValue = m_childAgents;

                sb[6].StatID = (uint) Stats.TotalPrim;
                sb[6].StatValue = m_numPrim;

                sb[7].StatID = (uint) Stats.ActivePrim;
                sb[7].StatValue = m_activePrim;

                sb[8].StatID = (uint)Stats.FrameMS;
                sb[8].StatValue = m_frameMS / framesUpdated;

                sb[9].StatID = (uint)Stats.NetMS;
                sb[9].StatValue = m_netMS / framesUpdated;

                sb[10].StatID = (uint)Stats.PhysicsMS;
                sb[10].StatValue = m_physicsMS / framesUpdated;

                sb[11].StatID = (uint)Stats.ImageMS ;
                sb[11].StatValue = m_imageMS / framesUpdated;

                sb[12].StatID = (uint)Stats.OtherMS;
                sb[12].StatValue = m_otherMS / framesUpdated;

                sb[13].StatID = (uint)Stats.InPacketsPerSecond;
                sb[13].StatValue = (m_inPacketsPerSecond / m_statsUpdateFactor);

                sb[14].StatID = (uint)Stats.OutPacketsPerSecond;
                sb[14].StatValue = (m_outPacketsPerSecond / m_statsUpdateFactor);

                sb[15].StatID = (uint)Stats.UnAckedBytes;
                sb[15].StatValue = m_unAckedBytes;

                sb[16].StatID = (uint)Stats.AgentMS;
                sb[16].StatValue = m_agentMS / framesUpdated;

                sb[17].StatID = (uint)Stats.PendingDownloads;
                sb[17].StatValue = m_pendingDownloads;

                sb[18].StatID = (uint)Stats.PendingUploads;
                sb[18].StatValue = m_pendingUploads;

                sb[19].StatID = (uint)Stats.ActiveScripts;
                sb[19].StatValue = m_activeScripts;

                sb[20].StatID = (uint)Stats.ScriptLinesPerSecond;
                sb[20].StatValue = m_scriptLinesPerSecond / m_statsUpdateFactor;

                sb[21].StatID = (uint)Stats.SimSpareMs;
                sb[21].StatValue = m_spareMS / framesUpdated;

                for (int i = 0; i < 22; i++)
                {
                    lastReportedSimStats[i] = sb[i].StatValue;
                }
              
                SimStats simStats 
                    = new SimStats(
                        ReportingRegion.RegionLocX, ReportingRegion.RegionLocY, regionFlags, (uint)m_objectCapacity,
                        rb, sb, m_scene.RegionInfo.originRegionID);

                handlerSendStatResult = OnSendStatsResult;
                if (handlerSendStatResult != null)
                {
                    handlerSendStatResult(simStats);
                }

                // Extra statistics that aren't currently sent to clients
                lock (m_lastReportedExtraSimStats)
                {
                    m_lastReportedExtraSimStats[LastReportedObjectUpdateStatName] = m_objectUpdates / m_statsUpdateFactor;
                    m_lastReportedExtraSimStats[SlowFramesStat.ShortName] = (float)SlowFramesStat.Value;

                    Dictionary<string, float> physicsStats = m_scene.PhysicsScene.GetStats();
    
                    if (physicsStats != null)
                    {
                        foreach (KeyValuePair<string, float> tuple in physicsStats)
                        {
                            // FIXME: An extremely dirty hack to divide MS stats per frame rather than per second
                            // Need to change things so that stats source can indicate whether they are per second or
                            // per frame.
                            if (tuple.Key.EndsWith("MS"))
                                m_lastReportedExtraSimStats[tuple.Key] = tuple.Value / framesUpdated;
                            else
                                m_lastReportedExtraSimStats[tuple.Key] = tuple.Value / m_statsUpdateFactor;
                        }
                    }
                }

                ResetValues();
            }
        }

        private void ResetValues()
        {
            m_timeDilation = 0;
            m_fps = 0;
            m_pfps = 0;
            m_agentUpdates = 0;
            m_objectUpdates = 0;
            //m_inPacketsPerSecond = 0;
            //m_outPacketsPerSecond = 0;
            m_unAckedBytes = 0;
            m_scriptLinesPerSecond = 0;

            m_frameMS = 0;
            m_agentMS = 0;
            m_netMS = 0;
            m_physicsMS = 0;
            m_imageMS = 0;
            m_otherMS = 0;
            m_spareMS = 0;

//Ckrinke This variable is not used, so comment to remove compiler warning until it is used.
//Ckrinke            m_scriptMS = 0;
        }

        # region methods called from Scene
        // The majority of these functions are additive
        // so that you can easily change the amount of
        // seconds in between sim stats updates

        public void AddTimeDilation(float td)
        {
            //float tdsetting = td;
            //if (tdsetting > 1.0f)
                //tdsetting = (tdsetting - (tdsetting - 0.91f));

            //if (tdsetting < 0)
                //tdsetting = 0.0f;
            m_timeDilation = td;
        }

        internal void CheckStatSanity()
        {
            if (m_rootAgents < 0 || m_childAgents < 0)
            {
                handlerStatsIncorrect = OnStatsIncorrect;
                if (handlerStatsIncorrect != null)
                {
                    handlerStatsIncorrect();
                }
            }
            if (m_rootAgents == 0 && m_childAgents == 0)
            {
                m_unAckedBytes = 0;
            }
        }

        public void AddFPS(int frames)
        {
            m_fps += frames;
        }

        public void AddPhysicsFPS(float frames)
        {
            m_pfps += frames;
        }

        public void AddObjectUpdates(int numUpdates)
        {
            m_objectUpdates += numUpdates;
        }

        public void AddAgentUpdates(int numUpdates)
        {
            m_agentUpdates += numUpdates;
        }

        public void AddInPackets(int numPackets)
        {
            m_inPacketsPerSecond = numPackets;
        }

        public void AddOutPackets(int numPackets)
        {
            m_outPacketsPerSecond = numPackets;
        }

        public void AddunAckedBytes(int numBytes)
        {
            m_unAckedBytes += numBytes;
            if (m_unAckedBytes < 0) m_unAckedBytes = 0;
        }

        public void addFrameMS(int ms)
        {
            m_frameMS += ms;

            // At the moment, we'll only report if a frame is over 120% of target, since commonly frames are a bit
            // longer than ideal due to the inaccuracy of the Sleep in Scene.Update() (which in itself is a concern).
            if (ms > SlowFramesStatReportThreshold)
                SlowFramesStat.Value++;
        }

        public void AddSpareMS(int ms)
        {
            m_spareMS += ms;
        }

        public void addNetMS(int ms)
        {
            m_netMS += ms;
        }

        public void addAgentMS(int ms)
        {
            m_agentMS += ms;
        }

        public void addPhysicsMS(int ms)
        {
            m_physicsMS += ms;
        }

        public void addImageMS(int ms)
        {
            m_imageMS += ms;
        }

        public void addOtherMS(int ms)
        {
            m_otherMS += ms;
        }

        public void AddPendingDownloads(int count)
        {
            m_pendingDownloads += count;

            if (m_pendingDownloads < 0)
                m_pendingDownloads = 0;

            //m_log.InfoFormat("[stats]: Adding {0} to pending downloads to make {1}", count, m_pendingDownloads);
        }

        public void addScriptLines(int count)
        {
            m_scriptLinesPerSecond += count;
        }

        public void AddPacketsStats(int inPackets, int outPackets, int unAckedBytes)
        {
            AddInPackets(inPackets);
            AddOutPackets(outPackets);
            AddunAckedBytes(unAckedBytes);
        }

        #endregion

        public Dictionary<string, float> GetExtraSimStats()
        {
            lock (m_lastReportedExtraSimStats)
                return new Dictionary<string, float>(m_lastReportedExtraSimStats);
        }
    }
}
