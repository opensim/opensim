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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Mono.Data.SqliteClient;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Monitoring;

namespace OpenSim.Region.UserStatistics
{
    public class SimStatsAJAX : IStatsController
    {
        #region IStatsController Members

        public string ReportName
        {
            get { return ""; }
        }

        public Hashtable ProcessModel(Hashtable pParams)
        {
            List<Scene> m_scene = (List<Scene>)pParams["Scenes"];
            
            Hashtable nh = new Hashtable();
            nh.Add("hdata", m_scene);
            nh.Add("simstats", pParams["SimStats"]);
            return nh;
        }

        public string RenderView(Hashtable pModelResult)
        {
            StringBuilder output = new StringBuilder();
            List<Scene> all_scenes = (List<Scene>) pModelResult["hdata"];
            Dictionary<UUID, USimStatsData> sdatadic = (Dictionary<UUID,USimStatsData>)pModelResult["simstats"];

            const string TableClass = "defaultr";
            const string TRClass = "defaultr";
            const string TDHeaderClass = "header";
            const string TDDataClass = "content";
            //const string TDDataClassRight = "contentright";
            const string TDDataClassCenter = "contentcenter";

            foreach (USimStatsData sdata in sdatadic.Values)
            {


                foreach (Scene sn in all_scenes)
                {
                    if (sn.RegionInfo.RegionID == sdata.RegionId)
                    {
                        output.Append("<H2>");
                        output.Append(sn.RegionInfo.RegionName);
                        output.Append("</H2>");
                    }
                }
                HTMLUtil.TABLE_O(ref output, TableClass);
                HTMLUtil.TR_O(ref output, TRClass);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("Dilatn");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("SimFPS");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("PhysFPS");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("AgntUp");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("RootAg");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("ChldAg");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("Prims");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("ATvPrm");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("AtvScr");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("ScrLPS");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TR_C(ref output);
                HTMLUtil.TR_O(ref output, TRClass);
                HTMLUtil.TD_O(ref output, TDDataClass);
                output.Append(sdata.TimeDilation);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClass);
                output.Append(sdata.SimFps);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.PhysicsFps);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.AgentUpdates);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.RootAgents);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.ChildAgents);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.TotalPrims);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.ActivePrims);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.ActiveScripts);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.ScriptLinesPerSecond);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TR_C(ref output);
                HTMLUtil.TR_O(ref output, TRClass);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("FrmMS");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("AgtMS");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("PhysMS");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("OthrMS");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("OutPPS");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("InPPS");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("NoAckKB");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("PndDWN");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDHeaderClass);
                output.Append("PndUP");
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TR_C(ref output);
                HTMLUtil.TR_O(ref output, TRClass);
                HTMLUtil.TD_O(ref output, TDDataClass);
                output.Append(sdata.TotalFrameTime);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClass);
                output.Append(sdata.AgentFrameTime);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.PhysicsFrameTime);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.OtherFrameTime);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.OutPacketsPerSecond);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.InPacketsPerSecond);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.UnackedBytes);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.PendingDownloads);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TD_O(ref output, TDDataClassCenter);
                output.Append(sdata.PendingUploads);
                HTMLUtil.TD_C(ref output);
                HTMLUtil.TR_C(ref output);
                HTMLUtil.TABLE_C(ref output);
                
            }
            
            return output.ToString();
        }

        /// <summary>
        /// Return stat information for all regions in the sim. Returns data of the form:
        /// <pre>
        /// {"REGIONNAME": {
        ///     "region": "REGIONNAME",
        ///     "timeDilation": "101", 
        ///     ...     // the rest of the stat info
        ///     },
        ///  ...    // entries for each region
        ///  }
        /// </pre>
        /// </summary>
        /// <param name="pModelResult"></param>
        /// <returns></returns>
        public string RenderJson(Hashtable pModelResult)
        {
            List<Scene> all_scenes = (List<Scene>) pModelResult["hdata"];
            Dictionary<UUID, USimStatsData> sdatadic = (Dictionary<UUID,USimStatsData>)pModelResult["simstats"];

            OSDMap allStatsInfo = new OpenMetaverse.StructuredData.OSDMap();
            foreach (USimStatsData sdata in sdatadic.Values)
            {
                OSDMap statsInfo = new OpenMetaverse.StructuredData.OSDMap();
                string regionName = "unknown";
                foreach (Scene sn in all_scenes)
                {
                    if (sn.RegionInfo.RegionID == sdata.RegionId)
                    {
                        regionName = sn.RegionInfo.RegionName;
                        break;
                    }
                }
                statsInfo.Add("region", new OSDString(regionName));
                statsInfo.Add("timeDilation", new OSDString(sdata.TimeDilation.ToString()));
                statsInfo.Add("simFPS", new OSDString(sdata.SimFps.ToString()));
                statsInfo.Add("physicsFPS", new OSDString(sdata.PhysicsFps.ToString()));
                statsInfo.Add("agentUpdates", new OSDString(sdata.AgentUpdates.ToString()));
                statsInfo.Add("rootAgents", new OSDString(sdata.RootAgents.ToString()));
                statsInfo.Add("childAgents", new OSDString(sdata.ChildAgents.ToString()));
                statsInfo.Add("totalPrims", new OSDString(sdata.TotalPrims.ToString()));
                statsInfo.Add("activePrims", new OSDString(sdata.ActivePrims.ToString()));
                statsInfo.Add("activeScripts", new OSDString(sdata.ActiveScripts.ToString()));
                statsInfo.Add("scriptLinesPerSec", new OSDString(sdata.ScriptLinesPerSecond.ToString()));
                statsInfo.Add("totalFrameTime", new OSDString(sdata.TotalFrameTime.ToString()));
                statsInfo.Add("agentFrameTime", new OSDString(sdata.AgentFrameTime.ToString()));
                statsInfo.Add("physicsFrameTime", new OSDString(sdata.PhysicsFrameTime.ToString()));
                statsInfo.Add("otherFrameTime", new OSDString(sdata.OtherFrameTime.ToString()));
                statsInfo.Add("outPacketsPerSec", new OSDString(sdata.OutPacketsPerSecond.ToString()));
                statsInfo.Add("inPacketsPerSec", new OSDString(sdata.InPacketsPerSecond.ToString()));
                statsInfo.Add("unackedByptes", new OSDString(sdata.UnackedBytes.ToString()));
                statsInfo.Add("pendingDownloads", new OSDString(sdata.PendingDownloads.ToString()));
                statsInfo.Add("pendingUploads", new OSDString(sdata.PendingUploads.ToString()));

                allStatsInfo.Add(regionName, statsInfo);
            }
            return allStatsInfo.ToString();
        }

        #endregion
    }
}
