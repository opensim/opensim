#region BSD License
/*
Copyright (c) 2004-2005 Matthew Holmes (matthew@wildfiregames.com), Dan Moorehead (dan05a@gmail.com)

Redistribution and use in source and binary forms, with or without modification, are permitted
provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this list of conditions
  and the following disclaimer.
* Redistributions in binary form must reproduce the above copyright notice, this list of conditions
  and the following disclaimer in the documentation and/or other materials provided with the
  distribution.
* The name of the author may not be used to endorse or promote products derived from this software
  without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING,
BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY
OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING
IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace Prebuild.Core.Parse
{
    /// <summary>
    ///
    /// </summary>
    public enum OperatorSymbol
    {
        /// <summary>
        ///
        /// </summary>
        None,
        /// <summary>
        ///
        /// </summary>
        Equal,
        /// <summary>
        ///
        /// </summary>
        NotEqual,
        /// <summary>
        ///
        /// </summary>
        LessThan,
        /// <summary>
        ///
        /// </summary>
        GreaterThan,
        /// <summary>
        ///
        /// </summary>
        LessThanEqual,
        /// <summary>
        ///
        /// </summary>
        GreaterThanEqual
    }

    /// <summary>
    ///
    /// </summary>
    public class Preprocessor
    {
        #region Constants

        /// <summary>
        /// Includes the regex to look for file tags in the <?include
        /// ?> processing instruction.
        /// </summary>
        private static readonly Regex includeFileRegex = new Regex("file=\"(.+?)\"");

        #endregion

        #region Fields

        readonly XmlDocument m_OutDoc = new XmlDocument();
        readonly Stack<IfContext> m_IfStack = new Stack<IfContext>();
        readonly Dictionary<string, object> m_Variables = new Dictionary<string, object>();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Preprocessor"/> class.
        /// </summary>
        public Preprocessor()
        {
            RegisterVariable("OS", GetOS());
            RegisterVariable("RuntimeVersion", Environment.Version.Major);
            RegisterVariable("RuntimeMajor", Environment.Version.Major);
            RegisterVariable("RuntimeMinor", Environment.Version.Minor);
            RegisterVariable("RuntimeRevision", Environment.Version.Revision);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the processed doc.
        /// </summary>
        /// <value>The processed doc.</value>
        public XmlDocument ProcessedDoc
        {
            get
            {
                return m_OutDoc;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Parts of this code were taken from NAnt and is subject to the GPL
        /// as per NAnt's license. Thanks to the NAnt guys for this little gem.
        /// </summary>
        /// <returns></returns>
        public static string GetOS()
        {
            PlatformID platId = Environment.OSVersion.Platform;
            if(platId == PlatformID.Win32NT || platId == PlatformID.Win32Windows)
            {
                return "Win32";
            }

            if (File.Exists("/System/Library/Frameworks/Cocoa.framework/Cocoa"))
            {
                return "MACOSX";
            }

            /*
             * .NET 1.x, under Mono, the UNIX code is 128. Under
             * .NET 2.x, Mono or MS, the UNIX code is 4
             */
            if(Environment.Version.Major == 1)
            {
                if((int)platId == 128)
                {
                    return "UNIX";
                }
            }
            else if((int)platId == 4)
            {
                return "UNIX";
            }

            return "Unknown";
        }

        private static bool CompareNum(OperatorSymbol oper, int val1, int val2)
        {
            switch(oper)
            {
                case OperatorSymbol.Equal:
                    return (val1 == val2);
                case OperatorSymbol.NotEqual:
                    return (val1 != val2);
                case OperatorSymbol.LessThan:
                    return (val1 < val2);
                case OperatorSymbol.LessThanEqual:
                    return (val1 <= val2);
                case OperatorSymbol.GreaterThan:
                    return (val1 > val2);
                case OperatorSymbol.GreaterThanEqual:
                    return (val1 >= val2);
            }

            throw new WarningException("Unknown operator type");
        }

        private static bool CompareStr(OperatorSymbol oper, string val1, string val2)
        {
            switch(oper)
            {
                case OperatorSymbol.Equal:
                    return (val1 == val2);
                case OperatorSymbol.NotEqual:
                    return (val1 != val2);
                case OperatorSymbol.LessThan:
                    return (val1.CompareTo(val2) < 0);
                case OperatorSymbol.LessThanEqual:
                    return (val1.CompareTo(val2) <= 0);
                case OperatorSymbol.GreaterThan:
                    return (val1.CompareTo(val2) > 0);
                case OperatorSymbol.GreaterThanEqual:
                    return (val1.CompareTo(val2) >= 0);
            }

            throw new WarningException("Unknown operator type");
        }

        private static char NextChar(int idx, string str)
        {
            if((idx + 1) >= str.Length)
            {
                return Char.MaxValue;
            }

            return str[idx + 1];
        }
        // Very very simple expression parser. Can only match expressions of the form
        // <var> <op> <value>:
        // OS = Windows
        // OS != Linux
        // RuntimeMinor > 0
        private bool ParseExpression(string exp)
        {
            if(exp == null)
            {
                throw new ArgumentException("Invalid expression, cannot be null");
            }

            exp = exp.Trim();
            if(exp.Length < 1)
            {
                throw new ArgumentException("Invalid expression, cannot be 0 length");
            }

            string id = "";
            string str = "";
            OperatorSymbol oper = OperatorSymbol.None;
            bool inStr = false;

            for(int i = 0; i < exp.Length; i++)
            {
                char c = exp[i];
                if(Char.IsWhiteSpace(c))
                {
                    continue;
                }

                if(Char.IsLetterOrDigit(c) || c == '_')
                {
                    if(inStr)
                    {
                        str += c;
                    }
                    else
                    {
                        id += c;
                    }
                }
                else if(c == '\"')
                {
                    inStr = !inStr;
                    if(inStr)
                    {
                        str = "";
                    }
                }
                else
                {
                    if(inStr)
                    {
                        str += c;
                    }
                    else
                    {
                        switch(c)
                        {
                            case '=':
                                oper = OperatorSymbol.Equal;
                                break;

                            case '!':
                                if(NextChar(i, exp) == '=')
                                {
                                    oper = OperatorSymbol.NotEqual;
                                }

                                break;

                            case '<':
                                if(NextChar(i, exp) == '=')
                                {
                                    oper = OperatorSymbol.LessThanEqual;
                                }
                                else
                                {
                                    oper = OperatorSymbol.LessThan;
                                }

                                break;

                            case '>':
                                if(NextChar(i, exp) == '=')
                                {
                                    oper = OperatorSymbol.GreaterThanEqual;
                                }
                                else
                                {
                                    oper = OperatorSymbol.GreaterThan;
                                }

                                break;
                        }
                    }
                }
            }


            if(inStr)
            {
                throw new WarningException("Expected end of string in expression");
            }

            if(oper == OperatorSymbol.None)
            {
                throw new WarningException("Expected operator in expression");
            }
            if(id.Length < 1)
            {
                throw new WarningException("Expected identifier in expression");
            }
            if(str.Length < 1)
            {
                throw new WarningException("Expected value in expression");
            }

            bool ret;
            try
            {
                object val = m_Variables[id.ToLower()];
                if(val == null)
                {
                    throw new WarningException("Unknown identifier '{0}'", id);
                }

                Type t = val.GetType();
                if(t.IsAssignableFrom(typeof(int)))
                {
                    int numVal = (int)val;
                    int numVal2 = Int32.Parse(str);
                    ret = CompareNum(oper, numVal, numVal2);
                }
                else
                {
                    string strVal = val.ToString();
                    string strVal2 = str;
                    ret = CompareStr(oper, strVal, strVal2);
                }
            }
            catch(ArgumentException ex)
            {
                ex.ToString();
                throw new WarningException("Invalid value type for system variable '{0}', expected int", id);
            }

            return ret;
        }

        /// <summary>
        /// Taken from current Prebuild included in OpenSim 0.7.x
        /// </summary>
        /// <param name="readerStack">
        /// A <see cref="Stack<XmlReader>"/>
        /// </param>
        /// <param name="include">
        /// A <see cref="System.String"/>
        /// </param>
        private static void WildCardInclude (Stack<XmlReader> readerStack, string include)
        {
            if (!include.Contains ("*")) {
                return;
            }

            // Console.WriteLine("Processing {0}", include);

            // Break up the include into pre and post wildcard sections
            string preWildcard = include.Substring (0, include.IndexOf ("*"));
            string postWildcard = include.Substring (include.IndexOf ("*") + 2);

            // If preWildcard is a directory, recurse
            if (Directory.Exists (preWildcard)) {
                string[] directories = Directory.GetDirectories (preWildcard);
                Array.Sort (directories);
                Array.Reverse (directories);
                foreach (string dirPath in directories) {
                    //Console.WriteLine ("Scanning : {0}", dirPath);

                    string includeFile = Path.Combine (dirPath, postWildcard);
                    if (includeFile.Contains ("*")) {
                        // postWildcard included another wildcard, recurse.
                        WildCardInclude (readerStack, includeFile);
                    } else {
                        FileInfo file = new FileInfo (includeFile);
                        if (file.Exists) {
                            //Console.WriteLine ("Including File: {0}", includeFile);
                            XmlReader newReader = new XmlTextReader (file.Open (FileMode.Open, FileAccess.Read, FileShare.Read));
                            readerStack.Push (newReader);
                        }
                    }
                }
            } else {
                // preWildcard is not a path to a directory, so the wildcard is in the filename
                string searchFilename = Path.GetFileName (preWildcard.Substring (preWildcard.IndexOf ("/") + 1) + "*" + postWildcard);
                Console.WriteLine ("searchFilename: {0}", searchFilename);

                string searchDirectory = Path.GetDirectoryName (preWildcard);
                Console.WriteLine ("searchDirectory: {0}", searchDirectory);

                string[] files = Directory.GetFiles (searchDirectory, searchFilename);
                Array.Sort (files);
                Array.Reverse (files);
                foreach (string includeFile in files) {
                    FileInfo file = new FileInfo (includeFile);
                    if (file.Exists) {
                        // Console.WriteLine ("Including File: {0}", includeFile);
                        XmlReader newReader = new XmlTextReader (file.Open (FileMode.Open, FileAccess.Read, FileShare.Read));
                        readerStack.Push (newReader);
                    }
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        ///
        /// </summary>
        /// <param name="name"></param>
        /// <param name="variableValue"></param>
        public void RegisterVariable(string name, object variableValue)
        {
            if(name == null || variableValue == null)
            {
                return;
            }

            m_Variables[name.ToLower()] = variableValue;
        }

        /// <summary>
        /// Performs validation on the xml source as well as evaluates conditional and flow expresions
        /// </summary>
        /// <exception cref="ArgumentException">For invalid use of conditional expressions or for invalid XML syntax.  If a XmlValidatingReader is passed, then will also throw exceptions for non-schema-conforming xml</exception>
        /// <param name="initialReader"></param>
        /// <returns>the output xml </returns>
        public string Process(XmlReader initialReader)
        {
            if(initialReader == null)
            {
                throw new ArgumentException("Invalid XML reader to pre-process");
            }

            IfContext context = new IfContext(true, true, IfState.None);
            StringWriter xmlText = new StringWriter();
            XmlTextWriter writer = new XmlTextWriter(xmlText);
            writer.Formatting = Formatting.Indented;

            // Create a queue of XML readers and add the initial
            // reader to it. Then we process until we run out of
            // readers which lets the <?include?> operation add more
            // readers to generate a multi-file parser and not require
            // XML fragments that a recursive version would use.
            Stack<XmlReader> readerStack = new Stack<XmlReader>();
            readerStack.Push(initialReader);

            while(readerStack.Count > 0)
            {
                // Pop off the next reader.
                XmlReader reader = readerStack.Pop();

                // Process through this XML reader until it is
                // completed (or it is replaced by the include
                // operation).
                while(reader.Read())
                {
                    // The prebuild file has a series of processing
                    // instructions which allow for specific
                    // inclusions based on operating system or to
                    // include additional files.
                    if(reader.NodeType == XmlNodeType.ProcessingInstruction)
                    {
                        bool ignore = false;

                        switch(reader.LocalName)
                        {
                            case "include":
                                // use regular expressions to parse out the attributes.
                                MatchCollection matches = includeFileRegex.Matches(reader.Value);

                                // make sure there is only one file attribute.
                                if(matches.Count > 1)
                                {
                                    throw new WarningException("An <?include ?> node was found, but it specified more than one file.");
                                }

                                if(matches.Count == 0)
                                {
                                    throw new WarningException("An <?include ?> node was found, but it did not specify the file attribute.");
                                }

                                // ***** Adding for wildcard handling
                                // Push current reader back onto the stack.
                                readerStack.Push (reader);

                                // Pull the file out from the regex and make sure it is a valid file before using it.
                                string filename = matches[0].Groups[1].Value;

                                filename = String.Join (Path.DirectorySeparatorChar.ToString (), filename.Split (new char[] { '/', '\\' }));

                                if (!filename.Contains ("*")) {

                                    FileInfo includeFile = new FileInfo (filename);
                                    if (!includeFile.Exists) {
                                        throw new WarningException ("Cannot include file: " + includeFile.FullName);
                                    }

                                    // Create a new reader object for this file. Then put the old reader back on the stack and start
                                    // processing using this new XML reader.

                                    XmlReader newReader = new XmlTextReader (includeFile.Open (FileMode.Open, FileAccess.Read, FileShare.Read));
                                    reader = newReader;
                                    readerStack.Push (reader);

                                } else {
                                    WildCardInclude (readerStack, filename);
                                }

                                reader = (XmlReader)readerStack.Pop ();
                                ignore = true;
                                break;

                            case "if":
                                m_IfStack.Push(context);
                                context = new IfContext(context.Keep & context.Active, ParseExpression(reader.Value), IfState.If);
                                ignore = true;
                                break;

                            case "elseif":
                                if(m_IfStack.Count == 0)
                                {
                                    throw new WarningException("Unexpected 'elseif' outside of 'if'");
                                }
                                if(context.State != IfState.If && context.State != IfState.ElseIf)
                                {
                                    throw new WarningException("Unexpected 'elseif' outside of 'if'");
                                }

                                context.State = IfState.ElseIf;
                                if(!context.EverKept)
                                {
                                    context.Keep = ParseExpression(reader.Value);
                                }
                                else
                                {
                                    context.Keep = false;
                                }

                                ignore = true;
                                break;

                            case "else":
                                if(m_IfStack.Count == 0)
                                {
                                    throw new WarningException("Unexpected 'else' outside of 'if'");
                                }
                                if(context.State != IfState.If && context.State != IfState.ElseIf)
                                {
                                    throw new WarningException("Unexpected 'else' outside of 'if'");
                                }

                                context.State = IfState.Else;
                                context.Keep = !context.EverKept;
                                ignore = true;
                                break;

                            case "endif":
                                if(m_IfStack.Count == 0)
                                {
                                    throw new WarningException("Unexpected 'endif' outside of 'if'");
                                }

                                context = m_IfStack.Pop();
                                ignore = true;
                                break;
                        }

                        if(ignore)
                        {
                            continue;
                        }
                    }//end pre-proc instruction

                    if(!context.Active || !context.Keep)
                    {
                        continue;
                    }

                    switch(reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            bool empty = reader.IsEmptyElement;
                            writer.WriteStartElement(reader.Name);

                            while (reader.MoveToNextAttribute())
                            {
                                writer.WriteAttributeString(reader.Name, reader.Value);
                            }

                            if(empty)
                            {
                                writer.WriteEndElement();
                            }

                            break;

                        case XmlNodeType.EndElement:
                            writer.WriteEndElement();
                            break;

                        case XmlNodeType.Text:
                            writer.WriteString(reader.Value);
                            break;

                        case XmlNodeType.CDATA:
                            writer.WriteCData(reader.Value);
                            break;

                        default:
                            break;
                    }
                }

                if(m_IfStack.Count != 0)
                {
                    throw new WarningException("Mismatched 'if', 'endif' pair");
                }
            }

            return xmlText.ToString();
        }

        #endregion
    }
}
