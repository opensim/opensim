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

using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using Prebuild.Core.Utilities;

namespace Prebuild.Core.Nodes
{
    /// <summary>
    ///
    /// </summary>
    [DataNode("Match")]
    public class MatchNode : DataNode
    {
        #region Fields

        private readonly List<string> m_Files = new List<string>();
        private Regex m_Regex;
        private BuildAction? m_BuildAction;
        private SubType? m_SubType;
        string m_ResourceName = "";
        private CopyToOutput m_CopyToOutput;
        private bool m_Link;
        private string m_LinkPath;
        private bool m_PreservePath;
        private string m_Destination = "";
        private readonly List<ExcludeNode> m_Exclusions = new List<ExcludeNode>();

        #endregion

        #region Properties

        /// <summary>
        ///
        /// </summary>
        public IEnumerable<string> Files
        {
            get
            {
                return m_Files;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public BuildAction? BuildAction
        {
            get
            {
                return m_BuildAction;
            }
        }

        public string DestinationPath
        {
            get
            {
                return m_Destination;
            }
        }
        /// <summary>
        ///
        /// </summary>
        public SubType? SubType
        {
            get
            {
                return m_SubType;
            }
        }

        public CopyToOutput CopyToOutput
        {
            get
            {
                return m_CopyToOutput;
            }
        }

        public bool IsLink
        {
            get
            {
                return m_Link;
            }
        }

        public string LinkPath
        {
            get
            {
                return m_LinkPath;
            }
        }
        /// <summary>
        ///
        /// </summary>
        public string ResourceName
        {
            get
            {
                return m_ResourceName;
            }
        }

        public bool PreservePath
        {
            get
            {
                return m_PreservePath;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Recurses the directories.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="pattern">The pattern.</param>
        /// <param name="recurse">if set to <c>true</c> [recurse].</param>
        /// <param name="useRegex">if set to <c>true</c> [use regex].</param>
        private void RecurseDirectories(string path, string pattern, bool recurse, bool useRegex, List<ExcludeNode> exclusions)
        {
            Match match;
            try
            {
                string[] files;

                Boolean excludeFile;
                if(!useRegex)
                {
                    try
                    {
                        files = Directory.GetFiles(path, pattern);
                    }
                    catch (IOException)
                    {
                        // swallow weird IOException error when running in a virtual box
                        // guest OS on a network share when the host OS is not Windows.
                        // This seems to happen on network shares
                        // when no files match, and may be related to this report:
                        // http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=254546

                        files = null;
                    }

                    if(files != null)
                    {
                        foreach (string file in files)
                        {
                            excludeFile = false;
                            string fileTemp;
                            if (file.Substring(0,2) == "./" || file.Substring(0,2) == ".\\")
                            {
                                fileTemp = file.Substring(2);
                            }
                            else
                            {
                                fileTemp = file;
                            }

                            // Check all excludions and set flag if there are any hits.
                            foreach ( ExcludeNode exclude in exclusions )
                            {
                                Regex exRegEx = new Regex( exclude.Pattern );
                                match = exRegEx.Match( file );
                                excludeFile |= match.Success;
                            }

                            if ( !excludeFile )
                            {
                                m_Files.Add( fileTemp );
                            }

                        }
                    }

                    // don't call return here, because we may need to recursively search directories below
                    // this one, even if no matches were found in this directory.
                }
                else
                {
                    try
                     {
                        files = Directory.GetFiles(path);
                    }
                    catch (IOException)
                    {
                        // swallow weird IOException error when running in a virtual box
                        // guest OS on a network share.
                        files = null;
                    }

                    if (files != null)
                    {
                        foreach (string file in files)
                        {
                            excludeFile = false;

                            match = m_Regex.Match(file);
                            if (match.Success)
                            {
                                // Check all excludions and set flag if there are any hits.
                                foreach (ExcludeNode exclude in exclusions)
                                {
                                    Regex exRegEx = new Regex(exclude.Pattern);
                                    match = exRegEx.Match(file);
                                    excludeFile |= !match.Success;
                                }

                                if (!excludeFile)
                                {
                                    m_Files.Add(file);
                                }
                            }
                        }
                    }
                }

                if(recurse)
                {
                    string[] dirs = Directory.GetDirectories(path);
                    if(dirs != null && dirs.Length > 0)
                    {
                        foreach (string str in dirs)
                        {
                            // hack to skip subversion folders.  Not having this can cause
                            // a significant performance hit when running on a network drive.
                            if (str.EndsWith(".svn"))
                                continue;

                            RecurseDirectories(Helper.NormalizePath(str), pattern, recurse, useRegex, exclusions);
                        }
                    }
                }
            }
            catch(DirectoryNotFoundException)
            {
                return;
            }
            catch(ArgumentException)
            {
                return;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        ///
        /// </summary>
        /// <param name="node"></param>
        public override void Parse(XmlNode node)
        {
            if( node == null )
            {
                throw new ArgumentNullException("node");
            }
            string path = Helper.AttributeValue(node, "path", ".");
            string pattern = Helper.AttributeValue(node, "pattern", "*");
            string destination = Helper.AttributeValue(node, "destination", string.Empty);
            bool recurse = (bool)Helper.TranslateValue(typeof(bool), Helper.AttributeValue(node, "recurse", "false"));
            bool useRegex = (bool)Helper.TranslateValue(typeof(bool), Helper.AttributeValue(node, "useRegex", "false"));
            string buildAction = Helper.AttributeValue(node, "buildAction", String.Empty);
            if (buildAction != string.Empty)
                m_BuildAction = (BuildAction)Enum.Parse(typeof(BuildAction), buildAction);


            //TODO: Figure out where the subtype node is being assigned
            //string subType = Helper.AttributeValue(node, "subType", string.Empty);
            //if (subType != String.Empty)
            //    m_SubType = (SubType)Enum.Parse(typeof(SubType), subType);
            m_ResourceName = Helper.AttributeValue(node, "resourceName", m_ResourceName);
            m_CopyToOutput = (CopyToOutput) Enum.Parse(typeof(CopyToOutput), Helper.AttributeValue(node, "copyToOutput", m_CopyToOutput.ToString()));
            m_Link = bool.Parse(Helper.AttributeValue(node, "link", bool.FalseString));
            if ( m_Link )
            {
                m_LinkPath = Helper.AttributeValue( node, "linkPath", string.Empty );
            }
            m_PreservePath = bool.Parse( Helper.AttributeValue( node, "preservePath", bool.FalseString ) );

            if ( buildAction == "Copy")
                m_Destination = destination;

            if(path != null && path.Length == 0)
                path = ".";//use current directory

            //throw new WarningException("Match must have a 'path' attribute");

            if(pattern == null)
            {
                throw new WarningException("Match must have a 'pattern' attribute");
            }

            path = Helper.NormalizePath(path);
            if(!Directory.Exists(path))
            {
                throw new WarningException("Match path does not exist: {0}", path);
            }

            try
            {
                if(useRegex)
                {
                    m_Regex = new Regex(pattern);
                }
            }
            catch(ArgumentException ex)
            {
                throw new WarningException("Could not compile regex pattern: {0}", ex.Message);
            }


            foreach(XmlNode child in node.ChildNodes)
            {
                IDataNode dataNode = Kernel.Instance.ParseNode(child, this);
                if(dataNode is ExcludeNode)
                {
                    ExcludeNode excludeNode = (ExcludeNode)dataNode;
                    m_Exclusions.Add( excludeNode );
                }
            }

            RecurseDirectories( path, pattern, recurse, useRegex, m_Exclusions );

            if (m_Files.Count < 1)
            {
                // Include the project name when the match node returns no matches to provide extra
                // debug info.
                ProjectNode project = Parent.Parent as ProjectNode;
                string projectName = "";

                if (project != null)
                    projectName = " in project " + project.AssemblyName;

                throw new WarningException("Match" + projectName + " returned no files: {0}{1}", Helper.EndPath(path), pattern);
            }
            m_Regex = null;
        }

        #endregion
    }
}
