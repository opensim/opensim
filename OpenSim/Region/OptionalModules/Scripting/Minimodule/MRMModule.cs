/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

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
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public class MRMModule : IRegionModule, IMRMModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Scene m_scene;
        
        private readonly Dictionary<UUID,MRMBase> m_scripts = new Dictionary<UUID, MRMBase>();

        private readonly Dictionary<Type,object> m_extensions = new Dictionary<Type, object>();

        private static readonly CSharpCodeProvider CScodeProvider = new CSharpCodeProvider();

        private readonly MicroScheduler m_microthreads = new MicroScheduler();

        public void RegisterExtension<T>(T instance)
        {
            m_extensions[typeof (T)] = instance;
        }

        public void Initialise(Scene scene, IConfigSource source)
        {
            if (source.Configs["MRM"] != null)
            {
                if (source.Configs["MRM"].GetBoolean("Enabled", false))
                {
                    m_log.Info("[MRM] Enabling MRM Module");
                    m_scene = scene;
                    scene.EventManager.OnRezScript += EventManager_OnRezScript;
                    scene.EventManager.OnFrame += EventManager_OnFrame;

                    scene.RegisterModuleInterface<IMRMModule>(this);
                }
                else
                {
                    m_log.Info("[MRM] Disabled MRM Module (Disabled in ini)");
                }
            }
            else
            {
                m_log.Info("[MRM] Disabled MRM Module (Default disabled)");
            }
        }

        void EventManager_OnFrame()
        {
            m_microthreads.Tick(1000);
        }

        static string ConvertMRMKeywords(string script)
        {
            script = script.Replace("microthreaded void ", "IEnumerable");
            script = script.Replace("relax;", "yield return null;");

            return script;
        }

        void EventManager_OnRezScript(uint localID, UUID itemID, string script, int startParam, bool postOnRez, string engine, int stateSource)
        {
            if (script.StartsWith("//MRM:C#"))
            {
                if (m_scene.GetSceneObjectPart(localID).OwnerID != m_scene.RegionInfo.MasterAvatarAssignedUUID
                    ||
                    m_scene.GetSceneObjectPart(localID).CreatorID != m_scene.RegionInfo.MasterAvatarAssignedUUID)
                    return;

                script = ConvertMRMKeywords(script);

                try
                {
                    m_log.Info("[MRM] Found C# MRM");

                    MRMBase mmb = (MRMBase)AppDomain.CurrentDomain.CreateInstanceFromAndUnwrap(
                                                CompileFromDotNetText(script, itemID.ToString()),
                                                "OpenSim.MiniModule");

                    InitializeMRM(mmb, localID, itemID);

                    m_scripts[itemID] = mmb;

                    m_log.Info("[MRM] Starting MRM");
                    mmb.Start();
                }
                catch (UnauthorizedAccessException e)
                {
                    m_log.Error("[MRM] UAE " + e.Message);
                    m_log.Error("[MRM] " + e.StackTrace);

                    if (e.InnerException != null)
                        m_log.Error("[MRM] " + e.InnerException);

                    m_scene.Broadcast(delegate(IClientAPI user)
                    {
                        user.SendAlertMessage(
                            "MRM UnAuthorizedAccess: " + e);
                    });
                }
                catch (Exception e)
                {
                    m_log.Info("[MRM] Error: " + e);
                    m_scene.Broadcast(delegate(IClientAPI user)
                                          {
                                              user.SendAlertMessage(
                                                  "Compile error while building MRM script, check OpenSim console for more information.");
                                          });
                }
            }
        }

        public void InitializeMRM(MRMBase mmb, uint localID, UUID itemID)
        {

            m_log.Info("[MRM] Created MRM Instance");

            IWorld m_world = new World(m_scene);
            IHost m_host = new Host(new SOPObject(m_scene, localID), m_scene, new ExtensionHandler(m_extensions),
                                    m_microthreads);

            mmb.InitMiniModule(m_world, m_host, itemID);

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
            m_log.Info("MRM 1");
            const string ext = ".cs";
            const string FilePrefix = "MiniModule";

            // Output assembly name
            string OutFile = Path.Combine("MiniModules", Path.Combine(
                                                             m_scene.RegionInfo.RegionID.ToString(),
                                                             FilePrefix + "_compiled_" + uuid + "_" +
                                                             Util.RandomClass.Next(9000) + ".dll"));

            // Create Directories for Assemblies
            if (!Directory.Exists("MiniModules"))
                Directory.CreateDirectory("MiniModules");
            string tmp = Path.Combine("MiniModules", m_scene.RegionInfo.RegionID.ToString());
            if (!Directory.Exists(tmp))
                Directory.CreateDirectory(tmp);


            m_log.Info("MRM 2");

            try
            {
                File.Delete(OutFile);
            }
            catch (UnauthorizedAccessException e)
            {
                throw new Exception("Unable to delete old existing " +
                                    "script-file before writing new. Compile aborted: " +
                                    e);
            }
            catch (IOException e)
            {
                throw new Exception("Unable to delete old existing " +
                                    "script-file before writing new. Compile aborted: " +
                                    e);
            }

            m_log.Info("MRM 3");

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
                            srcFileName + "\": " + ex);
            }

            m_log.Info("MRM 4");

            // Do actual compile
            CompilerParameters parameters = new CompilerParameters();

            parameters.IncludeDebugInformation = true;

            string rootPath =
                Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);

            List<string> libraries = new List<string>();
            string[] lines = Script.Split(new string[] {"\n"}, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in lines)
            {
                if (s.StartsWith("//@DEPENDS:"))
                {
                    libraries.Add(s.Replace("//@DEPENDS:", ""));
                }
            }

            libraries.Add("OpenSim.Region.OptionalModules.dll");
            libraries.Add("OpenMetaverseTypes.dll");
            libraries.Add("log4net.dll");

            foreach (string library in libraries)
            {
                parameters.ReferencedAssemblies.Add(Path.Combine(rootPath, library));
            }

            parameters.GenerateExecutable = false;
            parameters.OutputAssembly = OutFile;
            parameters.IncludeDebugInformation = true;
            parameters.TreatWarningsAsErrors = false;

            m_log.Info("MRM 5");

            CompilerResults results = CScodeProvider.CompileAssemblyFromSource(
                parameters, Script);

            m_log.Info("MRM 6");

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

            m_log.Info("MRM 7");

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

            m_log.Info("MRM 8");

            // Convert to base64
            //
            string filetext = Convert.ToBase64String(data);

            ASCIIEncoding enc = new ASCIIEncoding();

            Byte[] buf = enc.GetBytes(filetext);

            m_log.Info("MRM 9");

            FileStream sfs = File.Create(OutFile + ".cil.b64");
            sfs.Write(buf, 0, buf.Length);
            sfs.Close();

            m_log.Info("MRM 10");

            return OutFile;
        }
    }
}
