using System;
using System.Collections.Generic;
using Nini.Config;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Modules.ExportSerialiser;

using PumaCode.SvnDotNet.SubversionSharp;
using PumaCode.SvnDotNet.AprSharp;

using Slash=System.IO.Path;

namespace OpenSim.Region.Modules.SvnSerialiser
{
    public class SvnBackupModule : IRegionModule
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        private SvnClient m_svnClient;
        private bool m_enabled = false;
        private bool m_installBackupOnLoad = false;
        private string m_svnurl = "svn://insert.your.svn/here/";
        private string m_svnuser = "username";
        private string m_svnpass = "password";
        private string m_svndir = "SVNmodule\\repo";
        private IRegionSerialiser m_serialiser;
        private List<Scene> m_scenes = new List<Scene>();

        #region SvnModule Core

        public void SaveRegion(Scene scene)
        {
            m_log.Info("[SVNBACKUP]: Saving a region to SVN with name " + scene.RegionInfo.RegionName);

            List<string> filenames = m_serialiser.SerialiseRegion(scene, m_svndir + Slash.DirectorySeparatorChar + scene.RegionInfo.RegionID.ToString() + "\\");

            try
            {
                m_svnClient.Add3(m_svndir + Slash.DirectorySeparatorChar + scene.RegionInfo.RegionID.ToString(), true, false, false);
            }
            catch (SvnException) { }

            List<string> svnfilenames = new List<string>();
            foreach (string filename in filenames)
                svnfilenames.Add(m_svndir + Slash.DirectorySeparatorChar + scene.RegionInfo.RegionID.ToString() + Slash.DirectorySeparatorChar + filename);
            svnfilenames.Add(m_svndir + Slash.DirectorySeparatorChar + scene.RegionInfo.RegionID.ToString());

            m_svnClient.Commit3(svnfilenames, true, false);
            m_log.Info("[SVNBACKUP]: Region backup successful (" + scene.RegionInfo.RegionName + ").");
        }

        public void LoadRegion(Scene scene)
        {
            scene.LoadPrimsFromXml2(m_svndir + Slash.DirectorySeparatorChar + scene.RegionInfo.RegionID.ToString() + 
                Slash.DirectorySeparatorChar + "objects.xml");
            scene.RequestModuleInterface<OpenSim.Region.Environment.Modules.Terrain.ITerrainModule>().LoadFromFile(m_svndir + Slash.DirectorySeparatorChar + scene.RegionInfo.RegionID.ToString() +
                Slash.DirectorySeparatorChar + "heightmap.r32");
            m_log.Info("[SVNBACKUP]: Region load successful (" + scene.RegionInfo.RegionName + ").");
        }

        private void CheckoutSvn()
        {
            m_svnClient.Checkout2(m_svnurl, m_svndir, Svn.Revision.Head, Svn.Revision.Head, true, false);
        }

        private void CheckoutSvn(SvnRevision revision)
        {
            m_svnClient.Checkout2(m_svnurl, m_svndir, revision, revision, true, false);
        }

        private void CheckoutSvnPartial(string subdir)
        {
            if (!System.IO.Directory.Exists(m_svndir + Slash.DirectorySeparatorChar + subdir))
                System.IO.Directory.CreateDirectory(m_svndir + Slash.DirectorySeparatorChar + subdir);

            m_svnClient.Checkout2(m_svnurl + "/" + subdir, m_svndir, Svn.Revision.Head, Svn.Revision.Head, true, false);
        }

        private void CheckoutSvnPartial(string subdir, SvnRevision revision)
        {
            if (!System.IO.Directory.Exists(m_svndir + Slash.DirectorySeparatorChar + subdir))
                System.IO.Directory.CreateDirectory(m_svndir + Slash.DirectorySeparatorChar + subdir);

            m_svnClient.Checkout2(m_svnurl + "/" + subdir, m_svndir, revision, revision, true, false);
        }

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
                    m_log.Debug("[SVNBACKUP]: Updated " + item.Path.ToString() + " (" + item.Kind.ToString() + ") " + item.Revision.ToString());
                    m_log.Debug("[SVNBACKUP]: " + item.Url.ToString() + " -> " + item.CopyFromUrl.ToString());
                }
            }

            m_log.Debug("[SVNBACKUP]: Appending log message.");

            logMessage = new AprString("Automated Region Backup", pool);
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
            } catch(Exception) { }

            lock (m_scenes)
            {
                m_scenes.Add(scene);
            }

            scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
        }

        void EventManager_OnPluginConsole(string[] args)
        {
            if (args[0] == "svn" && args[1] == "save")
            {
                SaveAllScenes();
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

        private void LoadAllScenes()
        {
            CheckoutSvn();

            foreach (Scene scene in m_scenes)
            {
                LoadRegion(scene);
            }
        }


        private void LoadAllScenes(int revision)
        {
            CheckoutSvn(new SvnRevision(revision));

            foreach (Scene scene in m_scenes)
            {
                LoadRegion(scene);
            }
        }

        private void SaveAllScenes()
        {
            foreach (Scene scene in m_scenes)
            {
                SaveRegion(scene);
            }
        }

        public void PostInitialise()
        {
            if (m_enabled == false)
                return;

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

        private void SetupSerialiser()
        {

            if (m_scenes.Count > 0)
                m_serialiser = m_scenes[0].RequestModuleInterface<IRegionSerialiser>();
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
            if (!System.IO.Directory.Exists(m_svndir))
                System.IO.Directory.CreateDirectory(m_svndir);
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
    }
}
