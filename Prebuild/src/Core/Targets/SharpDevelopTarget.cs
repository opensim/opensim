#region BSD License
/*
Copyright (c) 2004 Matthew Holmes (matthew@wildfiregames.com), Dan Moorehead (dan05a@gmail.com)

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
using System.Text.RegularExpressions;

using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using Prebuild.Core.Nodes;
using Prebuild.Core.Utilities;

namespace Prebuild.Core.Targets
{
    /// <summary>
    ///
    /// </summary>
    [Target("sharpdev")]
    public class SharpDevelopTarget : ITarget
    {
        #region Fields

        private Kernel m_Kernel;

        #endregion

        #region Private Methods

        private static string PrependPath(string path)
        {
            string tmpPath = Helper.NormalizePath(path, '/');
            Regex regex = new Regex(@"(\w):/(\w+)");
            Match match = regex.Match(tmpPath);
            if(match.Success || tmpPath[0] == '.' || tmpPath[0] == '/')
            {
                tmpPath = Helper.NormalizePath(tmpPath);
            }
            else
            {
                tmpPath = Helper.NormalizePath("./" + tmpPath);
            }

            return tmpPath;
        }

        private static string BuildReference(SolutionNode solution, ReferenceNode refr)
        {
            string ret = "<Reference type=\"";
            if(solution.ProjectsTable.ContainsKey(refr.Name))
            {
                ret += "Project\" refto=\"" + refr.Name;
                ret += "\" localcopy=\"" + refr.LocalCopy.ToString() + "\" />";
            }
            else
            {
                ProjectNode project = (ProjectNode)refr.Parent;
                string fileRef = FindFileReference(refr.Name, project);

                if(refr.Path != null || fileRef != null)
                {
                    ret += "Assembly\" refto=\"";

                    string finalPath = (refr.Path != null) ? Helper.MakeFilePath(refr.Path, refr.Name, "dll") : fileRef;

                    ret += finalPath;
                    ret += "\" localcopy=\"" + refr.LocalCopy.ToString() + "\" />";
                    return ret;
                }

                ret += "Gac\" refto=\"";
                try
                {
                    //Assembly assem = Assembly.Load(refr.Name);
                    ret += refr.Name;// assem.FullName;
                }
                catch (System.NullReferenceException e)
                {
                    e.ToString();
                    ret += refr.Name;
                }
                ret += "\" localcopy=\"" + refr.LocalCopy.ToString() + "\" />";
            }

            return ret;
        }

        private static string FindFileReference(string refName, ProjectNode project)
        {
            foreach(ReferencePathNode refPath in project.ReferencePaths)
            {
                string fullPath = Helper.MakeFilePath(refPath.Path, refName, "dll");

                if(File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the XML doc file.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <param name="conf">The conf.</param>
        /// <returns></returns>
        public static string GenerateXmlDocFile(ProjectNode project, ConfigurationNode conf)
        {
            if( conf == null )
            {
                throw new ArgumentNullException("conf");
            }
            if( project == null )
            {
                throw new ArgumentNullException("project");
            }
            string docFile = (string)conf.Options["XmlDocFile"];
            if(docFile != null && docFile.Length == 0)//default to assembly name if not specified
            {
                return "False";
            }
            return "True";
        }

        private void WriteProject(SolutionNode solution, ProjectNode project)
        {
            string csComp = "Csc";
            string netRuntime = "MsNet";
            if(project.Runtime == ClrRuntime.Mono)
            {
                csComp = "Mcs";
                netRuntime = "Mono";
            }

            string projFile = Helper.MakeFilePath(project.FullPath, project.Name, "prjx");
            StreamWriter ss = new StreamWriter(projFile);

            m_Kernel.CurrentWorkingDirectory.Push();
            Helper.SetCurrentDir(Path.GetDirectoryName(projFile));

            using(ss)
            {
                ss.WriteLine(
                    "<Project name=\"{0}\" standardNamespace=\"{1}\" description=\"\" newfilesearch=\"None\" enableviewstate=\"True\" version=\"1.1\" projecttype=\"C#\">",
                    project.Name,
                    project.RootNamespace
                    );

                ss.WriteLine("  <Contents>");
                foreach(string file in project.Files)
                {
                    string buildAction = "Compile";
                    switch(project.Files.GetBuildAction(file))
                    {
                        case BuildAction.None:
                            buildAction = "Nothing";
                            break;

                        case BuildAction.Content:
                            buildAction = "Exclude";
                            break;

                        case BuildAction.EmbeddedResource:
                            buildAction = "EmbedAsResource";
                            break;

                        default:
                            buildAction = "Compile";
                            break;
                    }

                    // Sort of a hack, we try and resolve the path and make it relative, if we can.
                    string filePath = PrependPath(file);
                    ss.WriteLine("    <File name=\"{0}\" subtype=\"Code\" buildaction=\"{1}\" dependson=\"\" data=\"\" />", filePath, buildAction);
                }
                ss.WriteLine("  </Contents>");

                ss.WriteLine("  <References>");
                foreach(ReferenceNode refr in project.References)
                {
                    ss.WriteLine("    {0}", BuildReference(solution, refr));
                }
                ss.WriteLine("  </References>");

                ss.Write("  <DeploymentInformation");
                ss.Write(" target=\"\"");
                ss.Write(" script=\"\"");
                ss.Write(" strategy=\"File\"");
                ss.WriteLine(" />");

                int count = 0;

                ss.WriteLine("  <Configurations active=\"{0}\">", solution.ActiveConfig);

                foreach(ConfigurationNode conf in project.Configurations)
                {
                    ss.Write("    <Configuration");
                    ss.Write(" runwithwarnings=\"True\"");
                    ss.Write(" name=\"{0}\"", conf.Name);
                    ss.WriteLine(">");
                    ss.Write("      <CodeGeneration");
                    ss.Write(" runtime=\"{0}\"", netRuntime);
                    ss.Write(" compiler=\"{0}\"", csComp);
                    ss.Write(" compilerversion=\"\"");
                    ss.Write(" warninglevel=\"{0}\"", conf.Options["WarningLevel"]);
                    ss.Write(" nowarn=\"{0}\"", conf.Options["SuppressWarnings"]);
                    ss.Write(" includedebuginformation=\"{0}\"", conf.Options["DebugInformation"]);
                    ss.Write(" optimize=\"{0}\"", conf.Options["OptimizeCode"]);
                    ss.Write(" unsafecodeallowed=\"{0}\"", conf.Options["AllowUnsafe"]);
                    ss.Write(" generateoverflowchecks=\"{0}\"", conf.Options["CheckUnderflowOverflow"]);
                    ss.Write(" mainclass=\"{0}\"", project.StartupObject);
                    ss.Write(" target=\"{0}\"", project.Type);
                    ss.Write(" definesymbols=\"{0}\"", conf.Options["CompilerDefines"]);
                    ss.Write(" generatexmldocumentation=\"{0}\"", GenerateXmlDocFile(project, conf));
                    ss.Write(" win32Icon=\"{0}\"", Helper.NormalizePath(".\\" + project.AppIcon));
                    ss.Write(" noconfig=\"{0}\"", "False");
                    ss.Write(" nostdlib=\"{0}\"", conf.Options["NoStdLib"]);
                    ss.WriteLine(" />");

                    ss.Write("      <Execution");
                    ss.Write(" commandlineparameters=\"\"");
                    ss.Write(" consolepause=\"True\"");
                    ss.WriteLine(" />");

                    ss.Write("      <Output");
                    ss.Write(" directory=\".\\{0}\"", Helper.NormalizePath(conf.Options["OutputPath"].ToString()));
                    ss.Write(" assembly=\"{0}\"", project.AssemblyName);
                    ss.Write(" executeScript=\"{0}\"", conf.Options["RunScript"]);
                    if (conf.Options["PreBuildEvent"] != null && conf.Options["PreBuildEvent"].ToString().Length != 0)
                    {
                        ss.Write(" executeBeforeBuild=\"{0}\"", Helper.NormalizePath(conf.Options["PreBuildEvent"].ToString()));
                    }
                    else
                    {
                        ss.Write(" executeBeforeBuild=\"{0}\"", conf.Options["PreBuildEvent"]);
                    }
                    if (conf.Options["PostBuildEvent"] != null && conf.Options["PostBuildEvent"].ToString().Length != 0)
                    {
                        ss.Write(" executeAfterBuild=\"{0}\"", Helper.NormalizePath(conf.Options["PostBuildEvent"].ToString()));
                    }
                    else
                    {
                        ss.Write(" executeAfterBuild=\"{0}\"", conf.Options["PostBuildEvent"]);
                    }
                    ss.Write(" executeBeforeBuildArguments=\"{0}\"", conf.Options["PreBuildEventArgs"]);
                    ss.Write(" executeAfterBuildArguments=\"{0}\"", conf.Options["PreBuildEventArgs"]);
                    ss.WriteLine(" />");
                    ss.WriteLine("    </Configuration>");

                    count++;
                }
                ss.WriteLine("  </Configurations>");
                ss.WriteLine("</Project>");
            }

            m_Kernel.CurrentWorkingDirectory.Pop();
        }

        private void WriteCombine(SolutionNode solution)
        {
            m_Kernel.Log.Write("Creating SharpDevelop combine and project files");
            foreach(ProjectNode project in solution.Projects)
            {
                if(m_Kernel.AllowProject(project.FilterGroups))
                {
                    m_Kernel.Log.Write("...Creating project: {0}", project.Name);
                    WriteProject(solution, project);
                }
            }

            m_Kernel.Log.Write("");
            string combFile = Helper.MakeFilePath(solution.FullPath, solution.Name, "cmbx");
            StreamWriter ss = new StreamWriter(combFile);

            m_Kernel.CurrentWorkingDirectory.Push();
            Helper.SetCurrentDir(Path.GetDirectoryName(combFile));

            using(ss)
            {
                ss.WriteLine("<Combine fileversion=\"1.0\" name=\"{0}\" description=\"\">", solution.Name);

                int count = 0;
                foreach(ProjectNode project in solution.Projects)
                {
                    if(count == 0)
                        ss.WriteLine("  <StartMode startupentry=\"{0}\" single=\"True\">", project.Name);

                    ss.WriteLine("    <Execute entry=\"{0}\" type=\"None\" />", project.Name);
                    count++;
                }
                ss.WriteLine("  </StartMode>");

                ss.WriteLine("  <Entries>");
                foreach(ProjectNode project in solution.Projects)
                {
                    string path = Helper.MakePathRelativeTo(solution.FullPath, project.FullPath);
                    ss.WriteLine("    <Entry filename=\"{0}\" />",
                        Helper.MakeFilePath(path, project.Name, "prjx"));
                }
                ss.WriteLine("  </Entries>");

                count = 0;
                foreach(ConfigurationNode conf in solution.Configurations)
                {
                    if(count == 0)
                    {
                        ss.WriteLine("  <Configurations active=\"{0}\">", conf.Name);
                    }

                    ss.WriteLine("    <Configuration name=\"{0}\">", conf.Name);
                    foreach(ProjectNode project in solution.Projects)
                    {
                        ss.WriteLine("      <Entry name=\"{0}\" configurationname=\"{1}\" build=\"True\" />", project.Name, conf.Name);
                    }
                    ss.WriteLine("    </Configuration>");

                    count++;
                }
                ss.WriteLine("  </Configurations>");
                ss.WriteLine("</Combine>");
            }

            m_Kernel.CurrentWorkingDirectory.Pop();
        }

        private void CleanProject(ProjectNode project)
        {
            m_Kernel.Log.Write("...Cleaning project: {0}", project.Name);
            string projectFile = Helper.MakeFilePath(project.FullPath, project.Name, "prjx");
            Helper.DeleteIfExists(projectFile);
        }

        private void CleanSolution(SolutionNode solution)
        {
            m_Kernel.Log.Write("Cleaning SharpDevelop combine and project files for", solution.Name);

            string slnFile = Helper.MakeFilePath(solution.FullPath, solution.Name, "cmbx");
            Helper.DeleteIfExists(slnFile);

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
        public void Write(Kernel kern)
        {
            if( kern == null )
            {
                throw new ArgumentNullException("kern");
            }
            m_Kernel = kern;
            foreach(SolutionNode solution in kern.Solutions)
            {
                WriteCombine(solution);
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
            foreach(SolutionNode sol in kern.Solutions)
            {
                CleanSolution(sol);
            }
            m_Kernel = null;
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get
            {
                return "sharpdev";
            }
        }

        #endregion
    }
}
