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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Timers;
using log4net;
using Nini.Config;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Framework.Scenes;
using PumaCode.SvnDotNet.AprSharp;
using PumaCode.SvnDotNet.SubversionSharp;
using Slash = System.IO.Path;

namespace OpenSim.Region.Modules.SvnSerialiser
{
    public class SvnBackupModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly List<Scene> m_scenes = new List<Scene>();
        private readonly Timer m_timer = new Timer();

        private bool m_enabled = false;
        private bool m_installBackupOnLoad = false;
        private IRegionSerialiserModule m_serialiser;
        private bool m_svnAutoSave = false;
        private SvnClient m_svnClient;
        private string m_svndir = "SVNmodule" + Slash.DirectorySeparatorChar + "repo";
        private string m_svnpass = "password";

        private TimeSpan m_svnperiod = new TimeSpan(0, 0, 15, 0, 0);
        private string m_svnurl = "svn://insert.Your.svn/here/";
        private string m_svnuser = "username";

        #region SvnModule Core

        /// <summary>
        /// Exports a specified scene to the SVN repo directory, then commits.
        /// </summary>
        /// <param name="scene">The scene to export</param>
        public void SaveRegion(Scene scene)
        {
            List<string> svnfilenames = CreateAndAddExport(scene);

            m_svnClient.Commit3(svnfilenames, true, false);
            m_log.Info("[SVNBACKUP]: Region backup successful (" + scene.RegionInfo.RegionName + ").");
        }

        /// <summary>
        /// Saves all registered scenes to the SVN repo, then commits.
        /// </summary>
        public void SaveAllRegions()
        {
            List<string> svnfilenames = new List<string>();
            List<string> regions = new List<string>();

            foreach (Scene scene in m_scenes)
            {
                svnfilenames.AddRange(CreateAndAddExport(scene));
                regions.Add("'" + scene.RegionInfo.RegionName + "' ");
            }

            m_svnClient.Commit3(svnfilenames, true, false);
            m_log.Info("[SVNBACKUP]: Server backup successful (" + String.Concat(regions.ToArray()) + ").");
        }

        private List<string> CreateAndAddExport(Scene scene)
        {
            m_log.Info("[SVNBACKUP]: Saving a region to SVN with name " + scene.RegionInfo.RegionName);

            List<string> filenames = m_serialiser.SerialiseRegion(scene, m_svndir + Slash.DirectorySeparatorChar + scene.RegionInfo.RegionID + Slash.DirectorySeparatorChar);

            try
            {
                m_svnClient.Add3(m_svndir + Slash.DirectorySeparatorChar + scene.RegionInfo.RegionID, true, false, false);
            }
            catch (SvnException)
            {
            }

            List<string> svnfilenames = new List<string>();
            foreach (string filename in filenames)
                svnfilenames.Add(m_svndir + Slash.DirectorySeparatorChar + scene.RegionInfo.RegionID + Slash.DirectorySeparatorChar + filename);
            svnfilenames.Add(m_svndir + Slash.DirectorySeparatorChar + scene.RegionInfo.RegionID);

            return svnfilenames;
        }

        public void LoadRegion(Scene scene)
        {
            IRegionSerialiserModule serialiser = scene.RequestModuleInterface<IRegionSerialiserModule>();
            if (serialiser != null)
            {
                serialiser.LoadPrimsFromXml2(
                    scene,
                    m_svndir + Slash.DirectorySeparatorChar + scene.RegionInfo.RegionID 
                        + Slash.DirectorySeparatorChar + "objects.xml");
            
                scene.RequestModuleInterface<ITerrainModule>().LoadFromFile(
                    m_svndir + Slash.DirectorySeparatorChar + scene.RegionInfo.RegionID 
                        + Slash.DirectorySeparatorChar + "heightmap.r32");
            
                m_log.Info("[SVNBACKUP]: Region load successful (" + scene.RegionInfo.RegionName + ").");
            }
            else
            {
                m_log.ErrorFormat(
                    "[SVNBACKUP]: Region load of {0} failed - no serialisation module available", 
                    scene.RegionInfo.RegionName);
            }
        }

