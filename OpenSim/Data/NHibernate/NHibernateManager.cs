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
using System.Reflection;
using System.IO;
using log4net;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Mapping.Attributes;
using NHibernate.Tool.hbm2ddl;
using OpenMetaverse;
using Environment = NHibernate.Cfg.Environment;

namespace OpenSim.Data.NHibernate
{
    public class NHibernateManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string dialect;
        private Configuration cfg;
        private ISessionFactory factory;
        private ISession session;

        public NHibernateManager(string connect, string store)
        {

            // Split out the dialect, driver, and connect string
            char[] split = { ';' };
            string[] parts = connect.Split(split, 3);
            if (parts.Length != 3)
            {
                // TODO: make this a real exception type
                throw new Exception("Malformed Inventory connection string '" + connect + "'");
            }

            dialect = parts[0];

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

            //To create sql file uncomment code below and write the name of the file            
            //SchemaExport exp = new SchemaExport(cfg);
            //exp.SetOutputFile("nameofthefile.sql");
            //exp.Create(false, true);

            HbmSerializer.Default.Validate = true;
            using (MemoryStream stream =
                   HbmSerializer.Default.Serialize(Assembly.GetExecutingAssembly()))
                cfg.AddInputStream(stream);

            factory = cfg.BuildSessionFactory();
            session = factory.OpenSession();

            Assembly assem = GetType().Assembly;
            Migration m = new Migration((System.Data.Common.DbConnection)factory.ConnectionProvider.GetConnection(), assem, dialect, store);
            m.Update();
        }

        public object Load(Type type, UUID uuid)
        {
            object obj = null;
            try
            {
                obj = session.Load(type, uuid);
            }
            catch (Exception)
            {
                m_log.ErrorFormat("[NHIBERNATE] {0} not found with ID {1} ", type.Name, uuid);
            }
            return obj;
            
        }

        public bool Save(object obj)
        {
            try
            {
                session.BeginTransaction();
                session.Save(obj);
                session.Transaction.Commit();
                session.Flush();
                return true;
            }
            catch (Exception e)
            {
               m_log.Error("[NHIBERNATE] issue saving object ", e);
            }
            return false;
        }

        public bool Update(object obj)
        {
            try
            {
                session.BeginTransaction();
                session.Update(obj);
                session.Transaction.Commit();
                session.Flush();
                return true;
            }
            catch (Exception e)
            {
                m_log.Error("[NHIBERNATE] issue updating object ", e);
            }
            return false;
        }

        public bool Delete(object obj)
        {
            try
            {
                session.BeginTransaction();
                session.Delete(obj);
                session.Transaction.Commit();
                session.Flush();
                return true;
            }
            catch (Exception e)
            {
                
                m_log.Error("[NHIBERNATE] issue deleting object ", e);
            }
            return false;
        }

        public ISession GetSession()
        {
            return session;
        }
    }
}
