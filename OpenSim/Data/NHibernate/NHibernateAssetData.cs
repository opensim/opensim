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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using libsecondlife;
using log4net;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Expression;
using NHibernate.Mapping.Attributes;
using NHibernate.Tool.hbm2ddl;
using OpenSim.Framework;
using Environment=NHibernate.Cfg.Environment;

namespace OpenSim.Data.NHibernate
{
    /// <summary>
    /// A User storage interface for the DB4o database system
    /// </summary>
    public class NHibernateAssetData : AssetDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Configuration cfg;
        private ISessionFactory factory;
        private ISession session;

        override public void Dispose() { }

        public override void Initialise()
        {
            Initialise("SQLiteDialect;SqliteClientDriver;URI=file:Asset.db,version=3");
        }

        public override void Initialise(string connect)
        {
            // Split out the dialect, driver, and connect string
            char[] split = {';'};
            string[] parts = connect.Split(split, 3);
            if (parts.Length != 3)
            {
                // TODO: make this a real exception type
                throw new Exception("Malformed Inventory connection string '" + connect + "'");
            }

            string dialect = parts[0];

            // NHibernate setup
            cfg = new Configuration();
            cfg.SetProperty(Environment.ConnectionProvider,
                            "NHibernate.Connection.DriverConnectionProvider");
            cfg.SetProperty(Environment.Dialect,
                            "NHibernate.Dialect." + dialect);
            cfg.SetProperty(Environment.ConnectionDriver,
                            "NHibernate.Driver." + parts[1]);
            cfg.SetProperty(Environment.ConnectionString, parts[2]);
            cfg.AddAssembly("OpenSim.Data.NHibernate");



            HbmSerializer.Default.Validate = true;
            using (MemoryStream stream =
                   HbmSerializer.Default.Serialize(Assembly.GetExecutingAssembly()))
                cfg.AddInputStream(stream);

            factory  = cfg.BuildSessionFactory();
            session = factory.OpenSession();
            
            // This actually does the roll forward assembly stuff
            Assembly assem = GetType().Assembly;
            Migration m = new Migration((System.Data.Common.DbConnection)factory.ConnectionProvider.GetConnection(), assem, dialect, "AssetStore");
            m.Update();

        }

        override public AssetBase FetchAsset(LLUUID uuid)
        {
            try
            {
                return session.Load(typeof(AssetBase), uuid) as AssetBase;
            }
            catch (ObjectNotFoundException)
            {
                m_log.ErrorFormat("[NHIBERNATE] no such asset {0}", uuid);
                return null;
            } 
            catch (Exception e)
            {
                m_log.Error("[NHIBERNATE] unexpected exception: ", e);
                return null;
            }
        }

        private void Save(AssetBase asset)
        {
            try 
            {
                // a is not used anywhere?
                // AssetBase a = session.Load(typeof(AssetBase), asset.FullID) as AssetBase;
                session.Load(typeof(AssetBase), asset.FullID);
            }
            catch (ObjectNotFoundException)
            {
                session.Save(asset);
                session.Flush();
            }
            catch (Exception e)
            {
                m_log.Error("[NHIBERNATE] issue saving asset", e);
            }
        }

        override public void CreateAsset(AssetBase asset)
        {
            m_log.InfoFormat("[NHIBERNATE] inserting asset {0}", asset.FullID);
            Save(asset);
        }

        override public void UpdateAsset(AssetBase asset)
        {
            m_log.InfoFormat("[NHIBERNATE] updating asset {0}", asset.FullID);
            Save(asset);
        }

        // private void LogAssetLoad(AssetBase asset)
        // {
        //     string temporary = asset.Temporary ? "Temporary" : "Stored";
        //     string local = asset.Local ? "Local" : "Remote";

        //     int assetLength = (asset.Data != null) ? asset.Data.Length : 0;

        //     m_log.Info("[SQLITE]: " +
        //                              string.Format("Loaded {6} {5} Asset: [{0}][{3}/{4}] \"{1}\":{2} ({7} bytes)",
        //                                            asset.FullID, asset.Name, asset.Description, asset.Type,
        //                                            asset.InvType, temporary, local, assetLength));
        // }

        override public bool ExistsAsset(LLUUID uuid)
        {
            m_log.InfoFormat("[NHIBERNATE] ExistsAsset: {0}", uuid);
            return (FetchAsset(uuid) != null);
        }

        public void DeleteAsset(LLUUID uuid)
        {

        }

        public override string Name {
            get { return "NHibernate"; }
        }

        public override string Version {
            get { return "0.1"; }
        }

    }
}
