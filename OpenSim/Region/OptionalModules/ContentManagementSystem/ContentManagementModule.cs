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

#region Header

// ContentManagementModule.cs
// User: bongiojp

#endregion Header

using System;
using System.Collections.Generic;
using System.Threading;

using OpenMetaverse;

using Nini.Config;

using OpenSim;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;

using log4net;

namespace OpenSim.Region.OptionalModules.ContentManagement
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

                m_enabled = source.Configs["CMS"].GetBoolean("enabled", false);
                databaseDir = source.Configs["CMS"].GetString("directory", databaseDir);
                database = source.Configs["CMS"].GetString("database", database);
                channel = source.Configs["CMS"].GetInt("channel", channel);

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

            lock (this)
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

            lock (this)
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
