#region BSD License
/*
Copyright (c) 2004 Crestez Leonard (cleonard@go.ro)

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
    [Target("makefile")]
    public class MakefileTarget : ITarget
    {
        #region Fields

        private Kernel m_Kernel = null;

        #endregion

        #region Private Methods

        // This converts a path relative to the path of a project to
        // a path relative to the solution path.
        private string NicePath(ProjectNode proj, string path)
        {
            string res;
            SolutionNode solution = (SolutionNode)proj.Parent;
            res = Path.Combine(Helper.NormalizePath(proj.FullPath, '/'), Helper.NormalizePath(path, '/'));
            res = Helper.NormalizePath(res, '/');
            res = res.Replace("/./", "/");
            while (res.IndexOf("/../") >= 0)
            {
                int a = res.IndexOf("/../");
                int b = res.LastIndexOf("/", a - 1);
                res = res.Remove(b, a - b + 3);
            }
            res = Helper.MakePathRelativeTo(solution.FullPath, res);
            if (res.StartsWith("./"))
                res = res.Substring(2, res.Length - 2);
            res = Helper.NormalizePath(res, '/');
            return res;
        }

        private void WriteProjectFiles(StreamWriter f, SolutionNode solution, ProjectNode project)
        {
            // Write list of source code files
            f.WriteLine("SOURCES_{0} = \\", project.Name);
            foreach (string file in project.Files)
                if (project.Files.GetBuildAction(file) == BuildAction.Compile)
                    f.WriteLine("\t{0} \\", NicePath(project, file));
            f.WriteLine();

            // Write list of resource files
            f.WriteLine("RESOURCES_{0} = \\", project.Name);
            foreach (string file in project.Files)
                if (project.Files.GetBuildAction(file) == BuildAction.EmbeddedResource)
                {
                    string path = NicePath(project, file);
                    f.WriteLine("\t-resource:{0},{1} \\", path, Path.GetFileName(path));
                }
            f.WriteLine();

            // There's also Content and None in BuildAction.
            // What am I supposed to do with that?
        }

        private string FindFileReference(string refName, ProjectNode project)
        {
            foreach (ReferencePathNode refPath in project.ReferencePaths)
            {
                string fullPath = NicePath(project, Helper.MakeFilePath(refPath.Path, refName, "dll"));
                if (File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }

        private void WriteProjectReferences(StreamWriter f, SolutionNode solution, ProjectNode project)
        {
            f.WriteLine("REFERENCES_{0} = \\", project.Name);
            foreach (ReferenceNode refr in project.References)
            {
                string path;
                // Project references change with configurations.
                if (solution.ProjectsTable.ContainsKey(refr.Name))
                    continue;
                path = FindFileReference(refr.Name, project);
                if (path != null)
                    f.WriteLine("\t-r:{0} \\", path);
                else
                    f.WriteLine("\t-r:{0} \\", refr.Name);
            }
            f.WriteLine();
        }

        private void WriteProjectDependencies(StreamWriter f, SolutionNode solution, ProjectNode project)
        {
            f.WriteLine("DEPENDENCIES_{0} = \\", project.Name);
            f.WriteLine("\t$(SOURCES_{0}) \\", project.Name);
            foreach (string file in project.Files)
                if (project.Files.GetBuildAction(file) == BuildAction.EmbeddedResource)
                    f.WriteLine("\t{0} \\", NicePath(project, file));
            f.WriteLine();
        }

        private string ProjectTypeToExtension(ProjectType t)
        {
            if (t == ProjectType.Exe || t == ProjectType.WinExe)
            {
                return "exe";
            }
            else if (t == ProjectType.Library)
            {
                return "dll";
            }
            else
            {
                throw new FatalException("Bad ProjectType: {0}", t);
            }
        }

        private string ProjectTypeToTarget(ProjectType t)
        {
            if (t == ProjectType.Exe)
            {
                return "exe";
            }
            else if (t == ProjectType.WinExe)
            {
                return "winexe";
            }
            else if (t == ProjectType.Library)
            {
                return "library";
            }
            else
            {
                throw new FatalException("Bad ProjectType: {0}", t);
            }
        }

        private string ProjectOutput(ProjectNode project, ConfigurationNode config)
        {
            string filepath;
            filepath = Helper.MakeFilePath((string)config.Options["OutputPath"],
                    project.AssemblyName, ProjectTypeToExtension(project.Type));
            return NicePath(project, filepath);
        }

        // Returns true if two configs in one project have the same output.
        private bool ProjectClashes(ProjectNode project)
        {
            foreach (ConfigurationNode conf1 in project.Configurations)
                foreach (ConfigurationNode conf2 in project.Configurations)
                    if (ProjectOutput(project, conf1) == ProjectOutput(project, conf2) && conf1 != conf2)
                    {
                        m_Kernel.Log.Write("Warning: Configurations {0} and {1} for project {2} output the same file",
                                conf1.Name, conf2.Name, project.Name);
                        m_Kernel.Log.Write("Warning: I'm going to use some timestamps(extra empty files).");
                        return true;
                    }
            return false;
        }

        private void WriteProject(StreamWriter f, SolutionNode solution, ProjectNode project)
        {
            f.WriteLine("# This is for project {0}", project.Name);
            f.WriteLine();

            WriteProjectFiles(f, solution, project);
            WriteProjectReferences(f, solution, project);
            WriteProjectDependencies(f, solution, project);

            bool clash = ProjectClashes(project);

            foreach (ConfigurationNode conf in project.Configurations)
            {
                string outpath = ProjectOutput(project, conf);
                string filesToClean = outpath;

                if (clash)
                {
                    f.WriteLine("{0}-{1}: .{0}-{1}-timestamp", project.Name, conf.Name);
                    f.WriteLine();
                    f.Write(".{0}-{1}-timestamp: $(DEPENDENCIES_{0})", project.Name, conf.Name);
                }
                else
                {
                    f.WriteLine("{0}-{1}: {2}", project.Name, conf.Name, outpath);
                    f.WriteLine();
                    f.Write("{2}: $(DEPENDENCIES_{0})", project.Name, conf.Name, outpath);
                }
                // Dependencies on other projects.
                foreach (ReferenceNode refr in project.References)
                    if (solution.ProjectsTable.ContainsKey(refr.Name))
                    {
                        ProjectNode refProj = (ProjectNode)solution.ProjectsTable[refr.Name];
                        if (ProjectClashes(refProj))
                            f.Write(" .{0}-{1}-timestamp", refProj.Name, conf.Name);
                        else
                            f.Write(" {0}", ProjectOutput(refProj, conf));
                    }
                f.WriteLine();

                // make directory for output.
                if (Path.GetDirectoryName(outpath) != "")
                {
                    f.WriteLine("\tmkdir -p {0}", Path.GetDirectoryName(outpath));
                }
                // mcs command line.
                f.Write("\tgmcs", project.Name);
                f.Write(" -warn:{0}", conf.Options["WarningLevel"]);
                if ((bool)conf.Options["DebugInformation"])
                    f.Write(" -debug");
                if ((bool)conf.Options["AllowUnsafe"])
                    f.Write(" -unsafe");
                if ((bool)conf.Options["CheckUnderflowOverflow"])
                    f.Write(" -checked");
                if (project.StartupObject != "")
                    f.Write(" -main:{0}", project.StartupObject);
                if ((string)conf.Options["CompilerDefines"] != "")
                {
                    f.Write(" -define:\"{0}\"", conf.Options["CompilerDefines"]);
                }

                f.Write(" -target:{0} -out:{1}", ProjectTypeToTarget(project.Type), outpath);

                // Build references to other projects. Now that sux.
                // We have to reference the other project in the same conf.
                foreach (ReferenceNode refr in project.References)
                    if (solution.ProjectsTable.ContainsKey(refr.Name))
                    {
                        ProjectNode refProj;
                        refProj = (ProjectNode)solution.ProjectsTable[refr.Name];
                        f.Write(" -r:{0}", ProjectOutput(refProj, conf));
                    }

                f.Write(" $(REFERENCES_{0})", project.Name);
                f.Write(" $(RESOURCES_{0})", project.Name);
                f.Write(" $(SOURCES_{0})", project.Name);
                f.WriteLine();

                // Copy references with localcopy.
                foreach (ReferenceNode refr in project.References)
                    if (refr.LocalCopy)
                    {
                        string outPath, srcPath, destPath;
                        outPath = Helper.NormalizePath((string)conf.Options["OutputPath"]);
                        if (solution.ProjectsTable.ContainsKey(refr.Name))
                        {
                            ProjectNode refProj;
                            refProj = (ProjectNode)solution.ProjectsTable[refr.Name];
                            srcPath = ProjectOutput(refProj, conf);
                            destPath = Path.Combine(outPath, Path.GetFileName(srcPath));
                            destPath = NicePath(project, destPath);
                            if (srcPath != destPath)
                            {
                                f.WriteLine("\tcp -f {0} {1}", srcPath, destPath);
                                filesToClean += " " + destPath;
                            }
                            continue;
                        }
                        srcPath = FindFileReference(refr.Name, project);
                        if (srcPath != null)
                        {
                            destPath = Path.Combine(outPath, Path.GetFileName(srcPath));
                            destPath = NicePath(project, destPath);
                            f.WriteLine("\tcp -f {0} {1}", srcPath, destPath);
                            filesToClean += " " + destPath;
                        }
                    }

                if (clash)
                {
                    filesToClean += String.Format(" .{0}-{1}-timestamp", project.Name, conf.Name);
                    f.WriteLine("\ttouch .{0}-{1}-timestamp", project.Name, conf.Name);
                    f.Write("\trm -rf");
                    foreach (ConfigurationNode otherConf in project.Configurations)
                        if (otherConf != conf)
                            f.WriteLine(" .{0}-{1}-timestamp", project.Name, otherConf.Name);
                    f.WriteLine();
                }
                f.WriteLine();
                f.WriteLine("{0}-{1}-clean:", project.Name, conf.Name);
                f.WriteLine("\trm -rf {0}", filesToClean);
                f.WriteLine();
            }
        }

        private void WriteIntro(StreamWriter f, SolutionNode solution)
        {
            f.WriteLine("# Makefile for {0} generated by Prebuild ( http://dnpb.sf.net )", solution.Name);
            f.WriteLine("# Do not edit.");
            f.WriteLine("#");

            f.Write("# Configurations:");
            foreach (ConfigurationNode conf in solution.Configurations)
                f.Write(" {0}", conf.Name);
            f.WriteLine();

            f.WriteLine("# Projects:");
            foreach (ProjectNode proj in solution.Projects)
                f.WriteLine("#\t{0}", proj.Name);

            f.WriteLine("#");
            f.WriteLine("# Building:");
            f.WriteLine("#\t\"make\" to build everything under the default(first) configuration");
            f.WriteLine("#\t\"make CONF\" to build every project under configuration CONF");
            f.WriteLine("#\t\"make PROJ\" to build project PROJ under the default(first) configuration");
            f.WriteLine("#\t\"make PROJ-CONF\" to build project PROJ under configuration CONF");
            f.WriteLine("#");
            f.WriteLine("# Cleaning (removing results of build):");
            f.WriteLine("#\t\"make clean\" to clean everything, that's what you probably want");
            f.WriteLine("#\t\"make CONF\" to clean everything for a configuration");
            f.WriteLine("#\t\"make PROJ\" to clean everything for a project");
            f.WriteLine("#\t\"make PROJ-CONF\" to clea project PROJ under configuration CONF");
            f.WriteLine();
        }

        private void WritePhony(StreamWriter f, SolutionNode solution)
        {
            string defconf = "";
            foreach (ConfigurationNode conf in solution.Configurations)
            {
                defconf = conf.Name;
                break;
            }

            f.Write(".PHONY: all");
            foreach (ProjectNode proj in solution.Projects)
                f.Write(" {0} {0}-clean", proj.Name);
            foreach (ConfigurationNode conf in solution.Configurations)
                f.Write(" {0} {0}-clean", conf.Name);
            foreach (ProjectNode proj in solution.Projects)
                foreach (ConfigurationNode conf in solution.Configurations)
                    f.Write(" {0}-{1} {0}-{1}-clean", proj.Name, conf.Name);
            f.WriteLine();
            f.WriteLine();

            f.WriteLine("all: {0}", defconf);
            f.WriteLine();

            f.Write("clean:");
            foreach (ConfigurationNode conf in solution.Configurations)
                f.Write(" {0}-clean", conf.Name);
            f.WriteLine();
            f.WriteLine();

            foreach (ConfigurationNode conf in solution.Configurations)
            {
                f.Write("{0}: ", conf.Name);
                foreach (ProjectNode proj in solution.Projects)
                    f.Write(" {0}-{1}", proj.Name, conf.Name);
                f.WriteLine();
                f.WriteLine();

                f.Write("{0}-clean: ", conf.Name);
                foreach (ProjectNode proj in solution.Projects)
                    f.Write(" {0}-{1}-clean", proj.Name, conf.Name);
                f.WriteLine();
                f.WriteLine();
            }

            foreach (ProjectNode proj in solution.Projects)
            {
                f.WriteLine("{0}: {0}-{1}", proj.Name, defconf);
                f.WriteLine();

                f.Write("{0}-clean:", proj.Name);
                foreach (ConfigurationNode conf in proj.Configurations)
                    f.Write(" {0}-{1}-clean", proj.Name, conf.Name);
                f.WriteLine();
                f.WriteLine();
            }
        }

        private void WriteSolution(SolutionNode solution)
        {
            m_Kernel.Log.Write("Creating makefile for {0}", solution.Name);
            m_Kernel.CurrentWorkingDirectory.Push();

            string file = "Makefile";// Helper.MakeFilePath(solution.FullPath, solution.Name, "make");
            StreamWriter f = new StreamWriter(file);

            Helper.SetCurrentDir(Path.GetDirectoryName(file));

            using (f)
            {
                WriteIntro(f, solution);
                WritePhony(f, solution);

                foreach (ProjectNode project in solution.Projects)
                {
                    m_Kernel.Log.Write("...Creating Project: {0}", project.Name);
                    WriteProject(f, solution, project);
                }
            }

            m_Kernel.Log.Write("");
            m_Kernel.CurrentWorkingDirectory.Pop();
        }

        private void CleanSolution(SolutionNode solution)
        {
            m_Kernel.Log.Write("Cleaning makefile for {0}", solution.Name);

            string file = Helper.MakeFilePath(solution.FullPath, solution.Name, "make");
            Helper.DeleteIfExists(file);

            m_Kernel.Log.Write("");
        }

        #endregion

        #region ITarget Members

        public void Write(Kernel kern)
        {
            m_Kernel = kern;
            foreach (SolutionNode solution in kern.Solutions)
                WriteSolution(solution);
            m_Kernel = null;
        }

        public virtual void Clean(Kernel kern)
        {
            m_Kernel = kern;
            foreach (SolutionNode sol in kern.Solutions)
                CleanSolution(sol);
            m_Kernel = null;
        }

        public string Name
        {
            get
            {
                return "makefile";
            }
        }

        #endregion
    }
}
