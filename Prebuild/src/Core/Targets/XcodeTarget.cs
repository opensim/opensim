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
using System.Reflection;
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
    [Target("xcode")]
    public class XcodeTarget : ITarget
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
            //if(match.Success || tmpPath[0] == '.' || tmpPath[0] == '/')
            //{
            tmpPath = Helper.NormalizePath(tmpPath);
            //}
            //			else
            //			{
            //				tmpPath = Helper.NormalizePath("./" + tmpPath);
            //			}

            return tmpPath;
        }

        private static string BuildReference(SolutionNode solution, ReferenceNode refr)
        {
            string ret = "";
            if (solution.ProjectsTable.ContainsKey(refr.Name))
            {
                ProjectNode project = (ProjectNode)solution.ProjectsTable[refr.Name];
                string fileRef = FindFileReference(refr.Name, project);
                string finalPath = Helper.NormalizePath(Helper.MakeFilePath(project.FullPath + "/${build.dir}/", refr.Name, "dll"), '/');
                ret += finalPath;
                return ret;
            }
            else
            {
                ProjectNode project = (ProjectNode)refr.Parent;
                string fileRef = FindFileReference(refr.Name, project);

                if (refr.Path != null || fileRef != null)
                {
                    string finalPath = (refr.Path != null) ? Helper.NormalizePath(refr.Path + "/" + refr.Name + ".dll", '/') : fileRef;
                    ret += finalPath;
                    return ret;
                }

                try
                {
                    //Assembly assem = Assembly.Load(refr.Name);
                    //if (assem != null)
                    //{
                    //ret += (refr.Name + ".dll");
                    //}
                    //else
                    //{
                    ret += (refr.Name + ".dll");
                    //}
                }
                catch (System.NullReferenceException e)
                {
                    e.ToString();
                    ret += refr.Name + ".dll";
                }
            }
            return ret;
        }

        private static string BuildReferencePath(SolutionNode solution, ReferenceNode refr)
        {
            string ret = "";
            if (solution.ProjectsTable.ContainsKey(refr.Name))
            {
                ProjectNode project = (ProjectNode)solution.ProjectsTable[refr.Name];
                string fileRef = FindFileReference(refr.Name, project);
                string finalPath = Helper.NormalizePath(Helper.MakeReferencePath(project.FullPath + "/${build.dir}/"), '/');
                ret += finalPath;
                return ret;
            }
            else
            {
                ProjectNode project = (ProjectNode)refr.Parent;
                string fileRef = FindFileReference(refr.Name, project);

                if (refr.Path != null || fileRef != null)
                {
                    string finalPath = (refr.Path != null) ? Helper.NormalizePath(refr.Path, '/') : fileRef;
                    ret += finalPath;
                    return ret;
                }

                try
                {
                    Assembly assem = Assembly.Load(refr.Name);
                    if (assem != null)
                    {
                        ret += "";
                    }
                    else
                    {
                        ret += "";
                    }
                }
                catch (System.NullReferenceException e)
                {
                    e.ToString();
                    ret += "";
                }
            }
            return ret;
        }

        private static string FindFileReference(string refName, ProjectNode project)
        {
            foreach (ReferencePathNode refPath in project.ReferencePaths)
            {
                string fullPath = Helper.MakeFilePath(refPath.Path, refName, "dll");

                if (File.Exists(fullPath))
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
        public static string GetXmlDocFile(ProjectNode project, ConfigurationNode conf)
        {
            if (conf == null)
            {
                throw new ArgumentNullException("conf");
            }
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }
            string docFile = (string)conf.Options["XmlDocFile"];
            //			if(docFile != null && docFile.Length == 0)//default to assembly name if not specified
            //			{
            //				return Path.GetFileNameWithoutExtension(project.AssemblyName) + ".xml";
            //			}
            return docFile;
        }

        private void WriteProject(SolutionNode solution, ProjectNode project)
        {
            string projFile = Helper.MakeFilePath(project.FullPath, project.Name + (project.Type == ProjectType.Library ? ".dll" : ".exe"), "build");
            StreamWriter ss = new StreamWriter(projFile);

            m_Kernel.CurrentWorkingDirectory.Push();
            Helper.SetCurrentDir(Path.GetDirectoryName(projFile));
            bool hasDoc = false;

            using (ss)
            {
                ss.WriteLine("<?xml version=\"1.0\" ?>");
                ss.WriteLine("<project name=\"{0}\" default=\"build\">", project.Name);
                ss.WriteLine("    <target name=\"{0}\">", "build");
                ss.WriteLine("        <echo message=\"Build Directory is ${project::get-base-directory()}/${build.dir}\" />");
                ss.WriteLine("        <mkdir dir=\"${project::get-base-directory()}/${build.dir}\" />");
                ss.WriteLine("        <copy todir=\"${project::get-base-directory()}/${build.dir}\">");
                ss.WriteLine("            <fileset basedir=\"${project::get-base-directory()}\">");
                foreach (ReferenceNode refr in project.References)
                {
                    if (refr.LocalCopy)
                    {
                        ss.WriteLine("                <include name=\"{0}", Helper.NormalizePath(Helper.MakePathRelativeTo(project.FullPath, BuildReference(solution, refr)) + "\" />", '/'));
                    }
                }
                ss.WriteLine("            </fileset>");
                ss.WriteLine("        </copy>");
                ss.Write("        <csc");
                ss.Write(" target=\"{0}\"", project.Type.ToString().ToLower());
                ss.Write(" debug=\"{0}\"", "${build.debug}");
                foreach (ConfigurationNode conf in project.Configurations)
                {
                    if (conf.Options.KeyFile != "")
                    {
                        ss.Write(" keyfile=\"{0}\"", conf.Options.KeyFile);
                        break;
                    }
                }
                foreach (ConfigurationNode conf in project.Configurations)
                {
                    ss.Write(" unsafe=\"{0}\"", conf.Options.AllowUnsafe);
                    break;
                }
                foreach (ConfigurationNode conf in project.Configurations)
                {
                    ss.Write(" define=\"{0}\"", conf.Options.CompilerDefines);
                    break;
                }
                foreach (ConfigurationNode conf in project.Configurations)
                {
                    if (GetXmlDocFile(project, conf) != "")
                    {
                        ss.Write(" doc=\"{0}\"", "${project::get-base-directory()}/${build.dir}/" + GetXmlDocFile(project, conf));
                        hasDoc = true;
                    }
                    break;
                }
                ss.Write(" output=\"{0}", "${project::get-base-directory()}/${build.dir}/${project::get-name()}");
                if (project.Type == ProjectType.Library)
                {
                    ss.Write(".dll\"");
                }
                else
                {
                    ss.Write(".exe\"");
                }
                if (project.AppIcon != null && project.AppIcon.Length != 0)
                {
                    ss.Write(" win32icon=\"{0}\"", Helper.NormalizePath(project.AppIcon, '/'));
                }
                ss.WriteLine(">");
                ss.WriteLine("            <resources prefix=\"{0}\" dynamicprefix=\"true\" >", project.RootNamespace);
                foreach (string file in project.Files)
                {
                    switch (project.Files.GetBuildAction(file))
                    {
                        case BuildAction.EmbeddedResource:
                            ss.WriteLine("                {0}", "<include name=\"" + Helper.NormalizePath(PrependPath(file), '/') + "\" />");
                            break;
                        default:
                            if (project.Files.GetSubType(file) != SubType.Code && project.Files.GetSubType(file) != SubType.Settings)
                            {
                                ss.WriteLine("                <include name=\"{0}\" />", file.Substring(0, file.LastIndexOf('.')) + ".resx");
                            }
                            break;
                    }
                }
                //if (project.Files.GetSubType(file).ToString() != "Code")
                //{
                //	ps.WriteLine("    <EmbeddedResource Include=\"{0}\">", file.Substring(0, file.LastIndexOf('.')) + ".resx");

                ss.WriteLine("            </resources>");
                ss.WriteLine("            <sources failonempty=\"true\">");
                foreach (string file in project.Files)
                {
                    switch (project.Files.GetBuildAction(file))
                    {
                        case BuildAction.Compile:
                            ss.WriteLine("                <include name=\"" + Helper.NormalizePath(PrependPath(file), '/') + "\" />");
                            break;
                        default:
                            break;
                    }
                }
                ss.WriteLine("            </sources>");
                ss.WriteLine("            <references basedir=\"${project::get-base-directory()}\">");
                ss.WriteLine("                <lib>");
                ss.WriteLine("                    <include name=\"${project::get-base-directory()}\" />");
                ss.WriteLine("                    <include name=\"${project::get-base-directory()}/${build.dir}\" />");
                ss.WriteLine("                </lib>");
                foreach (ReferenceNode refr in project.References)
                {
                    ss.WriteLine("                <include name=\"{0}", Helper.NormalizePath(Helper.MakePathRelativeTo(project.FullPath, BuildReference(solution, refr)) + "\" />", '/'));
                }
                ss.WriteLine("            </references>");

                ss.WriteLine("        </csc>");
                ss.WriteLine("    </target>");

                ss.WriteLine("    <target name=\"clean\">");
                ss.WriteLine("        <delete dir=\"${bin.dir}\" failonerror=\"false\" />");
                ss.WriteLine("        <delete dir=\"${obj.dir}\" failonerror=\"false\" />");
                ss.WriteLine("    </target>");

                ss.WriteLine("    <target name=\"doc\" description=\"Creates documentation.\">");
                if (hasDoc)
                {
                    ss.WriteLine("        <property name=\"doc.target\" value=\"\" />");
                    ss.WriteLine("        <if test=\"${platform::is-unix()}\">");
                    ss.WriteLine("            <property name=\"doc.target\" value=\"Web\" />");
                    ss.WriteLine("        </if>");
                    ss.WriteLine("        <ndoc failonerror=\"false\" verbose=\"true\">");
                    ss.WriteLine("            <assemblies basedir=\"${project::get-base-directory()}\">");
                    ss.Write("                <include name=\"${build.dir}/${project::get-name()}");
                    if (project.Type == ProjectType.Library)
                    {
                        ss.WriteLine(".dll\" />");
                    }
                    else
                    {
                        ss.WriteLine(".exe\" />");
                    }

                    ss.WriteLine("            </assemblies>");
                    ss.WriteLine("            <summaries basedir=\"${project::get-base-directory()}\">");
                    ss.WriteLine("                <include name=\"${build.dir}/${project::get-name()}.xml\"/>");
                    ss.WriteLine("            </summaries>");
                    ss.WriteLine("            <referencepaths basedir=\"${project::get-base-directory()}\">");
                    ss.WriteLine("                <include name=\"${build.dir}\" />");
                    //					foreach(ReferenceNode refr in project.References)
                    //					{
                    //						string path = Helper.NormalizePath(Helper.MakePathRelativeTo(project.FullPath, BuildReferencePath(solution, refr)), '/');
                    //						if (path != "")
                    //						{
                    //							ss.WriteLine("                <include name=\"{0}\" />", path);
                    //						}
                    //					}
                    ss.WriteLine("            </referencepaths>");
                    ss.WriteLine("            <documenters>");
                    ss.WriteLine("                <documenter name=\"MSDN\">");
                    ss.WriteLine("                    <property name=\"OutputDirectory\" value=\"${project::get-base-directory()}/${build.dir}/doc/${project::get-name()}\" />");
                    ss.WriteLine("                    <property name=\"OutputTarget\" value=\"${doc.target}\" />");
                    ss.WriteLine("                    <property name=\"HtmlHelpName\" value=\"${project::get-name()}\" />");
                    ss.WriteLine("                    <property name=\"IncludeFavorites\" value=\"False\" />");
                    ss.WriteLine("                    <property name=\"Title\" value=\"${project::get-name()} SDK Documentation\" />");
                    ss.WriteLine("                    <property name=\"SplitTOCs\" value=\"False\" />");
                    ss.WriteLine("                    <property name=\"DefaulTOC\" value=\"\" />");
                    ss.WriteLine("                    <property name=\"ShowVisualBasic\" value=\"True\" />");
                    ss.WriteLine("                    <property name=\"AutoDocumentConstructors\" value=\"True\" />");
                    ss.WriteLine("                    <property name=\"ShowMissingSummaries\" value=\"${build.debug}\" />");
                    ss.WriteLine("                    <property name=\"ShowMissingRemarks\" value=\"${build.debug}\" />");
                    ss.WriteLine("                    <property name=\"ShowMissingParams\" value=\"${build.debug}\" />");
                    ss.WriteLine("                    <property name=\"ShowMissingReturns\" value=\"${build.debug}\" />");
                    ss.WriteLine("                    <property name=\"ShowMissingValues\" value=\"${build.debug}\" />");
                    ss.WriteLine("                    <property name=\"DocumentInternals\" value=\"False\" />");
                    ss.WriteLine("                    <property name=\"DocumentPrivates\" value=\"False\" />");
                    ss.WriteLine("                    <property name=\"DocumentProtected\" value=\"True\" />");
                    ss.WriteLine("                    <property name=\"DocumentEmptyNamespaces\" value=\"${build.debug}\" />");
                    ss.WriteLine("                    <property name=\"IncludeAssemblyVersion\" value=\"True\" />");
                    ss.WriteLine("                </documenter>");
                    ss.WriteLine("            </documenters>");
                    ss.WriteLine("        </ndoc>");
                }
                ss.WriteLine("    </target>");
                ss.WriteLine("</project>");
            }
            m_Kernel.CurrentWorkingDirectory.Pop();
        }

        private void WriteCombine(SolutionNode solution)
        {
            m_Kernel.Log.Write("Creating Xcode build files");
            foreach (ProjectNode project in solution.Projects)
            {
                if (m_Kernel.AllowProject(project.FilterGroups))
                {
                    m_Kernel.Log.Write("...Creating project: {0}", project.Name);
                    WriteProject(solution, project);
                }
            }

            m_Kernel.Log.Write("");
            DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(solution.FullPath, solution.Name + ".xcodeproj"));
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }
            string combFile = Helper.MakeFilePath(Path.Combine(solution.FullPath, solution.Name + ".xcodeproj"), "project", "pbxproj");
            StreamWriter ss = new StreamWriter(combFile);

            m_Kernel.CurrentWorkingDirectory.Push();
            Helper.SetCurrentDir(Path.GetDirectoryName(combFile));

            using (ss)
            {
                ss.WriteLine("<?xml version=\"1.0\" ?>");
                ss.WriteLine("<project name=\"{0}\" default=\"build\">", solution.Name);
                ss.WriteLine("    <echo message=\"Using '${nant.settings.currentframework}' Framework\"/>");
                ss.WriteLine();

                //ss.WriteLine("    <property name=\"dist.dir\" value=\"dist\" />");
                //ss.WriteLine("    <property name=\"source.dir\" value=\"source\" />");
                ss.WriteLine("    <property name=\"bin.dir\" value=\"bin\" />");
                ss.WriteLine("    <property name=\"obj.dir\" value=\"obj\" />");
                ss.WriteLine("    <property name=\"doc.dir\" value=\"doc\" />");
                ss.WriteLine("    <property name=\"project.main.dir\" value=\"${project::get-base-directory()}\" />");

                foreach (ConfigurationNode conf in solution.Configurations)
                {
                    // Set the project.config to a non-debug configuration
                    if (conf.Options["DebugInformation"].ToString().ToLower() != "true")
                    {
                        ss.WriteLine("    <property name=\"project.config\" value=\"{0}\" />", conf.Name);
                    }
                    ss.WriteLine();
                    ss.WriteLine("    <target name=\"{0}\" description=\"\">", conf.Name);
                    ss.WriteLine("        <property name=\"project.config\" value=\"{0}\" />", conf.Name);
                    ss.WriteLine("        <property name=\"build.debug\" value=\"{0}\" />", conf.Options["DebugInformation"].ToString().ToLower());
                    ss.WriteLine("    </target>");
                    ss.WriteLine();
                }

                ss.WriteLine("    <target name=\"net-1.1\" description=\"Sets framework to .NET 1.1\">");
                ss.WriteLine("        <property name=\"nant.settings.currentframework\" value=\"net-1.1\" />");
                ss.WriteLine("    </target>");
                ss.WriteLine();

                ss.WriteLine("    <target name=\"net-2.0\" description=\"Sets framework to .NET 2.0\">");
                ss.WriteLine("        <property name=\"nant.settings.currentframework\" value=\"net-2.0\" />");
                ss.WriteLine("    </target>");
                ss.WriteLine();

                ss.WriteLine("    <target name=\"mono-2.0\" description=\"Sets framework to mono 2.0\">");
                ss.WriteLine("        <property name=\"nant.settings.currentframework\" value=\"mono-2.0\" />");
                ss.WriteLine("    </target>");
                ss.WriteLine();

                ss.WriteLine("    <target name=\"mono-1.0\" description=\"Sets framework to mono 1.0\">");
                ss.WriteLine("        <property name=\"nant.settings.currentframework\" value=\"mono-1.0\" />");
                ss.WriteLine("    </target>");
                ss.WriteLine();

                ss.WriteLine("    <target name=\"init\" description=\"\">");
                ss.WriteLine("        <call target=\"${project.config}\" />");
                ss.WriteLine("        <sysinfo />");
                ss.WriteLine("        <echo message=\"Platform ${sys.os.platform}\" />");
                ss.WriteLine("        <property name=\"build.dir\" value=\"${bin.dir}/${project.config}\" />");
                ss.WriteLine("    </target>");
                ss.WriteLine();

                ss.WriteLine("    <target name=\"clean\" description=\"\">");
                ss.WriteLine("        <echo message=\"Deleting all builds from all configurations\" />");
                //ss.WriteLine("        <delete dir=\"${dist.dir}\" failonerror=\"false\" />");
                ss.WriteLine("        <delete dir=\"${bin.dir}\" failonerror=\"false\" />");
                ss.WriteLine("        <delete dir=\"${obj.dir}\" failonerror=\"false\" />");
                //foreach(ProjectNode project in solution.Projects)
                //{
                //    string path = Helper.MakePathRelativeTo(solution.FullPath, project.FullPath);
                //    ss.Write("        <nant buildfile=\"{0}\"",
                //        Helper.NormalizePath(Helper.MakeFilePath(path, project.Name + (project.Type == ProjectType.Library ? ".dll" : ".exe"), "build"),'/'));
                //    ss.WriteLine(" target=\"clean\" />");
                //}
                ss.WriteLine("    </target>");
                ss.WriteLine();

                ss.WriteLine("    <target name=\"build\" depends=\"init\" description=\"\">");

                foreach (ProjectNode project in solution.ProjectsTableOrder)
                {
                    string path = Helper.MakePathRelativeTo(solution.FullPath, project.FullPath);
                    ss.Write("        <nant buildfile=\"{0}\"",
                        Helper.NormalizePath(Helper.MakeFilePath(path, project.Name + (project.Type == ProjectType.Library ? ".dll" : ".exe"), "build"), '/'));
                    ss.WriteLine(" target=\"build\" />");
                }
                ss.WriteLine("    </target>");
                ss.WriteLine();

                ss.WriteLine("    <target name=\"build-release\" depends=\"Release, init, build\" description=\"Builds in Release mode\" />");
                ss.WriteLine();
                ss.WriteLine("    <target name=\"build-debug\" depends=\"Debug, init, build\" description=\"Builds in Debug mode\" />");
                ss.WriteLine();
                //ss.WriteLine("    <target name=\"package\" depends=\"clean, doc, copyfiles, zip\" description=\"Builds in Release mode\" />");
                ss.WriteLine("    <target name=\"package\" depends=\"clean, doc\" description=\"Builds all\" />");
                ss.WriteLine();

                ss.WriteLine("    <target name=\"doc\" depends=\"build-release\">");
                ss.WriteLine("        <echo message=\"Generating all documentation from all builds\" />");
                foreach (ProjectNode project in solution.Projects)
                {
                    string path = Helper.MakePathRelativeTo(solution.FullPath, project.FullPath);
                    ss.Write("        <nant buildfile=\"{0}\"",
                        Helper.NormalizePath(Helper.MakeFilePath(path, project.Name + (project.Type == ProjectType.Library ? ".dll" : ".exe"), "build"), '/'));
                    ss.WriteLine(" target=\"doc\" />");
                }
                ss.WriteLine("    </target>");
                ss.WriteLine();
                ss.WriteLine("</project>");
            }

            m_Kernel.CurrentWorkingDirectory.Pop();
        }

        private void CleanProject(ProjectNode project)
        {
            m_Kernel.Log.Write("...Cleaning project: {0}", project.Name);
            string projectFile = Helper.MakeFilePath(project.FullPath, project.Name + (project.Type == ProjectType.Library ? ".dll" : ".exe"), "build");
            Helper.DeleteIfExists(projectFile);
        }

        private void CleanSolution(SolutionNode solution)
        {
            m_Kernel.Log.Write("Cleaning Xcode build files for", solution.Name);

            string slnFile = Helper.MakeFilePath(solution.FullPath, solution.Name, "build");
            Helper.DeleteIfExists(slnFile);

            foreach (ProjectNode project in solution.Projects)
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
            if (kern == null)
            {
                throw new ArgumentNullException("kern");
            }
            m_Kernel = kern;
            foreach (SolutionNode solution in kern.Solutions)
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
            if (kern == null)
            {
                throw new ArgumentNullException("kern");
            }
            m_Kernel = kern;
            foreach (SolutionNode sol in kern.Solutions)
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
                return "xcode";
            }
        }

        #endregion
    }
}
