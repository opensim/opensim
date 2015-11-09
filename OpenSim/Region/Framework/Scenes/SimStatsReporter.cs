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
using System.Threading;
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
        private double m_lastUpdateTS;
        private double m_prevFrameStatsTS;
        private double m_FrameStatsTS;
        private float m_timeDilation;
        private int m_fps;

        private object m_statsLock = new object();
        private object m_statsFrameLock = new object();

        /// <summary>
        /// Parameter to adjust reported scene fps
        /// </summary>
        /// <remarks>
        /// The close we have to a frame rate as expected by viewers, users and scripts
        /// is heartbeat rate.
        /// heartbeat rate default value is very diferent from the expected one
        /// and can be changed from region to region acording to its specific simulation needs
        /// since this creates incompatibility with expected values,
        /// this scale factor can be used to normalize values to a Virtual FPS.
        /// original decision was to use a value of 55fps for all opensim
        /// corresponding, with default heartbeat rate, to a value of 5.
        /// </remarks>
        private float m_statisticsFPSfactor = 5.0f;
        private float m_targetFrameTime = 0.1f;
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
        private float m_scriptTimeMS;

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

        private System.Timers.Timer m_report = new System.Timers.Timer();

        private IEstateModule estateModule;

         public SimStatsReporter(Scene scene)
        {
            m_scene = scene;

            ReportingRegion = scene.RegionInfo;

            if(scene.Normalized55FPS)
                m_statisticsFPSfactor = 55.0f * m_scene.FrameTime;
            else
                m_statisticsFPSfactor = 1.0f;

            m_targetFrameTime = 1000.0f * m_scene.FrameTime /  m_statisticsFPSfactor;

            m_objectCapacity = scene.RegionInfo.ObjectCapacity;
            m_report.AutoReset = true;
            m_report.Interval = m_statsUpdatesEveryMS;
            m_report.Elapsed += TriggerStatsHeartbeat;
            m_report.Enabled = true;

            m_lastUpdateTS = Util.GetTimeStampMS();
            m_FrameStatsTS = m_lastUpdateTS;
            m_prevFrameStatsTS = m_lastUpdateTS;

            if (StatsManager.SimExtraStats != null)
                OnSendStatsResult += StatsManager.SimExtraStats.ReceiveClassicSimStatsPacket;

            /// At the moment, we'll only report if a frame is over 120% of target, since commonly frames are a bit
            /// longer than ideal (which in itself is a concern).
            SlowFramesStatReportThreshold = (int)Math.Ceiling(m_scene.FrameTime * 1000 * 1.2);

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
            
            // dont do it if if still been done

            if(Monitor.TryEnter(m_statsLock))
            {
                // m_log.Debug("Firing Stats Heart Beat");

                SimStatsPacket.StatBlock[] sb = new SimStatsPacket.StatBlock[m_statisticViewerArraySize];
                SimStatsPacket.StatBlock[] sbex = new SimStatsPacket.StatBlock[m_statisticExtraArraySize];
                SimStatsPacket.RegionBlock rb = new SimStatsPacket.RegionBlock();
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
                double timeTmp = m_lastUpdateTS;
                m_lastUpdateTS = Util.GetTimeStampMS();
                float updateElapsed = (float)((m_lastUpdateTS - timeTmp)/1000.0);

                // factor to consider updates integration time
                float updateTimeFactor = 1.0f / updateElapsed;

               
                // scene frame stats 
                float reportedFPS;
                float physfps;
                float timeDilation;
                float agentMS;
                float physicsMS;
                float otherMS;
                float sleeptime;
                float scriptTimeMS;
                float totalFrameTime;

                float invFrameElapsed;

                // get a copy under lock and reset
                lock(m_statsFrameLock)
                {
                    timeDilation   = m_timeDilation;
                    reportedFPS    = m_fps;
                    physfps        = m_pfps;
                    agentMS        = m_agentMS;
                    physicsMS      = m_physicsMS;
                    otherMS        = m_otherMS;
                    sleeptime      = m_sleeptimeMS;
                    scriptTimeMS   = m_scriptTimeMS;
                    totalFrameTime = m_frameMS;
                    // still not inv 
                    invFrameElapsed = (float)((m_FrameStatsTS - m_prevFrameStatsTS) / 1000.0);

                    ResetFrameStats();
                }

                if (invFrameElapsed / updateElapsed < 0.8)
                   // scene is in trouble, its account of time is most likely wrong
                   // can even be in stall
                   invFrameElapsed = updateTimeFactor;
                else
                    invFrameElapsed = 1.0f / invFrameElapsed;

                float perframefactor;
                if (reportedFPS <= 0)
                {
                   reportedFPS = 0.0f;
                   physfps = 0.0f;
                   perframefactor = 1.0f;
                   timeDilation = 0.0f;
                }
                else
                {
                   timeDilation /= reportedFPS;
                   reportedFPS *=  m_statisticsFPSfactor;
                   perframefactor = 1.0f / (float)reportedFPS;
                   reportedFPS *= invFrameElapsed;
                   physfps *= invFrameElapsed  * m_statisticsFPSfactor;
                }

                // some engines track frame time with error related to the simulation step size
                if(physfps > reportedFPS) 
                    physfps = reportedFPS;

                // save the reported value so there is something available for llGetRegionFPS 
                lastReportedSimFPS = reportedFPS;

                // scale frame stats

                totalFrameTime *= perframefactor;
                sleeptime      *= perframefactor;
                otherMS        *= perframefactor;
                physicsMS      *= perframefactor;
                agentMS        *= perframefactor;
                scriptTimeMS   *= perframefactor;

                // estimate spare time
                float sparetime;
                sparetime      = m_targetFrameTime - (physicsMS + agentMS + otherMS);

                if (sparetime < 0)
                    sparetime = 0;
                 else if (sparetime > totalFrameTime)
                        sparetime = totalFrameTime;

#endregion

                m_rootAgents = m_scene.SceneGraph.GetRootAgentCount();
                m_childAgents = m_scene.SceneGraph.GetChildAgentCount();
                m_numPrim = m_scene.SceneGraph.GetTotalObjectsCount();
                m_numGeoPrim = m_scene.SceneGraph.GetTotalPrimObjectsCount();
                m_numMesh = m_scene.SceneGraph.GetTotalMeshObjectsCount();
                m_activePrim = m_scene.SceneGraph.GetActiveObjectsCount();
                m_activeScripts = m_scene.SceneGraph.GetActiveScriptsCount();
                m_scriptLinesPerSecond = m_scene.SceneGraph.GetScriptLPS();

                 // FIXME: Checking for stat sanity is a complex approach.  What we really need to do is fix the code
                // so that stat numbers are always consistent.
                CheckStatSanity(); 
 
                for (int i = 0; i < m_statisticViewerArraySize; i++)
                {
                    sb[i] = new SimStatsPacket.StatBlock();
                }
              
                sb[0].StatID = (uint) Stats.TimeDilation;
                sb[0].StatValue = (Single.IsNaN(timeDilation)) ? 0.0f : (float)Math.Round(timeDilation,3);

                sb[1].StatID = (uint) Stats.SimFPS;
                sb[1].StatValue = (float)Math.Round(reportedFPS,1);;

                sb[2].StatID = (uint) Stats.PhysicsFPS;
                sb[2].StatValue =  (float)Math.Round(physfps,1);

                sb[3].StatID = (uint) Stats.AgentUpdates;
                sb[3].StatValue = m_agentUpdates * updateTimeFactor;

                sb[4].StatID = (uint) Stats.Agents;
                sb[4].StatValue = m_rootAgents;

                sb[5].StatID = (uint) Stats.ChildAgents;
                sb[5].StatValue = m_childAgents;

                sb[6].StatID = (uint) Stats.TotalPrim;
                sb[6].StatValue = m_numPrim;

                sb[7].StatID = (uint) Stats.ActivePrim;
                sb[7].StatValue = m_activePrim;

                sb[8].StatID = (uint)Stats.FrameMS;
                sb[8].StatValue = totalFrameTime;

                sb[9].StatID = (uint)Stats.NetMS;
                sb[9].StatValue = m_netMS * perframefactor;

                sb[10].StatID = (uint)Stats.PhysicsMS;
                sb[10].StatValue = physicsMS;

                sb[11].StatID = (uint)Stats.ImageMS ;
                sb[11].StatValue = m_imageMS * perframefactor;

                sb[12].StatID = (uint)Stats.OtherMS;
                sb[12].StatValue = otherMS;

                sb[13].StatID = (uint)Stats.InPacketsPerSecond;
                sb[13].StatValue = (float)Math.Round(m_inPacketsPerSecond * updateTimeFactor);

                sb[14].StatID = (uint)Stats.OutPacketsPerSecond;
                sb[14].StatValue = (float)Math.Round(m_outPacketsPerSecond * updateTimeFactor);

                sb[15].StatID = (uint)Stats.UnAckedBytes;
                sb[15].StatValue = m_unAckedBytes;

                sb[16].StatID = (uint)Stats.AgentMS;
                sb[16].StatValue = agentMS;

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
                sb[28].StatValue = (float)Math.Round(m_scriptEventsPerSecond * updateTimeFactor);

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
                sb[37].StatValue = scriptTimeMS;

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
                sbex[0].StatValue = m_scriptLinesPerSecond * updateTimeFactor;
                lastReportedSimStats[38] = m_scriptLinesPerSecond * updateTimeFactor;

                sbex[1].StatID = (uint)Stats.FrameDilation2;
                sbex[1].StatValue = (Single.IsNaN(timeDilation)) ? 0.1f : timeDilation;
                lastReportedSimStats[39] = (Single.IsNaN(timeDilation)) ? 0.1f : timeDilation;

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
                        m_lastReportedExtraSimStats[LastReportedObjectUpdateStatName] = m_objectUpdates * updateTimeFactor;
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
                                    m_lastReportedExtraSimStats[tuple.Key] = tuple.Value * updateTimeFactor;
                            }
                        }
                    }
                }

