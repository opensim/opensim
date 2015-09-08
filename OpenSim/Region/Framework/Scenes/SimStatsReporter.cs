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

        // Determines the size of the array that is used to collect StatBlocks
        // for sending viewer compatible stats must be conform with sb array filling below
        private const int m_statisticViewerArraySize = 38;
        // size of LastReportedSimFPS with extra stats.
        private const int m_statisticExtraArraySize = (int)(Stats.SimExtraCountEnd - Stats.SimExtraCountStart);

        /// <summary>
        /// These are the IDs of stats sent in the StatsPacket to the viewer.
        /// </summary>
        /// <remarks>
        /// Some of these are not relevant to OpenSimulator since it is architected differently to other simulators
        /// (e.g. script instructions aren't executed as part of the frame loop so 'script time' is tricky).
        /// </remarks>
        public enum Stats : uint
        {
// viewers defined IDs
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
            LSLScriptLinesPerSecond = 16, // viewers don't like this anymore
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
            SimIoPumpTime = 34,
	        SimPCTSscriptsRun = 35,
	        SimRegionIdle = 36, // dataserver only
	        SimRegionIdlePossible  = 37, // dataserver only
	        SimAIStepTimeMS = 38,
	        SimSkippedSillouet_PS  = 39,
	        SimSkippedCharsPerC  = 40,

// extra stats IDs irrelevant, just far from viewer defined ones
            SimExtraCountStart = 1000,

            internalLSLScriptLinesPerSecond = 1000,
            FrameDilation2 = 1001,
            UsersLoggingIn = 1002,
            TotalGeoPrim = 1003,
            TotalMesh = 1004,
            ThreadCount = 1005,
        
            SimExtraCountEnd = 1006
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
        /// Parameter to adjust reported scene fps
        /// </summary>
        /// <remarks>
        /// Our scene loop runs slower than other server implementations, apparantly because we work somewhat differently.
        /// However, we will still report an FPS that's closer to what people are used to seeing.  A lower FPS might
        /// affect clients and monitoring scripts/software.
        /// </remarks>
        private float m_reportedFpsCorrectionFactor = 1.0f;

        // saved last reported value so there is something available for llGetRegionFPS 
        private float lastReportedSimFPS;
        private float[] lastReportedSimStats = new float[m_statisticExtraArraySize + m_statisticViewerArraySize];
        private float m_pfps;

        /// <summary>
        /// Number of agent updates requested in this stats cycle
        /// </summary>
        private int m_agentUpdates;

        /// <summary>
        /// Number of object updates requested in this stats cycle
        /// </summary>
        private int m_objectUpdates;

        private float m_frameMS;

        private float m_netMS;
        private float m_agentMS;
        private float m_physicsMS;
        private float m_imageMS;
        private float m_otherMS;
        private float m_sleeptimeMS;

        private int m_rootAgents;
        private int m_childAgents;
        private int m_numPrim;
        private int m_numGeoPrim;
        private int m_numMesh;
        private int m_inPacketsPerSecond;
        private int m_outPacketsPerSecond;
        private int m_activePrim;
        private int m_unAckedBytes;
        private int m_pendingDownloads;
        private int m_pendingUploads = 0;  // FIXME: Not currently filled in
        private int m_activeScripts;
        private int m_scriptLinesPerSecond;
        private int m_scriptEventsPerSecond;

        private int m_objectCapacity = 45000;

         // The current number of users attempting to login to the region
        private int m_usersLoggingIn;

        // The last reported value of threads from the SmartThreadPool inside of
        // XEngine
        private int m_inUseThreads;

        private Scene m_scene;

        private RegionInfo ReportingRegion;

        private Timer m_report = new Timer();

        private IEstateModule estateModule;

         public SimStatsReporter(Scene scene)
        {
            m_scene = scene;

            m_statsUpdateFactor = (float)(m_statsUpdatesEveryMS / 1000.0f);
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
                    "Number of frames where frame time has been significantly longer than the desired frame time.",
                    " frames",
                    "scene",
                    m_scene.Name,
                    StatType.Push,
                    null,
                    StatVerbosity.Info);

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
            m_statsUpdateFactor = (float)(m_statsUpdatesEveryMS / 1000.0f);
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
              if (!m_scene.Active)
                return;

            SimStatsPacket.StatBlock[] sb = new SimStatsPacket.StatBlock[m_statisticViewerArraySize];
            SimStatsPacket.StatBlock[] sbex = new SimStatsPacket.StatBlock[m_statisticExtraArraySize];
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
                // factor to consider updates integration time
                float updateFactor = 1.0f / m_statsUpdateFactor;

                // the nominal frame time, corrected by reporting multiplier
                float TargetFrameTime = 1000.0f * m_scene.MinFrameTime / m_reportedFpsCorrectionFactor;

                // acumulated fps scaled by reporting multiplier
                float reportedFPS = (m_fps * m_reportedFpsCorrectionFactor);
                if (reportedFPS <= 0)
                   reportedFPS = 1;

                // factor to calculate per frame values
                float perframefactor = 1.0f / (float)reportedFPS;

                // fps considering the integration time
                reportedFPS = reportedFPS * updateFactor;
                // save the reported value so there is something available for llGetRegionFPS 
                lastReportedSimFPS = reportedFPS;


#endregion
                m_rootAgents = m_scene.SceneGraph.GetRootAgentCount();
                m_childAgents = m_scene.SceneGraph.GetChildAgentCount();
                m_numPrim = m_scene.SceneGraph.GetTotalObjectsCount();
                m_numGeoPrim = m_scene.SceneGraph.GetTotalPrimObjectsCount();
                m_numMesh = m_scene.SceneGraph.GetTotalMeshObjectsCount();
                m_activePrim = m_scene.SceneGraph.GetActiveObjectsCount();
                m_activeScripts = m_scene.SceneGraph.GetActiveScriptsCount();

                float physfps = m_pfps;
                if (physfps < 0)
                    physfps = 0;

                float sparetime;
                float sleeptime;

                float TotalFrameTime = m_frameMS * perframefactor;
                sleeptime = m_sleeptimeMS * perframefactor;

                sparetime = m_frameMS - m_sleeptimeMS; // total time minus sleep
                sparetime *= perframefactor; // average per frame
                sparetime = TargetFrameTime - sparetime; // real spare
                if (sparetime < 0)
                    sparetime = 0;
                 else if (sparetime > TotalFrameTime)
                        sparetime = TotalFrameTime;



                // FIXME: Checking for stat sanity is a complex approach.  What we really need to do is fix the code
                // so that stat numbers are always consistent.
                CheckStatSanity();
                
                // other MS is actually simulation time
                //                m_otherMS = m_frameMS - m_physicsMS - m_imageMS - m_netMS - m_agentMS;
                // m_imageMS  m_netMS are not included in m_frameMS

                m_otherMS = m_frameMS - m_physicsMS -  m_agentMS - m_sleeptimeMS;
                if (m_otherMS < 0)
                    m_otherMS = 0;

                for (int i = 0; i < m_statisticViewerArraySize; i++)
                {
                    sb[i] = new SimStatsPacket.StatBlock();
                }
              
                sb[0].StatID = (uint) Stats.TimeDilation;
                sb[0].StatValue = (Single.IsNaN(m_timeDilation)) ? 0.1f : m_timeDilation ;

                sb[1].StatID = (uint) Stats.SimFPS;
                sb[1].StatValue = reportedFPS;

                sb[2].StatID = (uint) Stats.PhysicsFPS;
                sb[2].StatValue = physfps * updateFactor;

                sb[3].StatID = (uint) Stats.AgentUpdates;
                sb[3].StatValue = m_agentUpdates * updateFactor;

                sb[4].StatID = (uint) Stats.Agents;
                sb[4].StatValue = m_rootAgents;

                sb[5].StatID = (uint) Stats.ChildAgents;
                sb[5].StatValue = m_childAgents;

                sb[6].StatID = (uint) Stats.TotalPrim;
                sb[6].StatValue = m_numPrim;

                sb[7].StatID = (uint) Stats.ActivePrim;
                sb[7].StatValue = m_activePrim;

                sb[8].StatID = (uint)Stats.FrameMS;
                sb[8].StatValue = TotalFrameTime;

                sb[9].StatID = (uint)Stats.NetMS;
                sb[9].StatValue = m_netMS * perframefactor;

                sb[10].StatID = (uint)Stats.PhysicsMS;
                sb[10].StatValue = m_physicsMS * perframefactor;

                sb[11].StatID = (uint)Stats.ImageMS ;
                sb[11].StatValue = m_imageMS * perframefactor;

                sb[12].StatID = (uint)Stats.OtherMS;
                sb[12].StatValue = m_otherMS * perframefactor;

                sb[13].StatID = (uint)Stats.InPacketsPerSecond;
                sb[13].StatValue = (m_inPacketsPerSecond * updateFactor);

                sb[14].StatID = (uint)Stats.OutPacketsPerSecond;
                sb[14].StatValue = (m_outPacketsPerSecond * updateFactor);

                sb[15].StatID = (uint)Stats.UnAckedBytes;
                sb[15].StatValue = m_unAckedBytes;

                sb[16].StatID = (uint)Stats.AgentMS;
                sb[16].StatValue = m_agentMS * perframefactor;

                sb[17].StatID = (uint)Stats.PendingDownloads;
                sb[17].StatValue = m_pendingDownloads;

                sb[18].StatID = (uint)Stats.PendingUploads;
                sb[18].StatValue = m_pendingUploads;

                sb[19].StatID = (uint)Stats.ActiveScripts;
                sb[19].StatValue = m_activeScripts;

                sb[20].StatID = (uint)Stats.SimSleepMs;
                sb[20].StatValue = sleeptime;

                sb[21].StatID = (uint)Stats.SimSpareMs;
                sb[21].StatValue = sparetime;

                //  this should came from phys engine
                sb[22].StatID = (uint)Stats.SimPhysicsStepMs;
                sb[22].StatValue = 20;

                // send the ones we dont have as zeros, to clean viewers state
                // specially arriving from regions with wrond IDs in use

                sb[23].StatID = (uint)Stats.VirtualSizeKb;
                sb[23].StatValue = 0;

                sb[24].StatID = (uint)Stats.ResidentSizeKb;
                sb[24].StatValue = 0;
                
                sb[25].StatID = (uint)Stats.PendingLocalUploads;
                sb[25].StatValue = 0;
                
                sb[26].StatID = (uint)Stats.PhysicsPinnedTasks;
                sb[26].StatValue = 0;
                
                sb[27].StatID = (uint)Stats.PhysicsLodTasks;
                sb[27].StatValue = 0;

                sb[28].StatID = (uint)Stats.ScriptEps; // we actuall have this, but not messing array order AGAIN
                sb[28].StatValue = m_scriptEventsPerSecond * updateFactor;

                sb[29].StatID = (uint)Stats.SimAIStepTimeMS;
                sb[29].StatValue = 0;

                sb[30].StatID = (uint)Stats.SimIoPumpTime;
                sb[30].StatValue = 0;

                sb[31].StatID = (uint)Stats.SimPCTSscriptsRun;
                sb[31].StatValue = 0;

                sb[32].StatID = (uint)Stats.SimRegionIdle;
                sb[32].StatValue = 0;

                sb[33].StatID = (uint)Stats.SimRegionIdlePossible;
                sb[33].StatValue = 0;

                sb[34].StatID = (uint)Stats.SimSkippedSillouet_PS;
                sb[34].StatValue = 0;

                sb[35].StatID = (uint)Stats.SimSkippedCharsPerC;
                sb[35].StatValue = 0;

                sb[36].StatID = (uint)Stats.SimPhysicsMemory;
                sb[36].StatValue = 0;

                sb[37].StatID = (uint)Stats.ScriptMS;
                sb[37].StatValue = 0;


                for (int i = 0; i < m_statisticViewerArraySize; i++)
                {
                    lastReportedSimStats[i] = sb[i].StatValue;
                }


                // add extra stats for internal use

                for (int i = 0; i < m_statisticExtraArraySize; i++)
                {
                    sbex[i] = new SimStatsPacket.StatBlock();
                }

                sbex[0].StatID = (uint)Stats.LSLScriptLinesPerSecond;
                sbex[0].StatValue = m_scriptLinesPerSecond * updateFactor;
                lastReportedSimStats[38] = m_scriptLinesPerSecond * updateFactor;

                sbex[1].StatID = (uint)Stats.FrameDilation2;
                sbex[1].StatValue = (Single.IsNaN(m_timeDilation)) ? 0.1f : m_timeDilation;
                lastReportedSimStats[39] = (Single.IsNaN(m_timeDilation)) ? 0.1f : m_timeDilation;

                sbex[2].StatID = (uint)Stats.UsersLoggingIn;
                sbex[2].StatValue = m_usersLoggingIn;
                lastReportedSimStats[40] = m_usersLoggingIn;

                sbex[3].StatID = (uint)Stats.TotalGeoPrim;
                sbex[3].StatValue = m_numGeoPrim;
                lastReportedSimStats[41] = m_numGeoPrim;

                sbex[4].StatID = (uint)Stats.TotalMesh;
                sbex[4].StatValue = m_numMesh;
                lastReportedSimStats[42] = m_numMesh;

                sbex[5].StatID = (uint)Stats.ThreadCount;
                sbex[5].StatValue = m_inUseThreads;
                lastReportedSimStats[43] = m_inUseThreads;

                SimStats simStats 
                    = new SimStats(
                        ReportingRegion.RegionLocX, ReportingRegion.RegionLocY, regionFlags, (uint)m_objectCapacity,
                        rb, sb, sbex, m_scene.RegionInfo.originRegionID);

                 handlerSendStatResult = OnSendStatsResult;
                if (handlerSendStatResult != null)
                {
                    handlerSendStatResult(simStats);
                }

                // Extra statistics that aren't currently sent to clients
                if (m_scene.PhysicsScene != null)
                {
                    lock (m_lastReportedExtraSimStats)
                    {
                        m_lastReportedExtraSimStats[LastReportedObjectUpdateStatName] = m_objectUpdates * updateFactor;
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
                                    m_lastReportedExtraSimStats[tuple.Key] = tuple.Value * perframefactor;
                                else
                                    m_lastReportedExtraSimStats[tuple.Key] = tuple.Value * updateFactor;
                            }
                        }
                    }
                }

