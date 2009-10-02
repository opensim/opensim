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
using System.Data.Common;
using System.Reflection;
using log4net;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Tool.hbm2ddl;
using OpenMetaverse;
using Environment=NHibernate.Cfg.Environment;

namespace OpenSim.Data.NHibernate
{
    public class NHibernateManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string dialect;
        private Configuration configuration;
        private ISessionFactory sessionFactory;

        #region Initialization

        /// <summary>
        /// Initiate NHibernate Manager
        /// </summary>
        /// <param name="connect">NHibernate dialect, driver and connection string separated by ';'</param>
        /// <param name="store">Name of the store</param>
        public NHibernateManager(string connect, string store)
        {
            ParseConnectionString(connect);

            //To create sql file uncomment code below and write the name of the file
            //SchemaExport exp = new SchemaExport(cfg);
            //exp.SetOutputFile("nameofthefile.sql");
            //exp.Create(false, true); 

            Assembly assembly = GetType().Assembly;

            sessionFactory = configuration.BuildSessionFactory();
            RunMigration(dialect, assembly, store);
        }

        /// <summary>
        /// Initiate NHibernate Manager with spesific assembly
        /// </summary>
        /// <param name="connect">NHibernate dialect, driver and connection string separated by ';'</param>
        /// <param name="store">Name of the store</param>
        /// <param name="assembly">Outside assembly to be included </param>
        public NHibernateManager(string connect, string store, Assembly assembly)
        {
            ParseConnectionString(connect);

            configuration.AddAssembly(assembly);
            sessionFactory = configuration.BuildSessionFactory();
            RunMigration(dialect, assembly, store);
        }

        /// <summary>
        /// Parses the connection string and creates the NHibernate configuration
        /// </summary>
        /// <param name="connect">NHibernate dialect, driver and connection string separated by ';'</param>
        private void ParseConnectionString(string connect)
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
        }

        /// <summary>
        /// Runs migration for the the store in assembly
        /// </summary>
        /// <param name="dialect">Dialect in use</param>
        /// <param name="assembly">Assembly where migration files exist</param>
        /// <param name="store">Name of the store in use</param>
        private void RunMigration(string dialect, Assembly assembly, string store)
        {
            // Migration subtype is the folder name under which migrations are stored. For mysql this folder is
            // MySQLDialect instead of MySQL5Dialect which is the dialect currently in use. To avoid renaming 
            // this folder each time the mysql version changes creating simple mapping:
            String migrationSubType = dialect;
            if (dialect.StartsWith("MySQL"))
            {
                migrationSubType = "MySQLDialect";
            }

            Migration migration = new Migration((DbConnection)sessionFactory.ConnectionProvider.GetConnection(), assembly, migrationSubType, store);
            migration.Update();
        }

        #endregion

        /// <summary>
        /// Gets object of given type from database with given id. 
        /// Uses stateless session for efficiency.
        /// </summary>
        /// <param name="type">Type of the object.</param>
        /// <param name="id">Id of the object.</param>
        /// <returns>The object or null if object was not found.</returns>
        public object Get(Type type, Object id)
        {
            using (IStatelessSession session = sessionFactory.OpenStatelessSession())
            {
                object obj = null;
                try
                {
                    obj = session.Get(type.FullName, id);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[NHIBERNATE] {0} of id {1} loading threw exception: " + e.ToString(), type.Name, id);
                }
                return obj;
            }
        }

        /// <summary>
        /// Gets object of given type from database with given id. 
        /// Use this method for objects containing collections. For flat objects stateless mode is more efficient.
        /// </summary>
        /// <param name="type">Type of the object.</param>
        /// <param name="id">Id of the object.</param>
        /// <returns>The object or null if object was not found.</returns>
        public object GetWithStatefullSession(Type type, Object id)
        {
            using (ISession session = sessionFactory.OpenSession())
            {
                object obj = null;
                try
                {
                    obj = session.Get(type.FullName, id);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[NHIBERNATE] {0} of id {1} loading threw exception: " + e.ToString(), type.Name, id);
                }
                return obj;
            }

        }

        /// <summary>
        /// Inserts given object to database.
        /// Uses stateless session for efficiency.
        /// </summary>
        /// <param name="obj">Object to be insterted.</param>
        /// <returns>Identifier of the object. Useful for situations when NHibernate generates the identifier.</returns>
        public object Insert(object obj)
        {
            try
            {
                using (IStatelessSession session = sessionFactory.OpenStatelessSession())
                {
                    using (ITransaction transaction=session.BeginTransaction())
                    {
                        Object identifier=session.Insert(obj);
                        transaction.Commit();
                        return identifier;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[NHIBERNATE] issue inserting object ", e);
                return null;
            }
        }

        /// <summary>
        /// Inserts given object to database.
        /// Use this method for objects containing collections. For flat objects stateless mode is more efficient.
        /// </summary>
        /// <param name="obj">Object to be insterted.</param>
        /// <returns>Identifier of the object. Useful for situations when NHibernate generates the identifier.</returns>
        public object InsertWithStatefullSession(object obj)
        {
            try
            {
                using (ISession session = sessionFactory.OpenSession())
                {
                    using (ITransaction transaction = session.BeginTransaction())
                    {
                        Object identifier = session.Save(obj);
                        transaction.Commit();
                        return identifier;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[NHIBERNATE] issue inserting object ", e);
                return null;
            }
        }

        /// <summary>
        /// Updates given object to database.
        /// Uses stateless session for efficiency.
        /// </summary>
        /// <param name="obj">Object to be updated.</param>
        /// <returns>True if operation was succesful.</returns>
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

        /// <summary>
        /// Updates given object to database.
        /// Use this method for objects containing collections. For flat objects stateless mode is more efficient.
        /// </summary>
        /// <param name="obj">Object to be updated.</param>
        /// <returns>True if operation was succesful.</returns>
        public bool UpdateWithStatefullSession(object obj)
        {
            try
            {
                using (ISession session = sessionFactory.OpenSession())
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

        /// <summary>
        /// Deletes given object from database.
        /// </summary>
        /// <param name="obj">Object to be deleted.</param>
        /// <returns>True if operation was succesful.</returns>
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

        /// <summary>
        /// Returns statefull session which can be used to execute custom nhibernate or sql queries.
        /// </summary>
        /// <returns>Statefull session</returns>
        public ISession GetSession()
        {
            return sessionFactory.OpenSession();
        }

        /// <summary>
        /// Drops the database schema. This exist for unit tests. It should not be invoked from other than test teardown.
        /// </summary>
        public void DropSchema()
        {
            SchemaExport export = new SchemaExport(this.configuration);
            export.Drop(true, true);

            using (ISession session = sessionFactory.OpenSession())
            {
                ISQLQuery sqlQuery = session.CreateSQLQuery("drop table migrations");
                sqlQuery.ExecuteUpdate();
            }
        }

    }
}
