#region BSD License
/*
Copyright (c) 2008 Matthew Holmes (matthew@wildfiregames.com), John Anderson (sontek@gmail.com)

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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;

using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using Prebuild.Core.Nodes;
using Prebuild.Core.Utilities;
using System.CodeDom.Compiler;

namespace Prebuild.Core.Targets
{

	/// <summary>
	/// 
	/// </summary>
	public abstract class VSGenericTarget : ITarget
	{
		#region Fields

	    readonly Hashtable tools = new Hashtable();
		Kernel kernel;
		#endregion

        #region Properties
        /// <summary>
        /// Gets or sets the solution version.
        /// </summary>
        /// <value>The solution version.</value>
        public abstract string SolutionVersion { get; }
        /// <summary>
        /// Gets or sets the product version.
        /// </summary>
        /// <value>The product version.</value>
        public abstract string ProductVersion { get; }
        /// <summary>
        /// Gets or sets the schema version.
        /// </summary>
        /// <value>The schema version.</value>
        public abstract string SchemaVersion { get; }
        /// <summary>
        /// Gets or sets the name of the version.
        /// </summary>
        /// <value>The name of the version.</value>
        public abstract string VersionName { get; }
        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        /// <value>The version.</value>
        public abstract VSVersion Version { get; }
        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public abstract string Name { get; }

        protected abstract string GetToolsVersionXml(FrameworkVersion version);
        public abstract string SolutionTag { get; }

        #endregion

		#region Constructors

		/// <summary>
        /// Initializes a new instance of the <see cref="VSGenericTarget"/> class.
		/// </summary>
		protected VSGenericTarget()
		{
			this.tools["C#"] = new ToolInfo("C#", "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", "csproj", "CSHARP", "$(MSBuildBinPath)\\Microsoft.CSHARP.Targets");
			this.tools["Database"] = new ToolInfo("Database", "{4F174C21-8C12-11D0-8340-0000F80270F8}", "dbp", "UNKNOWN");
			this.tools["Boo"] = new ToolInfo("Boo", "{45CEA7DC-C2ED-48A6-ACE0-E16144C02365}", "booproj", "Boo", "$(BooBinPath)\\Boo.Microsoft.Build.targets");
			this.tools["VisualBasic"] = new ToolInfo("VisualBasic", "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}", "vbproj", "VisualBasic", "$(MSBuildBinPath)\\Microsoft.VisualBasic.Targets");
			this.tools["Folder"] = new ToolInfo("Folder", "{2150E333-8FDC-42A3-9474-1A3956D46DE8}", null, null);
		}

		#endregion

		#region Private Methods

		private string MakeRefPath(ProjectNode project)
		{
			string ret = "";
			foreach (ReferencePathNode node in project.ReferencePaths)
			{
				try
				{
					string fullPath = Helper.ResolvePath(node.Path);
					if (ret.Length < 1)
					{
						ret = fullPath;
					}
					else
					{
						ret += ";" + fullPath;
					}
				}
				catch (ArgumentException)
				{
					this.kernel.Log.Write(LogType.Warning, "Could not resolve reference path: {0}", node.Path);
				}
			}

			return ret;
		}

		private static ProjectNode FindProjectInSolution(string name, SolutionNode solution)
		{
			SolutionNode node = solution;

			while (node.Parent is SolutionNode)
				node = node.Parent as SolutionNode;

			return FindProjectInSolutionRecursively(name, node);
		}

		private static ProjectNode FindProjectInSolutionRecursively(string name, SolutionNode solution)
		{
			if (solution.ProjectsTable.ContainsKey(name))
				return (ProjectNode)solution.ProjectsTable[name];

			foreach (SolutionNode child in solution.Solutions)
			{
				ProjectNode node = FindProjectInSolutionRecursively(name, child);
				if (node != null)
					return node;
			}

			return null;
		}

		private void WriteProject(SolutionNode solution, ProjectNode project)
		{
			if (!tools.ContainsKey(project.Language))
			{
				throw new UnknownLanguageException("Unknown .NET language: " + project.Language);
			}

			ToolInfo toolInfo = (ToolInfo)tools[project.Language];
			string projectFile = Helper.MakeFilePath(project.FullPath, project.Name, toolInfo.FileExtension);
			StreamWriter ps = new StreamWriter(projectFile);

			kernel.CurrentWorkingDirectory.Push();
			Helper.SetCurrentDir(Path.GetDirectoryName(projectFile));

			#region Project File
			using (ps)
			{
                ps.WriteLine("<Project DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\" ToolsVersion=\"{0}\">", this.Version == VSVersion.VS10 ? "4.0" : "3.5");
				ps.WriteLine("  <PropertyGroup>");
				ps.WriteLine("    <ProjectType>Local</ProjectType>");
				ps.WriteLine("    <ProductVersion>{0}</ProductVersion>", this.ProductVersion);
				ps.WriteLine("    <SchemaVersion>{0}</SchemaVersion>", this.SchemaVersion);
				ps.WriteLine("    <ProjectGuid>{{{0}}}</ProjectGuid>", project.Guid.ToString().ToUpper());

				// Visual Studio has a hard coded guid for the project type
				if (project.Type == ProjectType.Web)
					ps.WriteLine("    <ProjectTypeGuids>{349c5851-65df-11da-9384-00065b846f21};{fae04ec0-301f-11d3-bf4b-00c04f79efbc}</ProjectTypeGuids>");
				ps.WriteLine("    <Configuration Condition=\" '$(Configuration)' == '' \">Debug</Configuration>");
				ps.WriteLine("    <Platform Condition=\" '$(Platform)' == '' \">AnyCPU</Platform>");
				ps.WriteLine("    <ApplicationIcon>{0}</ApplicationIcon>", project.AppIcon);
				ps.WriteLine("    <AssemblyKeyContainerName>");
				ps.WriteLine("    </AssemblyKeyContainerName>");
				ps.WriteLine("    <AssemblyName>{0}</AssemblyName>", project.AssemblyName);
				foreach (ConfigurationNode conf in project.Configurations)
				{
					if (conf.Options.KeyFile != "")
					{
						ps.WriteLine("    <AssemblyOriginatorKeyFile>{0}</AssemblyOriginatorKeyFile>", conf.Options.KeyFile);
						ps.WriteLine("    <SignAssembly>true</SignAssembly>");
						break;
					}
				}
				ps.WriteLine("    <DefaultClientScript>JScript</DefaultClientScript>");
				ps.WriteLine("    <DefaultHTMLPageLayout>Grid</DefaultHTMLPageLayout>");
				ps.WriteLine("    <DefaultTargetSchema>IE50</DefaultTargetSchema>");
				ps.WriteLine("    <DelaySign>false</DelaySign>");
				ps.WriteLine("    <TargetFrameworkVersion>{0}</TargetFrameworkVersion>", project.FrameworkVersion.ToString().Replace("_", "."));

				ps.WriteLine("    <OutputType>{0}</OutputType>", project.Type == ProjectType.Web ? ProjectType.Library.ToString() : project.Type.ToString());
				ps.WriteLine("    <AppDesignerFolder>{0}</AppDesignerFolder>", project.DesignerFolder);
				ps.WriteLine("    <RootNamespace>{0}</RootNamespace>", project.RootNamespace);
				ps.WriteLine("    <StartupObject>{0}</StartupObject>", project.StartupObject);
                if (string.IsNullOrEmpty(project.DebugStartParameters))
                {
                    ps.WriteLine("    <StartArguments>{0}</StartArguments>", project.DebugStartParameters);
                }
				ps.WriteLine("    <FileUpgradeFlags>");
				ps.WriteLine("    </FileUpgradeFlags>");

				ps.WriteLine("  </PropertyGroup>");

				foreach (ConfigurationNode conf in project.Configurations)
				{
					ps.Write("  <PropertyGroup ");
					ps.WriteLine("Condition=\" '$(Configuration)|$(Platform)' == '{0}|AnyCPU' \">", conf.Name);
					ps.WriteLine("    <AllowUnsafeBlocks>{0}</AllowUnsafeBlocks>", conf.Options["AllowUnsafe"]);
					ps.WriteLine("    <BaseAddress>{0}</BaseAddress>", conf.Options["BaseAddress"]);
					ps.WriteLine("    <CheckForOverflowUnderflow>{0}</CheckForOverflowUnderflow>", conf.Options["CheckUnderflowOverflow"]);
					ps.WriteLine("    <ConfigurationOverrideFile>");
					ps.WriteLine("    </ConfigurationOverrideFile>");
					ps.WriteLine("    <DefineConstants>{0}</DefineConstants>", conf.Options["CompilerDefines"]);
					ps.WriteLine("    <DocumentationFile>{0}</DocumentationFile>", Helper.NormalizePath(conf.Options["XmlDocFile"].ToString()));
					ps.WriteLine("    <DebugSymbols>{0}</DebugSymbols>", conf.Options["DebugInformation"]);
					ps.WriteLine("    <FileAlignment>{0}</FileAlignment>", conf.Options["FileAlignment"]);
					ps.WriteLine("    <Optimize>{0}</Optimize>", conf.Options["OptimizeCode"]);
					if (project.Type != ProjectType.Web)
						ps.WriteLine("    <OutputPath>{0}</OutputPath>",
							Helper.EndPath(Helper.NormalizePath(conf.Options["OutputPath"].ToString())));
					else
						ps.WriteLine("    <OutputPath>{0}</OutputPath>",
							Helper.EndPath(Helper.NormalizePath("bin\\")));

					ps.WriteLine("    <RegisterForComInterop>{0}</RegisterForComInterop>", conf.Options["RegisterComInterop"]);
					ps.WriteLine("    <RemoveIntegerChecks>{0}</RemoveIntegerChecks>", conf.Options["RemoveIntegerChecks"]);
					ps.WriteLine("    <TreatWarningsAsErrors>{0}</TreatWarningsAsErrors>", conf.Options["WarningsAsErrors"]);
					ps.WriteLine("    <WarningLevel>{0}</WarningLevel>", conf.Options["WarningLevel"]);
					ps.WriteLine("    <NoStdLib>{0}</NoStdLib>", conf.Options["NoStdLib"]);
					ps.WriteLine("    <NoWarn>{0}</NoWarn>", conf.Options["SuppressWarnings"]);
					ps.WriteLine("  </PropertyGroup>");
				}

				//ps.WriteLine("      </Settings>");

			    List<ProjectNode> projectReferences = new List<ProjectNode>();
				List<ReferenceNode> otherReferences = new List<ReferenceNode>();

				foreach (ReferenceNode refr in project.References)
				{
					ProjectNode projectNode = FindProjectInSolution(refr.Name, solution);

					if (projectNode == null)
						otherReferences.Add(refr);
					else
						projectReferences.Add(projectNode);
				}
				// Assembly References
				ps.WriteLine("  <ItemGroup>");

				foreach (ReferenceNode refr in otherReferences)
				{
					ps.Write("    <Reference");
					ps.Write(" Include=\"");
					ps.Write(refr.Name);
					ps.WriteLine("\" >");
					ps.Write("        <Name>");
					ps.Write(refr.Name);
					ps.WriteLine("</Name>");
					// TODO: Allow reference to *.exe files
                    ps.WriteLine("      <Private>{0}</Private>", refr.LocalCopy);
					ps.WriteLine("    </Reference>");
				}
				ps.WriteLine("  </ItemGroup>");

				//Project References
				ps.WriteLine("  <ItemGroup>");
				foreach (ProjectNode projectReference in projectReferences)
				{
					ToolInfo tool = (ToolInfo)tools[projectReference.Language];
                    if (tools == null)
                        throw new UnknownLanguageException();

                    string path =
                        Helper.MakePathRelativeTo(project.FullPath,
                                                  Helper.MakeFilePath(projectReference.FullPath, projectReference.Name, tool.FileExtension));
                    ps.WriteLine("    <ProjectReference Include=\"{0}\">", path);

					// TODO: Allow reference to visual basic projects
					ps.WriteLine("      <Name>{0}</Name>", projectReference.Name);
					ps.WriteLine("      <Project>{0}</Project>", projectReference.Guid.ToString("B").ToUpper());
					ps.WriteLine("      <Package>{0}</Package>", tool.Guid.ToUpper());

                    ps.WriteLine("      <Private>False</Private>" );

					ps.WriteLine("    </ProjectReference>");
				}
				ps.WriteLine("  </ItemGroup>");

				//                ps.WriteLine("    </Build>");
				ps.WriteLine("  <ItemGroup>");

				//                ps.WriteLine("      <Include>");
				List<string> list = new List<string>();

			    foreach (string path in project.Files)
				{
					string lower = path.ToLower();
					if (lower.EndsWith(".resx"))
					{
						string codebehind = String.Format("{0}.Designer{1}", path.Substring(0, path.LastIndexOf('.')), toolInfo.LanguageExtension);
						if (!list.Contains(codebehind))
							list.Add(codebehind);
					}
				}
				
				foreach (string file in project.Files)
				{
					//					if (file == "Properties\\Bind.Designer.cs")
					//					{
					//						Console.WriteLine("Wait a minute!");
					//						Console.WriteLine(project.Files.GetSubType(file).ToString());
					//					}

					SubType subType = project.Files.GetSubType(file);

					if (subType != SubType.Code && subType != SubType.Settings && subType != SubType.Designer
						&& subType != SubType.CodeBehind)
					{
						ps.WriteLine("    <EmbeddedResource Include=\"{0}\">", file.Substring(0, file.LastIndexOf('.')) + ".resx");
						ps.WriteLine("      <DependentUpon>{0}</DependentUpon>", Path.GetFileName(file));
						ps.WriteLine("      <SubType>Designer</SubType>");
						ps.WriteLine("    </EmbeddedResource>");
						//
					}

					if (subType == SubType.Designer)
					{
						ps.WriteLine("    <EmbeddedResource Include=\"{0}\">", file);
						ps.WriteLine("      <SubType>" + subType + "</SubType>");
						ps.WriteLine("      <Generator>ResXFileCodeGenerator</Generator>");
						
						string autogen_name = file.Substring(0, file.LastIndexOf('.')) + ".Designer.cs";
                        string dependent_name = file.Substring(0, file.LastIndexOf('.')) + ".cs";

						ps.WriteLine("      <LastGenOutput>{0}</LastGenOutput>", autogen_name);

                        // Check for a parent .cs file with the same name as this designer file
                        if (File.Exists(dependent_name))
                            ps.WriteLine("      <DependentUpon>{0}</DependentUpon>", Path.GetFileName(dependent_name));
						
                        ps.WriteLine("    </EmbeddedResource>");
						if (File.Exists(autogen_name))
						{
							ps.WriteLine("    <Compile Include=\"{0}\">", autogen_name);
							ps.WriteLine("      <AutoGen>True</AutoGen>");
							ps.WriteLine("      <DesignTime>True</DesignTime>");

                            // If a parent .cs file exists, link this autogen file to it. Otherwise link
                            // to the designer file
                            if (File.Exists(dependent_name))
                                ps.WriteLine("      <DependentUpon>{0}</DependentUpon>", Path.GetFileName(dependent_name));
                            else
                                ps.WriteLine("      <DependentUpon>{0}</DependentUpon>", Path.GetFileName(file));
	
                            ps.WriteLine("    </Compile>");
						}
						list.Add(autogen_name);
					}
					if (subType == SubType.Settings)
					{
						ps.Write("    <{0} ", project.Files.GetBuildAction(file));
						ps.WriteLine("Include=\"{0}\">", file);
						string fileName = Path.GetFileName(file);
						if (project.Files.GetBuildAction(file) == BuildAction.None)
						{
							ps.WriteLine("      <Generator>SettingsSingleFileGenerator</Generator>");
							ps.WriteLine("      <LastGenOutput>{0}</LastGenOutput>", fileName.Substring(0, fileName.LastIndexOf('.')) + ".Designer.cs");
						}
						else
						{
							ps.WriteLine("      <SubType>Code</SubType>");
							ps.WriteLine("      <AutoGen>True</AutoGen>");
							ps.WriteLine("      <DesignTimeSharedInput>True</DesignTimeSharedInput>");
							string fileNameShort = fileName.Substring(0, fileName.LastIndexOf('.'));
							string fileNameShorter = fileNameShort.Substring(0, fileNameShort.LastIndexOf('.'));
							ps.WriteLine("      <DependentUpon>{0}</DependentUpon>", Path.GetFileName(fileNameShorter + ".settings"));
						}
						ps.WriteLine("    </{0}>", project.Files.GetBuildAction(file));
					}
					else if (subType != SubType.Designer)
					{
						string path = Helper.NormalizePath(file);
                        string path_lower = path.ToLower();

						if (!list.Contains(file))
						{
							ps.Write("    <{0} ", project.Files.GetBuildAction(path));

							int startPos = 0;
							if (project.Files.GetPreservePath(file))
							{
								while ((@"./\").IndexOf(file.Substring(startPos, 1)) != -1)
									startPos++;

							}
							else
							{
								startPos = file.LastIndexOf(Path.GetFileName(path));
							}
							
							ps.WriteLine("Include=\"{0}\">", path);

							int last_period_index = file.LastIndexOf('.');
							string short_file_name = file.Substring(0, last_period_index);
							string extension = Path.GetExtension(path);
							string designer_format = string.Format(".designer{0}", extension);

                            if (path_lower.EndsWith(designer_format))
							{
								int designer_index = path_lower.IndexOf(designer_format);
								string file_name = path.Substring(0, designer_index);

								if (File.Exists(file_name))
									ps.WriteLine("      <DependentUpon>{0}</DependentUpon>", Path.GetFileName(file_name));
								else if (File.Exists(file_name + ".resx"))
									ps.WriteLine("      <DependentUpon>{0}</DependentUpon>", Path.GetFileName(file_name + ".resx"));
							}
							else if (subType == SubType.CodeBehind)
							{
								ps.WriteLine("      <DependentUpon>{0}</DependentUpon>", Path.GetFileName(short_file_name));
							}
							if (project.Files.GetIsLink(file))
							{
								string alias = project.Files.GetLinkPath(file);
								alias += file.Substring(startPos);
								alias = Helper.NormalizePath(alias);
								ps.WriteLine("      <Link>{0}</Link>", alias);
							}
							else if (project.Files.GetBuildAction(file) != BuildAction.None)
							{
								if (project.Files.GetBuildAction(file) != BuildAction.EmbeddedResource)
								{
									ps.WriteLine("      <SubType>{0}</SubType>", subType);
								}
							}

							if (project.Files.GetCopyToOutput(file) != CopyToOutput.Never)
							{
								ps.WriteLine("      <CopyToOutputDirectory>{0}</CopyToOutputDirectory>", project.Files.GetCopyToOutput(file));
							}

							ps.WriteLine("    </{0}>", project.Files.GetBuildAction(file));
						}
					}
				}

				ps.WriteLine("  </ItemGroup>");
				ps.WriteLine("  <Import Project=\"" + toolInfo.ImportProject + "\" />");
				ps.WriteLine("  <PropertyGroup>");
				ps.WriteLine("    <PreBuildEvent>");
				ps.WriteLine("    </PreBuildEvent>");
				ps.WriteLine("    <PostBuildEvent>");
				ps.WriteLine("    </PostBuildEvent>");
				ps.WriteLine("  </PropertyGroup>");
				ps.WriteLine("</Project>");
			}
			#endregion

			#region User File

			ps = new StreamWriter(projectFile + ".user");
			using (ps)
			{
				ps.WriteLine("<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
				//ps.WriteLine( "<VisualStudioProject>" );
				//ps.WriteLine("  <{0}>", toolInfo.XMLTag);
				//ps.WriteLine("    <Build>");
				ps.WriteLine("  <PropertyGroup>");
				//ps.WriteLine("      <Settings ReferencePath=\"{0}\">", MakeRefPath(project));
				ps.WriteLine("    <Configuration Condition=\" '$(Configuration)' == '' \">Debug</Configuration>");
				ps.WriteLine("    <Platform Condition=\" '$(Platform)' == '' \">AnyCPU</Platform>");
				ps.WriteLine("    <ReferencePath>{0}</ReferencePath>", MakeRefPath(project));
				ps.WriteLine("    <LastOpenVersion>{0}</LastOpenVersion>", this.ProductVersion);
				ps.WriteLine("    <ProjectView>ProjectFiles</ProjectView>");
				ps.WriteLine("    <ProjectTrust>0</ProjectTrust>");
				ps.WriteLine("  </PropertyGroup>");
				foreach (ConfigurationNode conf in project.Configurations)
				{
					ps.Write("  <PropertyGroup");
					ps.Write(" Condition = \" '$(Configuration)|$(Platform)' == '{0}|AnyCPU' \"", conf.Name);
					ps.WriteLine(" />");
				}
				ps.WriteLine("</Project>");
			}
			#endregion

			kernel.CurrentWorkingDirectory.Pop();
		}

	    private void WriteSolution(SolutionNode solution, bool writeSolutionToDisk)
		{
			kernel.Log.Write("Creating {0} solution and project files", this.VersionName);

			foreach (SolutionNode child in solution.Solutions)
			{
				kernel.Log.Write("...Creating folder: {0}", child.Name);
				WriteSolution(child, false);
			}

			foreach (ProjectNode project in solution.Projects)
			{
				kernel.Log.Write("...Creating project: {0}", project.Name);
				WriteProject(solution, project);
			}

			foreach (DatabaseProjectNode project in solution.DatabaseProjects)
			{
				kernel.Log.Write("...Creating database project: {0}", project.Name);
				WriteDatabaseProject(solution, project);
			}

			if (writeSolutionToDisk) // only write main solution
			{
				kernel.Log.Write("");
				string solutionFile = Helper.MakeFilePath(solution.FullPath, solution.Name, "sln");
				
                using (StreamWriter ss = new StreamWriter(solutionFile))
                {
                    kernel.CurrentWorkingDirectory.Push();
                    Helper.SetCurrentDir(Path.GetDirectoryName(solutionFile));

					ss.WriteLine("Microsoft Visual Studio Solution File, Format Version {0}", this.SolutionVersion);
                    ss.WriteLine(SolutionTag);

					WriteProjectDeclarations(ss, solution, solution);

					ss.WriteLine("Global");

					ss.WriteLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
					foreach (ConfigurationNode conf in solution.Configurations)
					{
						ss.WriteLine("\t\t{0}|Any CPU = {0}|Any CPU", conf.Name);
					}
					ss.WriteLine("\tEndGlobalSection");

					ss.WriteLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
					WriteConfigurationLines(solution.Configurations, solution, ss);
					ss.WriteLine("\tEndGlobalSection");

					if (solution.Solutions.Count > 0)
					{
						ss.WriteLine("\tGlobalSection(NestedProjects) = preSolution");
						foreach (SolutionNode embeddedSolution in solution.Solutions)
						{
							WriteNestedProjectMap(ss, embeddedSolution);
						}
						ss.WriteLine("\tEndGlobalSection");
					}

					ss.WriteLine("EndGlobal");
				}

				kernel.CurrentWorkingDirectory.Pop();
			}
		}

		private void WriteProjectDeclarations(StreamWriter writer, SolutionNode actualSolution, SolutionNode embeddedSolution)
		{
			foreach (SolutionNode childSolution in embeddedSolution.Solutions)
			{
				WriteEmbeddedSolution(writer, childSolution);
				WriteProjectDeclarations(writer, actualSolution, childSolution);
			}

			foreach (ProjectNode project in embeddedSolution.Projects)
			{
				WriteProject(actualSolution, writer, project);
			}

			foreach (DatabaseProjectNode dbProject in embeddedSolution.DatabaseProjects)
			{
				WriteProject(actualSolution, writer, dbProject);
			}

			if (actualSolution.Guid == embeddedSolution.Guid)
			{
				WriteSolutionFiles(actualSolution, writer);
			}
		}

		private static void WriteNestedProjectMap(StreamWriter writer, SolutionNode embeddedSolution)
		{
			foreach (ProjectNode project in embeddedSolution.Projects)
			{
				WriteNestedProject(writer, embeddedSolution, project.Guid);
			}

			foreach (DatabaseProjectNode dbProject in embeddedSolution.DatabaseProjects)
			{
				WriteNestedProject(writer, embeddedSolution, dbProject.Guid);
			}

			foreach (SolutionNode child in embeddedSolution.Solutions)
			{
				WriteNestedProject(writer, embeddedSolution, child.Guid);
				WriteNestedProjectMap(writer, child);
			}
		}

		private static void WriteNestedProject(StreamWriter writer, SolutionNode solution, Guid projectGuid)
		{
			WriteNestedFolder(writer, solution.Guid, projectGuid);
		}

		private static void WriteNestedFolder(StreamWriter writer, Guid parentGuid, Guid childGuid)
		{
			writer.WriteLine("\t\t{0} = {1}",
				childGuid.ToString("B").ToUpper(),
				parentGuid.ToString("B").ToUpper());
		}

		private static void WriteConfigurationLines(ICollection configurations, SolutionNode solution, StreamWriter ss)
		{
			foreach (ProjectNode project in solution.Projects)
			{
				foreach (ConfigurationNode conf in configurations)
				{
					ss.WriteLine("\t\t{0}.{1}|Any CPU.ActiveCfg = {1}|Any CPU",
						project.Guid.ToString("B").ToUpper(),
						conf.Name);

					ss.WriteLine("\t\t{0}.{1}|Any CPU.Build.0 = {1}|Any CPU",
						project.Guid.ToString("B").ToUpper(),
						conf.Name);
				}
			}

			foreach (SolutionNode child in solution.Solutions)
			{
				WriteConfigurationLines(configurations, child, ss);
			}
		}

		private void WriteSolutionFiles(SolutionNode solution, StreamWriter ss)
		{
            if (solution.Files != null && solution.Files.Count > 0)
            {
                WriteProject(ss, "Folder", solution.Guid, "Solution Files", "Solution Files", solution.Files);
            }
		}

		private void WriteEmbeddedSolution(StreamWriter writer, SolutionNode embeddedSolution)
		{
			WriteProject(writer, "Folder", embeddedSolution.Guid, embeddedSolution.Name, embeddedSolution.Name, embeddedSolution.Files);
		}

		private void WriteProject(SolutionNode solution, StreamWriter ss, ProjectNode project)
		{
			WriteProject(ss, solution, project.Language, project.Guid, project.Name, project.FullPath);
		}

		private void WriteProject(SolutionNode solution, StreamWriter ss, DatabaseProjectNode dbProject)
		{
			if (solution.Files != null && solution.Files.Count > 0)
				WriteProject(ss, solution, "Database", dbProject.Guid, dbProject.Name, dbProject.FullPath);
		}

        private static bool ExtensionSpecified(string refName)
        {
            return refName.EndsWith(".dll") || refName.EndsWith(".exe");
        }

        private static string GetProjectExtension(ProjectNode project)
        {
            string extension = ".dll";
            if (project.Type == ProjectType.Exe)
            {
                extension = ".exe";
            }
            return extension;
        }

		const string ProjectDeclarationBeginFormat = "Project(\"{0}\") = \"{1}\", \"{2}\", \"{3}\"";
		const string ProjectDeclarationEndFormat = "EndProject";

		private void WriteProject(StreamWriter ss, SolutionNode solution, string language, Guid guid, string name, string projectFullPath)
		{
			if (!tools.ContainsKey(language))
				throw new UnknownLanguageException("Unknown .NET language: " + language);

			ToolInfo toolInfo = (ToolInfo)tools[language];

			string path = Helper.MakePathRelativeTo(solution.FullPath, projectFullPath);

			path = Helper.MakeFilePath(path, name, toolInfo.FileExtension);

			WriteProject(ss, language, guid, name, path);
		}

		private void WriteProject(StreamWriter writer, string language, Guid projectGuid, string name, string location)
		{
			WriteProject(writer, language, projectGuid, name, location, null);
		}

		private void WriteProject(StreamWriter writer, string language, Guid projectGuid, string name, string location, FilesNode files)
		{
			if (!tools.ContainsKey(language))
				throw new UnknownLanguageException("Unknown .NET language: " + language);

			ToolInfo toolInfo = (ToolInfo)tools[language];

			writer.WriteLine(ProjectDeclarationBeginFormat,
				toolInfo.Guid,
				name,
				location,
				projectGuid.ToString("B").ToUpper());

			if (files != null)
			{
				writer.WriteLine("\tProjectSection(SolutionItems) = preProject");

				foreach (string file in files)
					writer.WriteLine("\t\t{0} = {0}", file);

				writer.WriteLine("\tEndProjectSection");
			}

			writer.WriteLine(ProjectDeclarationEndFormat);
		}

		private void WriteDatabaseProject(SolutionNode solution, DatabaseProjectNode project)
		{
			string projectFile = Helper.MakeFilePath(project.FullPath, project.Name, "dbp");
			IndentedTextWriter ps = new IndentedTextWriter(new StreamWriter(projectFile), "   ");

			kernel.CurrentWorkingDirectory.Push();

			Helper.SetCurrentDir(Path.GetDirectoryName(projectFile));

			using (ps)
			{
				ps.WriteLine("# Microsoft Developer Studio Project File - Database Project");
				ps.WriteLine("Begin DataProject = \"{0}\"", project.Name);
				ps.Indent++;
				ps.WriteLine("MSDTVersion = \"80\"");
				// TODO: Use the project.Files property
				if (ContainsSqlFiles(Path.GetDirectoryName(projectFile)))
					WriteDatabaseFoldersAndFiles(ps, Path.GetDirectoryName(projectFile));

				ps.WriteLine("Begin DBRefFolder = \"Database References\"");
				ps.Indent++;
				foreach (DatabaseReferenceNode reference in project.References)
				{
					ps.WriteLine("Begin DBRefNode = \"{0}\"", reference.Name);
					ps.Indent++;
					ps.WriteLine("ConnectStr = \"{0}\"", reference.ConnectionString);
					ps.WriteLine("Provider = \"{0}\"", reference.ProviderId.ToString("B").ToUpper());
					//ps.WriteLine("Colorizer = 5");
					ps.Indent--;
					ps.WriteLine("End");
				}
				ps.Indent--;
				ps.WriteLine("End");
				ps.Indent--;
				ps.WriteLine("End");

				ps.Flush();
			}

			kernel.CurrentWorkingDirectory.Pop();
		}

		private bool ContainsSqlFiles(string folder)
		{
			foreach (string file in Directory.GetFiles(folder, "*.sql"))
			{
				return true; // if the folder contains 1 .sql file, that's good enough
			}

			foreach (string child in Directory.GetDirectories(folder))
			{
				if (ContainsSqlFiles(child))
					return true; // if 1 child folder contains a .sql file, still good enough
			}

			return false;
		}

		private void WriteDatabaseFoldersAndFiles(IndentedTextWriter writer, string folder)
		{
			foreach (string child in Directory.GetDirectories(folder))
			{
				if (ContainsSqlFiles(child))
				{
					writer.WriteLine("Begin Folder = \"{0}\"", Path.GetFileName(child));
					writer.Indent++;
					WriteDatabaseFoldersAndFiles(writer, child);
					writer.Indent--;
					writer.WriteLine("End");
				}
			}
			foreach (string file in Directory.GetFiles(folder, "*.sql"))
			{
				writer.WriteLine("Script = \"{0}\"", Path.GetFileName(file));
			}
		}

		private void CleanProject(ProjectNode project)
		{
			kernel.Log.Write("...Cleaning project: {0}", project.Name);

			ToolInfo toolInfo = (ToolInfo)tools[project.Language];
			string projectFile = Helper.MakeFilePath(project.FullPath, project.Name, toolInfo.FileExtension);
			string userFile = projectFile + ".user";

			Helper.DeleteIfExists(projectFile);
			Helper.DeleteIfExists(userFile);
		}

		private void CleanSolution(SolutionNode solution)
		{
			kernel.Log.Write("Cleaning {0} solution and project files", this.VersionName, solution.Name);

			string slnFile = Helper.MakeFilePath(solution.FullPath, solution.Name, "sln");
			string suoFile = Helper.MakeFilePath(solution.FullPath, solution.Name, "suo");

			Helper.DeleteIfExists(slnFile);
			Helper.DeleteIfExists(suoFile);

			foreach (ProjectNode project in solution.Projects)
			{
				CleanProject(project);
			}

			kernel.Log.Write("");
		}

		#endregion

		#region ITarget Members

		/// <summary>
		/// Writes the specified kern.
		/// </summary>
		/// <param name="kern">The kern.</param>
		public virtual void Write(Kernel kern)
		{
			if (kern == null)
			{
				throw new ArgumentNullException("kern");
			}
			kernel = kern;
			foreach (SolutionNode sol in kernel.Solutions)
			{
				WriteSolution(sol, true);
			}
			kernel = null;
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
			kernel = kern;
			foreach (SolutionNode sol in kernel.Solutions)
			{
				CleanSolution(sol);
			}
			kernel = null;
		}

		#endregion
	}
}
