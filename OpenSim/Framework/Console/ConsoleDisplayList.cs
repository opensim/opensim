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
    public class ConsoleDisplayList
    {
        /// <summary>
        /// The default divider between key and value for a list item.
        /// </summary>
        public const string DefaultKeyValueDivider = " : ";

        /// <summary>
        /// The divider used between key and value for a list item.
        /// </summary>
        public string KeyValueDivider { get; set; }

        /// <summary>
        /// Table rows
        /// </summary>
        public List<KeyValuePair<string, string>> Rows { get; private set; }

        /// <summary>
        /// Number of spaces to indent the list.
        /// </summary>
        public int Indent { get; set; }

        public ConsoleDisplayList()
        {
            Rows = [];
            KeyValueDivider = DefaultKeyValueDivider;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            AddToStringBuilder(sb);
            return sb.ToString();
        }

        public void AddToStringBuilder(StringBuilder sb)
        {
            var formatString = GetFormatString();
//            System.Console.WriteLine("FORMAT STRING [{0}]", formatString);

            // rows
            foreach (var row in Rows)
                sb.AppendFormat(formatString, row.Key, row.Value);
        }

        /// <summary>
        /// Gets the format string for the table.
        /// </summary>
        private string GetFormatString()
        {
            var formatSb = new StringBuilder();

            var longestKey = Rows.Select(row => row.Key.Length).Prepend(-1).Max();

            formatSb.Append(' ', Indent);

            // Can only do left formatting for now
            formatSb.AppendFormat("{{0,-{0}}}{1}{{1}}\n", longestKey, KeyValueDivider);

            return formatSb.ToString();
        }

        public void AddRow(object key, object value)
        {
            Rows.Add(new KeyValuePair<string, string>(key.ToString(), value.ToString()));
        }
    }
}