        private void CheckoutSvn()
        {
            m_svnClient.Checkout2(m_svnurl, m_svndir, Svn.Revision.Head, Svn.Revision.Head, true, false);
        }

        private void CheckoutSvn(SvnRevision revision)
        {
            m_svnClient.Checkout2(m_svnurl, m_svndir, revision, revision, true, false);
        }

        // private void CheckoutSvnPartial(string subdir)
        // {
        //     if (!Directory.Exists(m_svndir + Slash.DirectorySeparatorChar + subdir))
        //         Directory.CreateDirectory(m_svndir + Slash.DirectorySeparatorChar + subdir);

        //     m_svnClient.Checkout2(m_svnurl + "/" + subdir, m_svndir, Svn.Revision.Head, Svn.Revision.Head, true, false);
        // }

        // private void CheckoutSvnPartial(string subdir, SvnRevision revision)
        // {
        //     if (!Directory.Exists(m_svndir + Slash.DirectorySeparatorChar + subdir))
        //         Directory.CreateDirectory(m_svndir + Slash.DirectorySeparatorChar + subdir);

        //     m_svnClient.Checkout2(m_svnurl + "/" + subdir, m_svndir, revision, revision, true, false);
        // }

        #endregion

        #region SvnDotNet Callbacks

        private SvnError SimpleAuth(out SvnAuthCredSimple svnCredentials, IntPtr baton,
                                    AprString realm, AprString username, bool maySave, AprPool pool)
        {
            svnCredentials = SvnAuthCredSimple.Alloc(pool);
            svnCredentials.Username = new AprString(m_svnuser, pool);
            svnCredentials.Password = new AprString(m_svnpass, pool);
            svnCredentials.MaySave = false;
            return SvnError.NoError;
        }

        private SvnError GetCommitLogCallback(out AprString logMessage, out SvnPath tmpFile, AprArray commitItems, IntPtr baton, AprPool pool)
        {
            if (!commitItems.IsNull)
            {
                foreach (SvnClientCommitItem2 item in commitItems)
                {
                    m_log.Debug("[SVNBACKUP]: ... " + Path.GetFileName(item.Path.ToString()) + " (" + item.Kind.ToString() + ") r" + item.Revision.ToString());
                }
            }

            string msg = "Region Backup (" + System.Environment.MachineName + " at " + DateTime.UtcNow + " UTC)";

            m_log.Debug("[SVNBACKUP]: Saved with message: " + msg);

            logMessage = new AprString(msg, pool);
            tmpFile = new SvnPath(pool);

            return (SvnError.NoError);
        }

