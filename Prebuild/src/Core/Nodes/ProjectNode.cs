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
using System.Xml;

using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using Prebuild.Core.Utilities;

namespace Prebuild.Core.Nodes
{
    /// <summary>
    /// A set of values that the Project's type can be
    /// </summary>
    public enum ProjectType
    {
        /// <summary>
        /// The project is a console executable
        /// </summary>
        Exe,
        /// <summary>
        /// The project is a windows executable
        /// </summary>
        WinExe,
        /// <summary>
        /// The project is a library
        /// </summary>
        Library,
        /// <summary>
        /// The project is a website
        /// </summary>
        Web,
    }

    /// <summary>
    ///
    /// </summary>
    public enum ClrRuntime
    {
        /// <summary>
        ///
        /// </summary>
        Microsoft,
        /// <summary>
        ///
        /// </summary>
        Mono
    }
    /// <summary>
    /// The version of the .NET framework to use (Required for VS2008)
    /// <remarks>We don't need .NET 1.1 in here, it'll default when using vs2003.</remarks>
    /// </summary>
    public enum FrameworkVersion
    {
        /// <summary>
        /// .NET 2.0
        /// </summary>
        v2_0,
        /// <summary>
        /// .NET 3.0
        /// </summary>
        v3_0,
        /// <summary>
        /// .NET 3.5
        /// </summary>
        v3_5,
        /// <summary>
        /// .NET 4.0
        /// </summary>
        v4_0,
        /// <summary>
        /// .NET 4.5
        /// </summary>
        v4_5,
        /// <summary>
        /// .NET 4.5.1
        /// </summary>
        v4_5_1
    }
    /// <summary>
    /// The Node object representing /Prebuild/Solution/Project elements
    /// </summary>
    [DataNode("Project")]
    public class ProjectNode : DataNode, IComparable
    {
        #region Fields

        private string m_Name = "unknown";
        private string m_Path = "";
        private string m_FullPath = "";
        private string m_AssemblyName;
        private string m_AppIcon = "";
        private string m_ConfigFile = "";
        private string m_DesignerFolder = "";
        private string m_Language = "C#";
        private ProjectType m_Type = ProjectType.Exe;
        private ClrRuntime m_Runtime = ClrRuntime.Microsoft;
        private FrameworkVersion m_Framework = FrameworkVersion.v2_0;
        private string m_StartupObject = "";
        private string m_RootNamespace;
        private string m_FilterGroups = "";
        private string m_Version = "";
        private Guid m_Guid;
        private string m_DebugStartParameters;

        private readonly Dictionary<string, ConfigurationNode> m_Configurations = new Dictionary<string, ConfigurationNode>();
        private readonly List<ReferencePathNode> m_ReferencePaths = new List<ReferencePathNode>();
        private readonly List<ReferenceNode> m_References = new List<ReferenceNode>();
        private readonly List<AuthorNode> m_Authors = new List<AuthorNode>();
        private FilesNode m_Files;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get
            {
                return m_Name;
            }
        }
        /// <summary>
        /// The version of the .NET Framework to compile under
        /// </summary>
        public FrameworkVersion FrameworkVersion
        {
            get
            {
                return m_Framework;
            }
        }
        /// <summary>
        /// Gets the path.
        /// </summary>
        /// <value>The path.</value>
        public string Path
        {
            get
            {
                return m_Path;
            }
        }

        /// <summary>
        /// Gets the filter groups.
        /// </summary>
        /// <value>The filter groups.</value>
        public string FilterGroups
        {
            get
            {
                return m_FilterGroups;
            }
        }

        /// <summary>
        /// Gets the project's version
        /// </summary>
        /// <value>The project's version.</value>
        public string Version
        {
            get
            {
                return m_Version;
            }
        }

        /// <summary>
        /// Gets the full path.
        /// </summary>
        /// <value>The full path.</value>
        public string FullPath
        {
            get
            {
                return m_FullPath;
            }
        }

        /// <summary>
        /// Gets the name of the assembly.
        /// </summary>
        /// <value>The name of the assembly.</value>
        public string AssemblyName
        {
            get
            {
                return m_AssemblyName;
            }
        }

        /// <summary>
        /// Gets the app icon.
        /// </summary>
        /// <value>The app icon.</value>
        public string AppIcon
        {
            get
            {
                return m_AppIcon;
            }
        }

