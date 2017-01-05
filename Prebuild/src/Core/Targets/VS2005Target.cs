#region BSD License
/*
Copyright (c) 2004 Matthew Holmes (matthew@wildfiregames.com)

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
using System.Text;

using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using Prebuild.Core.Nodes;
using Prebuild.Core.Utilities;

namespace Prebuild.Core.Targets
{
    /// <summary>
    ///
    /// </summary>
    [Target("vs2005")]
    public class VS2005Target : VSGenericTarget
    {
        #region Inner Classes

        #endregion

        #region Fields

        string solutionVersion = "9.00";
        string productVersion = "8.0.50727";
        string schemaVersion = "2.0";
        string versionName = "Visual C# 2005";
        string name = "vs2005";

        VSVersion version = VSVersion.VS80;

        public override string SolutionTag
        {
            get { return "# Visual Studio 2005"; }
        }

        protected override string GetToolsVersionXml(FrameworkVersion frameworkVersion)
        {
            return string.Empty;
        }
        /// <summary>
        /// Gets or sets the solution version.
        /// </summary>
        /// <value>The solution version.</value>
        public override string SolutionVersion
        {
            get
            {
                return solutionVersion;
            }
        }
        /// <summary>
        /// Gets or sets the product version.
        /// </summary>
        /// <value>The product version.</value>
        public override string ProductVersion
        {
            get
            {
                return productVersion;
            }
        }
        /// <summary>
        /// Gets or sets the schema version.
        /// </summary>
        /// <value>The schema version.</value>
        public override string SchemaVersion
        {
            get
            {
                return schemaVersion;
            }
        }
        /// <summary>
        /// Gets or sets the name of the version.
        /// </summary>
        /// <value>The name of the version.</value>
        public override string VersionName
        {
            get
            {
                return versionName;
            }
        }
        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        /// <value>The version.</value>
        public override VSVersion Version
        {
            get
            {
                return version;
            }
        }
        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public override string Name
        {
            get
            {
                return name;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="VS2005Target"/> class.
        /// </summary>
        public VS2005Target()
            : base()
        {
        }

        #endregion
    }
}
