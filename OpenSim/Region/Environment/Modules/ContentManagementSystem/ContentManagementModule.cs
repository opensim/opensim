#region Header

// ContentManagementModule.cs 
// User: bongiojp

#endregion Header

using System;
using System.Collections.Generic;
using System.Threading;

using libsecondlife;

using Nini.Config;

using OpenSim;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Physics.Manager;

using log4net;

using Axiom.Math;

namespace OpenSim.Region.Environment.Modules.ContentManagement
{
    public class ContentManagementModule : IRegionModule
    {
        #region Static Fields

        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #endregion Static Fields

        #region Fields

        bool initialised = false;
        CMController m_control = null;
        bool m_enabled = false;
        CMModel m_model = null;
        bool m_posted = false;
        CMView m_view = null;

        #endregion Fields

        #region Public Properties

        public bool IsSharedModule
        {
            get { return true; }
        }

        public string Name
        {
            get { return "ContentManagementModule"; }
        }

        #endregion Public Properties

        #region Public Methods

        public void Close()
        {
        }

        public void Initialise(Scene scene, IConfigSource source)
        {
            string databaseDir = "./";
            string database = "FileSystemDatabase";
            int channel = 345;
            try
            {
                if (source.Configs["CMS"] == null)
                    return;
                    
                m_enabled = source.Configs["CMS"].GetBoolean("Enabled", false);
                databaseDir = source.Configs["CMS"].GetString("Directory", databaseDir);
                database = source.Configs["CMS"].GetString("Database", database);
                channel = source.Configs["CMS"].GetInt("Channel", channel);

                if (database != "FileSystemDatabase" && database != "GitDatabase")
                {
                    m_log.ErrorFormat("[Content Management]: The Database attribute must be defined as either FileSystemDatabase or GitDatabase");
                    m_enabled = false;
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[Content Management]: Exception thrown while reading parameters from configuration file. Message: " + e);
                m_enabled = false;
            }

            if (!m_enabled)
            {
                m_log.Info("[Content Management]: Content Management System is not Enabled.");
                return;
            }

            lock(this)
            {
                if (!initialised) //only init once
                {
                    m_view = new CMView();
                    m_model = new CMModel();
                    m_control = new CMController(m_model, m_view, scene, channel);
                    m_model.Initialise(database);
                    m_view.Initialise(m_model);
                    
                    initialised = true;
                    m_model.InitialiseDatabase(scene, databaseDir);
                }
                else
                {
                    m_model.InitialiseDatabase(scene, databaseDir);
                    m_control.RegisterNewRegion(scene);
                }
            }
        }

        public void PostInitialise()
        {
            if (! m_enabled)
                return;

            lock(this)
            {
                if (!m_posted) //only post once
                {
                    m_model.PostInitialise();
                    m_posted = true;
                }
            }
        }

        #endregion Public Methods
    }
}