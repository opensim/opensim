#region BSD License
/*
Copyright (c) 2004-2008
Matthew Holmes (matthew@wildfiregames.com),
Dan Moorehead (dan05a@gmail.com),
Rob Loach (http://www.robloach.net),
C.J. Adams-Collier (cjac@colliertech.org)

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

#define NO_VALIDATE

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using Prebuild.Core.Nodes;
using Prebuild.Core.Utilities;

namespace Prebuild.Core
{
    /// <summary>
    ///
    /// </summary>
    public class Kernel : IDisposable
    {
        #region Inner Classes

        private struct NodeEntry
        {
            public Type Type;
            public DataNodeAttribute Attribute;
        }

        #endregion

        #region Fields

        private static readonly Kernel m_Instance = new Kernel();

        /// <summary>
        /// This must match the version of the schema that is embeeded
        /// </summary>
        private const string m_SchemaVersion = "1.10";
        private const string m_Schema = "prebuild-" + m_SchemaVersion + ".xsd";
        private const string m_SchemaURI = "http://dnpb.sourceforge.net/schemas/" + m_Schema;
        bool disposed;
        private Version m_Version;
        private const string m_Revision = "";
        private CommandLineCollection m_CommandLine;
        private Log m_Log;
        private CurrentDirectory m_CurrentWorkingDirectory;
        private XmlSchemaCollection m_Schemas;

        private readonly Dictionary<string, ITarget> m_Targets = new Dictionary<string, ITarget>();
        private readonly Dictionary<string, NodeEntry> m_Nodes = new Dictionary<string, NodeEntry>();

        readonly List<SolutionNode> m_Solutions = new List<SolutionNode>();
        string m_Target;
        string m_Clean;
        string[] m_RemoveDirectories;
        XmlDocument m_CurrentDoc;
        bool m_PauseAfterFinish;
        string[] m_ProjectGroups;

        #endregion

        #region Constructors

        private Kernel()
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether [pause after finish].
        /// </summary>
        /// <value><c>true</c> if [pause after finish]; otherwise, <c>false</c>.</value>
        public bool PauseAfterFinish
        {
            get
            {
                return m_PauseAfterFinish;
            }
        }

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <value>The instance.</value>
        public static Kernel Instance
        {
            get
            {
                return m_Instance;
            }
        }

        /// <summary>
        /// Gets the version.
        /// </summary>
        /// <value>The version.</value>
        public string Version
        {
            get
            {
                return String.Format("{0}.{1}.{2}{3}", m_Version.Major, m_Version.Minor, m_Version.Build, m_Revision);
            }
        }

        /// <summary>
        /// Gets the command line.
        /// </summary>
        /// <value>The command line.</value>
        public CommandLineCollection CommandLine
        {
            get
            {
                return m_CommandLine;
            }
        }

        /// <summary>
        /// Gets the targets.
        /// </summary>
        /// <value>The targets.</value>
        public Dictionary<string, ITarget> Targets
        {
            get
            {
                return m_Targets;
            }
        }

        /// <summary>
        /// Gets the log.
        /// </summary>
        /// <value>The log.</value>
        public Log Log
        {
            get
            {
                return m_Log;
            }
        }

        /// <summary>
        /// Gets the current working directory.
        /// </summary>
        /// <value>The current working directory.</value>
        public CurrentDirectory CurrentWorkingDirectory
        {
            get
            {
                return m_CurrentWorkingDirectory;
            }
        }

        /// <summary>
        /// Gets the solutions.
        /// </summary>
        /// <value>The solutions.</value>
        public List<SolutionNode> Solutions
        {
            get
            {
                return m_Solutions;
            }
        }

        /// <summary>
        /// Gets the XmlDocument object representing the prebuild.xml
        /// being processed
        /// </summary>
        /// <value>The XmlDocument object</value>
        public XmlDocument CurrentDoc
        {
            get
            {
                return m_CurrentDoc;
            }
        }

        #endregion

        #region Private Methods

        private static void RemoveDirectories(string rootDir, string[] dirNames)
        {
            foreach(string dir in Directory.GetDirectories(rootDir))
            {
                string simpleName = Path.GetFileName(dir);

                if(Array.IndexOf(dirNames, simpleName) != -1)
                {
                    //delete if the name matches one of the directory names to delete
                    string fullDirPath = Path.GetFullPath(dir);
                    Directory.Delete(fullDirPath,true);
                }
                else//not a match, so check children
                {
                    RemoveDirectories(dir,dirNames);
                    //recurse, checking children for them
                }
            }
        }

//		private void RemoveDirectoryMatches(string rootDir, string dirPattern)
//		{
//			foreach(string dir in Directory.GetDirectories(rootDir))
//			{
//				foreach(string match in Directory.GetDirectories(dir))
//				{//delete all child directories that match
//					Directory.Delete(Path.GetFullPath(match),true);
//				}
//				//recure through the rest checking for nested matches to delete
//				RemoveDirectoryMatches(dir,dirPattern);
//			}
//		}

        private void LoadSchema()
        {
            Assembly assembly = GetType().Assembly;
            Stream stream = assembly.GetManifestResourceStream("Prebuild.data." + m_Schema);
            if(stream == null)
            {
                //try without the default namespace prepending to it in case was compiled with SharpDevelop or MonoDevelop instead of Visual Studio .NET
                stream = assembly.GetManifestResourceStream(m_Schema);
                if(stream == null)
                {
                    throw new System.Reflection.TargetException(string.Format("Could not find the scheme embedded resource file '{0}'.", m_Schema));
                }
            }
            XmlReader schema = new XmlTextReader(stream);

            m_Schemas = new XmlSchemaCollection();
            m_Schemas.Add(m_SchemaURI, schema);
        }

        private void CacheVersion()
        {
            m_Version = Assembly.GetEntryAssembly().GetName().Version;
        }

        private void CacheTargets(Assembly assm)
        {
            foreach(Type t in assm.GetTypes())
            {
                TargetAttribute ta = (TargetAttribute)Helper.CheckType(t, typeof(TargetAttribute), typeof(ITarget));

                if(ta == null)
                    continue;

                if (t.IsAbstract)
                    continue;

                ITarget target = (ITarget)assm.CreateInstance(t.FullName);
                if (target == null)
                {
                    throw new MissingMethodException("Could not create ITarget instance");
                }

                m_Targets[ta.Name] = target;
            }
        }

        private void CacheNodeTypes(Assembly assm)
        {
            foreach(Type t in assm.GetTypes())
            {
                foreach (DataNodeAttribute dna in t.GetCustomAttributes(typeof(DataNodeAttribute), true))
                {
                    NodeEntry ne = new NodeEntry();
                    ne.Type = t;
                    ne.Attribute = dna;
                    m_Nodes[dna.Name] = ne;
                }
            }
        }

        private void LogBanner()
        {
            m_Log.Write("Prebuild v" + Version);
            m_Log.Write("Copyright (c) 2004-2010");
            m_Log.Write("Matthew Holmes (matthew@wildfiregames.com),");
            m_Log.Write("Dan Moorehead (dan05a@gmail.com),");
            m_Log.Write("David Hudson (jendave@yahoo.com),");
            m_Log.Write("Rob Loach (http://www.robloach.net),");
            m_Log.Write("C.J. Adams-Collier (cjac@colliertech.org),");
            m_Log.Write("John Hurliman (john.hurliman@intel.com),");

            m_Log.Write("See 'prebuild /usage' for help");
            m_Log.Write();
        }



        private void ProcessFile(string file)
        {
            ProcessFile(file, m_Solutions);
        }

        public void ProcessFile(ProcessNode node, SolutionNode parent)
        {
            if (node.IsValid)
            {
                List<SolutionNode> list = new List<SolutionNode>();
                ProcessFile(node.Path, list);

                foreach (SolutionNode solution in list)
                    parent.SolutionsTable[solution.Name] = solution;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="file"></param>
        /// <param name="solutions"></param>
        /// <returns></returns>
        public void ProcessFile(string file, IList<SolutionNode> solutions)
        {
            m_CurrentWorkingDirectory.Push();

            string path = file;
            try
            {
                try
                {
                    path = Helper.ResolvePath(path);
                }
                catch(ArgumentException)
                {
                    m_Log.Write("Could not open Prebuild file: " + path);
                    m_CurrentWorkingDirectory.Pop();
                    return;
                }

                Helper.SetCurrentDir(Path.GetDirectoryName(path));

                XmlTextReader reader = new XmlTextReader(path);

                Core.Parse.Preprocessor pre = new Core.Parse.Preprocessor();

                //register command line arguments as XML variables
                IEnumerator<KeyValuePair<string, string>> dict = m_CommandLine.GetEnumerator();
                while (dict.MoveNext())
                {
                    string name = dict.Current.Key.Trim();
                    if (name.Length > 0)
                        pre.RegisterVariable(name, dict.Current.Value);
                }

                string xml = pre.Process(reader);//remove script and evaulate pre-proccessing to get schema-conforming XML

                // See if the user put into a pseudo target of "prebuild:preprocessed-input" to indicate they want to see the
                // output before the system processes it.
                if (m_CommandLine.WasPassed("ppi"))
                {
                    // Get the filename if there is one, otherwise use a default.
                    string ppiFile = m_CommandLine["ppi"];
                    if (ppiFile == null || ppiFile.Trim().Length == 0)
                    {
                        ppiFile = "preprocessed-input.xml";
                    }

                    // Write out the string to the given stream.
                    try
                    {
                        using (StreamWriter ppiWriter = new StreamWriter(ppiFile))
                        {
                            ppiWriter.WriteLine(xml);
                        }
                    }
                    catch(IOException ex)
                    {
                        Console.WriteLine("Could not write PPI file '{0}': {1}", ppiFile, ex.Message);
                    }

                    // Finish processing this special tag.
                    return;
                }

                m_CurrentDoc = new XmlDocument();
                try
                {
#if NO_VALIDATE
                    XmlReader validator = XmlReader.Create(new StringReader(xml));
                    m_CurrentDoc.Load(validator);
#else
                    XmlValidatingReader validator = new XmlValidatingReader(new XmlTextReader(new StringReader(xml)));

                    //validate while reading from string into XmlDocument DOM structure in memory
                    foreach(XmlSchema schema in m_Schemas)
                    {
                        validator.Schemas.Add(schema);
                    }
                    m_CurrentDoc.Load(validator);
#endif
                }
                catch(XmlException e)
                {
                    throw new XmlException(e.ToString());
                }

                //is there a purpose to writing it?  An syntax/schema problem would have been found during pre.Process() and reported with details
                if(m_CommandLine.WasPassed("ppo"))
                {
                    string ppoFile = m_CommandLine["ppo"];
                    if(ppoFile == null || ppoFile.Trim().Length < 1)
                    {
                        ppoFile = "preprocessed.xml";
                    }

                    StreamWriter writer = null;
                    try
                    {
                        writer = new StreamWriter(ppoFile);
                        writer.Write(xml);
                    }
                    catch(IOException ex)
                    {
                        Console.WriteLine("Could not write PPO file '{0}': {1}", ppoFile, ex.Message);
                    }
                    finally
                    {
                        if(writer != null)
                        {
                            writer.Close();
                        }
                    }
                    return;
                }
                //start reading the xml config file
                XmlElement rootNode = m_CurrentDoc.DocumentElement;
                //string suggestedVersion = Helper.AttributeValue(rootNode,"version","1.0");
                Helper.CheckForOSVariables = Helper.ParseBoolean(rootNode,"checkOsVars",false);

                foreach(XmlNode node in rootNode.ChildNodes)//solutions or if pre-proc instructions
                {
                    IDataNode dataNode = ParseNode(node, null);
                    if(dataNode is ProcessNode)
                    {
                        ProcessNode proc = (ProcessNode)dataNode;
                        if(proc.IsValid)
                        {
                            ProcessFile(proc.Path);
                        }
                    }
                    else if(dataNode is SolutionNode)
                    {
                        solutions.Add((SolutionNode)dataNode);
                    }
                }
            }
            catch(XmlSchemaException xse)
            {
                m_Log.Write("XML validation error at line {0} in {1}:\n\n{2}",
                    xse.LineNumber, path, xse.Message);
            }
            finally
            {
                m_CurrentWorkingDirectory.Pop();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Allows the project.
        /// </summary>
        /// <param name="projectGroupsFlags">The project groups flags.</param>
        /// <returns></returns>
        public bool AllowProject(string projectGroupsFlags)
        {
            if(m_ProjectGroups != null && m_ProjectGroups.Length > 0)
            {
                if(projectGroupsFlags != null && projectGroupsFlags.Length == 0)
                {
                    foreach(string group in projectGroupsFlags.Split('|'))
                    {
                        if(Array.IndexOf(m_ProjectGroups, group) != -1) //if included in the filter list
                        {
                            return true;
                        }
                    }
                }
                return false;//not included in the list or no groups specified for the project
            }
            return true;//no filter specified in the command line args
        }

        /// <summary>
        /// Gets the type of the node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns></returns>
        public Type GetNodeType(XmlNode node)
        {
            if( node == null )
            {
                throw new ArgumentNullException("node");
            }
            if(!m_Nodes.ContainsKey(node.Name))
            {
                return null;
            }

            NodeEntry ne = m_Nodes[node.Name];
            return ne.Type;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="node"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public IDataNode ParseNode(XmlNode node, IDataNode parent)
        {
            return ParseNode(node, parent, null);
        }

        //Create an instance of the data node type that is mapped to the name of the xml DOM node
        /// <summary>
        /// Parses the node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="parent">The parent.</param>
        /// <param name="preNode">The pre node.</param>
        /// <returns></returns>
        public IDataNode ParseNode(XmlNode node, IDataNode parent, IDataNode preNode)
        {
            IDataNode dataNode;

            try
            {
                if( node == null )
                {
                    throw new ArgumentNullException("node");
                }
                if(preNode == null)
                {
                    if(!m_Nodes.ContainsKey(node.Name))
                    {
                        Console.WriteLine("WARNING: Unknown XML node: " + node.Name);
                        return null;
                    }

                    NodeEntry ne = m_Nodes[node.Name];
                    Type type = ne.Type;
                    //DataNodeAttribute dna = ne.Attribute;

                    dataNode = (IDataNode)type.Assembly.CreateInstance(type.FullName);
                    if(dataNode == null)
                    {
                        throw new System.Reflection.TargetException("Could not create new parser instance: " + type.FullName);
                    }
                }
                else
                    dataNode = preNode;

                dataNode.Parent = parent;
                dataNode.Parse(node);
            }
            catch(WarningException wex)
            {
                m_Log.Write(LogType.Warning, wex.Message);
                return null;
            }
            catch(FatalException fex)
            {
                m_Log.WriteException(LogType.Error, fex);
                throw;
            }
            catch(Exception ex)
            {
                m_Log.WriteException(LogType.Error, ex);
                throw;
            }

            return dataNode;
        }

        /// <summary>
        /// Initializes the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="args">The args.</param>
        public void Initialize(LogTargets target, string[] args)
        {
            CacheTargets(GetType().Assembly);
            CacheNodeTypes(GetType().Assembly);
            CacheVersion();

            m_CommandLine = new CommandLineCollection(args);

            string logFile = null;
            if(m_CommandLine.WasPassed("log"))
            {
                logFile = m_CommandLine["log"];

                if(logFile != null && logFile.Length == 0)
                {
                    logFile = "Prebuild.log";
                }
            }
            else
            {
                target = target & ~LogTargets.File;	//dont output to a file
            }

            m_Log = new Log(target, logFile);
            LogBanner();

            m_CurrentWorkingDirectory = new CurrentDirectory();

            m_Target = m_CommandLine["target"];
            m_Clean = m_CommandLine["clean"];
            string removeDirs = m_CommandLine["removedir"];
            if(removeDirs != null && removeDirs.Length == 0)
            {
                m_RemoveDirectories = removeDirs.Split('|');
            }

            string flags = m_CommandLine["allowedgroups"];//allows filtering by specifying a pipe-delimited list of groups to include
            if(flags != null && flags.Length == 0)
            {
                m_ProjectGroups = flags.Split('|');
            }
            m_PauseAfterFinish = m_CommandLine.WasPassed("pause");

            LoadSchema();
        }

        /// <summary>
        /// Processes this instance.
        /// </summary>
        public void Process()
        {
            bool perfomedOtherTask = false;
            if(m_RemoveDirectories != null && m_RemoveDirectories.Length > 0)
            {
                try
                {
                    RemoveDirectories(".",m_RemoveDirectories);
                }
                catch(IOException e)
                {
                    m_Log.Write("Failed to remove directories named {0}",m_RemoveDirectories);
                    m_Log.WriteException(LogType.Error,e);
                }
                catch(UnauthorizedAccessException e)
                {
                    m_Log.Write("Failed to remove directories named {0}",m_RemoveDirectories);
                    m_Log.WriteException(LogType.Error,e);
                }
                perfomedOtherTask = true;
            }

            if(m_Target != null && m_Clean != null)
            {
                m_Log.Write(LogType.Error, "The options /target and /clean cannot be passed together");
                return;
            }

            if(m_Target == null && m_Clean == null)
            {
                if(perfomedOtherTask) //finished
                {
                    return;
                }
                m_Log.Write(LogType.Error, "Must pass either /target or /clean to process a Prebuild file");
                return;
            }

            string file = "./prebuild.xml";
            if(m_CommandLine.WasPassed("file"))
            {
                file = m_CommandLine["file"];
            }

            ProcessFile(file);

            string target = (m_Target != null ? m_Target.ToLower() : m_Clean.ToLower());
            bool clean = (m_Target == null);
            if(clean && target != null && target.Length == 0)
            {
                target = "all";
            }
            if(clean && target == "all")//default to all if no target was specified for clean
            {
                //check if they passed yes
                if (!m_CommandLine.WasPassed("yes"))
                {
                    Console.WriteLine("WARNING: This operation will clean ALL project files for all targets, are you sure? (y/n):");
                    string ret = Console.ReadLine();
                    if(ret == null)
                    {
                        return;
                    }
                    ret = ret.Trim().ToLower();
                    if((ret.ToLower() != "y" && ret.ToLower() != "yes"))
                    {
                        return;
                    }
                }
                //clean all targets (just cleaning vs2002 target didn't clean nant)
                foreach(ITarget targ in m_Targets.Values)
                {
                    targ.Clean(this);
                }
            }
            else
            {
                if (!m_Targets.ContainsKey(target)) {
                    m_Log.Write(LogType.Error, "Unknown Target \"{0}\"", target);
                    return;
                }
                ITarget targ = m_Targets[target];

                if(clean)
                {
                    targ.Clean(this);
                }
                else
                {
                    targ.Write(this);
                }
            }

            m_Log.Flush();
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        ///
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose objects
        /// </summary>
        /// <param name="disposing">
        /// If true, it will dispose close the handle
        /// </param>
        /// <remarks>
        /// Will dispose managed and unmanaged resources.
        /// </remarks>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                    if (m_Log != null)
                    {
                        m_Log.Close();
                        m_Log = null;
                    }
                }
            }
            disposed = true;
        }

        /// <summary>
        ///
        /// </summary>
        ~Kernel()
        {
            Dispose(false);
        }

        /// <summary>
        /// Closes and destroys this object
        /// </summary>
        /// <remarks>
        /// Same as Dispose(true)
        /// </remarks>
        public void Close()
        {
            Dispose();
        }

        #endregion
    }
}
