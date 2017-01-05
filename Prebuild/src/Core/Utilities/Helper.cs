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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Xml;
using Prebuild.Core.Nodes;

namespace Prebuild.Core.Utilities
{
    /// <summary>
    ///
    /// </summary>
    public class Helper
    {
        #region Fields

        static bool checkForOSVariables;

        /// <summary>
        ///
        /// </summary>
        public static bool CheckForOSVariables
        {
            get
            {
                return checkForOSVariables;
            }
            set
            {
                checkForOSVariables = value;
            }
        }

        #endregion

        #region Public Methods

        #region String Parsing

        public delegate string StringLookup(string key);

        /// <summary>
        /// Gets a collection of StringLocationPair objects that represent the matches
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="beforeGroup">The before group.</param>
        /// <param name="afterGroup">The after group.</param>
        /// <param name="includeDelimitersInSubstrings">if set to <c>true</c> [include delimiters in substrings].</param>
        /// <returns></returns>
        public static StringCollection FindGroups(string target, string beforeGroup, string afterGroup, bool includeDelimitersInSubstrings)
        {
            if( beforeGroup == null )
            {
                throw new ArgumentNullException("beforeGroup");
            }
            if( afterGroup == null )
            {
                throw new ArgumentNullException("afterGroup");
            }
            StringCollection results = new StringCollection();
            if(target == null || target.Length == 0)
            {
                return results;
            }

            int beforeMod = 0;
            int afterMod = 0;
            if(includeDelimitersInSubstrings)
            {
                //be sure to not exlude the delims
                beforeMod = beforeGroup.Length;
                afterMod = afterGroup.Length;
            }
            int startIndex = 0;
            while((startIndex = target.IndexOf(beforeGroup,startIndex)) != -1) {
                int endIndex = target.IndexOf(afterGroup,startIndex);//the index of the char after it
                if(endIndex == -1)
                {
                    break;
                }
                int length = endIndex - startIndex - beforeGroup.Length;//move to the first char in the string
                string substring = substring = target.Substring(startIndex + beforeGroup.Length - beforeMod,
                    length - afterMod);

                results.Add(substring);
                //results.Add(new StringLocationPair(substring,startIndex));
                startIndex = endIndex + 1;
                //the Interpolate*() methods will not work if expressions are expandded inside expression due to an optimization
                //so start after endIndex

            }
            return results;
        }

        /// <summary>
        /// Replaces the groups.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="beforeGroup">The before group.</param>
        /// <param name="afterGroup">The after group.</param>
        /// <param name="lookup">The lookup.</param>
        /// <returns></returns>
        public static string ReplaceGroups(string target, string beforeGroup, string afterGroup, StringLookup lookup) {
            if( target == null )
            {
                throw new ArgumentNullException("target");
            }
            //int targetLength = target.Length;
            StringCollection strings = FindGroups(target,beforeGroup,afterGroup,false);
            if( lookup == null )
            {
                throw new ArgumentNullException("lookup");
            }
            foreach(string substring in strings)
            {
                target = target.Replace(beforeGroup + substring + afterGroup, lookup(substring) );
            }
            return target;
        }

        /// <summary>
        /// Replaces ${var} statements in a string with the corresonding values as detirmined by the lookup delegate
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="lookup">The lookup.</param>
        /// <returns></returns>
        public static string InterpolateForVariables(string target, StringLookup lookup)
        {
            return ReplaceGroups(target, "${" , "}" , lookup);
        }

        /// <summary>
        /// Replaces ${var} statements in a string with the corresonding environment variable with name var
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public static string InterpolateForEnvironmentVariables(string target)
        {
            return InterpolateForVariables(target, new StringLookup(Environment.GetEnvironmentVariable));
        }

        #endregion

        /// <summary>
        /// Translates the value.
        /// </summary>
        /// <param name="translateType">Type of the translate.</param>
        /// <param name="translationItem">The translation item.</param>
        /// <returns></returns>
        public static object TranslateValue(Type translateType, string translationItem)
        {
            if(translationItem == null)
            {
                return null;
            }

