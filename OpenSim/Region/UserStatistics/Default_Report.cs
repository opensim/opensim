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
    public class Default_Report : IStatsController
    {

        public Default_Report()
        {

        }

        #region IStatsController Members

        public Hashtable ProcessModel(Hashtable pParams)
        {
            SqliteConnection conn = (SqliteConnection)pParams["DatabaseConnection"];
            List<Scene> m_scene = (List<Scene>)pParams["Scenes"];

            stats_default_page_values mData = rep_DefaultReport_data(conn, m_scene);
            mData.sim_stat_data = (Dictionary<UUID,USimStatsData>)pParams["SimStats"];

            Hashtable nh = new Hashtable();
            nh.Add("hdata", mData);
            
            return nh;
        }

        public string RenderView(Hashtable pModelResult)
        {
            stats_default_page_values mData = (stats_default_page_values) pModelResult["hdata"];
            return rep_Default_report_view(mData);
        }

        #endregion

        public string rep_Default_report_view(stats_default_page_values values)
        {

            StringBuilder output = new StringBuilder();



            const string TableClass = "defaultr";
            const string TRClass = "defaultr";
            const string TDHeaderClass = "header";
            const string TDDataClass = "content";
            //const string TDDataClassRight = "contentright";
            const string TDDataClassCenter = "contentcenter";

            const string STYLESHEET =
                @"
<STYLE>
body
{
	font-size:15px; font-family:Helvetica, Verdana; color:Black;
}
TABLE.defaultr { }
TR.defaultr { padding: 5px; }
TD.header { font-weight:bold; padding:5px; }
TD.content {}
TD.contentright { text-align: right; }
TD.contentcenter { text-align: center; }
TD.align_top { vertical-align: top; }
</STYLE>
";
            HTMLUtil.HtmlHeaders_O(ref output);
            
            HTMLUtil.InsertProtoTypeAJAX(ref output);
            string[] ajaxUpdaterDivs = new string[2];
            int[] ajaxUpdaterSeconds = new int[2];
            string[] ajaxUpdaterReportFragments = new string[2];

            ajaxUpdaterDivs[0] = "activeconnections";
            ajaxUpdaterSeconds[0] = 10;
            ajaxUpdaterReportFragments[0] = "activeconnectionsajax.ajax";

            ajaxUpdaterDivs[1] = "activesimstats";
            ajaxUpdaterSeconds[1] = 20;
            ajaxUpdaterReportFragments[1] = "simstatsajax.ajax";

            HTMLUtil.InsertPeriodicUpdaters(ref output, ajaxUpdaterDivs, ajaxUpdaterSeconds, ajaxUpdaterReportFragments);
            
            output.Append(STYLESHEET);
            HTMLUtil.HtmlHeaders_C(ref output);
            
            HTMLUtil.TABLE_O(ref output, TableClass);
            HTMLUtil.TR_O(ref output, TRClass);
            HTMLUtil.TD_O(ref output, TDHeaderClass);
            output.Append("# Users Total");
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, TDHeaderClass);
            output.Append("# Sessions Total");
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, TDHeaderClass);
            output.Append("Avg Client FPS");
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, TDHeaderClass);
            output.Append("Avg Client Mem Use");
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, TDHeaderClass);
            output.Append("Avg Sim FPS");
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, TDHeaderClass);
            output.Append("Avg Ping");
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, TDHeaderClass);
            output.Append("KB Out Total");
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, TDHeaderClass);
            output.Append("KB In Total");
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TR_C(ref output);
            HTMLUtil.TR_O(ref output, TRClass);
            HTMLUtil.TD_O(ref output, TDDataClass);
            output.Append(values.total_num_users);
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, TDDataClass);
            output.Append(values.total_num_sessions);
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, TDDataClassCenter);
            output.Append(values.avg_client_fps);
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, TDDataClassCenter);
            output.Append(values.avg_client_mem_use);
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, TDDataClassCenter);
            output.Append(values.avg_sim_fps);
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, TDDataClassCenter);
            output.Append(values.avg_ping);
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, TDDataClassCenter);
            output.Append(values.total_kb_out);
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, TDDataClassCenter);
            output.Append(values.total_kb_in);
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TR_C(ref output);
            HTMLUtil.TABLE_C(ref output);

            HTMLUtil.HR(ref output, "");
            HTMLUtil.TABLE_O(ref output, "");
            HTMLUtil.TR_O(ref output, "");
            HTMLUtil.TD_O(ref output, "align_top");
            output.Append("<DIV id=\"activeconnections\">loading...</DIV>");
            HTMLUtil.TD_C(ref output);
            HTMLUtil.TD_O(ref output, "align_top");
            output.Append("<DIV id=\"activesimstats\">loading...</DIV>");

            HTMLUtil.TD_C(ref output);
            HTMLUtil.TR_C(ref output);
            HTMLUtil.TABLE_C(ref output);
            output.Append("</BODY></HTML>");
            // TODO: FIXME: template
            return output.ToString();
        }

       

        public stats_default_page_values rep_DefaultReport_data(SqliteConnection db, List<Scene> m_scene)
        {
            stats_default_page_values returnstruct = new stats_default_page_values();
            returnstruct.all_scenes = m_scene.ToArray();
            lock (db)
            {
                string SQL = @"SELECT COUNT(DISTINCT agent_id) as agents, COUNT(*) as sessions, AVG(avg_fps) as client_fps, 
                                AVG(avg_sim_fps) as savg_sim_fps, AVG(avg_ping) as sav_ping, SUM(n_out_kb) as num_in_kb, 
                                SUM(n_out_pk) as num_in_packets, SUM(n_in_kb) as num_out_kb, SUM(n_in_pk) as num_out_packets, AVG(mem_use) as sav_mem_use
                                FROM stats_session_data;";
                SqliteCommand cmd = new SqliteCommand(SQL, db);
                SqliteDataReader sdr = cmd.ExecuteReader();
                if (sdr.HasRows)
                {
                    sdr.Read();
                    returnstruct.total_num_users = Convert.ToInt32(sdr["agents"]);
                    returnstruct.total_num_sessions = Convert.ToInt32(sdr["sessions"]);
                    returnstruct.avg_client_fps = Convert.ToSingle(sdr["client_fps"]);
                    returnstruct.avg_sim_fps = Convert.ToSingle(sdr["savg_sim_fps"]);
                    returnstruct.avg_ping = Convert.ToSingle(sdr["sav_ping"]);
                    returnstruct.total_kb_out = Convert.ToSingle(sdr["num_out_kb"]);
                    returnstruct.total_kb_in = Convert.ToSingle(sdr["num_in_kb"]);
                    returnstruct.avg_client_mem_use = Convert.ToSingle(sdr["sav_mem_use"]);

                }
            }
            return returnstruct;
        }

    }

    public struct stats_default_page_values
    {
        public int total_num_users;
        public int total_num_sessions;
        public float avg_client_fps;
        public float avg_client_mem_use;
        public float avg_sim_fps;
        public float avg_ping;
        public float total_kb_out;
        public float total_kb_in;
        public float avg_client_resends;
        public Scene[] all_scenes;
        public Dictionary<UUID, USimStatsData> sim_stat_data;
    }
}
