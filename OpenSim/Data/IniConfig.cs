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

using System.IO;
using System.Text.RegularExpressions;

/*
 Taken from public code listing at by Alex Pinsker
 http://alexpinsker.blogspot.com/2005/12/reading-ini-file-from-c_113432097333021549.html
 */

namespace OpenSim.Data
{
    /// <summary>
    /// Parse settings from ini-like files
    /// </summary>
    public class IniFile
    {
        static IniFile()
        {
            _iniKeyValuePatternRegex = new Regex(
                @"((\s)*(?<Key>([^\=^\s^\n]+))[\s^\n]*
                # key part (surrounding whitespace stripped)
                \=
                (\s)*(?<Value>([^\n^\s]+(\n){0,1})))
                # value part (surrounding whitespace stripped)
                ",
                RegexOptions.IgnorePatternWhitespace |
                RegexOptions.Compiled |
                RegexOptions.CultureInvariant);
        }

        private static Regex _iniKeyValuePatternRegex;

        public IniFile(string iniFileName)
        {
            _iniFileName = iniFileName;
        }

        public string ParseFileReadValue(string key)
        {
            using (StreamReader reader =
                new StreamReader(_iniFileName))
            {
                do
                {
                    string line = reader.ReadLine();
                    Match match =
                        _iniKeyValuePatternRegex.Match(line);
                    if (match.Success)
                    {
                        string currentKey =
                            match.Groups["Key"].Value as string;
                        if (currentKey != null &&
                            currentKey.Trim().CompareTo(key) == 0)
                        {
                            string value =
                                match.Groups["Value"].Value as string;
                            return value;
                        }
                    }
                } while (reader.Peek() != -1);
            }
            return null;
        }

        public string IniFileName
        {
            get { return _iniFileName; }
        }

        private string _iniFileName;
    }
}
