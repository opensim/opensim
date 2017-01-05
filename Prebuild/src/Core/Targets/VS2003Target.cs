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

using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using Prebuild.Core.Nodes;
using Prebuild.Core.Utilities;

namespace Prebuild.Core.Targets
{
    [Target("vs2003")]
    public class VS2003Target : ITarget
    {

        #region Fields

        string solutionVersion = "8.00";
        string productVersion = "7.10.3077";
        string schemaVersion = "2.0";
        string versionName = "2003";
        VSVersion version = VSVersion.VS71;

        readonly Dictionary<string, ToolInfo> m_Tools = new Dictionary<string, ToolInfo>();
        Kernel m_Kernel;

        /// <summary>
        /// Gets or sets the solution version.
        /// </summary>
        /// <value>The solution version.</value>
        protected string SolutionVersion
        {
            get
            {
                return solutionVersion;
            }
            set
            {
                solutionVersion = value;
            }
        }
        /// <summary>
        /// Gets or sets the product version.
        /// </summary>
        /// <value>The product version.</value>
        protected string ProductVersion
        {
            get
            {
                return productVersion;
            }
            set
            {
                productVersion = value;
            }
        }
        /// <summary>
        /// Gets or sets the schema version.
        /// </summary>
        /// <value>The schema version.</value>
        protected string SchemaVersion
        {
            get
            {
                return schemaVersion;
            }
            set
            {
                schemaVersion = value;
            }
        }
        /// <summary>
        /// Gets or sets the name of the version.
        /// </summary>
        /// <value>The name of the version.</value>
        protected string VersionName
        {
            get
            {
                return versionName;
            }
            set
            {
                versionName = value;
            }
        }
        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        /// <value>The version.</value>
        protected VSVersion Version
        {
            get
            {
                return version;
            }
            set
            {
                version = value;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="VS2003Target"/> class.
        /// </summary>
        public VS2003Target()
        {
            m_Tools["C#"] = new ToolInfo("C#", "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", "csproj", "CSHARP");
            m_Tools["VB.NET"] = new ToolInfo("VB.NET", "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}", "vbproj", "VisualBasic");
        }

        #endregion

        #region Private Methods

        private string MakeRefPath(ProjectNode project)
        {
            string ret = "";
            foreach(ReferencePathNode node in project.ReferencePaths)
            {
                try
                {
                    string fullPath = Helper.ResolvePath(node.Path);
                    if(ret.Length < 1)
                    {
                        ret = fullPath;
                    }
                    else
                    {
                        ret += ";" + fullPath;
                    }
                }
                catch(ArgumentException)
                {
                    m_Kernel.Log.Write(LogType.Warning, "Could not resolve reference path: {0}", node.Path);
                }
            }

            return ret;
        }

        private void WriteProject(SolutionNode solution, ProjectNode project)
        {
            if(!m_Tools.ContainsKey(project.Language))
            {
                throw new UnknownLanguageException("Unknown .NET language: " + project.Language);
            }

            ToolInfo toolInfo = m_Tools[project.Language];
            string projectFile = Helper.MakeFilePath(project.FullPath, project.Name, toolInfo.FileExtension);
            StreamWriter ps = new StreamWriter(projectFile);

            m_Kernel.CurrentWorkingDirectory.Push();
            Helper.SetCurrentDir(Path.GetDirectoryName(projectFile));

            using(ps)
            {
                ps.WriteLine("<VisualStudioProject>");
                ps.WriteLine("    <{0}", toolInfo.XmlTag);
                ps.WriteLine("\t\t\t\tProjectType = \"Local\"");
                ps.WriteLine("\t\t\t\tProductVersion = \"{0}\"", ProductVersion);
                ps.WriteLine("\t\t\t\tSchemaVersion = \"{0}\"", SchemaVersion);
                ps.WriteLine("\t\t\t\tProjectGuid = \"{{{0}}}\"", project.Guid.ToString().ToUpper());
                ps.WriteLine("\t\t>");

                ps.WriteLine("\t\t\t\t<Build>");
                ps.WriteLine("            <Settings");
                ps.WriteLine("\t\t\t\t  ApplicationIcon = \"{0}\"",project.AppIcon);
                ps.WriteLine("\t\t\t\t  AssemblyKeyContainerName = \"\"");
                ps.WriteLine("\t\t\t\t  AssemblyName = \"{0}\"", project.AssemblyName);
                ps.WriteLine("\t\t\t\t  AssemblyOriginatorKeyFile = \"\"");
                ps.WriteLine("\t\t\t\t  DefaultClientScript = \"JScript\"");
                ps.WriteLine("\t\t\t\t  DefaultHTMLPageLayout = \"Grid\"");
                ps.WriteLine("\t\t\t\t  DefaultTargetSchema = \"IE50\"");
                ps.WriteLine("\t\t\t\t  DelaySign = \"false\"");

                if(Version == VSVersion.VS70)
                {
                    ps.WriteLine("\t\t\t\t  NoStandardLibraries = \"false\"");
                }

                ps.WriteLine("\t\t\t\t  OutputType = \"{0}\"", project.Type);

                foreach(ConfigurationNode conf in project.Configurations)
                {
                    if (conf.Options["PreBuildEvent"] != null && conf.Options["PreBuildEvent"].ToString().Length != 0)
                    {
                        ps.WriteLine("\t\t\t\t  PreBuildEvent = \"{0}\"", Helper.NormalizePath(conf.Options["PreBuildEvent"].ToString()));
                    }
                    else
                    {
                        ps.WriteLine("\t\t\t\t  PreBuildEvent = \"{0}\"", conf.Options["PreBuildEvent"]);
                    }
                    if (conf.Options["PostBuildEvent"] != null && conf.Options["PostBuildEvent"].ToString().Length != 0)
                    {
                        ps.WriteLine("\t\t\t\t  PostBuildEvent = \"{0}\"", Helper.NormalizePath(conf.Options["PostBuildEvent"].ToString()));
                    }
                    else
                    {
                        ps.WriteLine("\t\t\t\t  PostBuildEvent = \"{0}\"", conf.Options["PostBuildEvent"]);
                    }
                    if (conf.Options["RunPostBuildEvent"] == null)
                    {
                        ps.WriteLine("\t\t\t\t  RunPostBuildEvent = \"{0}\"", "OnBuildSuccess");
                    }
                    else
                    {
                        ps.WriteLine("\t\t\t\t  RunPostBuildEvent = \"{0}\"", conf.Options["RunPostBuildEvent"]);
                    }
                    break;
                }

                ps.WriteLine("\t\t\t\t  RootNamespace = \"{0}\"", project.RootNamespace);
                ps.WriteLine("\t\t\t\t  StartupObject = \"{0}\"", project.StartupObject);
                ps.WriteLine("\t\t     >");

                foreach(ConfigurationNode conf in project.Configurations)
                {
                    ps.WriteLine("\t\t\t\t  <Config");
                    ps.WriteLine("\t\t\t\t      Name = \"{0}\"", conf.Name);
                    ps.WriteLine("\t\t\t\t      AllowUnsafeBlocks = \"{0}\"", conf.Options["AllowUnsafe"].ToString().ToLower());
                    ps.WriteLine("\t\t\t\t      BaseAddress = \"{0}\"", conf.Options["BaseAddress"]);
                    ps.WriteLine("\t\t\t\t      CheckForOverflowUnderflow = \"{0}\"", conf.Options["CheckUnderflowOverflow"].ToString().ToLower());
                    ps.WriteLine("\t\t\t\t      ConfigurationOverrideFile = \"\"");
                    ps.WriteLine("\t\t\t\t      DefineConstants = \"{0}\"", conf.Options["CompilerDefines"]);
                    ps.WriteLine("\t\t\t\t      DocumentationFile = \"{0}\"", GetXmlDocFile(project, conf));//default to the assembly name
                    ps.WriteLine("\t\t\t\t      DebugSymbols = \"{0}\"", conf.Options["DebugInformation"].ToString().ToLower());
                    ps.WriteLine("\t\t\t\t      FileAlignment = \"{0}\"", conf.Options["FileAlignment"]);
                    ps.WriteLine("\t\t\t\t      IncrementalBuild = \"{0}\"", conf.Options["IncrementalBuild"].ToString().ToLower());

                    if(Version == VSVersion.VS71)
                    {
                        ps.WriteLine("\t\t\t\t      NoStdLib = \"{0}\"", conf.Options["NoStdLib"].ToString().ToLower());
                        ps.WriteLine("\t\t\t\t      NoWarn = \"{0}\"", conf.Options["SuppressWarnings"].ToString().ToLower());
                    }

                    ps.WriteLine("\t\t\t\t      Optimize = \"{0}\"", conf.Options["OptimizeCode"].ToString().ToLower());
                    ps.WriteLine("                    OutputPath = \"{0}\"",
                        Helper.EndPath(Helper.NormalizePath(conf.Options["OutputPath"].ToString())));
                    ps.WriteLine("                    RegisterForComInterop = \"{0}\"", conf.Options["RegisterComInterop"].ToString().ToLower());
                    ps.WriteLine("                    RemoveIntegerChecks = \"{0}\"", conf.Options["RemoveIntegerChecks"].ToString().ToLower());
                    ps.WriteLine("                    TreatWarningsAsErrors = \"{0}\"", conf.Options["WarningsAsErrors"].ToString().ToLower());
                    ps.WriteLine("                    WarningLevel = \"{0}\"", conf.Options["WarningLevel"]);
                    ps.WriteLine("                />");
                }

                ps.WriteLine("            </Settings>");

                ps.WriteLine("            <References>");
                foreach(ReferenceNode refr in project.References)
                {
                    ps.WriteLine("                <Reference");
                    ps.WriteLine("                    Name = \"{0}\"", refr.Name);
                    ps.WriteLine("                    AssemblyName = \"{0}\"", refr.Name);

                    if(solution.ProjectsTable.ContainsKey(refr.Name))
                    {
                        ProjectNode refProject = solution.ProjectsTable[refr.Name];
                        ps.WriteLine("                    Project = \"{{{0}}}\"", refProject.Guid.ToString().ToUpper());
                        ps.WriteLine("                    Package = \"{0}\"", toolInfo.Guid.ToUpper());
                    }
                    else
                    {
                        if(refr.Path != null)
                        {
                            ps.WriteLine("                    HintPath = \"{0}\"", Helper.MakeFilePath(refr.Path, refr.Name, "dll"));
                        }

                    }

                    if(refr.LocalCopySpecified)
                    {
                        ps.WriteLine("                    Private = \"{0}\"",refr.LocalCopy);
                    }

                    ps.WriteLine("                />");
                }
                ps.WriteLine("            </References>");

                ps.WriteLine("        </Build>");
                ps.WriteLine("        <Files>");

                ps.WriteLine("            <Include>");

                foreach(string file in project.Files)
                {
                    string fileName = file.Replace(".\\", "");
                    ps.WriteLine("                <File");
                    ps.WriteLine("                    RelPath = \"{0}\"", fileName);
                    ps.WriteLine("                    SubType = \"{0}\"", project.Files.GetSubType(file));
                    ps.WriteLine("                    BuildAction = \"{0}\"", project.Files.GetBuildAction(file));
                    ps.WriteLine("                />");

                    if (project.Files.GetSubType(file) != SubType.Code && project.Files.GetSubType(file) != SubType.Settings)
                    {
                        ps.WriteLine("                <File");
                        ps.WriteLine("                    RelPath = \"{0}\"", fileName.Substring(0, fileName.LastIndexOf('.')) + ".resx");
                        int slash = fileName.LastIndexOf('\\');
                        if (slash == -1)
                        {
                            ps.WriteLine("                    DependentUpon = \"{0}\"", fileName);
                        }
                        else
                        {
                            ps.WriteLine("                    DependentUpon = \"{0}\"", fileName.Substring(slash + 1, fileName.Length - slash - 1));
                        }
                        ps.WriteLine("                    BuildAction = \"{0}\"", "EmbeddedResource");
                        ps.WriteLine("                />");

                    }
                }
                ps.WriteLine("            </Include>");

                ps.WriteLine("        </Files>");
                ps.WriteLine("    </{0}>", toolInfo.XmlTag);
                ps.WriteLine("</VisualStudioProject>");
            }

            ps = new StreamWriter(projectFile + ".user");
            using(ps)
            {
                ps.WriteLine("<VisualStudioProject>");
                ps.WriteLine("    <{0}>", toolInfo.XmlTag);
                ps.WriteLine("        <Build>");

                ps.WriteLine("            <Settings ReferencePath=\"{0}\">", MakeRefPath(project));
                foreach(ConfigurationNode conf in project.Configurations)
                {
                    ps.WriteLine("                <Config");
                    ps.WriteLine("                    Name = \"{0}\"", conf.Name);
                    ps.WriteLine("                />");
                }
                ps.WriteLine("            </Settings>");

                ps.WriteLine("        </Build>");
                ps.WriteLine("    </{0}>", toolInfo.XmlTag);
                ps.WriteLine("</VisualStudioProject>");
            }

            m_Kernel.CurrentWorkingDirectory.Pop();
        }

        /// <summary>
        /// Gets the XML doc file.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <param name="conf">The conf.</param>
        /// <returns></returns>
        public static string GetXmlDocFile(ProjectNode project, ConfigurationNode conf)
        {
            if( conf == null )
            {
                throw new ArgumentNullException("conf");
            }
            if( project == null )
            {
                throw new ArgumentNullException("project");
            }
            //			if(!(bool)conf.Options["GenerateXmlDocFile"]) //default to none, if the generate option is false
            //			{
            //				return string.Empty;
            //			}

            //default to "AssemblyName.xml"
            //string defaultValue = Path.GetFileNameWithoutExtension(project.AssemblyName) + ".xml";
            //return (string)conf.Options["XmlDocFile", defaultValue];

            //default to no XmlDocFile file
            return (string)conf.Options["XmlDocFile", ""];
        }

        private void WriteSolution(SolutionNode solution)
        {
            m_Kernel.Log.Write("Creating Visual Studio {0} solution and project files", VersionName);

            foreach(ProjectNode project in solution.Projects)
            {
                if(m_Kernel.AllowProject(project.FilterGroups))
                {
                    m_Kernel.Log.Write("...Creating project: {0}", project.Name);
                    WriteProject(solution, project);
                }
            }

            m_Kernel.Log.Write("");
            string solutionFile = Helper.MakeFilePath(solution.FullPath, solution.Name, "sln");
            StreamWriter ss = new StreamWriter(solutionFile);

            m_Kernel.CurrentWorkingDirectory.Push();
            Helper.SetCurrentDir(Path.GetDirectoryName(solutionFile));

            using(ss)
            {
                ss.WriteLine("Microsoft Visual Studio Solution File, Format Version {0}", SolutionVersion);
                foreach(ProjectNode project in solution.Projects)
                {
                    if(!m_Tools.ContainsKey(project.Language))
                    {
                        throw new UnknownLanguageException("Unknown .NET language: " + project.Language);
                    }

                    ToolInfo toolInfo = m_Tools[project.Language];

                    string path = Helper.MakePathRelativeTo(solution.FullPath, project.FullPath);
                    ss.WriteLine("Project(\"{0}\") = \"{1}\", \"{2}\", \"{{{3}}}\"",
                        toolInfo.Guid, project.Name, Helper.MakeFilePath(path, project.Name,
                        toolInfo.FileExtension), project.Guid.ToString().ToUpper());

                    ss.WriteLine("\tProjectSection(ProjectDependencies) = postProject");
                    ss.WriteLine("\tEndProjectSection");

                    ss.WriteLine("EndProject");
                }

                ss.WriteLine("Global");

                ss.WriteLine("\tGlobalSection(SolutionConfiguration) = preSolution");
                foreach(ConfigurationNode conf in solution.Configurations)
                {
                    ss.WriteLine("\t\t{0} = {0}", conf.Name);
                }
                ss.WriteLine("\tEndGlobalSection");

                ss.WriteLine("\tGlobalSection(ProjectDependencies) = postSolution");
                foreach(ProjectNode project in solution.Projects)
                {
                    for(int i = 0; i < project.References.Count; i++)
                    {
                        ReferenceNode refr = project.References[i];
                        if(solution.ProjectsTable.ContainsKey(refr.Name))
                        {
                            ProjectNode refProject = solution.ProjectsTable[refr.Name];
                            ss.WriteLine("\t\t({{{0}}}).{1} = ({{{2}}})",
                                project.Guid.ToString().ToUpper()
                                , i,
                                refProject.Guid.ToString().ToUpper()
                                );
                        }
                    }
                }
                ss.WriteLine("\tEndGlobalSection");

                ss.WriteLine("\tGlobalSection(ProjectConfiguration) = postSolution");
                foreach(ProjectNode project in solution.Projects)
                {
                    foreach(ConfigurationNode conf in solution.Configurations)
                    {
                        ss.WriteLine("\t\t{{{0}}}.{1}.ActiveCfg = {1}|.NET",
                            project.Guid.ToString().ToUpper(),
                            conf.Name);

                        ss.WriteLine("\t\t{{{0}}}.{1}.Build.0 = {1}|.NET",
                            project.Guid.ToString().ToUpper(),
                            conf.Name);
                    }
                }
                ss.WriteLine("\tEndGlobalSection");

                if(solution.Files != null)
                {
                    ss.WriteLine("\tGlobalSection(SolutionItems) = postSolution");
                    foreach(string file in solution.Files)
                    {
                        ss.WriteLine("\t\t{0} = {0}", file);
                    }
                    ss.WriteLine("\tEndGlobalSection");
                }

                ss.WriteLine("\tGlobalSection(ExtensibilityGlobals) = postSolution");
                ss.WriteLine("\tEndGlobalSection");
                ss.WriteLine("\tGlobalSection(ExtensibilityAddIns) = postSolution");
                ss.WriteLine("\tEndGlobalSection");

                ss.WriteLine("EndGlobal");
            }

            m_Kernel.CurrentWorkingDirectory.Pop();
        }

        private void CleanProject(ProjectNode project)
        {
            m_Kernel.Log.Write("...Cleaning project: {0}", project.Name);

            ToolInfo toolInfo = m_Tools[project.Language];
            string projectFile = Helper.MakeFilePath(project.FullPath, project.Name, toolInfo.FileExtension);
            string userFile = projectFile + ".user";

            Helper.DeleteIfExists(projectFile);
            Helper.DeleteIfExists(userFile);
        }

        private void CleanSolution(SolutionNode solution)
        {
            m_Kernel.Log.Write("Cleaning Visual Studio {0} solution and project files", VersionName, solution.Name);

            string slnFile = Helper.MakeFilePath(solution.FullPath, solution.Name, "sln");
            string suoFile = Helper.MakeFilePath(solution.FullPath, solution.Name, "suo");

            Helper.DeleteIfExists(slnFile);
            Helper.DeleteIfExists(suoFile);

            foreach(ProjectNode project in solution.Projects)
            {
                CleanProject(project);
            }

            m_Kernel.Log.Write("");
        }

        #endregion

        #region ITarget Members

        /// <summary>
        /// Writes the specified kern.
        /// </summary>
        /// <param name="kern">The kern.</param>
        public virtual void Write(Kernel kern)
        {
            if( kern == null )
            {
                throw new ArgumentNullException("kern");
            }
            m_Kernel = kern;
            foreach(SolutionNode sol in m_Kernel.Solutions)
            {
                WriteSolution(sol);
            }
            m_Kernel = null;
        }

        /// <summary>
        /// Cleans the specified kern.
        /// </summary>
        /// <param name="kern">The kern.</param>
        public virtual void Clean(Kernel kern)
        {
            if( kern == null )
            {
                throw new ArgumentNullException("kern");
            }
            m_Kernel = kern;
            foreach(SolutionNode sol in m_Kernel.Solutions)
            {
                CleanSolution(sol);
            }
            m_Kernel = null;
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public virtual string Name
        {
            get
            {
                return "vs2003";
            }
        }

        #endregion
    }
}
