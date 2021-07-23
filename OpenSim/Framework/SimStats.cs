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

using OpenMetaverse;
using System.Collections.Generic;

namespace OpenSim.Framework
{
    /// <summary>
    /// These are the IDs of stats required by viewers protocol 
    /// </summary>
    /// <remarks>
    /// Some of these are not relevant to OpenSimulator since it is architected differently to other simulators
    /// (e.g. script instructions aren't executed as part of the frame loop so 'script time' is tricky).
    /// </remarks>
    public enum StatsID : uint
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
        SimRegionIdlePossible = 37, // dataserver only
        SimAIStepTimeMS = 38,
        SimSkippedSillouet_PS = 39,
        SimSkippedCharsPerC = 40,

        // extra stats IDs, just far from viewer defined ones
        SimExtraCountStart = 1000,

        internalLSLScriptLinesPerSecond = 1000,
        FrameDilation2 = 1001,
        UsersLoggingIn = 1002,
        TotalGeoPrim = 1003,
        TotalMesh = 1004,
        ScriptEngineThreadCount = 1005,
        NPCs = 1006,

        SimExtraCountEnd = 1007
    }

    // stats values are stored on a float[]
    // so we need readable indexes to it
    // Values sent to viewers via lludp must be first and up to fake index ViewerArraySize
    // fake index ArraySize defines the needed array size
    // this does not follow same order as IDs, because legacy order

    public enum StatsIndex : int
    {
        // index into data array
        TimeDilation = 0,
        SimFPS = 1,
        PhysicsFPS = 2,
        AgentUpdates = 3,
        Agents = 4,
        ChildAgents = 5,
        TotalPrim = 6,
        ActivePrim = 7,
        FrameMS = 8,
        NetMS = 9,
        PhysicsMS = 10,
        ImageMS = 11,
        OtherMS = 12,
        InPacketsPerSecond = 13,
        OutPacketsPerSecond = 14,
        UnAckedBytes = 15,
        AgentMS = 16,
        PendingDownloads = 17,
        PendingUploads = 18,
        ActiveScripts = 19,
        SimSleepMs = 20,
        SimSpareMs = 21,
        SimPhysicsStepMs = 22,
        VirtualSizeKb = 23,
        ResidentSizeKb = 24,
        PendingLocalUploads = 25,
        PhysicsPinnedTasks = 26,
        PhysicsLodTasks = 27,
        ScriptEps = 28,
        SimAIStepTimeMS = 29,
        SimIoPumpTime = 30,
        SimPCTSscriptsRun = 31,
        SimRegionIdle = 32,
        SimRegionIdlePossible = 33,
        SimSkippedSillouet_PS = 34,
        SimSkippedCharsPerC = 35,
        SimPhysicsMemory = 36,
        ScriptMS = 37,

        LSLScriptLinesPerSecond = 38,
        SimPhysicsShapeMs = 39,
        SimPhysicsOtherMs = 40,

        ViewerArraySize = 41,  // just a marker to the end of viewer only stats and start of extra

        internalLSLScriptLinesPerSecond = 41,
        FrameDilation2 = 42,
        UsersLoggingIn = 43,
        TotalGeoPrim = 44,
        TotalMesh = 45,
        ScriptEngineThreadCount = 46,
        NPCs = 47,

        ArraySize = 48 // last is marker for array size
    }

    /// <summary>
    /// Enapsulate statistics for a simulator/scene.
    ///
    /// TODO: This looks very much like the OpenMetaverse SimStatsPacket.  It should be much more generic stats
    /// storage.
    /// </summary>

    public class SimStats
    {
        public uint RegionX;
        public uint RegionY;
        public uint RegionSizeX;
        public uint RegionSizeY;
        public uint RegionFlags;
        public uint ObjectCapacity;
        public UUID RegionUUID;
        public string RegionName;

        public float[] StatsValues
        {
            get { return m_statsValues; }
        }
        private float[] m_statsValues;

        // a fixed array with the IDs for each viewer relevant stat
        // order and size must match StatsIndex enum
        public static readonly uint[] StatsIndexID = new uint[]
        {
            (uint)StatsID.TimeDilation,
            (uint)StatsID.SimFPS,
            (uint)StatsID.PhysicsFPS,
            (uint)StatsID.AgentUpdates,
            (uint)StatsID.Agents,
            (uint)StatsID.ChildAgents,
            (uint)StatsID.TotalPrim,
            (uint)StatsID.ActivePrim,
            (uint)StatsID.FrameMS,
            (uint)StatsID.NetMS,
            (uint)StatsID.PhysicsMS,
            (uint)StatsID.ImageMS,
            (uint)StatsID.OtherMS,
            (uint)StatsID.InPacketsPerSecond,
            (uint)StatsID.OutPacketsPerSecond,
            (uint)StatsID.UnAckedBytes,
            (uint)StatsID.AgentMS,
            (uint)StatsID.PendingDownloads,
            (uint)StatsID.PendingUploads,
            (uint)StatsID.ActiveScripts,
            (uint)StatsID.SimSleepMs,
            (uint)StatsID.SimSpareMs,
            (uint)StatsID.SimPhysicsStepMs,
            (uint)StatsID.VirtualSizeKb,
            (uint)StatsID.ResidentSizeKb,
            (uint)StatsID.PendingLocalUploads,
            (uint)StatsID.PhysicsPinnedTasks,
            (uint)StatsID.PhysicsLodTasks,
            (uint)StatsID.ScriptEps,
            (uint)StatsID.SimAIStepTimeMS,
            (uint)StatsID.SimIoPumpTime,
            (uint)StatsID.SimPCTSscriptsRun,
            (uint)StatsID.SimRegionIdle,
            (uint)StatsID.SimRegionIdlePossible,
            (uint)StatsID.SimSkippedSillouet_PS,
            (uint)StatsID.SimSkippedCharsPerC,
            (uint)StatsID.SimPhysicsMemory,
            (uint)StatsID.ScriptMS,

            (uint)StatsID.LSLScriptLinesPerSecond,
            (uint)StatsID.SimPhysicsShapeMs,
            (uint)StatsID.SimPhysicsOtherMs,

            (uint)StatsID.internalLSLScriptLinesPerSecond,
            (uint)StatsID.FrameDilation2,
            (uint)StatsID.UsersLoggingIn,
            (uint)StatsID.TotalGeoPrim,
            (uint)StatsID.TotalMesh,
            (uint)StatsID.ScriptEngineThreadCount,
            (uint)StatsID.NPCs
        };

        public SimStats(
            uint regionX, uint regionY,
            uint regionSizeX, uint regionSizeY,
            uint regionFlags, uint objectCapacity,
            float[] values,
            UUID pRUUID, string regionName)
        {
            RegionUUID = pRUUID;
            RegionName = regionName;
            RegionX = regionX;
            RegionY = regionY;
            RegionSizeX = regionSizeX;
            RegionSizeY = regionSizeY;
            RegionFlags = regionFlags;
            ObjectCapacity = objectCapacity;
            m_statsValues = values;
        }
    }
}