            try
            {
                string lowerVal = translationItem.ToLower();
                if(translateType == typeof(bool))
                {
                    return (lowerVal == "true" || lowerVal == "1" || lowerVal == "y" || lowerVal == "yes" || lowerVal == "on");
                }
                else if(translateType == typeof(int))
                {
                    return (Int32.Parse(translationItem));
                }
                else
                {
                    return translationItem;
                }
            }
            catch(FormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// Deletes if exists.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        public static bool DeleteIfExists(string file)
        {
            string resFile = null;
            try
            {
                resFile = ResolvePath(file);
            }
            catch(ArgumentException)
            {
                return false;
            }

            if(!File.Exists(resFile))
            {
                return false;
            }

            File.Delete(resFile);
            return true;
        }

        static readonly char seperator = Path.DirectorySeparatorChar;

        // This little gem was taken from the NeL source, thanks guys!
        /// <summary>
        /// Makes a relative path
        /// </summary>
        /// <param name="startPath">Path to start from</param>
        /// <param name="endPath">Path to end at</param>
        /// <returns>Path that will get from startPath to endPath</returns>
        public static string MakePathRelativeTo(string startPath, string endPath)
        {
            string tmp = NormalizePath(startPath, seperator);
            string src = NormalizePath(endPath, seperator);
            string prefix = "";

            while(true)
            {
                if((String.Compare(tmp, 0, src, 0, tmp.Length) == 0))
                {
                    string ret;
                    int size = tmp.Length;
                    if(size == src.Length)
                    {
                        return "./";
                    }
                    if((src.Length > tmp.Length) && src[tmp.Length - 1] != seperator)
                    {
                    }
                    else
                    {
                        ret = prefix + endPath.Substring(size, endPath.Length - size);
                        ret = ret.Trim();
                        if(ret[0] == seperator)
                        {
                            ret = "." + ret;
                        }

                        return NormalizePath(ret);
                    }

                }

                if(tmp.Length < 2)
                {
                    break;
                }

                int lastPos = tmp.LastIndexOf(seperator, tmp.Length - 2);
                int prevPos = tmp.IndexOf(seperator);

                if((lastPos == prevPos) || (lastPos == -1))
                {
                    break;
                }

                tmp = tmp.Substring(0, lastPos + 1);
                prefix += ".." + seperator.ToString();
            }

            return endPath;
        }

        /// <summary>
        /// Resolves the path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public static string ResolvePath(string path)
        {
            string tmpPath = NormalizePath(path);
            if(tmpPath.Length < 1)
            {
                tmpPath = ".";
            }

            tmpPath = Path.GetFullPath(tmpPath);
            if(!File.Exists(tmpPath) && !Directory.Exists(tmpPath))
            {
                throw new ArgumentException("Path could not be resolved: " + tmpPath);
            }

            return tmpPath;
        }

        /// <summary>
        /// Normalizes the path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="separatorCharacter">The separator character.</param>
        /// <returns></returns>
        public static string NormalizePath(string path, char separatorCharacter)
        {
            if(path == null || path == "" || path.Length < 1)
            {
                return "";
            }

            string tmpPath = path.Replace('\\', '/');
            tmpPath = tmpPath.Replace('/', separatorCharacter);
            return tmpPath;
        }

        /// <summary>
        /// Normalizes the path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public static string NormalizePath(string path)
        {
            return NormalizePath(path, Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Ends the path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="separatorCharacter">The separator character.</param>
        /// <returns></returns>
        public static string EndPath(string path, char separatorCharacter)
        {
            if(path == null || path == "" || path.Length < 1)
            {
                return "";
            }

            if(!path.EndsWith(separatorCharacter.ToString()))
            {
                return (path + separatorCharacter);
            }

            return path;
        }

        /// <summary>
        /// Ends the path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public static string EndPath(string path)
        {
            return EndPath(path, Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Makes the file path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="name">The name.</param>
        /// <param name="ext">The ext.</param>
        /// <returns></returns>
        public static string MakeFilePath(string path, string name, string ext)
        {
            string ret = EndPath(NormalizePath(path));

            if( name == null )
            {
                throw new ArgumentNullException("name");
            }

            ret += name;
            if(!name.EndsWith("." + ext))
            {
                ret += "." + ext;
            }

            //foreach(char c in Path.GetInvalidPathChars())
            //{
            //    ret = ret.Replace(c, '_');
            //}

            return ret;
        }

        /// <summary>
        /// Makes the file path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        public static string MakeFilePath(string path, string name)
        {
            string ret = EndPath(NormalizePath(path));

            if( name == null )
            {
                throw new ArgumentNullException("name");
            }

            ret += name;

            //foreach (char c in Path.GetInvalidPathChars())
            //{
            //    ret = ret.Replace(c, '_');
            //}

            return ret;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string MakeReferencePath(string path)
        {
            string ret = EndPath(NormalizePath(path));

            //foreach (char c in Path.GetInvalidPathChars())
            //{
            //    ret = ret.Replace(c, '_');
            //}

            return ret;
        }

        /// <summary>
        /// Sets the current dir.
        /// </summary>
        /// <param name="path">The path.</param>
        public static void SetCurrentDir(string path)
        {
            if( path == null )
            {
                throw new ArgumentNullException("path");
            }
            if(path.Length < 1)
            {
                return;
            }

            Environment.CurrentDirectory = path;
        }

        /// <summary>
        /// Checks the type.
        /// </summary>
        /// <param name="typeToCheck">The type to check.</param>
        /// <param name="attr">The attr.</param>
        /// <param name="inter">The inter.</param>
        /// <returns></returns>
        public static object CheckType(Type typeToCheck, Type attr, Type inter)
        {
            if(typeToCheck == null || attr == null)
            {
                return null;
            }

            object[] attrs = typeToCheck.GetCustomAttributes(attr, false);
            if(attrs == null || attrs.Length < 1)
            {
                return null;
            }
            if( inter == null )
            {
                throw new ArgumentNullException("inter");
            }

            if(typeToCheck.GetInterface(inter.FullName) == null)
            {
                return null;
            }

            return attrs[0];
        }

        /// <summary>
        /// Attributes the value.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="attr">The attr.</param>
        /// <param name="def">The def.</param>
        /// <returns></returns>
        public static string AttributeValue(XmlNode node, string attr, string def)
        {
            if( node == null )
            {
                throw new ArgumentNullException("node");
            }
            if(node.Attributes[attr] == null)
            {
                return def;
            }
            string val = node.Attributes[attr].Value;
            if(!CheckForOSVariables)
            {
                return val;
            }

            return InterpolateForEnvironmentVariables(val);
        }

        /// <summary>
        /// Parses the boolean.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="attr">The attr.</param>
        /// <param name="defaultValue">if set to <c>true</c> [default value].</param>
        /// <returns></returns>
        public static bool ParseBoolean(XmlNode node, string attr, bool defaultValue)
        {
            if( node == null )
            {
                throw new ArgumentNullException("node");
            }
            if(node.Attributes[attr] == null)
            {
                return defaultValue;
            }
            return bool.Parse(node.Attributes[attr].Value);
        }

        /// <summary>
        /// Enums the attribute value.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="attr">The attr.</param>
        /// <param name="enumType">Type of the enum.</param>
        /// <param name="def">The def.</param>
        /// <returns></returns>
        public static object EnumAttributeValue(XmlNode node, string attr, Type enumType, object def)
        {
            if( def == null )
            {
                throw new ArgumentNullException("def");
            }
            string val = AttributeValue(node, attr, def.ToString());
            return Enum.Parse(enumType, val, true);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="projectType"></param>
        /// <returns></returns>
        public static string AssemblyFullName(string assemblyName, ProjectType projectType)
        {
            return assemblyName + (projectType == ProjectType.Library ? ".dll" : ".exe");
        }

        #endregion
    }
}
