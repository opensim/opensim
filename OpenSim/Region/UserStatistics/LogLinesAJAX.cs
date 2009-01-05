using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Data.SqliteClient;
using OpenMetaverse;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework.Statistics;

namespace OpenSim.Region.UserStatistics
{
    public class LogLinesAJAX : IStatsController
    {
        private Regex normalizeEndLines = new Regex(@"\r\n", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);

        private Regex webFormat = new Regex(@"[^\s]*\s([^,]*),[^\s]*\s([A-Z]*)[^\s-][^\[]*\[([^\]]*)\]([^\n]*)",
                                            RegexOptions.Singleline | RegexOptions.Compiled);
        private Regex TitleColor = new Regex(@"[^\s]*\s(?:[^,]*),[^\s]*\s(?:[A-Z]*)[^\s-][^\[]*\[([^\]]*)\](?:[^\n]*)",
                                    RegexOptions.Singleline | RegexOptions.Compiled);


        #region IStatsController Members

        public Hashtable ProcessModel(Hashtable pParams)
        {
            Hashtable nh = new Hashtable();
            nh.Add("loglines", pParams["LogLines"]);
            return nh;
        }

        public string RenderView(Hashtable pModelResult)
        {
            StringBuilder output = new StringBuilder();
            
            HTMLUtil.HR(ref output, "");
            output.Append("<H3>ActiveLog</H3>\n");

            string tmp = normalizeEndLines.Replace(pModelResult["loglines"].ToString(), "\n");

            string[] result = Regex.Split(tmp, "\n");

            string formatopen = "";
            string formatclose = "";

            for (int i = 0; i < result.Length;i++ )
            {
                if (result[i].Length >= 30)
                {
                    string logtype = result[i].Substring(24, 6);
                    switch (logtype)
                    {
                        case "WARN  ":
                            formatopen = "<font color=\"#7D7C00\">";
                            formatclose = "</font>";
                            break;

                        case "ERROR ":
                            formatopen = "<font color=\"#FF0000\">";
                            formatclose = "</font>";
                            break;

                        default:
                            formatopen = "";
                            formatclose = "";
                            break;

                    }
                }
                StringBuilder replaceStr = new StringBuilder();
                //string titlecolorresults = 
                
                string formatresult = Regex.Replace(TitleColor.Replace(result[i], "$1"), "[^ABCDEFabcdef0-9]", "");
                if (formatresult.Length > 6)
                {
                    formatresult = formatresult.Substring(0, 6);
                   
                }
                for (int j = formatresult.Length; j <= 5; j++)
                    formatresult += "0";
                replaceStr.Append("$1 - [<font color=\"#");
                replaceStr.Append(formatresult);
                replaceStr.Append("\">$3</font>] $4<br />");
                string repstr = replaceStr.ToString();
                
                output.Append(formatopen);
                output.Append(webFormat.Replace(result[i], repstr));
                output.Append(formatclose);
            }
                

            return output.ToString();
        }

        #endregion
    }
}
