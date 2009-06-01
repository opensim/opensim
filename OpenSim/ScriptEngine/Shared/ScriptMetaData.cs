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

namespace OpenSim.ScriptEngine.Shared
{
    public class ScriptMetaData: Dictionary<string, string>
    {
        private static readonly char[] LineSeparator = "\r\n".ToCharArray();
        private static readonly char[] Separator = { ':' };
        public static ScriptMetaData Extract(ref string Script)
        {
            ScriptMetaData ret = new ScriptMetaData();
            if (string.IsNullOrEmpty(Script))
                return ret;

            // Process it line by line
            string Line = "";
            for (int i = 0; i < Script.Length + 1; i++)
            {
                // Found a line separator?
                if (i < Script.Length
                    && Script[i] != LineSeparator[0]
                    && Script[i] != LineSeparator[1])
                {
                    // No, not end of line. Add to current line
                    Line += Script[i];
                }
                else
                {
                    // Extract MetaData from this line. Returns False if not found.
                    if (!_GetMetaFromLine(ret, Line))
                        continue;
                    // Empty for next round
                    Line = "";
                }
            }
            return ret;
        }

        private static bool _GetMetaFromLine(ScriptMetaData ret, string line)
        {
            line = line.Trim();

            // Empty line? We may find more later
            if (line == "")
                return true;

            // Is this a comment? If not, then return false
            if (!line.StartsWith("//"))
                return false;

            // It is a comment
            string[] keyval = line.Split(Separator, 2, StringSplitOptions.None);
            keyval[0] = keyval[0].Substring(2, keyval[0].Length - 2).Trim();
            keyval[1] = keyval[1].Trim();

            // Add it
            if (keyval[0] != "")
                if (!ret.ContainsKey(keyval[0]))
                {
                    //m_log.DebugFormat("[DotNetEngine] Script metadata: Key: \"{0}\", Value: \"{1}\".", keyval[0], keyval[1]);
                    ret.Add(keyval[0], keyval[1]);
                }

            return true;
        }

    }
}