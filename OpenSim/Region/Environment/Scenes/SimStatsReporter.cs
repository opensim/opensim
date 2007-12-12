using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using libsecondlife.Packets;
using OpenSim.Framework;
using Timer = System.Timers.Timer;

namespace OpenSim.Region.Environment.Scenes
{
    public class SimStatsReporter
    {
        public delegate void SendStatResult(SimStatsPacket pack);
        
        public event SendStatResult OnSendStatsResult;

        private enum Stats : uint 
        {
            TimeDilation = 0,
            SimFPS = 1,
            PhysicsFPS = 2,
            AgentUpdates = 3,
            TotalPrim = 11,
            Agents = 13,
            ChildAgents = 14,
            InPacketsPerSecond = 17,
            OutPacketsPerSecond = 18,
            UnAckedBytes = 24
        }

        private int statsUpdatesEveryMS = 1000;
        private float m_timeDilation = 0;
        private int m_fps = 0;
        private float m_pfps = 0;
        private float m_agentUpdates = 0;
        private int m_rootAgents = 0;
        private int m_childAgents = 0;
        private int m_numPrim = 0;
        private int m_inPacketsPerSecond = 0;
        private int m_outPacketsPerSecond = 0;
        private int m_unAckedBytes = 0;
        private RegionInfo ReportingRegion;

        private Timer m_report = new Timer();
        

        public SimStatsReporter(RegionInfo regionData)
        {
            ReportingRegion = regionData;
            m_report.AutoReset = true;
            m_report.Interval = statsUpdatesEveryMS;
            m_report.Elapsed += new ElapsedEventHandler(statsHeartBeat);
            m_report.Enabled = true;
        }

        private void statsHeartBeat(object sender, EventArgs e)
        {
            m_report.Enabled = false;
            SimStatsPacket statpack = new SimStatsPacket();
            SimStatsPacket.StatBlock[] sb = new SimStatsPacket.StatBlock[10];
            statpack.Region = new SimStatsPacket.RegionBlock();
            statpack.Region.RegionX = ReportingRegion.RegionLocX;
            statpack.Region.RegionY = ReportingRegion.RegionLocY;
            try
            {
                statpack.Region.RegionFlags = (uint)ReportingRegion.EstateSettings.regionFlags;
            }
            catch(System.Exception)
            {
                statpack.Region.RegionFlags = (uint)0;
            }
            statpack.Region.ObjectCapacity = (uint)15000;

            sb[0] = new SimStatsPacket.StatBlock();
            sb[0].StatID = (uint)Stats.TimeDilation;
            sb[0].StatValue = (m_timeDilation);

            sb[1] = new SimStatsPacket.StatBlock();
            sb[1].StatID = (uint)Stats.SimFPS;
            sb[1].StatValue = (int)(m_fps * 5);

            sb[2] = new SimStatsPacket.StatBlock();
            sb[2].StatID = (uint)Stats.PhysicsFPS;
            sb[2].StatValue = (m_pfps / statsUpdatesEveryMS);

            sb[3] = new SimStatsPacket.StatBlock();
            sb[3].StatID = (uint)Stats.AgentUpdates;
            sb[3].StatValue = (m_agentUpdates / statsUpdatesEveryMS);

            sb[4] = new SimStatsPacket.StatBlock();
            sb[4].StatID = (uint)Stats.Agents;
            sb[4].StatValue = m_rootAgents;

            sb[5] = new SimStatsPacket.StatBlock();
            sb[5].StatID = (uint)Stats.ChildAgents;
            sb[5].StatValue = m_childAgents;

            sb[6] = new SimStatsPacket.StatBlock();
            sb[6].StatID = (uint)Stats.TotalPrim;
            sb[6].StatValue = m_numPrim;

            sb[7] = new SimStatsPacket.StatBlock();
            sb[7].StatID = (uint)Stats.InPacketsPerSecond;
            sb[7].StatValue = (int)(m_inPacketsPerSecond / statsUpdatesEveryMS);

            sb[8] = new SimStatsPacket.StatBlock();
            sb[8].StatID = (uint)Stats.OutPacketsPerSecond;
            sb[8].StatValue = (int)(m_outPacketsPerSecond / statsUpdatesEveryMS);

            sb[9] = new SimStatsPacket.StatBlock();
            sb[9].StatID = (uint)Stats.UnAckedBytes;
            sb[9].StatValue = (int) (m_unAckedBytes / statsUpdatesEveryMS);
            
            statpack.Stat = sb;

            if (OnSendStatsResult != null)
            {
                OnSendStatsResult(statpack);
            }
            resetvalues();
            m_report.Enabled = true;
        }

        private void resetvalues()
        {
            m_fps = 0;
            m_pfps = 0;
            m_agentUpdates = 0;
            m_inPacketsPerSecond = 0;
            m_outPacketsPerSecond = 0;
            m_unAckedBytes = 0;

        }
        public void SetTimeDilation(float td)
        {
            m_timeDilation = td;
        }
        public void SetRootAgents(int rootAgents)
        {
            m_rootAgents = rootAgents;
        }
        public void SetChildAgents(int childAgents)
        {
            m_childAgents = childAgents;
        }
        public void SetObjects(int objects)
        {
            m_numPrim = objects;
        }
        public void AddFPS(int frames)
        {
            m_fps += frames;
        }
        public void AddPhysicsFPS(float frames)
        {
            m_pfps += frames;
        }
        public void AddAgentUpdates(float numUpdates)
        {
            m_agentUpdates += numUpdates;
        }
        public void AddInPackets(int numPackets)
        {
            m_inPacketsPerSecond += numPackets;
        }
        public void AddOutPackets(int numPackets)
        {
            m_outPacketsPerSecond += numPackets;
        }
        public void AddunAckedBytes(int numBytes)
        {
            m_unAckedBytes += numBytes;
        }
    }
}
