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
using NHibernate.Tool.hbm2ddl;
using OpenMetaverse;
using Environment = NHibernate.Cfg.Environment;

namespace OpenSim.Data.NHibernate
{
    public class NHibernateManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string dialect;
        private Configuration configuration;
        private ISessionFactory sessionFactory;

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
            configuration = new Configuration();
            configuration.SetProperty(Environment.ConnectionProvider,
                            "NHibernate.Connection.DriverConnectionProvider");
            configuration.SetProperty(Environment.Dialect,
                            "NHibernate.Dialect." + dialect);
            configuration.SetProperty(Environment.ConnectionDriver,
                            "NHibernate.Driver." + parts[1]);
            configuration.SetProperty(Environment.ConnectionString, parts[2]);
            configuration.AddAssembly("OpenSim.Data.NHibernate");

            //To create sql file uncomment code below and write the name of the file
            //SchemaExport exp = new SchemaExport(cfg);
            //exp.SetOutputFile("nameofthefile.sql");
            //exp.Create(false, true);

            sessionFactory = configuration.BuildSessionFactory();

            Assembly assembly = GetType().Assembly;

            // Migration subtype is the folder name under which migrations are stored. For mysql this folder is
            // MySQLDialect instead of MySQL5Dialect which is the dialect currently in use. To avoid renaming 
            // this folder each time the mysql version changes creating simple mapping:
            String migrationSubType = dialect;
            if (dialect.StartsWith("MySQL"))
            {
                migrationSubType="MySQLDialect";
            }

            Migration migration = new Migration((System.Data.Common.DbConnection)sessionFactory.ConnectionProvider.GetConnection(), assembly, migrationSubType, store);
            migration.Update();
        }

        public object Load(Type type, UUID uuid)
        {
            using (IStatelessSession session = sessionFactory.OpenStatelessSession())
            {
                object obj = null;
                try
                {
                    obj = session.Get(type.FullName, uuid);
                }
                catch (Exception)
                {
                    m_log.ErrorFormat("[NHIBERNATE] {0} not found with ID {1} ", type.Name, uuid);
                }
                return obj;
            }
            
        }

        public bool Save(object obj)
        {
            try
            {
                using (IStatelessSession session = sessionFactory.OpenStatelessSession())
                {
                    using (ITransaction transaction=session.BeginTransaction())
                    {
                        session.Insert(obj);
                        transaction.Commit();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[NHIBERNATE] issue inserting object ", e);
                return false;
            }
        }

        public bool Update(object obj)
        {
            try
            {
                using (IStatelessSession session = sessionFactory.OpenStatelessSession())
                {
                    using (ITransaction transaction = session.BeginTransaction())
                    {
                        session.Update(obj);
                        transaction.Commit();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[NHIBERNATE] issue updating object ", e);
                return false;
            }
        }

        public bool Delete(object obj)
        {
            try
            {
                using (IStatelessSession session = sessionFactory.OpenStatelessSession())
                {
                    using (ITransaction transaction = session.BeginTransaction())
                    {
                        session.Delete(obj);
                        transaction.Commit();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[NHIBERNATE] issue deleting object ", e);
                return false;
            }
        }

        public void DropSchema()
        {
            SchemaExport export = new SchemaExport(this.configuration);
            export.Drop(true, true);

            using (ISession session = sessionFactory.OpenSession())
            {
                ISQLQuery sqlQuery=session.CreateSQLQuery("drop table migrations");
                sqlQuery.ExecuteUpdate();
            }
        }

        public ISession GetSession()
        {
            return sessionFactory.OpenSession();
        }
    }
}
