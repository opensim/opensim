using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using log4net;
using Microsoft.CSharp;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public class MRMModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Scene m_scene;
        
        private readonly Dictionary<UUID,MRMBase> m_scripts = new Dictionary<UUID, MRMBase>();

        private static readonly CSharpCodeProvider CScodeProvider = new CSharpCodeProvider();

        public void Initialise(Scene scene, IConfigSource source)
        {
            if (source.Configs["MRM"] != null)
            {
                if (source.Configs["MRM"].GetBoolean("Enabled", false))
                {
                    m_log.Info("[MRM] Enabling MRM Module");
                    m_scene = scene;
                    scene.EventManager.OnRezScript += EventManager_OnRezScript;
                }
                else
                {
                    m_log.Info("[MRM] Disabled MRM Module (Express)");
                }
            }
            else
            {
                m_log.Info("[MRM] Disabled MRM Module (Omission)");
            }
        }

        void EventManager_OnRezScript(uint localID, UUID itemID, string script, int startParam, bool postOnRez, string engine, int stateSource)
        {
            if (script.StartsWith("//MiniMod:C#"))
            {
                m_log.Info("[MRM] Found C# MRM");
                IWorld m_world = new World(m_scene);
                IHost m_host = new Host(new SOPObject(m_scene, localID));

                MRMBase mmb = (MRMBase) AppDomain.CurrentDomain.CreateInstanceFromAndUnwrap(
                                                          CompileFromDotNetText(script, itemID.ToString()),
                                                          "OpenSim.MiniModule");
                m_log.Info("[MRM] Created MRM Instance");
                mmb.InitMiniModule(m_world, m_host);
                m_scripts[itemID] = mmb;

                m_log.Info("[MRM] Starting MRM");
                mmb.Start();
            }
        }

        public void PostInitialise()
        {
            
        }

        public void Close()
        {
            foreach (KeyValuePair<UUID, MRMBase> pair in m_scripts)
            {
                pair.Value.Stop();
            }
        }

        public string Name
        {
            get { return "MiniRegionModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        /// <summary>
        /// Stolen from ScriptEngine Common
        /// </summary>
        /// <param name="Script"></param>
        /// <param name="uuid">Unique ID for this module</param>
        /// <returns></returns>
        internal string CompileFromDotNetText(string Script, string uuid)
        {
            const string ext = ".cs";
            const string FilePrefix = "MiniModule";

            // Output assembly name
            string OutFile = Path.Combine("MiniModules", Path.Combine(
                                                             m_scene.RegionInfo.RegionID.ToString(),
                                                             FilePrefix + "_compiled_" + uuid + ".dll"));

            // Create Directories for Assemblies
            if (!Directory.Exists("MiniModules"))
                Directory.CreateDirectory("MiniModules");
            string tmp = Path.Combine("MiniModules", m_scene.RegionInfo.RegionID.ToString());
            if (!Directory.Exists(tmp))
                Directory.CreateDirectory(tmp);

            try
            {
                File.Delete(OutFile);
            }
            catch (IOException e)
            {
                throw new Exception("Unable to delete old existing " +
                                    "script-file before writing new. Compile aborted: " +
                                    e);
            }

            // DEBUG - write source to disk
            string srcFileName = FilePrefix + "_source_" +
                                 Path.GetFileNameWithoutExtension(OutFile) + ext;
            try
            {
                File.WriteAllText(Path.Combine(Path.Combine(
                                                   "MiniModules",
                                                   m_scene.RegionInfo.RegionID.ToString()),
                                               srcFileName), Script);
            }
            catch (Exception ex) //NOTLEGIT - Should be just FileIOException
            {
                m_log.Error("[Compiler]: Exception while " +
                            "trying to write script source to file \"" +
                            srcFileName + "\": " + ex.ToString());
            }

            // Do actual compile
            CompilerParameters parameters = new CompilerParameters();

            parameters.IncludeDebugInformation = true;

            string rootPath =
                Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);


            // TODO: Add Libraries
            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath,
                                                             "OpenSim.Region.OptionalModules.dll"));
            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath,
                                                             "log4net.dll"));

            parameters.GenerateExecutable = false;
            parameters.OutputAssembly = OutFile;
            parameters.IncludeDebugInformation = true;
            parameters.TreatWarningsAsErrors = false;

            CompilerResults results = CScodeProvider.CompileAssemblyFromSource(
                parameters, Script);

            int display = 5;
            if (results.Errors.Count > 0)
            {
                string errtext = String.Empty;
                foreach (CompilerError CompErr in results.Errors)
                {
                    // Show 5 errors max
                    //
                    if (display <= 0)
                        break;
                    display--;

                    string severity = "Error";
                    if (CompErr.IsWarning)
                    {
                        severity = "Warning";
                    }

                    string text = CompErr.ErrorText;

                    // The Second Life viewer's script editor begins
                    // countingn lines and columns at 0, so we subtract 1.
                    errtext += String.Format("Line ({0},{1}): {4} {2}: {3}\n",
                                             CompErr.Line - 1, CompErr.Column - 1,
                                             CompErr.ErrorNumber, text, severity);
                }

                if (!File.Exists(OutFile))
                {
                    throw new Exception(errtext);
                }
            }

            if (!File.Exists(OutFile))
            {
                string errtext = String.Empty;
                errtext += "No compile error. But not able to locate compiled file.";
                throw new Exception(errtext);
            }

            FileInfo fi = new FileInfo(OutFile);

            Byte[] data = new Byte[fi.Length];

            try
            {
                FileStream fs = File.Open(OutFile, FileMode.Open, FileAccess.Read);
                fs.Read(data, 0, data.Length);
                fs.Close();
            }
            catch (IOException)
            {
                string errtext = String.Empty;
                errtext += "No compile error. But not able to open file.";
                throw new Exception(errtext);
            }

            // Convert to base64
            //
            string filetext = Convert.ToBase64String(data);

            ASCIIEncoding enc = new ASCIIEncoding();

            Byte[] buf = enc.GetBytes(filetext);

            FileStream sfs = File.Create(OutFile + ".cil.b64");
            sfs.Write(buf, 0, buf.Length);
            sfs.Close();

            return OutFile;
        }
    }
}