//                LastReportedObjectUpdates = m_objectUpdates / m_statsUpdateFactor;
                ResetValues();
            }
        }

        private void ResetValues()
        {
            // Reset the number of frames that the physics library has
            // processed since the last stats report

            m_timeDilation = 0;
            m_fps = 0;
            m_pfps = 0;
            m_agentUpdates = 0;
            m_objectUpdates = 0;
            //m_inPacketsPerSecond = 0;
            //m_outPacketsPerSecond = 0;
            m_unAckedBytes = 0;
            m_scriptLinesPerSecond = 0;
            m_scriptEventsPerSecond = 0;

            m_frameMS = 0;
            m_agentMS = 0;
            m_netMS = 0;
            m_physicsMS = 0;
            m_imageMS = 0;
            m_otherMS = 0;
            m_sleeptimeMS = 0;

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

        public void addFrameMS(float ms)
        {
            m_frameMS += ms;

            // At the moment, we'll only report if a frame is over 120% of target, since commonly frames are a bit
            // longer than ideal due to the inaccuracy of the Sleep in Scene.Update() (which in itself is a concern).
            if (ms > SlowFramesStatReportThreshold)
                SlowFramesStat.Value++;
        }

        public void addNetMS(float ms)
        {
            m_netMS += ms;
        }

        public void addAgentMS(float ms)
        {
            m_agentMS += ms;
        }

        public void addPhysicsMS(float ms)
        {
            m_physicsMS += ms;
        }

        public void addImageMS(float ms)
        {
            m_imageMS += ms;
        }

        public void addOtherMS(float ms)
        {
            m_otherMS += ms;
        }

        public void addSleepMS(float ms)
        {
            m_sleeptimeMS += ms;
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

        public void addScriptEvents(int count)
        {
            m_scriptEventsPerSecond += count;
        }

        public void AddPacketsStats(int inPackets, int outPackets, int unAckedBytes)
        {
            AddInPackets(inPackets);
            AddOutPackets(outPackets);
            AddunAckedBytes(unAckedBytes);
        }

        public void UpdateUsersLoggingIn(bool isLoggingIn)
        {
            // Determine whether the user has started logging in or has completed
            // logging into the region
            if (isLoggingIn)
            {
                // The user is starting to login to the region so increment the
                // number of users attempting to login to the region
                m_usersLoggingIn++;
            }
            else
            {
                // The user has finished logging into the region so decrement the
                // number of users logging into the region
                m_usersLoggingIn--;
            }
        }

        public void SetThreadCount(int inUseThreads)
        {
            // Save the new number of threads to our member variable to send to
            // the extra stats collector
            m_inUseThreads = inUseThreads;
        }

        #endregion

        public Dictionary<string, float> GetExtraSimStats()
        {
            lock (m_lastReportedExtraSimStats)
                return new Dictionary<string, float>(m_lastReportedExtraSimStats);
        }
    }
}
