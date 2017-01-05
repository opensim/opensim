#region BSD License
/*

Copyright (c) 2004 - 2008
Matthew Holmes        (matthew@wildfiregames.com),
Dan     Moorehead     (dan05a@gmail.com),
Dave    Hudson        (jendave@yahoo.com),
C.J.    Adams-Collier (cjac@colliertech.org),

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

* Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.

* The name of the author may not be used to endorse or promote
products derived from this software without specific prior written
permission.

THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT,
INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING
IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.

*/
#endregion

#region MIT X11 license

/*
 Portions of this file authored by Lluis Sanchez Gual

 Copyright (C) 2006 Novell, Inc (http://www.novell.com)

 Permission is hereby granted, free of charge, to any person obtaining
 a copy of this software and associated documentation files (the
 "Software"), to deal in the Software without restriction, including
 without limitation the rights to use, copy, modify, merge, publish,
 distribute, sublicense, and/or sell copies of the Software, and to
 permit persons to whom the Software is furnished to do so, subject to
 the following conditions:

 The above copyright notice and this permission notice shall be
 included in all copies or substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
 LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
 OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
 WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

#endregion
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Xsl;
using System.Net;
using System.Diagnostics;

using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using Prebuild.Core.Nodes;
using Prebuild.Core.Utilities;

namespace Prebuild.Core.Targets
{
    public enum ClrVersion
    {
        Default,
        Net_1_1,
        Net_2_0
    }

    public class SystemPackage
    {
        string name;
        string version;
        string description;
        string[] assemblies;
        bool isInternal;
        ClrVersion targetVersion;

        public void Initialize(string name,
                               string version,
                               string description,
                               string[] assemblies,
                               ClrVersion targetVersion,
                               bool isInternal)
        {
            this.isInternal = isInternal;
            this.name = name;
            this.version = version;
            this.assemblies = assemblies;
            this.description = description;
            this.targetVersion = targetVersion;
        }

        public string Name
        {
            get { return name; }
        }

        public string Version
        {
            get { return version; }
        }

        public string Description
        {
            get { return description; }
        }

        public ClrVersion TargetVersion
        {
            get { return targetVersion; }
        }

        // The package is part of the mono SDK
        public bool IsCorePackage
        {
            get { return name == "mono"; }
        }

        // The package has been registered by an add-in, and is not installed
        // in the system.
        public bool IsInternalPackage
        {
            get { return isInternal; }
        }

        public string[] Assemblies
        {
            get { return assemblies; }
        }

    }


    /// <summary>
    ///
    /// </summary>
    [Target("autotools")]
    public class AutotoolsTarget : ITarget
    {
        #region Fields

        Kernel m_Kernel;
        XmlDocument autotoolsDoc;
        XmlUrlResolver xr;
        System.Security.Policy.Evidence e;
        readonly Dictionary<string, SystemPackage> assemblyPathToPackage = new Dictionary<string, SystemPackage>();
        readonly Dictionary<string, string> assemblyFullNameToPath = new Dictionary<string, string>();
        readonly Dictionary<string, SystemPackage> packagesHash = new Dictionary<string, SystemPackage>();
        readonly List<SystemPackage> packages = new List<SystemPackage>();

        #endregion

        #region Private Methods

        private static void mkdirDashP(string dirName)
        {
            DirectoryInfo di = new DirectoryInfo(dirName);
            if (di.Exists)
                return;

            string parentDirName = System.IO.Path.GetDirectoryName(dirName);
            DirectoryInfo parentDi = new DirectoryInfo(parentDirName);
            if (!parentDi.Exists)
                mkdirDashP(parentDirName);

            di.Create();
        }

        private static void chkMkDir(string dirName)
        {
            System.IO.DirectoryInfo di =
                new System.IO.DirectoryInfo(dirName);

            if (!di.Exists)
                di.Create();
        }

        private void transformToFile(string filename, XsltArgumentList argList, string nodeName)
        {
            // Create an XslTransform for this file
            XslTransform templateTransformer =
                new XslTransform();

            // Load up the template
            XmlNode templateNode =
                autotoolsDoc.SelectSingleNode(nodeName + "/*");
            templateTransformer.Load(templateNode.CreateNavigator(), xr, e);

            // Create a writer for the transformed template
            XmlTextWriter templateWriter =
                new XmlTextWriter(filename, null);

            // Perform transformation, writing the file
            templateTransformer.Transform
                (m_Kernel.CurrentDoc, argList, templateWriter, xr);
        }

        static string NormalizeAsmName(string name)
        {
            int i = name.IndexOf(", PublicKeyToken=null");
            if (i != -1)
                return name.Substring(0, i).Trim();
            return name;
        }

        private void AddAssembly(string assemblyfile, SystemPackage package)
        {
            if (!File.Exists(assemblyfile))
                return;

            try
            {
                System.Reflection.AssemblyName an = System.Reflection.AssemblyName.GetAssemblyName(assemblyfile);
                assemblyFullNameToPath[NormalizeAsmName(an.FullName)] = assemblyfile;
                assemblyPathToPackage[assemblyfile] = package;
            }
            catch
            {
            }
        }

        private static List<string> GetAssembliesWithLibInfo(string line, string file)
        {
            List<string> references = new List<string>();
            List<string> libdirs = new List<string>();
            List<string> retval = new List<string>();
            foreach (string piece in line.Split(' '))
            {
                if (piece.ToLower().Trim().StartsWith("/r:") || piece.ToLower().Trim().StartsWith("-r:"))
                {
                    references.Add(ProcessPiece(piece.Substring(3).Trim(), file));
                }
                else if (piece.ToLower().Trim().StartsWith("/lib:") || piece.ToLower().Trim().StartsWith("-lib:"))
                {
                    libdirs.Add(ProcessPiece(piece.Substring(5).Trim(), file));
                }
            }

            foreach (string refrnc in references)
            {
                foreach (string libdir in libdirs)
                {
                    if (File.Exists(libdir + Path.DirectorySeparatorChar + refrnc))
                    {
                        retval.Add(libdir + Path.DirectorySeparatorChar + refrnc);
                    }
                }
            }

            return retval;
        }

        private static List<string> GetAssembliesWithoutLibInfo(string line, string file)
        {
            List<string> references = new List<string>();
            foreach (string reference in line.Split(' '))
            {
                if (reference.ToLower().Trim().StartsWith("/r:") || reference.ToLower().Trim().StartsWith("-r:"))
                {
                    string final_ref = reference.Substring(3).Trim();
                    references.Add(ProcessPiece(final_ref, file));
                }
            }
            return references;
        }

        private static string ProcessPiece(string piece, string pcfile)
        {
            int start = piece.IndexOf("${");
            if (start == -1)
                return piece;

            int end = piece.IndexOf("}");
            if (end == -1)
                return piece;

            string variable = piece.Substring(start + 2, end - start - 2);
            string interp = GetVariableFromPkgConfig(variable, Path.GetFileNameWithoutExtension(pcfile));
            return ProcessPiece(piece.Replace("${" + variable + "}", interp), pcfile);
        }

        private static string GetVariableFromPkgConfig(string var, string pcfile)
        {
            ProcessStartInfo psi = new ProcessStartInfo("pkg-config");
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.Arguments = String.Format("--variable={0} {1}", var, pcfile);
            Process p = new Process();
            p.StartInfo = psi;
            p.Start();
            string ret = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            if (String.IsNullOrEmpty(ret))
                return String.Empty;
            return ret;
        }

        private void ParsePCFile(string pcfile)
        {
            // Don't register the package twice
            string pname = Path.GetFileNameWithoutExtension(pcfile);
            if (packagesHash.ContainsKey(pname))
                return;

            List<string> fullassemblies = null;
            string version = "";
            string desc = "";

            SystemPackage package = new SystemPackage();

            using (StreamReader reader = new StreamReader(pcfile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string lowerLine = line.ToLower();
                    if (lowerLine.StartsWith("libs:") && lowerLine.IndexOf(".dll") != -1)
                    {
                        string choppedLine = line.Substring(5).Trim();
                        if (choppedLine.IndexOf("-lib:") != -1 || choppedLine.IndexOf("/lib:") != -1)
                        {
                            fullassemblies = GetAssembliesWithLibInfo(choppedLine, pcfile);
                        }
                        else
                        {
                            fullassemblies = GetAssembliesWithoutLibInfo(choppedLine, pcfile);
                        }
                    }
                    else if (lowerLine.StartsWith("version:"))
                    {
                        // "version:".Length == 8
                        version = line.Substring(8).Trim();
                    }
                    else if (lowerLine.StartsWith("description:"))
                    {
                        // "description:".Length == 12
                        desc = line.Substring(12).Trim();
                    }
                }
            }

            if (fullassemblies == null)
                return;

            foreach (string assembly in fullassemblies)
            {
                AddAssembly(assembly, package);
            }

            package.Initialize(pname,
                               version,
                               desc,
                               fullassemblies.ToArray(),
                               ClrVersion.Default,
                               false);
            packages.Add(package);
            packagesHash[pname] = package;
        }

        void RegisterSystemAssemblies(string prefix, string version, ClrVersion ver)
        {
            SystemPackage package = new SystemPackage();
            List<string> list = new List<string>();

            string dir = Path.Combine(prefix, version);
            if (!Directory.Exists(dir))
            {
                return;
            }

            foreach (string assembly in Directory.GetFiles(dir, "*.dll"))
            {
                AddAssembly(assembly, package);
                list.Add(assembly);
            }

            package.Initialize("mono",
                               version,
                               "The Mono runtime",
                               list.ToArray(),
                               ver,
                               false);
            packages.Add(package);
        }

        void RunInitialization()
        {
            string versionDir;

            if (Environment.Version.Major == 1)
            {
                versionDir = "1.0";
            }
            else
            {
                versionDir = "2.0";
            }

            //Pull up assemblies from the installed mono system.
            string prefix = Path.GetDirectoryName(typeof(int).Assembly.Location);

            if (prefix.IndexOf(Path.Combine("mono", versionDir)) == -1)
                prefix = Path.Combine(prefix, "mono");
            else
                prefix = Path.GetDirectoryName(prefix);

            RegisterSystemAssemblies(prefix, "1.0", ClrVersion.Net_1_1);
            RegisterSystemAssemblies(prefix, "2.0", ClrVersion.Net_2_0);

            string search_dirs = Environment.GetEnvironmentVariable("PKG_CONFIG_PATH");
            string libpath = Environment.GetEnvironmentVariable("PKG_CONFIG_LIBPATH");

            if (String.IsNullOrEmpty(libpath))
            {
                string path_dirs = Environment.GetEnvironmentVariable("PATH");
                foreach (string pathdir in path_dirs.Split(Path.PathSeparator))
                {
                    if (pathdir == null)
                        continue;
                    if (File.Exists(pathdir + Path.DirectorySeparatorChar + "pkg-config"))
                    {
                        libpath = Path.Combine(pathdir, "..");
                        libpath = Path.Combine(libpath, "lib");
                        libpath = Path.Combine(libpath, "pkgconfig");
                        break;
                    }
                }
            }
            search_dirs += Path.PathSeparator + libpath;
            if (!string.IsNullOrEmpty(search_dirs))
            {
                List<string> scanDirs = new List<string>();
                foreach (string potentialDir in search_dirs.Split(Path.PathSeparator))
                {
                    if (!scanDirs.Contains(potentialDir))
                        scanDirs.Add(potentialDir);
                }
                foreach (string pcdir in scanDirs)
                {
                    if (pcdir == null)
                        continue;

                    if (Directory.Exists(pcdir))
                    {
                        foreach (string pcfile in Directory.GetFiles(pcdir, "*.pc"))
                        {
                            ParsePCFile(pcfile);
                        }
                    }
                }
            }
        }

        private void WriteCombine(SolutionNode solution)
        {
            #region "Create Solution directory if it doesn't exist"
            string solutionDir = Path.Combine(solution.FullPath,
                                              Path.Combine("autotools",
                                                           solution.Name));
            chkMkDir(solutionDir);
            #endregion

            #region "Write Solution-level files"
            XsltArgumentList argList = new XsltArgumentList();
            argList.AddParam("solutionName", "", solution.Name);
            // $solutionDir is $rootDir/$solutionName/
            transformToFile(Path.Combine(solutionDir, "configure.ac"),
                            argList, "/Autotools/SolutionConfigureAc");
            transformToFile(Path.Combine(solutionDir, "Makefile.am"),
                            argList, "/Autotools/SolutionMakefileAm");
            transformToFile(Path.Combine(solutionDir, "autogen.sh"),
                            argList, "/Autotools/SolutionAutogenSh");
            #endregion

            foreach (ProjectNode project in solution.ProjectsTableOrder)
            {
              m_Kernel.Log.Write(String.Format("Writing project: {0}",
                                               project.Name));
              WriteProject(solution, project);
            }
        }

        private static string FindFileReference(string refName,
                                                ProjectNode project)
        {
            foreach (ReferencePathNode refPath in project.ReferencePaths)
            {
              string fullPath =
                Helper.MakeFilePath(refPath.Path, refName, "dll");

              if (File.Exists(fullPath)) {
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

        /// <summary>
        /// Normalizes the path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public static string NormalizePath(string path)
        {
            if (path == null)
            {
                return "";
            }

            StringBuilder tmpPath;

            if (Core.Parse.Preprocessor.GetOS() == "Win32")
            {
                tmpPath = new StringBuilder(path.Replace('\\', '/'));
                tmpPath.Replace("/", @"\\");
            }
            else
            {
                tmpPath = new StringBuilder(path.Replace('\\', '/'));
                tmpPath = tmpPath.Replace('/', Path.DirectorySeparatorChar);
            }
            return tmpPath.ToString();
        }

        private void WriteProject(SolutionNode solution, ProjectNode project)
        {
            string solutionDir = Path.Combine(solution.FullPath, Path.Combine("autotools", solution.Name));
            string projectDir = Path.Combine(solutionDir, project.Name);
            string projectVersion = project.Version;
            bool hasAssemblyConfig = false;
            chkMkDir(projectDir);

            List<string>
                compiledFiles = new List<string>(),
                contentFiles = new List<string>(),
                embeddedFiles = new List<string>(),

                binaryLibs = new List<string>(),
                pkgLibs = new List<string>(),
                systemLibs = new List<string>(),
                runtimeLibs = new List<string>(),

                extraDistFiles = new List<string>(),
                localCopyTargets = new List<string>();

            // If there exists a .config file for this assembly, copy
            // it to the project folder

            // TODO: Support copying .config.osx files
            // TODO: support processing the .config file for native library deps
            string projectAssemblyName = project.Name;
            if (project.AssemblyName != null)
                projectAssemblyName = project.AssemblyName;

            if (File.Exists(Path.Combine(project.FullPath, projectAssemblyName) + ".dll.config"))
            {
                hasAssemblyConfig = true;
                System.IO.File.Copy(Path.Combine(project.FullPath, projectAssemblyName + ".dll.config"), Path.Combine(projectDir, projectAssemblyName + ".dll.config"), true);
                extraDistFiles.Add(project.AssemblyName + ".dll.config");
            }

            foreach (ConfigurationNode conf in project.Configurations)
            {
                if (conf.Options.KeyFile != string.Empty)
                {
                    // Copy snk file into the project's directory
                    // Use the snk from the project directory directly
                    string source = Path.Combine(project.FullPath, conf.Options.KeyFile);
                    string keyFile = conf.Options.KeyFile;
                    Regex re = new Regex(".*/");
                    keyFile = re.Replace(keyFile, "");

                    string dest = Path.Combine(projectDir, keyFile);
                    // Tell the user if there's a problem copying the file
                    try
                    {
                        mkdirDashP(System.IO.Path.GetDirectoryName(dest));
                        System.IO.File.Copy(source, dest, true);
                    }
                    catch (System.IO.IOException e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }

            // Copy compiled, embedded and content files into the project's directory
            foreach (string filename in project.Files)
            {
                string source = Path.Combine(project.FullPath, filename);
                string dest = Path.Combine(projectDir, filename);

                if (filename.Contains("AssemblyInfo.cs"))
                {
                    // If we've got an AssemblyInfo.cs, pull the version number from it
                    string[] sources = { source };
                    string[] args = { "" };
                    Microsoft.CSharp.CSharpCodeProvider cscp =
                        new Microsoft.CSharp.CSharpCodeProvider();

                    string tempAssemblyFile = Path.Combine(Path.GetTempPath(), project.Name + "-temp.dll");
                    System.CodeDom.Compiler.CompilerParameters cparam =
                        new System.CodeDom.Compiler.CompilerParameters(args, tempAssemblyFile);

                    System.CodeDom.Compiler.CompilerResults cr =
                        cscp.CompileAssemblyFromFile(cparam, sources);

                    foreach (System.CodeDom.Compiler.CompilerError error in cr.Errors)
                        Console.WriteLine("Error! '{0}'", error.ErrorText);

                    try {
                      string projectFullName = cr.CompiledAssembly.FullName;
                      Regex verRegex = new Regex("Version=([\\d\\.]+)");
                      Match verMatch = verRegex.Match(projectFullName);
                      if (verMatch.Success)
                        projectVersion = verMatch.Groups[1].Value;
                    }catch{
                      Console.WriteLine("Couldn't compile AssemblyInfo.cs");
                    }

                    // Clean up the temp file
                    try
                    {
                        if (File.Exists(tempAssemblyFile))
                            File.Delete(tempAssemblyFile);
                    }
                    catch
                    {
                        Console.WriteLine("Error! '{0}'", e);
                    }

                }

                // Tell the user if there's a problem copying the file
                try
                {
                    mkdirDashP(System.IO.Path.GetDirectoryName(dest));
                    System.IO.File.Copy(source, dest, true);
                }
                catch (System.IO.IOException e)
                {
                    Console.WriteLine(e.Message);
                }

                switch (project.Files.GetBuildAction(filename))
                {
                    case BuildAction.Compile:
                        compiledFiles.Add(filename);
                        break;
                    case BuildAction.Content:
                        contentFiles.Add(filename);
                        extraDistFiles.Add(filename);
                        break;
                    case BuildAction.EmbeddedResource:
                        embeddedFiles.Add(filename);
                        break;
                }
            }

            // Set up references
            for (int refNum = 0; refNum < project.References.Count; refNum++)
            {
                ReferenceNode refr = project.References[refNum];
                Assembly refAssembly = Assembly.LoadWithPartialName(refr.Name);

                /* Determine which pkg-config (.pc) file refers to
                   this assembly */

                SystemPackage package = null;

                if (packagesHash.ContainsKey(refr.Name))
                {
                  package = packagesHash[refr.Name];

                }
                else
                {
                    string assemblyFullName = string.Empty;
                    if (refAssembly != null)
                        assemblyFullName = refAssembly.FullName;

                    string assemblyFileName = string.Empty;
                    if (assemblyFullName != string.Empty &&
                        assemblyFullNameToPath.ContainsKey(assemblyFullName)
                        )
                        assemblyFileName =
                          assemblyFullNameToPath[assemblyFullName];

                    if (assemblyFileName != string.Empty &&
                        assemblyPathToPackage.ContainsKey(assemblyFileName)
                        )
                        package = assemblyPathToPackage[assemblyFileName];

                }

                /* If we know the .pc file and it is not "mono"
                   (already in the path), add a -pkg: argument */

                if (package != null &&
                    package.Name != "mono" &&
                    !pkgLibs.Contains(package.Name)
                    )
                    pkgLibs.Add(package.Name);

                string fileRef =
                  FindFileReference(refr.Name, (ProjectNode)refr.Parent);

                if (refr.LocalCopy ||
                    solution.ProjectsTable.ContainsKey(refr.Name) ||
                    fileRef != null ||
                    refr.Path != null
                    )
                {

                    /* Attempt to copy the referenced lib to the
                       project's directory */

                    string filename = refr.Name + ".dll";
                    string source = filename;
                    if (refr.Path != null)
                        source = Path.Combine(refr.Path, source);
                    source = Path.Combine(project.FullPath, source);
                    string dest = Path.Combine(projectDir, filename);

                    /* Since we depend on this binary dll to build, we
                     * will add a compile- time dependency on the
                     * copied dll, and add the dll to the list of
                     * files distributed with this package
                     */

                    binaryLibs.Add(refr.Name + ".dll");
                    extraDistFiles.Add(refr.Name + ".dll");

                    // TODO: Support copying .config.osx files
                    // TODO: Support for determining native dependencies
                    if (File.Exists(source + ".config"))
                    {
                        System.IO.File.Copy(source + ".config", Path.GetDirectoryName(dest), true);
                        extraDistFiles.Add(refr.Name + ".dll.config");
                    }

                    try
                    {
                        System.IO.File.Copy(source, dest, true);
                    }
                    catch (System.IO.IOException)
                    {
                      if (solution.ProjectsTable.ContainsKey(refr.Name)){

                        /* If an assembly is referenced, marked for
                         * local copy, in the list of projects for
                         * this solution, but does not exist, put a
                         * target into the Makefile.am to build the
                         * assembly and copy it to this project's
                         * directory
                         */

                        ProjectNode sourcePrj =
                          ((solution.ProjectsTable[refr.Name]));

                        string target =
                          String.Format("{0}:\n" +
                                        "\t$(MAKE) -C ../{1}\n" +
                                        "\tln ../{2}/$@ $@\n",
                                        filename,
                                        sourcePrj.Name,
                                        sourcePrj.Name );

                        localCopyTargets.Add(target);
                      }
                    }
                }
                else if( !pkgLibs.Contains(refr.Name) )
                {
                    // Else, let's assume it's in the GAC or the lib path
                    string assemName = string.Empty;
                    int index = refr.Name.IndexOf(",");

                    if (index > 0)
                        assemName = refr.Name.Substring(0, index);
                    else
                        assemName = refr.Name;

                    m_Kernel.Log.Write(String.Format(
                    "Warning: Couldn't find an appropriate assembly " +
                    "for reference:\n'{0}'", refr.Name
                                                     ));
                    systemLibs.Add(assemName);
                }
            }

            const string lineSep = " \\\n\t";
            string compiledFilesString = string.Empty;
            if (compiledFiles.Count > 0)
                compiledFilesString =
                    lineSep + string.Join(lineSep, compiledFiles.ToArray());

            string embeddedFilesString = "";
            if (embeddedFiles.Count > 0)
                embeddedFilesString =
                    lineSep + string.Join(lineSep, embeddedFiles.ToArray());

            string contentFilesString = "";
            if (contentFiles.Count > 0)
                contentFilesString =
                    lineSep + string.Join(lineSep, contentFiles.ToArray());

            string extraDistFilesString = "";
            if (extraDistFiles.Count > 0)
                extraDistFilesString =
                    lineSep + string.Join(lineSep, extraDistFiles.ToArray());

            string pkgLibsString = "";
            if (pkgLibs.Count > 0)
                pkgLibsString =
                    lineSep + string.Join(lineSep, pkgLibs.ToArray());

            string binaryLibsString = "";
            if (binaryLibs.Count > 0)
                binaryLibsString =
                    lineSep + string.Join(lineSep, binaryLibs.ToArray());

            string systemLibsString = "";
            if (systemLibs.Count > 0)
                systemLibsString =
                    lineSep + string.Join(lineSep, systemLibs.ToArray());

            string localCopyTargetsString = "";
            if (localCopyTargets.Count > 0)
                localCopyTargetsString =
                    string.Join("\n", localCopyTargets.ToArray());

            string monoPath = "";
            foreach (string runtimeLib in runtimeLibs)
            {
                monoPath += ":`pkg-config --variable=libdir " + runtimeLib + "`";
            }

            // Add the project name to the list of transformation
            // parameters
            XsltArgumentList argList = new XsltArgumentList();
            argList.AddParam("projectName", "", project.Name);
            argList.AddParam("solutionName", "", solution.Name);
            argList.AddParam("assemblyName", "", projectAssemblyName);
            argList.AddParam("compiledFiles", "", compiledFilesString);
            argList.AddParam("embeddedFiles", "", embeddedFilesString);
            argList.AddParam("contentFiles", "", contentFilesString);
            argList.AddParam("extraDistFiles", "", extraDistFilesString);
            argList.AddParam("pkgLibs", "", pkgLibsString);
            argList.AddParam("binaryLibs", "", binaryLibsString);
            argList.AddParam("systemLibs", "", systemLibsString);
            argList.AddParam("monoPath", "", monoPath);
            argList.AddParam("localCopyTargets", "", localCopyTargetsString);
            argList.AddParam("projectVersion", "", projectVersion);
            argList.AddParam("hasAssemblyConfig", "", hasAssemblyConfig ? "true" : "");

            // Transform the templates
            transformToFile(Path.Combine(projectDir, "configure.ac"), argList, "/Autotools/ProjectConfigureAc");
            transformToFile(Path.Combine(projectDir, "Makefile.am"), argList, "/Autotools/ProjectMakefileAm");
            transformToFile(Path.Combine(projectDir, "autogen.sh"), argList, "/Autotools/ProjectAutogenSh");

            if (project.Type == Core.Nodes.ProjectType.Library)
                transformToFile(Path.Combine(projectDir, project.Name + ".pc.in"), argList, "/Autotools/ProjectPcIn");
            if (project.Type == Core.Nodes.ProjectType.Exe || project.Type == Core.Nodes.ProjectType.WinExe)
                transformToFile(Path.Combine(projectDir, project.Name.ToLower() + ".in"), argList, "/Autotools/ProjectWrapperScriptIn");
        }

        private void CleanProject(ProjectNode project)
        {
            m_Kernel.Log.Write("...Cleaning project: {0}", project.Name);
            string projectFile = Helper.MakeFilePath(project.FullPath, "Include", "am");
            Helper.DeleteIfExists(projectFile);
        }

        private void CleanSolution(SolutionNode solution)
        {
            m_Kernel.Log.Write("Cleaning Autotools make files for", solution.Name);

            string slnFile = Helper.MakeFilePath(solution.FullPath, "Makefile", "am");
            Helper.DeleteIfExists(slnFile);

            slnFile = Helper.MakeFilePath(solution.FullPath, "Makefile", "in");
            Helper.DeleteIfExists(slnFile);

            slnFile = Helper.MakeFilePath(solution.FullPath, "configure", "ac");
            Helper.DeleteIfExists(slnFile);

            slnFile = Helper.MakeFilePath(solution.FullPath, "configure");
            Helper.DeleteIfExists(slnFile);

            slnFile = Helper.MakeFilePath(solution.FullPath, "Makefile");
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
            m_Kernel.Log.Write("Parsing system pkg-config files");
            RunInitialization();

            const string streamName = "autotools.xml";
            string fqStreamName = String.Format("Prebuild.data.{0}",
                                                streamName
                                                );

            // Retrieve stream for the autotools template XML
            Stream autotoolsStream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(fqStreamName);

            if(autotoolsStream == null) {

              /*
               * try without the default namespace prepended, in
               * case prebuild.exe assembly was compiled with
               * something other than Visual Studio .NET
               */

              autotoolsStream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(streamName);
              if(autotoolsStream == null){
                string errStr =
                  String.Format("Could not find embedded resource file:\n" +
                                "'{0}' or '{1}'",
                                streamName, fqStreamName
                                );

                m_Kernel.Log.Write(errStr);

                throw new System.Reflection.TargetException(errStr);
              }
            }

            // Create an XML URL Resolver with default credentials
            xr = new System.Xml.XmlUrlResolver();
            xr.Credentials = CredentialCache.DefaultCredentials;

            // Create a default evidence - no need to limit access
            e = new System.Security.Policy.Evidence();

            // Load the autotools XML
            autotoolsDoc = new XmlDocument();
            autotoolsDoc.Load(autotoolsStream);

            /* rootDir is the filesystem location where the Autotools
             * build tree will be created - for now we'll make it
             * $PWD/autotools
             */

            string pwd = Directory.GetCurrentDirectory();
            //string pwd = System.Environment.GetEnvironmentVariable("PWD");
            //if (pwd.Length != 0)
            //{
            string rootDir = Path.Combine(pwd, "autotools");
            //}
            //else
            //{
            //    pwd = Assembly.GetExecutingAssembly()
            //}
            chkMkDir(rootDir);

            foreach (SolutionNode solution in kern.Solutions)
            {
              m_Kernel.Log.Write(String.Format("Writing solution: {0}",
                                        solution.Name));
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
                return "autotools";
            }
        }

        #endregion
    }
}
