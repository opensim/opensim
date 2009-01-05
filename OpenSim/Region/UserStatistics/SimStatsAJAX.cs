using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Mono.Data.SqliteClient;
using OpenMetaverse;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework.Statistics;

namespace OpenSim.Region.UserStatistics
{
    public class SimStatsAJAX : IStatsController
    {
        #region IStatsController Members

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
                output.Append("ScrLPS");
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
                output.Append(sdata.ScriptLinesPerSecond);
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

        #endregion
    }
}