        #endregion

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource source)
        {
            try
            {
                if (!source.Configs["SVN"].GetBoolean("Enabled", false))
                    return;

                m_enabled = true;

                m_svndir = source.Configs["SVN"].GetString("Directory", m_svndir);
                m_svnurl = source.Configs["SVN"].GetString("URL", m_svnurl);
                m_svnuser = source.Configs["SVN"].GetString("Username", m_svnuser);
                m_svnpass = source.Configs["SVN"].GetString("Password", m_svnpass);
                m_installBackupOnLoad = source.Configs["SVN"].GetBoolean("ImportOnStartup", m_installBackupOnLoad);
                m_svnAutoSave = source.Configs["SVN"].GetBoolean("Autosave", m_svnAutoSave);
                m_svnperiod = new TimeSpan(0, source.Configs["SVN"].GetInt("AutosavePeriod", (int) m_svnperiod.TotalMinutes), 0);
            }
            catch (Exception)
            {
            }

            lock (m_scenes)
            {
                m_scenes.Add(scene);
            }
            //Only register it once, to prevent command being executed x*region times
            if (m_scenes.Count == 1)
            {
                scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
            }
        }

        public void PostInitialise()
        {
            if (m_enabled == false)
                return;

            if (m_svnAutoSave)
            {
                m_timer.Interval = m_svnperiod.TotalMilliseconds;
                m_timer.Elapsed += m_timer_Elapsed;
                m_timer.AutoReset = true;
                m_timer.Start();
            }

            m_log.Info("[SVNBACKUP]: Connecting to SVN server " + m_svnurl + " ...");
            SetupSvnProvider();

            m_log.Info("[SVNBACKUP]: Creating repository in " + m_svndir + ".");
            CreateSvnDirectory();
            CheckoutSvn();
            SetupSerialiser();

            if (m_installBackupOnLoad)
            {
                m_log.Info("[SVNBACKUP]: Importing latest SVN revision to scenes...");
                foreach (Scene scene in m_scenes)
                {
                    LoadRegion(scene);
                }
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "SvnBackupModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        private void EventManager_OnPluginConsole(string[] args)
        {
            if (args[0] == "svn" && args[1] == "save")
            {
                SaveAllRegions();
            }
            if (args.Length == 2)
            {
                if (args[0] == "svn" && args[1] == "load")
                {
                    LoadAllScenes();
                }
            }
            if (args.Length == 3)
            {
                if (args[0] == "svn" && args[1] == "load")
                {
                    LoadAllScenes(Int32.Parse(args[2]));
                }
            }
            if (args.Length == 3)
            {
                if (args[0] == "svn" && args[1] == "load-region")
                {
                    LoadScene(args[2]);
                }
            }
            if (args.Length == 4)
            {
                if (args[0] == "svn" && args[1] == "load-region")
                {
                    LoadScene(args[2], Int32.Parse(args[3]));
                }
            }
        }

        public void LoadScene(string name)
        {
            CheckoutSvn();

            foreach (Scene scene in m_scenes)
            {
                if (scene.RegionInfo.RegionName.ToLower().Equals(name.ToLower()))
                {
                    LoadRegion(scene);
                    return;
                }
            }
            m_log.Warn("[SVNBACKUP]: No region loaded - unable to find matching name.");
        }

        public void LoadScene(string name, int revision)
        {
            CheckoutSvn(new SvnRevision(revision));

            foreach (Scene scene in m_scenes)
            {
                if (scene.RegionInfo.RegionName.ToLower().Equals(name.ToLower()))
                {
                    LoadRegion(scene);
                    return;
                }
            }
            m_log.Warn("[SVNBACKUP]: No region loaded - unable to find matching name.");
        }

        public void LoadAllScenes()
        {
            CheckoutSvn();

            foreach (Scene scene in m_scenes)
            {
                LoadRegion(scene);
            }
        }

        public void LoadAllScenes(int revision)
        {
            CheckoutSvn(new SvnRevision(revision));

            foreach (Scene scene in m_scenes)
            {
                LoadRegion(scene);
            }
        }

        private void m_timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            SaveAllRegions();
        }

        private void SetupSerialiser()
        {
            if (m_scenes.Count > 0)
                m_serialiser = m_scenes[0].RequestModuleInterface<IRegionSerialiserModule>();
        }

        private void SetupSvnProvider()
        {
            m_svnClient = new SvnClient();
            m_svnClient.AddUsernameProvider();
            m_svnClient.AddPromptProvider(new SvnAuthProviderObject.SimplePrompt(SimpleAuth), IntPtr.Zero, 2);
            m_svnClient.OpenAuth();
            m_svnClient.Context.LogMsgFunc2 = new SvnDelegate(new SvnClient.GetCommitLog2(GetCommitLogCallback));
        }

        private void CreateSvnDirectory()
        {
            if (!Directory.Exists(m_svndir))
                Directory.CreateDirectory(m_svndir);
        }
    }
}
