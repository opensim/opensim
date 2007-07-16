#region BSD License
/*

Copyright (c) 2004 - 2006
Matthew Holmes        (matthew@wildfiregames.com),
Dan     Moorehead     (dan05a@gmail.com),
Dave    Hudson        (jendave@yahoo.com),
C.J.    Adams-Collier (cjcollier@colliertech.org),

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

#region CVS Information
/*
 * $Source$
 * $Author: jendave $
 * $Date: 2006-07-28 22:43:24 -0700 (Fri, 28 Jul 2006) $
 * $Revision: 136 $
 */
#endregion

using System;
using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using Prebuild.Core.Nodes;
using Prebuild.Core.Parse;
using Prebuild.Core.Utilities;

namespace Prebuild.Core.Targets
{
	/// <summary>
	/// 
	/// </summary>
	[Target("autotools")]
	public class AutotoolsTarget : ITarget
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
			string ret = "";
			if(solution.ProjectsTable.ContainsKey(refr.Name))
			{
				ProjectNode project = (ProjectNode)solution.ProjectsTable[refr.Name];				
				string fileRef = FindFileReference(refr.Name, project);
				string finalPath = Helper.NormalizePath(Helper.MakeFilePath(project.FullPath + "/$(BUILD_DIR)/$(CONFIG)/", refr.Name, "dll"), '/');
				ret += finalPath;
				return ret;
			}
			else
			{
				ProjectNode project = (ProjectNode)refr.Parent;
				string fileRef = FindFileReference(refr.Name, project);

				if(refr.Path != null || fileRef != null)
				{
					string finalPath = (refr.Path != null) ? Helper.NormalizePath(refr.Path + "/" + refr.Name + ".dll", '/') : fileRef;
					ret += Path.Combine(project.Path, finalPath);					
					return ret;
				}

				try
				{
					//Assembly assem = Assembly.Load(refr.Name);
                    //if (assem != null)
                    //{
                    //    int index = refr.Name.IndexOf(",");
                    //    if ( index > 0)
                    //    {
                    //        ret += assem.Location;
                    //        //Console.WriteLine("Location1: " + assem.Location);
                    //    }
                    //    else
                    //    {
                    //        ret += (refr.Name + ".dll");
                    //        //Console.WriteLine("Location2: " + assem.Location);
                    //    }
                    //}
                    //else
                    //{
						int index = refr.Name.IndexOf(",");
						if ( index > 0)
						{
							ret += refr.Name.Substring(0, index) + ".dll";
							//Console.WriteLine("Location3: " + assem.Location);
						}
						else
						{
							ret += (refr.Name + ".dll");
							//Console.WriteLine("Location4: " + assem.Location);
						}
					//}
				}
				catch (System.NullReferenceException e)
				{
					e.ToString();
					int index = refr.Name.IndexOf(",");
					if ( index > 0)
					{
						ret += refr.Name.Substring(0, index) + ".dll";
						//Console.WriteLine("Location5: " + assem.Location);
					}
					else
					{
						ret += (refr.Name + ".dll");
						//Console.WriteLine("Location6: " + assem.Location);
					}
				}
			}
			return ret;
		}

		private static string BuildReferencePath(SolutionNode solution, ReferenceNode refr)
		{
			string ret = "";
			if(solution.ProjectsTable.ContainsKey(refr.Name))
			{
				ProjectNode project = (ProjectNode)solution.ProjectsTable[refr.Name];	
				string finalPath = Helper.NormalizePath(Helper.MakeReferencePath(project.FullPath + "/${build.dir}/"), '/');
				ret += finalPath;
				return ret;
			}
			else
			{
				ProjectNode project = (ProjectNode)refr.Parent;
				string fileRef = FindFileReference(refr.Name, project);

				
				if(refr.Path != null || fileRef != null)
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
			if(path == null)
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
			string projFile = Helper.MakeFilePath(project.FullPath, "Include", "am");
			StreamWriter ss = new StreamWriter(projFile);
			ss.NewLine = "\n";

			m_Kernel.CurrentWorkingDirectory.Push();
			Helper.SetCurrentDir(Path.GetDirectoryName(projFile));

			using(ss)
			{
				ss.WriteLine(Helper.AssemblyFullName(project.AssemblyName, project.Type) + ":");
				ss.WriteLine("\tmkdir -p " + Helper.MakePathRelativeTo(solution.FullPath, project.Path) + "/$(BUILD_DIR)/$(CONFIG)/");
				foreach(string file in project.Files)
				{
					if (project.Files.GetSubType(file) != SubType.Code && project.Files.GetSubType(file) != SubType.Settings)
					{
						ss.Write("\tresgen ");
						ss.Write(Helper.NormalizePath(Path.Combine(project.Path, file.Substring(0, file.LastIndexOf('.')) + ".resx "), '/'));
						if (project.Files.GetResourceName(file) != "")
						{
							ss.WriteLine(Helper.NormalizePath(Path.Combine(project.Path, project.RootNamespace + "." + project.Files.GetResourceName(file) + ".resources"), '/'));
						}
						else
						{
							ss.WriteLine(Helper.NormalizePath(Path.Combine(project.Path, project.RootNamespace + "." + file.Substring(0, file.LastIndexOf('.')) + ".resources"), '/'));
						}
					}
				}
				ss.WriteLine("\t$(CSC)\t/out:" + Helper.MakePathRelativeTo(solution.FullPath, project.Path) + "/$(BUILD_DIR)/$(CONFIG)/" + Helper.AssemblyFullName(project.AssemblyName, project.Type) + " \\");
				ss.WriteLine("\t\t/target:" + project.Type.ToString().ToLower() + " \\");
				if (project.References.Count > 0)
				{
					ss.Write("\t\t/reference:");
					bool firstref = true;
					foreach(ReferenceNode refr in project.References)
					{
						if (firstref)
						{
							firstref = false;
						}
						else
						{
							ss.Write(",");
						}
						ss.Write("{0}", Helper.NormalizePath(Helper.MakePathRelativeTo(solution.FullPath, BuildReference(solution, refr)), '/'));
					}
					ss.WriteLine(" \\");
				}
				//ss.WriteLine("\t\tProperties/AssemblyInfo.cs \\");

				foreach(string file in project.Files)
				{
					switch(project.Files.GetBuildAction(file))
					{
						case BuildAction.EmbeddedResource:
							ss.Write("\t\t/resource:");
							ss.WriteLine(Helper.NormalizePath(Path.Combine(project.Path, file), '/') + " \\");
							break;
						default:
							if (project.Files.GetSubType(file) != SubType.Code && project.Files.GetSubType(file) != SubType.Settings)
							{
								ss.Write("\t\t/resource:");
								if (project.Files.GetResourceName(file) != "")
								{
									ss.WriteLine(Helper.NormalizePath(Path.Combine(project.Path, project.RootNamespace + "." + project.Files.GetResourceName(file) + ".resources"), '/') + "," + project.RootNamespace + "." + project.Files.GetResourceName(file) + ".resources" + " \\");
								}
								else
								{
									ss.WriteLine(Helper.NormalizePath(Path.Combine(project.Path, project.RootNamespace + "." + file.Substring(0, file.LastIndexOf('.')) + ".resources"), '/') + "," + project.RootNamespace + "." + file.Substring(0, file.LastIndexOf('.')) + ".resources" + " \\");
								}
							}
							break;
					}
				}
				
				foreach(ConfigurationNode conf in project.Configurations)
				{
					if (conf.Options.KeyFile !="")
					{
						ss.WriteLine("\t\t/keyfile:" + Helper.NormalizePath(Path.Combine(project.Path, conf.Options.KeyFile), '/') + " \\");	
						break;
					}
				}
				foreach(ConfigurationNode conf in project.Configurations)
				{
					if (conf.Options.AllowUnsafe)
					{
						ss.WriteLine("\t\t/unsafe \\");
						break;
					}
				}
				if (project.AppIcon != "")
				{
					ss.WriteLine("\t\t/win32icon:" + Helper.NormalizePath(Path.Combine(project.Path, project.AppIcon), '/') + " \\");
				}

				foreach(ConfigurationNode conf in project.Configurations)
				{
					ss.WriteLine("\t\t/define:{0}", conf.Options.CompilerDefines.Replace(';', ',') + " \\");
					break;
				}
				
				foreach(ConfigurationNode conf in project.Configurations)
				{
					if (GetXmlDocFile(project, conf) !="")
					{
						ss.WriteLine("\t\t/doc:" + Helper.MakePathRelativeTo(solution.FullPath, project.Path) + "/$(BUILD_DIR)/$(CONFIG)/" + project.Name + ".xml \\");
						break;
					}
				}
				foreach(string file in project.Files)
				{
					switch(project.Files.GetBuildAction(file))
					{
						case BuildAction.Compile:
							ss.WriteLine("\t\t\\");
							ss.Write("\t\t" + NormalizePath(Path.Combine(Helper.MakePathRelativeTo(solution.FullPath, project.Path), file)));
							break;
						default:
							break;
					}
				}
				ss.WriteLine();
				ss.WriteLine();

				if (project.Type == ProjectType.Library)
				{
					ss.WriteLine("install-data-local:");
					ss.WriteLine("	echo \"$(GACUTIL) /i bin/Release/" + project.Name + ".dll /f $(GACUTIL_FLAGS)\";  \\");
					ss.WriteLine("	$(GACUTIL) /i bin/Release/" + project.Name + ".dll /f $(GACUTIL_FLAGS) || exit 1;");
					ss.WriteLine();
					ss.WriteLine("uninstall-local:");
					ss.WriteLine("	echo \"$(GACUTIL) /u " + project.Name + " $(GACUTIL_FLAGS)\"; \\");
					ss.WriteLine("	$(GACUTIL) /u " + project.Name + " $(GACUTIL_FLAGS) || exit 1;");
					ss.WriteLine();
				}
				ss.WriteLine("CLEANFILES = $(BUILD_DIR)/$(CONFIG)/" + Helper.AssemblyFullName(project.AssemblyName, project.Type) + " $(BUILD_DIR)/$(CONFIG)/" + project.AssemblyName + ".mdb $(BUILD_DIR)/$(CONFIG)/" + project.AssemblyName + ".pdb " + project.AssemblyName + ".xml");
				ss.WriteLine("EXTRA_DIST = \\");
				ss.Write("	$(FILES)");
				foreach(ConfigurationNode conf in project.Configurations)
				{
					if (conf.Options.KeyFile != "")
					{
						ss.Write(" \\");
						ss.WriteLine("\t" + conf.Options.KeyFile);
					}
					break;
				}
			}                
			m_Kernel.CurrentWorkingDirectory.Pop();
		}
		bool hasLibrary = false;

		private void WriteCombine(SolutionNode solution)
		{
		
			/* TODO: These vars should be pulled from the prebuild.xml file */
			string releaseVersion  = "2.0.0";
			string assemblyVersion = "2.1.0.0";
			string description     = 
			  "Tao Framework " + solution.Name + " Binding For .NET";
		
			hasLibrary = false;
			m_Kernel.Log.Write("Creating Autotools make files");
			foreach(ProjectNode project in solution.Projects)
			{
				if(m_Kernel.AllowProject(project.FilterGroups)) 
				{
					m_Kernel.Log.Write("...Creating makefile: {0}", project.Name);
					WriteProject(solution, project);
				}
			}

			m_Kernel.Log.Write("");
			string combFile = Helper.MakeFilePath(solution.FullPath, "Makefile", "am");
			StreamWriter ss = new StreamWriter(combFile);
			ss.NewLine = "\n";

			m_Kernel.CurrentWorkingDirectory.Push();
			Helper.SetCurrentDir(Path.GetDirectoryName(combFile));
            
			using(ss)
			{
				foreach(ProjectNode project in solution.ProjectsTableOrder)
				{
					if (project.Type == ProjectType.Library)
					{
						hasLibrary = true;
						break;
					}
				}

				if (hasLibrary)
				{
					ss.Write("pkgconfig_in_files = ");
					foreach(ProjectNode project in solution.ProjectsTableOrder)
					{
						if (project.Type == ProjectType.Library)
						{
							string combFilepc = Helper.MakeFilePath(solution.FullPath, project.Name, "pc.in");
							ss.Write(" " + project.Name + ".pc.in ");
							StreamWriter sspc = new StreamWriter(combFilepc);
							sspc.NewLine = "\n";
							using(sspc)
							{
								sspc.WriteLine("prefix=@prefix@");
								sspc.WriteLine("exec_prefix=${prefix}");
								sspc.WriteLine("libdir=${exec_prefix}/lib");
								sspc.WriteLine();
								sspc.WriteLine("Name: @PACKAGE_NAME@");
								sspc.WriteLine("Description: @DESCRIPTION@");
								sspc.WriteLine("Version: @ASSEMBLY_VERSION@");
								sspc.WriteLine("Libs:  -r:${libdir}/mono/gac/@PACKAGE_NAME@/@ASSEMBLY_VERSION@__@PUBKEY@/@PACKAGE_NAME@.dll");
							}
						}
					}
				
					ss.WriteLine();
					ss.WriteLine("pkgconfigdir=$(prefix)/lib/pkgconfig");
					ss.WriteLine("pkgconfig_DATA=$(pkgconfig_in_files:.pc.in=.pc)");
				}
				ss.WriteLine();
				foreach(ProjectNode project in solution.ProjectsTableOrder)
				{
					string path = Helper.MakePathRelativeTo(solution.FullPath, project.FullPath);
					ss.WriteLine("-include x {0}",
						Helper.NormalizePath(Helper.MakeFilePath(path, "Include", "am"),'/'));
				}
				ss.WriteLine();
				ss.WriteLine("all: \\");
				ss.Write("\t");
				foreach(ProjectNode project in solution.ProjectsTableOrder)
				{
					string path = Helper.MakePathRelativeTo(solution.FullPath, project.FullPath);
					ss.Write(Helper.AssemblyFullName(project.AssemblyName, project.Type) + " ");
						
				}
				ss.WriteLine();
				if (hasLibrary)
				{
					ss.WriteLine("EXTRA_DIST = \\");
					ss.WriteLine("\t$(pkgconfig_in_files)");
				}
				else
				{
					ss.WriteLine("EXTRA_DIST = ");
				}
				ss.WriteLine();
				ss.WriteLine("DISTCLEANFILES = \\");
				ss.WriteLine("\tconfigure \\");
				ss.WriteLine("\tMakefile.in  \\");
				ss.WriteLine("\taclocal.m4");
			}
			combFile = Helper.MakeFilePath(solution.FullPath, "configure", "ac");
			StreamWriter ts = new StreamWriter(combFile);
			ts.NewLine = "\n";
			using(ts)
			{
				if (this.hasLibrary)
				{
					foreach(ProjectNode project in solution.ProjectsTableOrder)
					{
						if (project.Type == ProjectType.Library)
						{
							ts.WriteLine("AC_INIT(" + project.Name + ".pc.in)");
							break;
						}
					}
				}
				else
				{
					ts.WriteLine("AC_INIT(Makefile.am)");
				}
				ts.WriteLine("AC_PREREQ(2.53)");
				ts.WriteLine("AC_CANONICAL_SYSTEM");
				
				ts.WriteLine("PACKAGE_NAME={0}", solution.Name);
				ts.WriteLine("PACKAGE_VERSION={0}", releaseVersion);
				ts.WriteLine("DESCRIPTION=\"{0}\"", description);
				ts.WriteLine("AC_SUBST(DESCRIPTION)");
				ts.WriteLine("AM_INIT_AUTOMAKE([$PACKAGE_NAME],[$PACKAGE_VERSION],[$DESCRIPTION])");
				
				ts.WriteLine("ASSEMBLY_VERSION={0}", assemblyVersion);
				ts.WriteLine("AC_SUBST(ASSEMBLY_VERSION)");
				
				ts.WriteLine("PUBKEY=`sn -t $PACKAGE_NAME.snk | grep 'Public Key Token' | awk -F: '{print $2}' | sed -e 's/^ //'`");
				ts.WriteLine("AC_SUBST(PUBKEY)");
				
				ts.WriteLine();
				ts.WriteLine("AM_MAINTAINER_MODE");
				ts.WriteLine();
				ts.WriteLine("dnl AC_PROG_INTLTOOL([0.25])");
				ts.WriteLine();
				ts.WriteLine("AC_PROG_INSTALL");
				ts.WriteLine();
				ts.WriteLine("MONO_REQUIRED_VERSION=1.1");
				ts.WriteLine();
				ts.WriteLine("AC_MSG_CHECKING([whether we're compiling from CVS])");
				ts.WriteLine("if test -f \"$srcdir/.cvs_version\" ; then");
				ts.WriteLine("        from_cvs=yes");
				ts.WriteLine("else");
				ts.WriteLine("  if test -f \"$srcdir/.svn\" ; then");
				ts.WriteLine("        from_cvs=yes");
				ts.WriteLine("  else");
				ts.WriteLine("        from_cvs=no");
				ts.WriteLine("  fi");
				ts.WriteLine("fi");
				ts.WriteLine();
				ts.WriteLine("AC_MSG_RESULT($from_cvs)");
				ts.WriteLine();
				ts.WriteLine("AC_PATH_PROG(MONO, mono)");
				ts.WriteLine("AC_PATH_PROG(GMCS, gmcs)");
				ts.WriteLine("AC_PATH_PROG(GACUTIL, gacutil)");
				ts.WriteLine();
				ts.WriteLine("AC_MSG_CHECKING([for mono])");
				ts.WriteLine("dnl if test \"x$MONO\" = \"x\" ; then");
				ts.WriteLine("dnl  AC_MSG_ERROR([Can't find \"mono\" in your PATH])");
				ts.WriteLine("dnl else");
				ts.WriteLine("  AC_MSG_RESULT([found])");
				ts.WriteLine("dnl fi");
				ts.WriteLine();
				ts.WriteLine("AC_MSG_CHECKING([for gmcs])");
				ts.WriteLine("dnl if test \"x$GMCS\" = \"x\" ; then");
				ts.WriteLine("dnl  AC_MSG_ERROR([Can't find \"gmcs\" in your PATH])");
				ts.WriteLine("dnl else");
				ts.WriteLine("  AC_MSG_RESULT([found])");
				ts.WriteLine("dnl fi");
				ts.WriteLine();
                //ts.WriteLine("AC_MSG_CHECKING([for gacutil])");
                //ts.WriteLine("if test \"x$GACUTIL\" = \"x\" ; then");
                //ts.WriteLine("  AC_MSG_ERROR([Can't find \"gacutil\" in your PATH])");
                //ts.WriteLine("else");
                //ts.WriteLine("  AC_MSG_RESULT([found])");
                //ts.WriteLine("fi");
				ts.WriteLine();
				ts.WriteLine("AC_SUBST(PATH)");
				ts.WriteLine("AC_SUBST(LD_LIBRARY_PATH)");
				ts.WriteLine();
				ts.WriteLine("dnl CSFLAGS=\"-debug -nowarn:1574\"");
				ts.WriteLine("CSFLAGS=\"\"");
				ts.WriteLine("AC_SUBST(CSFLAGS)");
				ts.WriteLine();
				//				ts.WriteLine("AC_MSG_CHECKING(--disable-sdl argument)");
				//				ts.WriteLine("AC_ARG_ENABLE(sdl,");
				//				ts.WriteLine("    [  --disable-sdl         Disable Sdl interface.],");
				//				ts.WriteLine("    [disable_sdl=$disableval],");
				//				ts.WriteLine("    [disable_sdl=\"no\"])");
				//				ts.WriteLine("AC_MSG_RESULT($disable_sdl)");
				//				ts.WriteLine("if test \"$disable_sdl\" = \"yes\"; then");
				//				ts.WriteLine("  AC_DEFINE(FEAT_SDL)");
				//				ts.WriteLine("fi");
				ts.WriteLine();
				ts.WriteLine("dnl Find pkg-config");
				ts.WriteLine("AC_PATH_PROG(PKGCONFIG, pkg-config, no)");
				ts.WriteLine("if test \"x$PKG_CONFIG\" = \"xno\"; then");
				ts.WriteLine("        AC_MSG_ERROR([You need to install pkg-config])");
				ts.WriteLine("fi");
				ts.WriteLine();
				ts.WriteLine("PKG_CHECK_MODULES(MONO_DEPENDENCY, mono >= $MONO_REQUIRED_VERSION, has_mono=true, has_mono=false)");
				ts.WriteLine("BUILD_DIR=\"bin\"");
				ts.WriteLine("AC_SUBST(BUILD_DIR)");
				ts.WriteLine("CONFIG=\"Release\"");
				ts.WriteLine("AC_SUBST(CONFIG)");
				ts.WriteLine();
				ts.WriteLine("if test \"x$has_mono\" = \"xtrue\"; then");
				ts.WriteLine("  AC_PATH_PROG(RUNTIME, mono, no)");
				ts.WriteLine("  AC_PATH_PROG(CSC, gmcs, no)");
				ts.WriteLine("  if test `uname -s` = \"Darwin\"; then");
				ts.WriteLine("        LIB_PREFIX=");
				ts.WriteLine("        LIB_SUFFIX=.dylib");
				ts.WriteLine("  else");
				ts.WriteLine("        LIB_PREFIX=.so");
				ts.WriteLine("        LIB_SUFFIX=");
				ts.WriteLine("  fi");
				ts.WriteLine("else");
				ts.WriteLine("  AC_PATH_PROG(CSC, csc.exe, no)");
				ts.WriteLine("  if test x$CSC = \"xno\"; then");
				ts.WriteLine("        AC_MSG_ERROR([You need to install either mono or .Net])");
				ts.WriteLine("  else");
				ts.WriteLine("    RUNTIME=");
				ts.WriteLine("    LIB_PREFIX=");
				ts.WriteLine("    LIB_SUFFIX=.dylib");
				ts.WriteLine("  fi");
				ts.WriteLine("fi");
				ts.WriteLine();
				ts.WriteLine("AC_SUBST(LIB_PREFIX)");
				ts.WriteLine("AC_SUBST(LIB_SUFFIX)");
				ts.WriteLine();
				ts.WriteLine("AC_SUBST(BASE_DEPENDENCIES_CFLAGS)");
				ts.WriteLine("AC_SUBST(BASE_DEPENDENCIES_LIBS)");
				ts.WriteLine();
				ts.WriteLine("dnl Find monodoc");
				ts.WriteLine("MONODOC_REQUIRED_VERSION=1.0");
				ts.WriteLine("AC_SUBST(MONODOC_REQUIRED_VERSION)");
				ts.WriteLine("PKG_CHECK_MODULES(MONODOC_DEPENDENCY, monodoc >= $MONODOC_REQUIRED_VERSION, enable_monodoc=yes, enable_monodoc=no)");
				ts.WriteLine();
				ts.WriteLine("if test \"x$enable_monodoc\" = \"xyes\"; then");
				ts.WriteLine("        AC_PATH_PROG(MONODOC, monodoc, no)");
				ts.WriteLine("        if test x$MONODOC = xno; then");
				ts.WriteLine("           enable_monodoc=no");
				ts.WriteLine("        fi");
				ts.WriteLine("else");
				ts.WriteLine("        MONODOC=");
				ts.WriteLine("fi");
				ts.WriteLine();
				ts.WriteLine("AC_SUBST(MONODOC)");
				ts.WriteLine("AM_CONDITIONAL(ENABLE_MONODOC, test \"x$enable_monodoc\" = \"xyes\")");
				ts.WriteLine();
				ts.WriteLine("AC_PATH_PROG(GACUTIL, gacutil, no)");
				ts.WriteLine("if test \"x$GACUTIL\" = \"xno\" ; then");
				ts.WriteLine("        AC_MSG_ERROR([No gacutil tool found])");
				ts.WriteLine("fi");
				ts.WriteLine();
				//				foreach(ProjectNode project in solution.ProjectsTableOrder)
				//				{
				//					if (project.Type == ProjectType.Library)
				//					{
				//					}
				//				}
				ts.WriteLine("GACUTIL_FLAGS='/package $(PACKAGE_NAME) /gacdir $(DESTDIR)$(prefix)'");
				ts.WriteLine("AC_SUBST(GACUTIL_FLAGS)");
				ts.WriteLine();
				ts.WriteLine("winbuild=no");
				ts.WriteLine("case \"$host\" in");
				ts.WriteLine("       *-*-mingw*|*-*-cygwin*)");
				ts.WriteLine("               winbuild=yes");
				ts.WriteLine("               ;;");
				ts.WriteLine("esac");
				ts.WriteLine("AM_CONDITIONAL(WINBUILD, test x$winbuild = xyes)");
				ts.WriteLine();
				//				ts.WriteLine("dnl Check for SDL");
				//				ts.WriteLine();
				//				ts.WriteLine("AC_PATH_PROG([SDL_CONFIG], [sdl-config])");
				//				ts.WriteLine("have_sdl=no");
				//				ts.WriteLine("if test -n \"${SDL_CONFIG}\"; then");
				//				ts.WriteLine("    have_sdl=yes");
				//				ts.WriteLine("    SDL_CFLAGS=`$SDL_CONFIG --cflags`");
				//				ts.WriteLine("    SDL_LIBS=`$SDL_CONFIG --libs`");
				//				ts.WriteLine("    #");
				//				ts.WriteLine("    # sdl-config sometimes emits an rpath flag pointing at its library");
				//				ts.WriteLine("    # installation directory.  We don't want this, as it prevents users from");
				//				ts.WriteLine("    # linking sdl-viewer against, for example, a locally compiled libGL when a");
				//				ts.WriteLine("    # version of the library also exists in SDL's library installation");
				//				ts.WriteLine("    # directory, typically /usr/lib.");
				//				ts.WriteLine("    #");
				//				ts.WriteLine("    SDL_LIBS=`echo $SDL_LIBS | sed 's/-Wl,-rpath,[[^ ]]* //'`");
				//				ts.WriteLine("fi");
				//				ts.WriteLine("AC_SUBST([SDL_CFLAGS])");
				//				ts.WriteLine("AC_SUBST([SDL_LIBS])");
				ts.WriteLine();
				ts.WriteLine("AC_OUTPUT([");
				ts.WriteLine("Makefile");
                // TODO: this does not work quite right.
				//ts.WriteLine("Properties/AssemblyInfo.cs");
				foreach(ProjectNode project in solution.ProjectsTableOrder)
				{
					if (project.Type == ProjectType.Library)
					{
						ts.WriteLine(project.Name + ".pc");
					}
					//					string path = Helper.MakePathRelativeTo(solution.FullPath, project.FullPath);
					//					ts.WriteLine(Helper.NormalizePath(Helper.MakeFilePath(path, "Include"),'/'));
				}
				ts.WriteLine("])");
				ts.WriteLine();
				ts.WriteLine("#po/Makefile.in");
				ts.WriteLine();
				ts.WriteLine("echo \"---\"");
				ts.WriteLine("echo \"Configuration summary\"");
				ts.WriteLine("echo \"\"");
				ts.WriteLine("echo \"   * Installation prefix: $prefix\"");
				ts.WriteLine("echo \"   * compiler:            $CSC\"");
				ts.WriteLine("echo \"   * Documentation:       $enable_monodoc ($MONODOC)\"");
				ts.WriteLine("echo \"   * Package Name:        $PACKAGE_NAME\"");
				ts.WriteLine("echo \"   * Version:             $PACKAGE_VERSION\"");
				ts.WriteLine("echo \"   * Public Key:          $PUBKEY\"");
				ts.WriteLine("echo \"\"");
				ts.WriteLine("echo \"---\"");
				ts.WriteLine();
			}

            ts.NewLine = "\n";
            foreach (ProjectNode project in solution.ProjectsTableOrder)
            {
                if (project.GenerateAssemblyInfoFile)
                {
                    GenerateAssemblyInfoFile(solution, combFile);
                }
            }
		}

        private static void GenerateAssemblyInfoFile(SolutionNode solution, string combFile)
        {
            System.IO.Directory.CreateDirectory(Helper.MakePathRelativeTo(solution.FullPath, "Properties"));
            combFile = Helper.MakeFilePath(solution.FullPath + "/Properties/", "AssemblyInfo.cs", "in");
            StreamWriter ai = new StreamWriter(combFile);
            
            using (ai)
            {
                ai.WriteLine("#region License");
                ai.WriteLine("/*");
                ai.WriteLine("MIT License");
                ai.WriteLine("Copyright (c)2003-2006 Tao Framework Team");
                ai.WriteLine("http://www.taoframework.com");
                ai.WriteLine("All rights reserved.");
                ai.WriteLine("");
                ai.WriteLine("Permission is hereby granted, free of charge, to any person obtaining a copy");
                ai.WriteLine("of this software and associated documentation files (the \"Software\"), to deal");
                ai.WriteLine("in the Software without restriction, including without limitation the rights");
                ai.WriteLine("to use, copy, modify, merge, publish, distribute, sublicense, and/or sell");
                ai.WriteLine("copies of the Software, and to permit persons to whom the Software is");
                ai.WriteLine("furnished to do so, subject to the following conditions:");
                ai.WriteLine("");
                ai.WriteLine("The above copyright notice and this permission notice shall be included in all");
                ai.WriteLine("copies or substantial portions of the Software.");
                ai.WriteLine("");
                ai.WriteLine("THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR");
                ai.WriteLine("IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,");
                ai.WriteLine("FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE");
                ai.WriteLine("AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER");
                ai.WriteLine("LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,");
                ai.WriteLine("OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE");
                ai.WriteLine("SOFTWARE.");
                ai.WriteLine("*/");
                ai.WriteLine("#endregion License");
                ai.WriteLine("");
                ai.WriteLine("using System;");
                ai.WriteLine("using System.Reflection;");
                ai.WriteLine("using System.Runtime.InteropServices;");
                ai.WriteLine("using System.Security;");
                ai.WriteLine("using System.Security.Permissions;");
                ai.WriteLine("");
                ai.WriteLine("[assembly: AllowPartiallyTrustedCallers]");
                ai.WriteLine("[assembly: AssemblyCompany(\"Tao Framework -- http://www.taoframework.com\")]");
                ai.WriteLine("[assembly: AssemblyConfiguration(\"Retail\")]");
                ai.WriteLine("[assembly: AssemblyCopyright(\"Copyright (c)2003-2006 Tao Framework Team.  All rights reserved.\")]");
                ai.WriteLine("[assembly: AssemblyCulture(\"\")]");
                ai.WriteLine("[assembly: AssemblyDefaultAlias(\"@PACKAGE_NAME@\")]");
                ai.WriteLine("[assembly: AssemblyDelaySign(false)]");
                ai.WriteLine("[assembly: AssemblyDescription(\"@DESCRIPTION@\")]");
                ai.WriteLine("[assembly: AssemblyFileVersion(\"@ASSEMBLY_VERSION@\")]");
                ai.WriteLine("[assembly: AssemblyInformationalVersion(\"@ASSEMBLY_VERSION@\")]");
                ai.WriteLine("[assembly: AssemblyKeyName(\"\")]");
                ai.WriteLine("[assembly: AssemblyProduct(\"@PACKAGE_NAME@.dll\")]");
                ai.WriteLine("[assembly: AssemblyTitle(\"@DESCRIPTION@\")]");
                ai.WriteLine("[assembly: AssemblyTrademark(\"Tao Framework -- http://www.taoframework.com\")]");
                ai.WriteLine("[assembly: AssemblyVersion(\"@ASSEMBLY_VERSION@\")]");
                ai.WriteLine("[assembly: CLSCompliant(true)]");
                ai.WriteLine("[assembly: ComVisible(false)]");
                ai.WriteLine("[assembly: SecurityPermission(SecurityAction.RequestMinimum, Flags = SecurityPermissionFlag.Execution)]");
                ai.WriteLine("[assembly: SecurityPermission(SecurityAction.RequestMinimum, Flags = SecurityPermissionFlag.SkipVerification)]");
                ai.WriteLine("[assembly: SecurityPermission(SecurityAction.RequestMinimum, Flags = SecurityPermissionFlag.UnmanagedCode)]");

            }
            //return combFile;
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
				return "autotools";
			}
		}

		#endregion
	}
}
