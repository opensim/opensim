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
using System.Reflection;
using System.Xml;

using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using Prebuild.Core.Utilities;

namespace Prebuild.Core.Nodes
{
    /// <summary>
    ///
    /// </summary>
    [DataNode("Options")]
    public class OptionsNode : DataNode
    {
        #region Fields

        private static readonly Dictionary<string,FieldInfo> m_OptionFields = new Dictionary<string, FieldInfo>();

        [OptionNode("CompilerDefines")]
        private string m_CompilerDefines = "";

        /// <summary>
        ///
        /// </summary>
        public string CompilerDefines
        {
            get
            {
                return m_CompilerDefines;
            }
            set
            {
                m_CompilerDefines = value;
            }
        }

        [OptionNode("OptimizeCode")]
        private bool m_OptimizeCode;

        /// <summary>
        ///
        /// </summary>
        public bool OptimizeCode
        {
            get
            {
                return m_OptimizeCode;
            }
            set
            {
                m_OptimizeCode = value;
            }
        }

        [OptionNode("CheckUnderflowOverflow")]
        private bool m_CheckUnderflowOverflow;

        /// <summary>
        ///
        /// </summary>
        public bool CheckUnderflowOverflow
        {
            get
            {
                return m_CheckUnderflowOverflow;
            }
            set
            {
                m_CheckUnderflowOverflow = value;
            }
        }

        [OptionNode("AllowUnsafe")]
        private bool m_AllowUnsafe;

        /// <summary>
        ///
        /// </summary>
        public bool AllowUnsafe
        {
            get
            {
                return m_AllowUnsafe;
            }
            set
            {
                m_AllowUnsafe = value;
            }
        }

        [OptionNode("PreBuildEvent")]
        private string m_PreBuildEvent;

        /// <summary>
        ///
        /// </summary>
        public string PreBuildEvent
        {
            get
            {
                return m_PreBuildEvent;
            }
            set
            {
                m_PreBuildEvent = value;
            }
        }

        [OptionNode("PostBuildEvent")]
        private string m_PostBuildEvent;

        /// <summary>
        ///
        /// </summary>
        public string PostBuildEvent
        {
            get
            {
                return m_PostBuildEvent;
            }
            set
            {
                m_PostBuildEvent = value;
            }
        }

        [OptionNode("PreBuildEventArgs")]
        private string m_PreBuildEventArgs;

        /// <summary>
        ///
        /// </summary>
        public string PreBuildEventArgs
        {
            get
            {
                return m_PreBuildEventArgs;
            }
            set
            {
                m_PreBuildEventArgs = value;
            }
        }

        [OptionNode("PostBuildEventArgs")]
        private string m_PostBuildEventArgs;

        /// <summary>
        ///
        /// </summary>
        public string PostBuildEventArgs
        {
            get
            {
                return m_PostBuildEventArgs;
            }
            set
            {
                m_PostBuildEventArgs = value;
            }
        }

        [OptionNode("RunPostBuildEvent")]
        private string m_RunPostBuildEvent;

        /// <summary>
        ///
        /// </summary>
        public string RunPostBuildEvent
        {
            get
            {
                return m_RunPostBuildEvent;
            }
            set
            {
                m_RunPostBuildEvent = value;
            }
        }

        [OptionNode("RunScript")]
        private string m_RunScript;

        /// <summary>
        ///
        /// </summary>
        public string RunScript
        {
            get
            {
                return m_RunScript;
            }
            set
            {
                m_RunScript = value;
            }
        }

        [OptionNode("WarningLevel")]
        private int m_WarningLevel = 4;

        /// <summary>
        ///
        /// </summary>
        public int WarningLevel
        {
            get
            {
                return m_WarningLevel;
            }
            set
            {
                m_WarningLevel = value;
            }
        }

        [OptionNode("WarningsAsErrors")]
        private bool m_WarningsAsErrors;

        /// <summary>
        ///
        /// </summary>
        public bool WarningsAsErrors
        {
            get
            {
                return m_WarningsAsErrors;
            }
            set
            {
                m_WarningsAsErrors = value;
            }
        }

        [OptionNode("SuppressWarnings")]
        private string m_SuppressWarnings = "";

        /// <summary>
        ///
        /// </summary>
        public string SuppressWarnings
        {
            get
            {
                return m_SuppressWarnings;
            }
            set
            {
                m_SuppressWarnings = value;
            }
        }

        [OptionNode("OutputPath")]
        private string m_OutputPath = "bin/";

        /// <summary>
        ///
        /// </summary>
        public string OutputPath
        {
            get
            {
                return m_OutputPath;
            }
            set
            {
                m_OutputPath = value;
            }
        }

        [OptionNode("GenerateDocumentation")]
        private bool m_GenerateDocumentation;

        /// <summary>
        ///
        /// </summary>
        public bool GenerateDocumentation
        {
            get
            {
                return m_GenerateDocumentation;
            }
            set
            {
                m_GenerateDocumentation = value;
            }
        }

        [OptionNode("GenerateXmlDocFile")]
        private bool m_GenerateXmlDocFile;

        /// <summary>
        ///
        /// </summary>
        public bool GenerateXmlDocFile
        {
            get
            {
                return m_GenerateXmlDocFile;
            }
            set
            {
                m_GenerateXmlDocFile = value;
            }
        }

