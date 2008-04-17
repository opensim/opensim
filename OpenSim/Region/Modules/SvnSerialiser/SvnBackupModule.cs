using System;
using System.Collections.Generic;
using Nini.Config;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Modules.ExportSerialiser;

using PumaCode.SvnDotNet.SubversionSharp;
using PumaCode.SvnDotNet.AprSharp;

namespace OpenSim.Region.Modules.SvnSerialiser
{
    public class SvnBackupModule : IRegionModule
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        private SvnClient m_svnClient;
        private bool m_installBackupOnLoad = false;
        private string m_svnurl = "svn://url.tld/repository/";
        private string m_svnuser = "user";
        private string m_svnpass = "password";
        private string m_svndir = "modsvn/";
        private IRegionSerialiser m_serialiser;
        private List<Scene> m_scenes = new List<Scene>();

        #region SvnModule Core

        public void SaveRegion(Scene scene)
        {
            List<string> filenames = m_serialiser.SerialiseRegion(scene, m_svndir);
            foreach (string filename in filenames)
            {
                m_svnClient.Add3(filename, false, true, false);
            }

            m_svnClient.Commit3(filenames, true, false);
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
                foreach (SvnClientCommitItem item in commitItems)
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
            lock (m_scenes)
            {
                m_scenes.Add(scene);
            }

            scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
        }

        void EventManager_OnPluginConsole(string[] args)
        {
            if (args[0] == "testsvn")
                SaveRegion(m_scenes[0]);
        }

        public void PostInitialise()
        {
            m_log.Info("[SVNBACKUP]: Disabled.");
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
