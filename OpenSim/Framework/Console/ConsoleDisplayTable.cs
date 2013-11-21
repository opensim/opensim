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
using System.Linq;
using System.Text;

namespace OpenSim.Framework.Console
{
    /// <summary>
    /// Used to generated a formatted table for the console.
    /// </summary>
    /// <remarks>
    /// Currently subject to change.  If you use this, be prepared to change your code when this class changes.
    /// </remarks>
    public class ConsoleDisplayTable
    {
        /// <summary>
        /// Default number of spaces between table columns.
        /// </summary>
        public const int DefaultTableSpacing = 2;

        /// <summary>
        /// Table columns.
        /// </summary>
        public List<ConsoleDisplayTableColumn> Columns { get; private set; }

        /// <summary>
        /// Table rows
        /// </summary>
        public List<ConsoleDisplayTableRow> Rows { get; private set; }

        /// <summary>
        /// Number of spaces to indent the whole table.
        /// </summary>
        public int Indent { get; set; }

        /// <summary>
        /// Spacing between table columns
        /// </summary>
        public int TableSpacing { get; set; }

        public ConsoleDisplayTable()
        {
            TableSpacing = DefaultTableSpacing;
            Columns = new List<ConsoleDisplayTableColumn>();
            Rows = new List<ConsoleDisplayTableRow>();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            AddToStringBuilder(sb);
            return sb.ToString();
        }

        public void AddColumn(string name, int width)
        {
            Columns.Add(new ConsoleDisplayTableColumn(name, width));
        }

        public void AddRow(params object[] cells)
        {
            Rows.Add(new ConsoleDisplayTableRow(cells));
        }

        public void AddToStringBuilder(StringBuilder sb)
        {
            string formatString = GetFormatString();
//            System.Console.WriteLine("FORMAT STRING [{0}]", formatString);

            // columns
            sb.AppendFormat(formatString, Columns.ConvertAll(c => c.Header).ToArray());

            // rows
            foreach (ConsoleDisplayTableRow row in Rows)
                sb.AppendFormat(formatString, row.Cells.ToArray());
        }

        /// <summary>
        /// Gets the format string for the table.
        /// </summary>
        private string GetFormatString()
        {
            StringBuilder formatSb = new StringBuilder();

            formatSb.Append(' ', Indent);

            for (int i = 0; i < Columns.Count; i++)
            {
                if (i != 0)
                    formatSb.Append(' ', TableSpacing);

                // Can only do left formatting for now
                formatSb.AppendFormat("{{{0},-{1}}}", i, Columns[i].Width);
            }

            formatSb.Append('\n');

            return formatSb.ToString();
        }
    }

    public struct ConsoleDisplayTableColumn
    {
        public string Header { get; set; }
        public int Width { get; set; }

        public ConsoleDisplayTableColumn(string header, int width) : this()
        {
            Header = header;
            Width = width;
        }
    }

    public struct ConsoleDisplayTableRow
    {
        public List<object> Cells { get; private set; }

        public ConsoleDisplayTableRow(List<object> cells) : this()
        {
            Cells = cells;
        }

        public ConsoleDisplayTableRow(params object[] cells) : this()
        {
            Cells = new List<object>(cells);
        }
    }
}