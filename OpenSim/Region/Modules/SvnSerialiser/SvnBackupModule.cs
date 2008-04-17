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
        private string m_svnurl = "svn://your.svn.tld/";
        private string m_svnuser = "user";
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
                // Ignore this error, it means the sim is already under version control.
            catch (SvnException) { }

            List<string> svnfilenames = new List<string>();
            foreach (string filename in filenames)
                svnfilenames.Add(m_svndir + Slash.DirectorySeparatorChar + scene.RegionInfo.RegionID.ToString() + Slash.DirectorySeparatorChar + filename);
            svnfilenames.Add(m_svndir + Slash.DirectorySeparatorChar + scene.RegionInfo.RegionID.ToString());

            m_svnClient.Commit3(svnfilenames, true, false);
            m_log.Info("[SVNBACKUP]: Backup successful.");
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
                SaveRegion(m_scenes[0]);
        }

        public void PostInitialise()
        {
            if (m_enabled == false)
                return;

            m_log.Info("[SVNBACKUP]: Connecting...");

            m_svnClient = new SvnClient();
            m_svnClient.AddUsernameProvider();
            m_svnClient.AddPromptProvider(new SvnAuthProviderObject.SimplePrompt(SimpleAuth), IntPtr.Zero, 2);
            m_svnClient.OpenAuth();

            m_log.Info("[SVNBACKUP]: Checking out base directory...");

            if (!System.IO.Directory.Exists(m_svndir))
                System.IO.Directory.CreateDirectory(m_svndir);

            m_svnClient.Checkout2(m_svnurl, m_svndir, Svn.Revision.Head, Svn.Revision.Head, true, false);

            if (m_scenes.Count > 0)
                m_serialiser = m_scenes[0].RequestModuleInterface<IRegionSerialiser>();

            if (m_installBackupOnLoad)
            {
                //TODO
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
    }
}
