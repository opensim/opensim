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
using System.Text.RegularExpressions;
using Mono.Data.SqliteClient;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Monitoring;

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

        public string ReportName
        {
            get { return ""; }
        }

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

            for (int i = 0; i < result.Length; i++)
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

        /// <summary>
        /// Return the last log lines. Output in the format:
        /// <pre>
        /// {"logLines": [
        /// "line1",
        /// "line2",
        /// ...
        /// ]
        /// }
        /// </pre>
        /// </summary>
        /// <param name="pModelResult"></param>
        /// <returns></returns>
        public string RenderJson(Hashtable pModelResult)
        {
            OSDMap logInfo = new OpenMetaverse.StructuredData.OSDMap();

            OSDArray logLines = new OpenMetaverse.StructuredData.OSDArray();
            string tmp = normalizeEndLines.Replace(pModelResult["loglines"].ToString(), "\n");
            string[] result = Regex.Split(tmp, "\n");
            for (int i = 0; i < result.Length; i++)
            {
                logLines.Add(new OSDString(result[i]));
            }
            logInfo.Add("logLines", logLines);
            return logInfo.ToString();
        }

        #endregion
    }
}
