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
            o.Append("<td");
            if (pclass.Length > 0)
            {
                GenericClass(ref o, pclass);
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
					first arg	: div to update
					second arg	: interval to poll in seconds
					third arg	: file to get data
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

        public static void HtmlHeaders_O ( ref StringBuilder o)
        {
            o.Append("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">\n");
		    o.Append("<html xmlns=\"http://www.w3.org/1999/xhtml\" xml:lang=\"nl\">");
            o.Append("<head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=iso-8859-1\" />");


        }
        public static void HtmlHeaders_C ( ref StringBuilder o)
        {
            o.Append("</HEAD>");
            o.Append("<BODY>");
        }
    }
}
