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
* 
*/

using System;
using System.Timers;
using libsecondlife.Packets;
using OpenSim.Framework;

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
            ActivePrim = 12,
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
        private int m_activePrim = 0;
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
            SimStatsPacket statpack = (SimStatsPacket) PacketPool.Instance.GetPacket(PacketType.SimStats);
            // TODO: don't create new blocks if recycling an old packet

            SimStatsPacket.StatBlock[] sb = new SimStatsPacket.StatBlock[11];
            statpack.Region = new SimStatsPacket.RegionBlock();
            statpack.Region.RegionX = ReportingRegion.RegionLocX;
            statpack.Region.RegionY = ReportingRegion.RegionLocY;
            try
            {
                statpack.Region.RegionFlags = (uint) ReportingRegion.EstateSettings.regionFlags;
            }
            catch (Exception)
            {
                statpack.Region.RegionFlags = (uint) 0;
            }
            statpack.Region.ObjectCapacity = (uint) 15000;

            #region various statistic googly moogly

            float simfps = (int) (m_fps*5);

            if (simfps > 45)
                simfps = simfps - (simfps - 45);
            if (simfps < 0)
                simfps = 0;

            float physfps = (m_pfps/statsUpdatesEveryMS);

            if (physfps > 50)
                physfps = physfps - (physfps - 50);

            if (physfps < 0)
                physfps = 0;

            #endregion

            sb[0] = new SimStatsPacket.StatBlock();
            sb[0].StatID = (uint) Stats.TimeDilation;
            sb[0].StatValue = (m_timeDilation);

            sb[1] = new SimStatsPacket.StatBlock();
            sb[1].StatID = (uint) Stats.SimFPS;
            sb[1].StatValue = simfps;

            sb[2] = new SimStatsPacket.StatBlock();
            sb[2].StatID = (uint) Stats.PhysicsFPS;
            sb[2].StatValue = physfps;

            sb[3] = new SimStatsPacket.StatBlock();
            sb[3].StatID = (uint) Stats.AgentUpdates;
            sb[3].StatValue = (m_agentUpdates/statsUpdatesEveryMS);

            sb[4] = new SimStatsPacket.StatBlock();
            sb[4].StatID = (uint) Stats.Agents;
            sb[4].StatValue = m_rootAgents;

            sb[5] = new SimStatsPacket.StatBlock();
            sb[5].StatID = (uint) Stats.ChildAgents;
            sb[5].StatValue = m_childAgents;

            sb[6] = new SimStatsPacket.StatBlock();
            sb[6].StatID = (uint) Stats.TotalPrim;
            sb[6].StatValue = m_numPrim;

            sb[7] = new SimStatsPacket.StatBlock();
            sb[7].StatID = (uint) Stats.ActivePrim;
            sb[7].StatValue = m_activePrim;

            sb[8] = new SimStatsPacket.StatBlock();
            sb[8].StatID = (uint) Stats.InPacketsPerSecond;
            sb[8].StatValue = (int) (m_inPacketsPerSecond/statsUpdatesEveryMS);

            sb[9] = new SimStatsPacket.StatBlock();
            sb[9].StatID = (uint) Stats.OutPacketsPerSecond;
            sb[9].StatValue = (int) (m_outPacketsPerSecond/statsUpdatesEveryMS);

            sb[10] = new SimStatsPacket.StatBlock();
            sb[10].StatID = (uint) Stats.UnAckedBytes;
            sb[10].StatValue = (int) (m_unAckedBytes/statsUpdatesEveryMS);

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
            if (m_timeDilation > 1.0f)
                m_timeDilation = (m_timeDilation - (m_timeDilation - 0.91f));

            if (m_timeDilation < 0)
                m_timeDilation = 0.0f;
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

        public void SetActiveObjects(int objects)
        {
            m_activePrim = objects;
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
