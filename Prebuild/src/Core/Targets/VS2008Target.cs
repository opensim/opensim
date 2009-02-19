using System;
using System.Collections.Generic;
using System.Text;
using Prebuild.Core.Attributes;

namespace Prebuild.Core.Targets
{
    [Target("vs2008")]
    public class VS2008Target : VS2005Target
    {
        protected override string SolutionTag
        {
            get { return "# Visual Studio 2008"; }
        }

        protected override string SolutionVersion
        {
            get
            {
                return "10.00";
            }
        }

        protected override string VersionName
        {
            get
            {
                return "Visual C# 2008";
            }
        }

        protected override string ToolsVersionXml
        {
            get
            {
                return " ToolsVersion=\"3.5\"";
            }
        }

        protected override string ProductVersion
        {
            get
            {
                return "9.0.21022";
            }
        }

        public override string Name
        {
            get
            {
                return "vs2008";
            }
        }
    }
}
