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
using System.Text;

namespace OpenSim.Region.UserStatistics
{
    public static class HTMLUtil
    {

        public static void TR_O(ref StringBuilder o, string pclass)
        {
            o.Append("<tr");
            if (pclass.Length > 0)
            {
                GenericClass(ref o, pclass);
            }
            o.Append(">\n\t");
        }

        public static void TR_C(ref StringBuilder o)
        {
            o.Append("</tr>\n");
        }

        public static void TD_O(ref StringBuilder o, string pclass)
        {
            TD_O(ref o, pclass, 0, 0);
        }

        public static void TD_O(ref StringBuilder o, string pclass, int rowspan, int colspan)
        {
            o.Append("<td");
            if (pclass.Length > 0)
            {
                GenericClass(ref o, pclass);
            }
            if (rowspan > 1)
            {
                o.Append(" rowspan=\"");
                o.Append(rowspan);
                o.Append("\"");
            }
            if (colspan > 1)
            {
                o.Append(" colspan=\"");
                o.Append(colspan);
                o.Append("\"");
            }
            o.Append(">");
        }

        public static void TD_C(ref StringBuilder o)
        {
            o.Append("</td>");
        }

        public static void TABLE_O(ref StringBuilder o, string pclass)
        {
            o.Append("<table");
            if (pclass.Length > 0)
            {
                GenericClass(ref o, pclass);
            }
            o.Append(">\n\t");
        }

        public static void TABLE_C(ref StringBuilder o)
        {
            o.Append("</table>\n");
        }

        public static void BLOCKQUOTE_O(ref StringBuilder o, string pclass)
        {
            o.Append("<blockquote");
            if (pclass.Length > 0)
            {
                GenericClass(ref o, pclass);
            }
            o.Append(" />\n");
        }

        public static void BLOCKQUOTE_C(ref StringBuilder o)
        {
            o.Append("</blockquote>\n");
        }

        public static void BR(ref StringBuilder o)
        {
            o.Append("<br />\n");
        }

        public static void HR(ref StringBuilder o, string pclass)
        {
            o.Append("<hr");
            if (pclass.Length > 0)
            {
                GenericClass(ref o, pclass);
            }
            o.Append(" />\n");
        }

        public static void UL_O(ref StringBuilder o, string pclass)
        {
            o.Append("<ul");
            if (pclass.Length > 0)
            {
                GenericClass(ref o, pclass);
            }
            o.Append(" />\n");
        }

        public static void UL_C(ref StringBuilder o)
        {
            o.Append("</ul>\n");
        }

        public static void OL_O(ref StringBuilder o, string pclass)
        {
            o.Append("<ol");
            if (pclass.Length > 0)
            {
                GenericClass(ref o, pclass);
            }
            o.Append(" />\n");
        }

        public static void OL_C(ref StringBuilder o)
        {
            o.Append("</ol>\n");
        }

        public static void LI_O(ref StringBuilder o, string pclass)
        {
            o.Append("<li");
            if (pclass.Length > 0)
            {
                GenericClass(ref o, pclass);
            }
            o.Append(" />\n");
        }

        public static void LI_C(ref StringBuilder o)
        {
            o.Append("</li>\n");
        }

        public static void GenericClass(ref StringBuilder o, string pclass)
        {
            o.Append(" class=\"");
            o.Append(pclass);
            o.Append("\"");
        }

        public static void InsertProtoTypeAJAX(ref StringBuilder o)
        {
            o.Append("<script type=\"text/javascript\" src=\"prototype.js\"></script>\n");
            o.Append("<script type=\"text/javascript\" src=\"updater.js\"></script>\n");
        }

        public static void InsertPeriodicUpdaters(ref StringBuilder o, string[] divID, int[] seconds, string[] reportfrag)
        {
            o.Append("<script type=\"text/javascript\">\n");
            o.Append(
                @"
                // <![CDATA[
                document.observe('dom:loaded', function() {
                    /*
                    first arg    : div to update
                    second arg    : interval to poll in seconds
                    third arg    : file to get data
                    */
");
            for (int i = 0; i < divID.Length; i++)
            {

                o.Append("new updater('");
                o.Append(divID[i]);
                o.Append("', ");
                o.Append(seconds[i]);
                o.Append(", '");
                o.Append(reportfrag[i]);
                o.Append("');\n");
            }

            o.Append(@"
                });
                // ]]>
                </script>");
        }

        public static void HtmlHeaders_O(ref StringBuilder o)
        {
            o.Append("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">\n");
            o.Append("<html xmlns=\"http://www.w3.org/1999/xhtml\" xml:lang=\"nl\">");
            o.Append("<head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=iso-8859-1\" />");
        }

        public static void HtmlHeaders_C(ref StringBuilder o)
        {
            o.Append("</HEAD>");
            o.Append("<BODY>");
        }

        public static void AddReportLinks(ref StringBuilder o, Dictionary<string, IStatsController> reports, string pClass)
        {
            int repcount = 0;
            foreach (string str in reports.Keys)
            {
                if (reports[str].ReportName.Length > 0)
                {
                    if (repcount > 0)
                    {
                        o.Append("|&nbsp;&nbsp;");
                    }
                    A(ref o, reports[str].ReportName, str, pClass);
                    o.Append("&nbsp;&nbsp;");
                    repcount++;
                }
            }
        }

        public static void A(ref StringBuilder o, string linktext, string linkhref, string pClass)
        {
            o.Append("<A");
            if (pClass.Length > 0)
            {
                GenericClass(ref o, pClass);
            }
            o.Append(" href=\"");
            o.Append(linkhref);
            o.Append("\">");
            o.Append(linktext);
            o.Append("</A>");
        }
    }
}