//                LastReportedObjectUpdates = m_objectUpdates / m_statsUpdateFactor;
                ResetValues();
                Monitor.Exit(m_statsLock);
            }
        }

        private void ResetValues()
        {
            m_agentUpdates = 0;
            m_objectUpdates = 0;
            m_unAckedBytes = 0;
            m_scriptEventsPerSecond = 0;

            m_netMS = 0;
            m_imageMS = 0;
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

        # region methods called from Scene

        public void AddFrameStats(float _timeDilation, float _physicsFPS, float _agentMS,
                             float _physicsMS, float _otherMS , float _sleepMS,
                             float _frameMS, float _scriptTimeMS)
        {
            lock(m_statsFrameLock)
            {
                m_fps++;
                m_timeDilation += _timeDilation;
                m_pfps         += _physicsFPS;
                m_agentMS      += _agentMS;
                m_physicsMS    += _physicsMS;
                m_otherMS      += _otherMS;
                m_sleeptimeMS  += _sleepMS;
                m_frameMS      += _frameMS;
                m_scriptTimeMS += _scriptTimeMS;

                if (_frameMS > SlowFramesStatReportThreshold)
                    SlowFramesStat.Value++;

                m_FrameStatsTS = Util.GetTimeStampMS();
            }
        } 

        private void ResetFrameStats()
        {
            m_fps          = 0;
            m_timeDilation = 0.0f;
            m_pfps         = 0.0f;
            m_agentMS      = 0.0f;
            m_physicsMS    = 0.0f;
            m_otherMS      = 0.0f;
            m_sleeptimeMS  = 0.0f;
            m_frameMS      = 0.0f;
            m_scriptTimeMS = 0.0f;

            m_prevFrameStatsTS = m_FrameStatsTS;
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


        public void addNetMS(float ms)
        {
            m_netMS += ms;
        }

        public void addImageMS(float ms)
        {
            m_imageMS += ms;
        }

        public void AddPendingDownloads(int count)
        {
            m_pendingDownloads += count;

            if (m_pendingDownloads < 0)
                m_pendingDownloads = 0;

            //m_log.InfoFormat("[stats]: Adding {0} to pending downloads to make {1}", count, m_pendingDownloads);
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
