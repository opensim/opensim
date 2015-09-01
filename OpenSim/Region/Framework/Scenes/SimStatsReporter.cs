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
        // for sending to the SimStats and SimExtraStatsCollector
        private const int m_statisticArraySize = 28;

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
            SimIoPumpTime = 34,
            FrameDilation = 35,
            UsersLoggingIn = 36,
            TotalGeoPrim = 37,
            TotalMesh = 38,
            ThreadCount = 39
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
<<<<<<< HEAD
        private float[] lastReportedSimStats = new float[m_statisticArraySize];
=======
        private float[] lastReportedSimStats = new float[23];
>>>>>>> avn/ubitvar
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

        private int m_netMS;
        private int m_agentMS;
        private int m_physicsMS;
        private int m_imageMS;
        private int m_otherMS;
<<<<<<< HEAD
        private int m_scriptMS;
=======
        private int m_sleeptimeMS;

//Ckrinke: (3-21-08) Comment out to remove a compiler warning. Bring back into play when needed.
//Ckrinke        private int m_scriptMS = 0;
>>>>>>> avn/ubitvar

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

        private int m_objectCapacity = 45000;

        // This is the number of frames that will be stored and then averaged for
        // the Total, Simulation, Physics, and Network Frame Time; It is set to
        // 10 by default but can be changed by the OpenSim.ini configuration file
        // NumberOfFrames parameter
        private int m_numberFramesStored;

        // The arrays that will hold the time it took to run the past N frames,
        // where N is the num_frames_to_average given by the configuration file
        private double[] m_totalFrameTimeMilliseconds;
        private double[] m_simulationFrameTimeMilliseconds;
        private double[] m_physicsFrameTimeMilliseconds;
        private double[] m_networkFrameTimeMilliseconds;

        // The location of the next time in milliseconds that will be
        // (over)written when the next frame completes
        private int m_nextLocation = 0;

        // The correct number of frames that have completed since the last stats
        // update for physics
        private int m_numberPhysicsFrames;

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
            : this(scene, Scene.m_defaultNumberFramesStored)
        {
        }

        public SimStatsReporter(Scene scene, int numberOfFrames)
        {
            // Store the number of frames from the OpenSim.ini configuration file
            m_numberFramesStored = numberOfFrames;

            // Initialize the different frame time arrays to the correct sizes
            m_totalFrameTimeMilliseconds = new double[m_numberFramesStored];
            m_simulationFrameTimeMilliseconds = new double[m_numberFramesStored];
            m_physicsFrameTimeMilliseconds = new double[m_numberFramesStored];
            m_networkFrameTimeMilliseconds = new double[m_numberFramesStored];

            // Initialize the current number of users logging into the region
            m_usersLoggingIn = 0;

            m_scene = scene;
            m_reportedFpsCorrectionFactor = scene.MinFrameSeconds * m_nominalReportedFps;
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
            SlowFramesStatReportThreshold = (int)Math.Ceiling(scene.MinFrameTicks * 1.2);

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
            double totalSumFrameTime;
            double simulationSumFrameTime;
            double physicsSumFrameTime;
            double networkSumFrameTime;
            float frameDilation;
            int currentFrame;

            if (!m_scene.Active)
                return;

<<<<<<< HEAD
            // Create arrays to hold the statistics for this current scene,
            // these will be passed to the SimExtraStatsCollector, they are also
            // sent to the SimStats class
            SimStatsPacket.StatBlock[] sb = new
                SimStatsPacket.StatBlock[m_statisticArraySize];
=======
            SimStatsPacket.StatBlock[] sb = new SimStatsPacket.StatBlock[23];
>>>>>>> avn/ubitvar
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

               // ORIGINAL code commented out until we have time to add our own
               // statistics to the statistics window, this will be done as a
               // new section given the title of our current project
                // We're going to lie about the FPS because we've been lying since 2008.  The actual FPS is currently
                // locked at a maximum of 11.  Maybe at some point this can change so that we're not lying.
                //int reportedFPS = (int)(m_fps * m_reportedFpsCorrectionFactor);
               int reportedFPS = m_fps;

                // save the reported value so there is something available for llGetRegionFPS 
                lastReportedSimFPS = reportedFPS / m_statsUpdateFactor;

               // ORIGINAL code commented out until we have time to add our own
               // statistics to the statistics window
                //float physfps = ((m_pfps / 1000));
               float physfps = m_numberPhysicsFrames;

                //if (physfps > 600)
                //physfps = physfps - (physfps - 600);

                if (physfps < 0)
                    physfps = 0;

#endregion
                float factor = 1 / m_statsUpdateFactor;

                if (reportedFPS <= 0)
                    reportedFPS = 1;

                float perframe = 1.0f / (float)reportedFPS;

                float TotalFrameTime = m_frameMS * perframe;

                float targetframetime = 1100.0f / (float)m_nominalReportedFps;

                float sparetime;
                float sleeptime;

                if (TotalFrameTime > targetframetime)
                {
                    sparetime = 0;
                    sleeptime = 0;
                }
                else
                {
                    sparetime = m_frameMS - m_physicsMS - m_agentMS;
                    sparetime *= perframe;
                    if (sparetime < 0)
                        sparetime = 0;
                    else if (sparetime > TotalFrameTime)
                        sparetime = TotalFrameTime;
                    sleeptime = m_sleeptimeMS * perframe;
                }

                m_rootAgents = m_scene.SceneGraph.GetRootAgentCount();
                m_childAgents = m_scene.SceneGraph.GetChildAgentCount();
                m_numPrim = m_scene.SceneGraph.GetTotalObjectsCount();
                m_numGeoPrim = m_scene.SceneGraph.GetTotalPrimObjectsCount();
                m_numMesh = m_scene.SceneGraph.GetTotalMeshObjectsCount();
                m_activePrim = m_scene.SceneGraph.GetActiveObjectsCount();
                m_activeScripts = m_scene.SceneGraph.GetActiveScriptsCount();

                // FIXME: Checking for stat sanity is a complex approach.  What we really need to do is fix the code
                // so that stat numbers are always consistent.
                CheckStatSanity();
                
                // other MS is actually simulation time
                //                m_otherMS = m_frameMS - m_physicsMS - m_imageMS - m_netMS - m_agentMS;
                // m_imageMS  m_netMS are not included in m_frameMS

<<<<<<< HEAD
                uint thisFrame = m_scene.Frame;
                uint numFrames = thisFrame - m_lastUpdateFrame;
                float framesUpdated = (float)numFrames * m_reportedFpsCorrectionFactor;
                m_lastUpdateFrame = thisFrame;

                // Avoid div-by-zero if somehow we've not updated any frames.
                if (framesUpdated == 0)
                    framesUpdated = 1;

                for (int i = 0; i < m_statisticArraySize; i++)
=======
                m_otherMS = m_frameMS - m_physicsMS -  m_agentMS - m_sleeptimeMS;
                if (m_otherMS < 0)
                    m_otherMS = 0;

                for (int i = 0; i < 23; i++)
>>>>>>> avn/ubitvar
                {
                    sb[i] = new SimStatsPacket.StatBlock();
                }

                // Resetting the sums of the frame times to prevent any errors
                // in calculating the moving average for frame time
                totalSumFrameTime = 0;
                simulationSumFrameTime = 0;
                physicsSumFrameTime = 0;
                networkSumFrameTime = 0;

                // Loop through all the frames that were stored for the current
                // heartbeat to process the moving average of frame times
                for (int i = 0; i < m_numberFramesStored; i++)
                {
                    // Sum up each frame time in order to calculate the moving
                    // average of frame time
                    totalSumFrameTime += m_totalFrameTimeMilliseconds[i];
                    simulationSumFrameTime +=
                        m_simulationFrameTimeMilliseconds[i];
                    physicsSumFrameTime += m_physicsFrameTimeMilliseconds[i];
                    networkSumFrameTime += m_networkFrameTimeMilliseconds[i];
                }

                // Get the index that represents the current frame based on the next one known; go back
                // to the last index if next one is stated to restart at 0
                if (m_nextLocation == 0)
                    currentFrame = m_numberFramesStored - 1;
                else
                    currentFrame = m_nextLocation - 1;

                // Calculate the frame dilation; which is currently based on the ratio between the sum of the
                // physics and simulation rate, and the set minimum time to run a scene's frame
                frameDilation = (float)(m_simulationFrameTimeMilliseconds[currentFrame] +
                   m_physicsFrameTimeMilliseconds[currentFrame]) / m_scene.MinFrameTicks;

                // ORIGINAL code commented out until we have time to add our own
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

               // ORIGINAL code commented out until we have time to add our own
               // statistics to the statistics window
                sb[8].StatID = (uint)Stats.FrameMS;
<<<<<<< HEAD
                //sb[8].StatValue = m_frameMS / framesUpdated;
                sb[8].StatValue = (float) totalSumFrameTime / m_numberFramesStored;

                sb[9].StatID = (uint)Stats.NetMS;
                //sb[9].StatValue = m_netMS / framesUpdated;
                sb[9].StatValue = (float) networkSumFrameTime / m_numberFramesStored;

                sb[10].StatID = (uint)Stats.PhysicsMS;
                //sb[10].StatValue = m_physicsMS / framesUpdated;
                sb[10].StatValue = (float) physicsSumFrameTime / m_numberFramesStored;
=======
                sb[8].StatValue = TotalFrameTime;

                sb[9].StatID = (uint)Stats.NetMS;
                sb[9].StatValue = m_netMS * perframe;

                sb[10].StatID = (uint)Stats.PhysicsMS;
                sb[10].StatValue = m_physicsMS * perframe;
>>>>>>> avn/ubitvar

                sb[11].StatID = (uint)Stats.ImageMS ;
                sb[11].StatValue = m_imageMS * perframe;

                sb[12].StatID = (uint)Stats.OtherMS;
<<<<<<< HEAD
                //sb[12].StatValue = m_otherMS / framesUpdated;
                sb[12].StatValue = (float) simulationSumFrameTime / m_numberFramesStored;
=======
                sb[12].StatValue = m_otherMS * perframe;
>>>>>>> avn/ubitvar

                sb[13].StatID = (uint)Stats.InPacketsPerSecond;
                sb[13].StatValue = (m_inPacketsPerSecond / m_statsUpdateFactor);

                sb[14].StatID = (uint)Stats.OutPacketsPerSecond;
                sb[14].StatValue = (m_outPacketsPerSecond / m_statsUpdateFactor);

                sb[15].StatID = (uint)Stats.UnAckedBytes;
                sb[15].StatValue = m_unAckedBytes;

                sb[16].StatID = (uint)Stats.AgentMS;
                sb[16].StatValue = m_agentMS * perframe;

                sb[17].StatID = (uint)Stats.PendingDownloads;
                sb[17].StatValue = m_pendingDownloads;

                sb[18].StatID = (uint)Stats.PendingUploads;
                sb[18].StatValue = m_pendingUploads;

                sb[19].StatID = (uint)Stats.ActiveScripts;
                sb[19].StatValue = m_activeScripts;

                sb[20].StatID = (uint)Stats.ScriptLinesPerSecond;
                sb[20].StatValue = m_scriptLinesPerSecond / m_statsUpdateFactor;

                sb[21].StatID = (uint)Stats.SimSpareMs;
                sb[21].StatValue = sparetime;

                sb[22].StatID = (uint)Stats.SimSleepMs;
                sb[22].StatValue = sleeptime;

                // Current ratio between the sum of physics and sim rate, and the
                // minimum time to run a scene's frame
                sb[22].StatID = (uint)Stats.FrameDilation;
                sb[22].StatValue = frameDilation;

                // Current number of users currently attemptint to login to region
                sb[23].StatID = (uint)Stats.UsersLoggingIn;
                sb[23].StatValue = m_usersLoggingIn;

                // Total number of geometric primitives in the scene
                sb[24].StatID = (uint)Stats.TotalGeoPrim;
                sb[24].StatValue = m_numGeoPrim;

                // Total number of mesh objects in the scene
                sb[25].StatID = (uint)Stats.TotalMesh;
                sb[25].StatValue = m_numMesh;

                // Current number of threads that XEngine is using
                sb[26].StatID = (uint)Stats.ThreadCount;
                sb[26].StatValue = m_inUseThreads;

                sb[27].StatID = (uint)Stats.ScriptMS;
                sb[27].StatValue = (numFrames <= 0) ? 0 : ((float)m_scriptMS / numFrames);

                for (int i = 0; i < m_statisticArraySize; i++)
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
                if (m_scene.PhysicsScene != null)
                {
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
                                    m_lastReportedExtraSimStats[tuple.Key] = tuple.Value * perframe;
                                else
                                    m_lastReportedExtraSimStats[tuple.Key] = tuple.Value / m_statsUpdateFactor;
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
            m_numberPhysicsFrames = 0;

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
<<<<<<< HEAD
            m_scriptMS = 0;
            m_spareMS = 0;
=======
//            m_spareMS = 0;
            m_sleeptimeMS = 0;

//Ckrinke This variable is not used, so comment to remove compiler warning until it is used.
//Ckrinke            m_scriptMS = 0;
>>>>>>> avn/ubitvar
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

<<<<<<< HEAD
        public void AddScriptMS(int ms)
        {
            m_scriptMS += ms;
        }

        public void addPhysicsFrame(int frames)
        {
            // Add the number of physics frames to the correct total physics
            // frames
            m_numberPhysicsFrames += frames;
        }

        public void addFrameTimeMilliseconds(double total, double simulation,
            double physics, double network)
        {
            // Save the frame times from the current frame into the appropriate
            // arrays
            m_totalFrameTimeMilliseconds[m_nextLocation] = total;
            m_simulationFrameTimeMilliseconds[m_nextLocation] = simulation;
            m_physicsFrameTimeMilliseconds[m_nextLocation] = physics;
            m_networkFrameTimeMilliseconds[m_nextLocation] = network;

            // Update to the next location in the list
            m_nextLocation++;

            // Since the list will begin to overwrite the oldest frame values
            // first, the next location needs to loop back to the beginning of the
            // list whenever it reaches the end
            m_nextLocation = m_nextLocation % m_numberFramesStored;
=======
        public void addSleepMS(int ms)
        {
            m_sleeptimeMS += ms;
>>>>>>> avn/ubitvar
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
