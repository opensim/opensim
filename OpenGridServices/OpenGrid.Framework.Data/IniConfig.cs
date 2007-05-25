using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

/* 
 Taken from public code listing at by Alex Pinsker
 http://alexpinsker.blogspot.com/2005/12/reading-ini-file-from-c_113432097333021549.html
 */

namespace OpenGrid.Framework.Data
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
        static private Regex _iniKeyValuePatternRegex;

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

                }
                while (reader.Peek() != -1);
            }
            return null;
        }

        public string IniFileName
        {
            get { return _iniFileName; }
        }    private string _iniFileName;
    }
}
