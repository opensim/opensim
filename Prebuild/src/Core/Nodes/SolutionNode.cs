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

#region CVS Information
/*
 * $Source$
 * $Author: jendave $
 * $Date: 2006-02-28 17:15:42 +0100 (ti, 28 feb 2006) $
 * $Revision: 92 $
 */
#endregion

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Xml;

using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using Prebuild.Core.Utilities;

namespace Prebuild.Core.Nodes
{
	/// <summary>
	/// 
	/// </summary>
	[DataNode("Solution")]
	public class SolutionNode : DataNode
	{
		#region Fields
        
		private string m_Name = "unknown";
		private string m_Version = "1.0.0";
		private string m_Path = "";
		private string m_FullPath = "";
		private string m_ActiveConfig = "Debug";
        
		private OptionsNode m_Options;
		private FilesNode m_Files;
		private Hashtable m_Configurations;
		private Hashtable m_Projects;
		private ArrayList m_ProjectsOrder;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="SolutionNode"/> class.
		/// </summary>
		public SolutionNode()
		{
			m_Configurations = new Hashtable();
			m_Projects = new Hashtable();
			m_ProjectsOrder = new ArrayList();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the active config.
		/// </summary>
		/// <value>The active config.</value>
		public string ActiveConfig 
		{ 
			get 
			{ 
				return m_ActiveConfig; 
			} 
			set 
			{ 
				m_ActiveConfig = value; 
			} 
		}

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
		/// Gets the version.
		/// </summary>
		/// <value>The version.</value>
		public string Version 
		{
			get 
			{
				return m_Version;
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
		/// Gets the options.
		/// </summary>
		/// <value>The options.</value>
		public OptionsNode Options
		{
			get
			{
				return m_Options;
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
		/// Gets the configurations.
		/// </summary>
		/// <value>The configurations.</value>
		public ICollection Configurations
		{
			get
			{
                ArrayList tmp = new ArrayList(ConfigurationsTable.Values);
                tmp.Sort();
                return tmp;
            }
		}

		/// <summary>
		/// Gets the configurations table.
		/// </summary>
		/// <value>The configurations table.</value>
		public Hashtable ConfigurationsTable
		{
			get
			{
				return m_Configurations;
			}
		}
        
		/// <summary>
		/// Gets the projects.
		/// </summary>
		/// <value>The projects.</value>
		public ICollection Projects
		{
			get
			{
                ArrayList tmp = new ArrayList(m_Projects.Values);
                tmp.Sort();
                return tmp;
			}
		}

		/// <summary>
		/// Gets the projects table.
		/// </summary>
		/// <value>The projects table.</value>
		public Hashtable ProjectsTable
		{
			get
			{
				return m_Projects;
			}
		}

		/// <summary>
		/// Gets the projects table.
		/// </summary>
		/// <value>The projects table.</value>
		public ArrayList ProjectsTableOrder
		{
			get
			{
				return m_ProjectsOrder;
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
			m_Version = Helper.AttributeValue(node, "version", m_Version);
			m_ActiveConfig = Helper.AttributeValue(node, "activeConfig", m_ActiveConfig);
			m_Path = Helper.AttributeValue(node, "path", m_Path);

			m_FullPath = m_Path;
			try
			{
				m_FullPath = Helper.ResolvePath(m_FullPath);
			}
			catch
			{
				throw new WarningException("Could not resolve solution path: {0}", m_Path);
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
					if(dataNode is OptionsNode)
					{
						m_Options = (OptionsNode)dataNode;
					}
					else if(dataNode is FilesNode)
					{
						m_Files = (FilesNode)dataNode;
					}
					else if(dataNode is ConfigurationNode)
					{
						m_Configurations[((ConfigurationNode)dataNode).Name] = dataNode;
					}
					else if(dataNode is ProjectNode)
					{
						m_Projects[((ProjectNode)dataNode).Name] = dataNode;
						m_ProjectsOrder.Add(dataNode);
					}
				}
			}
			finally
			{
				Kernel.Instance.CurrentWorkingDirectory.Pop();
			}
		}

		#endregion
	}
}