        [OptionNode("XmlDocFile")]
        private string m_XmlDocFile = "";

        /// <summary>
        ///
        /// </summary>
        public string XmlDocFile
        {
            get
            {
                return m_XmlDocFile;
            }
            set
            {
                m_XmlDocFile = value;
            }
        }

        [OptionNode("KeyFile")]
        private string m_KeyFile = "";

        /// <summary>
        ///
        /// </summary>
        public string KeyFile
        {
            get
            {
                return m_KeyFile;
            }
            set
            {
                m_KeyFile = value;
            }
        }

        [OptionNode("DebugInformation")]
        private bool m_DebugInformation;

        /// <summary>
        ///
        /// </summary>
        public bool DebugInformation
        {
            get
            {
                return m_DebugInformation;
            }
            set
            {
                m_DebugInformation = value;
            }
        }

        [OptionNode("RegisterComInterop")]
        private bool m_RegisterComInterop;

        /// <summary>
        ///
        /// </summary>
        public bool RegisterComInterop
        {
            get
            {
                return m_RegisterComInterop;
            }
            set
            {
                m_RegisterComInterop = value;
            }
        }

        [OptionNode("RemoveIntegerChecks")]
        private bool m_RemoveIntegerChecks;

        /// <summary>
        ///
        /// </summary>
        public bool RemoveIntegerChecks
        {
            get
            {
                return m_RemoveIntegerChecks;
            }
            set
            {
                m_RemoveIntegerChecks = value;
            }
        }

        [OptionNode("IncrementalBuild")]
        private bool m_IncrementalBuild;

        /// <summary>
        ///
        /// </summary>
        public bool IncrementalBuild
        {
            get
            {
                return m_IncrementalBuild;
            }
            set
            {
                m_IncrementalBuild = value;
            }
        }

        [OptionNode("BaseAddress")]
        private string m_BaseAddress = "285212672";

        /// <summary>
        ///
        /// </summary>
        public string BaseAddress
        {
            get
            {
                return m_BaseAddress;
            }
            set
            {
                m_BaseAddress = value;
            }
        }

        [OptionNode("FileAlignment")]
        private int m_FileAlignment = 4096;

        /// <summary>
        ///
        /// </summary>
        public int FileAlignment
        {
            get
            {
                return m_FileAlignment;
            }
            set
            {
                m_FileAlignment = value;
            }
        }

        [OptionNode("NoStdLib")]
        private bool m_NoStdLib;

        /// <summary>
        ///
        /// </summary>
        public bool NoStdLib
        {
            get
            {
                return m_NoStdLib;
            }
            set
            {
                m_NoStdLib = value;
            }
        }

        private readonly List<string> m_FieldsDefined = new List<string>();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the <see cref="OptionsNode"/> class.
        /// </summary>
        static OptionsNode()
        {
            Type t = typeof(OptionsNode);

            foreach(FieldInfo f in t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                object[] attrs = f.GetCustomAttributes(typeof(OptionNodeAttribute), false);
                if(attrs == null || attrs.Length < 1)
                {
                    continue;
                }

                OptionNodeAttribute ona = (OptionNodeAttribute)attrs[0];
                m_OptionFields[ona.NodeName] = f;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the <see cref="Object"/> at the specified index.
        /// </summary>
        /// <value></value>
        public object this[string index]
        {
            get
            {
                if(!m_OptionFields.ContainsKey(index))
                {
                    return null;
                }

                FieldInfo f = m_OptionFields[index];
                return f.GetValue(this);
            }
        }

        /// <summary>
        /// Gets the <see cref="Object"/> at the specified index.
        /// </summary>
        /// <value></value>
        public object this[string index, object defaultValue]
        {
            get
            {
                object valueObject = this[index];
                if(valueObject !=  null && valueObject is string && ((string)valueObject).Length == 0)
                {
                    return defaultValue;
                }
                return valueObject;
            }
        }


        #endregion

        #region Private Methods

        private void FlagDefined(string name)
        {
            if(!m_FieldsDefined.Contains(name))
            {
                m_FieldsDefined.Add(name);
            }
        }

        private void SetOption(string nodeName, string val)
        {
            lock(m_OptionFields)
            {
                if(!m_OptionFields.ContainsKey(nodeName))
                {
                    return;
                }

                FieldInfo f = m_OptionFields[nodeName];
                f.SetValue(this, Helper.TranslateValue(f.FieldType, val));
                FlagDefined(f.Name);
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
            if( node == null )
            {
                throw new ArgumentNullException("node");
            }

            foreach(XmlNode child in node.ChildNodes)
            {
                SetOption(child.Name, Helper.InterpolateForEnvironmentVariables(child.InnerText));
            }
        }

        /// <summary>
        /// Copies to.
        /// </summary>
        /// <param name="opt">The opt.</param>
        public void CopyTo(OptionsNode opt)
        {
            if(opt == null)
            {
                return;
            }

            foreach(FieldInfo f in m_OptionFields.Values)
            {
                if(m_FieldsDefined.Contains(f.Name))
                {
                    f.SetValue(opt, f.GetValue(this));
                    opt.m_FieldsDefined.Add(f.Name);
                }
            }
        }

        #endregion
    }
}