        /// <summary>
        /// Gets the app icon.
        /// </summary>
        /// <value>The app icon.</value>
        public string ConfigFile
        {
            get
            {
                return m_ConfigFile;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public string DesignerFolder
        {
            get
            {
                return m_DesignerFolder;
            }
        }

        /// <summary>
        /// Gets the language.
        /// </summary>
        /// <value>The language.</value>
        public string Language
        {
            get
            {
                return m_Language;
            }
        }

        /// <summary>
        /// Gets the type.
        /// </summary>
        /// <value>The type.</value>
        public ProjectType Type
        {
            get
            {
                return m_Type;
            }
        }

        /// <summary>
        /// Gets the runtime.
        /// </summary>
        /// <value>The runtime.</value>
        public ClrRuntime Runtime
        {
            get
            {
                return m_Runtime;
            }
        }

        private bool m_GenerateAssemblyInfoFile;

        /// <summary>
        ///
        /// </summary>
        public bool GenerateAssemblyInfoFile
        {
            get
            {
                return m_GenerateAssemblyInfoFile;
            }
            set
            {
                m_GenerateAssemblyInfoFile = value;
            }
        }

        /// <summary>
        /// Gets the startup object.
        /// </summary>
        /// <value>The startup object.</value>
        public string StartupObject
        {
            get
            {
                return m_StartupObject;
            }
        }

        /// <summary>
        /// Gets the root namespace.
        /// </summary>
        /// <value>The root namespace.</value>
        public string RootNamespace
        {
            get
            {
                return m_RootNamespace;
            }
        }

        /// <summary>
        /// Gets the configurations.
        /// </summary>
        /// <value>The configurations.</value>
        public List<ConfigurationNode> Configurations
        {
            get
            {
                List<ConfigurationNode> tmp = new List<ConfigurationNode>(ConfigurationsTable.Values);
                tmp.Sort();
                return tmp;
            }
        }

        /// <summary>
        /// Gets the configurations table.
        /// </summary>
        /// <value>The configurations table.</value>
        public Dictionary<string, ConfigurationNode> ConfigurationsTable
        {
            get
            {
                return m_Configurations;
            }
        }

        /// <summary>
        /// Gets the reference paths.
        /// </summary>
        /// <value>The reference paths.</value>
        public List<ReferencePathNode> ReferencePaths
        {
            get
            {
                List<ReferencePathNode> tmp = new List<ReferencePathNode>(m_ReferencePaths);
                tmp.Sort();
                return tmp;
            }
        }

        /// <summary>
        /// Gets the references.
        /// </summary>
        /// <value>The references.</value>
        public List<ReferenceNode> References
        {
            get
            {
                List<ReferenceNode> tmp = new List<ReferenceNode>(m_References);
                tmp.Sort();
                return tmp;
            }
        }

        /// <summary>
        /// Gets the Authors list.
        /// </summary>
        /// <value>The list of the project's authors.</value>
        public List<AuthorNode> Authors
        {
            get
            {
                return m_Authors;
            }
        }

        /// <summary>
        /// Gets the files.
        /// </summary>
        /// <value>The files.</value>
        public FilesNode Files
        {
            get
            {
                return m_Files;
            }
        }

        /// <summary>
        /// Gets or sets the parent.
        /// </summary>
        /// <value>The parent.</value>
        public override IDataNode Parent
        {
            get
            {
                return base.Parent;
            }
            set
            {
                base.Parent = value;
                if(base.Parent is SolutionNode && m_Configurations.Count < 1)
                {
                    SolutionNode parent = (SolutionNode)base.Parent;
                    foreach(ConfigurationNode conf in parent.Configurations)
                    {
                        m_Configurations[conf.NameAndPlatform] = (ConfigurationNode) conf.Clone();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the GUID.
        /// </summary>
        /// <value>The GUID.</value>
        public Guid Guid
        {
            get
            {
                return m_Guid;
            }
        }

        public string DebugStartParameters
        {
            get
            {
                return m_DebugStartParameters;
            }
        }

        #endregion

        #region Private Methods

        private void HandleConfiguration(ConfigurationNode conf)
        {
            if(String.Compare(conf.Name, "all", true) == 0) //apply changes to all, this may not always be applied first,
                //so it *may* override changes to the same properties for configurations defines at the project level
            {
                foreach(ConfigurationNode confNode in m_Configurations.Values)
                {
                    conf.CopyTo(confNode);//update the config templates defines at the project level with the overrides
                }
            }
            if(m_Configurations.ContainsKey(conf.NameAndPlatform))
            {
                ConfigurationNode parentConf = m_Configurations[conf.NameAndPlatform];
                conf.CopyTo(parentConf);//update the config templates defines at the project level with the overrides
            }
            else
            {
                m_Configurations[conf.NameAndPlatform] = conf;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Parses the specified node.
        /// </summary>
        /// <param name="node">The node.</param>
        public override void Parse(XmlNode node)
        {
            m_Name = Helper.AttributeValue(node, "name", m_Name);
            m_Path = Helper.AttributeValue(node, "path", m_Path);
            m_FilterGroups = Helper.AttributeValue(node, "filterGroups", m_FilterGroups);
            m_Version = Helper.AttributeValue(node, "version", m_Version);
            m_AppIcon = Helper.AttributeValue(node, "icon", m_AppIcon);
            m_ConfigFile = Helper.AttributeValue(node, "configFile", m_ConfigFile);
            m_DesignerFolder = Helper.AttributeValue(node, "designerFolder", m_DesignerFolder);
            m_AssemblyName = Helper.AttributeValue(node, "assemblyName", m_AssemblyName);
            m_Language = Helper.AttributeValue(node, "language", m_Language);
            m_Type = (ProjectType)Helper.EnumAttributeValue(node, "type", typeof(ProjectType), m_Type);
            m_Runtime = (ClrRuntime)Helper.EnumAttributeValue(node, "runtime", typeof(ClrRuntime), m_Runtime);
            m_Framework = (FrameworkVersion)Helper.EnumAttributeValue(node, "frameworkVersion", typeof(FrameworkVersion), m_Framework);
            m_StartupObject = Helper.AttributeValue(node, "startupObject", m_StartupObject);
            m_RootNamespace = Helper.AttributeValue(node, "rootNamespace", m_RootNamespace);

            int hash = m_Name.GetHashCode();
             Guid guidByHash = new Guid(hash, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            string guid = Helper.AttributeValue(node, "guid", guidByHash.ToString());
            m_Guid = new Guid(guid);

            m_GenerateAssemblyInfoFile = Helper.ParseBoolean(node, "generateAssemblyInfoFile", false);
            m_DebugStartParameters = Helper.AttributeValue(node, "debugStartParameters", string.Empty);

            if(string.IsNullOrEmpty(m_AssemblyName))
            {
                m_AssemblyName = m_Name;
            }

            if(string.IsNullOrEmpty(m_RootNamespace))
            {
                m_RootNamespace = m_Name;
            }

            m_FullPath = m_Path;
            try
            {
                m_FullPath = Helper.ResolvePath(m_FullPath);
            }
            catch
            {
                throw new WarningException("Could not resolve Solution path: {0}", m_Path);
            }

            Kernel.Instance.CurrentWorkingDirectory.Push();
            try
            {
                Helper.SetCurrentDir(m_FullPath);

                if( node == null )
                {
                    throw new ArgumentNullException("node");
                }

                foreach(XmlNode child in node.ChildNodes)
                {
                    IDataNode dataNode = Kernel.Instance.ParseNode(child, this);
                    if(dataNode is ConfigurationNode)
                    {
                        HandleConfiguration((ConfigurationNode)dataNode);
                    }
                    else if(dataNode is ReferencePathNode)
                    {
                        m_ReferencePaths.Add((ReferencePathNode)dataNode);
                    }
                    else if(dataNode is ReferenceNode)
                    {
                        m_References.Add((ReferenceNode)dataNode);
                    }
                    else if(dataNode is AuthorNode)
                    {
                        m_Authors.Add((AuthorNode)dataNode);
                    }
                    else if(dataNode is FilesNode)
                    {
                        m_Files = (FilesNode)dataNode;
                    }
                }
            }
            finally
            {
                Kernel.Instance.CurrentWorkingDirectory.Pop();
            }
        }

        #endregion

        #region IComparable Members

        public int CompareTo(object obj)
        {
            ProjectNode that = (ProjectNode)obj;
            return m_Name.CompareTo(that.m_Name);
        }

        #endregion
    }
}
