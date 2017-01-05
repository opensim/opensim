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
using System.IO;
using System.Xml;

using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using Prebuild.Core.Utilities;
using Prebuild.Core.Targets;

namespace Prebuild.Core.Nodes
{
    /// <summary>
    ///
    /// </summary>
    public enum BuildAction
    {
        /// <summary>
        ///
        /// </summary>
        None,
        /// <summary>
        ///
        /// </summary>
        Compile,
        /// <summary>
        ///
        /// </summary>
        Content,
        /// <summary>
        ///
        /// </summary>
        EmbeddedResource,
        /// <summary>
        ///
        /// </summary>
        ApplicationDefinition,
        /// <summary>
        ///
        /// </summary>
        Page,
        /// <summary>
        ///
        /// </summary>
        Copy
    }

    /// <summary>
    ///
    /// </summary>
    public enum SubType
    {
        /// <summary>
        ///
        /// </summary>
        Code,
        /// <summary>
        ///
        /// </summary>
        Component,
        /// <summary>
        ///
        /// </summary>
        Designer,
        /// <summary>
        ///
        /// </summary>
        Form,
        /// <summary>
        ///
        /// </summary>
        Settings,
        /// <summary>
        ///
        /// </summary>
        UserControl,
        /// <summary>
        ///
        /// </summary>
        CodeBehind,
    }

    public enum CopyToOutput
    {
        Never,
        Always,
        PreserveNewest
    }

    /// <summary>
    ///
    /// </summary>
    [DataNode("File")]
    public class FileNode : DataNode
    {
        #region Fields

        private string m_Path;
        private string m_ResourceName = "";
        private BuildAction? m_BuildAction;
        private bool m_Valid;
        private SubType? m_SubType;
        private CopyToOutput m_CopyToOutput = CopyToOutput.Never;
        private bool m_Link = false;
        private string m_LinkPath = string.Empty;
        private bool m_PreservePath = false;


        #endregion

        #region Properties

        /// <summary>
        ///
        /// </summary>
        public string Path
        {
            get
            {
                return m_Path;
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

        /// <summary>
        ///
        /// </summary>
        public BuildAction BuildAction
        {
            get
            {
                if (m_BuildAction != null)
                    return m_BuildAction.Value;
                else
                    return GetBuildActionByFileName(this.Path);

            }
        }

        public CopyToOutput CopyToOutput
        {
            get
            {
                return this.m_CopyToOutput;
            }
        }

        public bool IsLink
        {
            get
            {
                return this.m_Link;
            }
        }

        public string LinkPath
        {
            get
            {
                return this.m_LinkPath;
            }
        }
        /// <summary>
        ///
        /// </summary>
        public SubType SubType
        {
            get
            {
                if (m_SubType != null)
                    return m_SubType.Value;
                else
                    return GetSubTypeByFileName(this.Path);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public bool IsValid
        {
            get
            {
                return m_Valid;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public bool PreservePath
        {
            get
            {
                return m_PreservePath;
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
            string buildAction = Helper.AttributeValue(node, "buildAction", String.Empty);
            if (buildAction != string.Empty)
                m_BuildAction = (BuildAction)Enum.Parse(typeof(BuildAction), buildAction);
            string subType = Helper.AttributeValue(node, "subType", string.Empty);
            if (subType != String.Empty)
                m_SubType = (SubType)Enum.Parse(typeof(SubType), subType);

            Console.WriteLine("[FileNode]:BuildAction is {0}", buildAction);


            m_ResourceName = Helper.AttributeValue(node, "resourceName", m_ResourceName.ToString());
            this.m_Link = bool.Parse(Helper.AttributeValue(node, "link", bool.FalseString));
            if ( this.m_Link == true )
            {
                this.m_LinkPath = Helper.AttributeValue( node, "linkPath", string.Empty );
            }
            this.m_CopyToOutput = (CopyToOutput) Enum.Parse(typeof(CopyToOutput), Helper.AttributeValue(node, "copyToOutput", this.m_CopyToOutput.ToString()));
            this.m_PreservePath = bool.Parse( Helper.AttributeValue( node, "preservePath", bool.FalseString ) );

            if( node == null )
            {
                throw new ArgumentNullException("node");
            }

            m_Path = Helper.InterpolateForEnvironmentVariables(node.InnerText);
            if(m_Path == null)
            {
                m_Path = "";
            }

            m_Path = m_Path.Trim();
            m_Valid = true;
            if(!File.Exists(m_Path))
            {
                m_Valid = false;
                Kernel.Instance.Log.Write(LogType.Warning, "File does not exist: {0}", m_Path);
            }

            if (System.IO.Path.GetExtension(m_Path) == ".settings")
            {
                m_SubType = SubType.Settings;
                m_BuildAction = BuildAction.None;
            }
        }

        #endregion
    }
}
