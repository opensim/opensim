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
using System.Collections.Specialized;
using System.Xml;

using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using System.IO;

namespace Prebuild.Core.Nodes
{
    /// <summary>
    ///
    /// </summary>
    [DataNode("Files")]
    public class FilesNode : DataNode
    {
        #region Fields

        private readonly List<string> m_Files = new List<string>();
        private readonly Dictionary<string,BuildAction> m_BuildActions = new Dictionary<string, BuildAction>();
        private readonly Dictionary<string, SubType> m_SubTypes = new Dictionary<string, SubType>();
        private readonly Dictionary<string, string> m_ResourceNames = new Dictionary<string, string>();
        private readonly Dictionary<string, CopyToOutput> m_CopyToOutputs = new Dictionary<string, CopyToOutput>();
        private readonly Dictionary<string, bool> m_Links = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> m_LinkPaths = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> m_PreservePaths = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> m_DestinationPath = new Dictionary<string, string>();
        private readonly NameValueCollection m_CopyFiles = new NameValueCollection();

        #endregion

        #region Properties

        public int Count
        {
            get
            {
                return m_Files.Count;
            }
        }

        public string[] Destinations
        {
            get { return m_CopyFiles.AllKeys; }
        }

        public int CopyFiles
        {
            get { return m_CopyFiles.Count; }
        }

        #endregion

        #region Public Methods

        public BuildAction GetBuildAction(string file)
        {
            if(!m_BuildActions.ContainsKey(file))
            {
                return BuildAction.Compile;
            }

            return m_BuildActions[file];
        }

        public string GetDestinationPath(string file)
        {
            if( !m_DestinationPath.ContainsKey(file))
            {
                return null;
            }
            return m_DestinationPath[file];
        }

        public string[] SourceFiles(string dest)
        {
            return m_CopyFiles.GetValues(dest);
        }

        public CopyToOutput GetCopyToOutput(string file)
        {
            if (!m_CopyToOutputs.ContainsKey(file))
            {
                return CopyToOutput.Never;
            }
            return m_CopyToOutputs[file];
        }

        public bool GetIsLink(string file)
        {
            if (!m_Links.ContainsKey(file))
            {
                return false;
            }
            return m_Links[file];
        }

        public bool Contains(string file)
        {
            return m_Files.Contains(file);
        }

        public string GetLinkPath( string file )
        {
            if ( !m_LinkPaths.ContainsKey( file ) )
            {
                return string.Empty;
            }
            return m_LinkPaths[ file ];
        }

        public SubType GetSubType(string file)
        {
            if(!m_SubTypes.ContainsKey(file))
            {
                return SubType.Code;
            }

            return m_SubTypes[file];
        }

        public string GetResourceName(string file)
        {
            if(!m_ResourceNames.ContainsKey(file))
            {
                return string.Empty;
            }

            return m_ResourceNames[file];
        }

        public bool GetPreservePath( string file )
        {
            if ( !m_PreservePaths.ContainsKey( file ) )
            {
                return false;
            }

            return m_PreservePaths[ file ];
        }

        public override void Parse(XmlNode node)
        {
            if( node == null )
            {
                throw new ArgumentNullException("node");
            }
            foreach(XmlNode child in node.ChildNodes)
            {
                IDataNode dataNode = Kernel.Instance.ParseNode(child, this);
                if(dataNode is FileNode)
                {
                    FileNode fileNode = (FileNode)dataNode;
                    if(fileNode.IsValid)
                    {
                        if (!m_Files.Contains(fileNode.Path))
                        {
                            m_Files.Add(fileNode.Path);
                            m_BuildActions[fileNode.Path] = fileNode.BuildAction;
                            m_SubTypes[fileNode.Path] = fileNode.SubType;
                            m_ResourceNames[fileNode.Path] = fileNode.ResourceName;
                            m_PreservePaths[ fileNode.Path ] = fileNode.PreservePath;
                            m_Links[ fileNode.Path ] = fileNode.IsLink;
                            m_LinkPaths[ fileNode.Path ] = fileNode.LinkPath;
                            m_CopyToOutputs[ fileNode.Path ] = fileNode.CopyToOutput;

                        }
                    }
                }
                else if(dataNode is MatchNode)
                {
                    foreach(string file in ((MatchNode)dataNode).Files)
                    {
                        MatchNode matchNode = (MatchNode)dataNode;
                        if (!m_Files.Contains(file))
                        {
                            m_Files.Add(file);
                            if (matchNode.BuildAction == null)
                                m_BuildActions[file] = GetBuildActionByFileName(file);
                            else
                                m_BuildActions[file] = matchNode.BuildAction.Value;

                            if (matchNode.BuildAction == BuildAction.Copy)
                            {
                                m_CopyFiles.Add(matchNode.DestinationPath, file);
                                m_DestinationPath[file] = matchNode.DestinationPath;
                            }

                            m_SubTypes[file] = matchNode.SubType == null ? GetSubTypeByFileName(file) : matchNode.SubType.Value;
                            m_ResourceNames[ file ] = matchNode.ResourceName;
                            m_PreservePaths[ file ] = matchNode.PreservePath;
                            m_Links[ file ] = matchNode.IsLink;
                            m_LinkPaths[ file ] = matchNode.LinkPath;
                            m_CopyToOutputs[ file ] = matchNode.CopyToOutput;

                        }
                    }
                }
            }
        }

        // TODO: Check in to why StringCollection's enumerator doesn't implement
        // IEnumerator?
        public IEnumerator<string> GetEnumerator()
        {
            return m_Files.GetEnumerator();
        }

        #endregion

    }
}
