using System;
using System.IO;
using System.Collections.Generic;
using log4net.Config;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using OpenMetaverse;
using OpenSim.Framework;
using log4net;
using System.Data;
using System.Data.Common;
using System.Reflection;

namespace OpenSim.Data.Tests
{
    /// <summary>This is a base class for testing any Data service for any DBMS. 
    /// Requires NUnit 2.5 or better (to support the generics).
    /// </summary>
    /// <typeparam name="TConn"></typeparam>
    /// <typeparam name="TService"></typeparam>
    public class BasicDataServiceTest<TConn, TService>
        where TConn : DbConnection, new()
        where TService : class, new()
    {
        protected string m_connStr;
        private TService m_service;
        private string m_file;

        // TODO: Is this in the right place here? 
        protected static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public BasicDataServiceTest()
            : this("")
        { 
        }

        public BasicDataServiceTest(string conn)
        {
            m_connStr = !String.IsNullOrEmpty(conn) ? conn : DefaultTestConns.Get(typeof(TConn));

            OpenSim.Tests.Common.TestLogging.LogToConsole();    // TODO: Is that right?
        }

        /// <summary>
        /// To be overridden in derived classes. Do whatever init with the m_service, like setting the conn string to it.
        /// You'd probably want to to cast the 'service' to a more specific type and store it in a member var.  
        /// This framework takes care of disposing it, if it's disposable.
        /// </summary>
        /// <param name="service">The service being tested</param>
        protected virtual void InitService(object service)
        {
        }

        [TestFixtureSetUp]
        public void Init()
        {
            // Sorry, some SQLite-specific stuff goes here (not a big deal, as its just some file ops)
            if (typeof(TConn).Name.StartsWith("Sqlite"))
            {
                // SQLite doesn't work on power or z linux
                if (Directory.Exists("/proc/ppc64") || Directory.Exists("/proc/dasd"))
                    Assert.Ignore();

                // for SQLite, if no explicit conn string is specified, use a temp file
                if (String.IsNullOrEmpty(m_connStr))
                {
                    m_file = Path.GetTempFileName() + ".db";
                    m_connStr = "URI=file:" + m_file + ",version=3";
                }
            }

            if (String.IsNullOrEmpty(m_connStr))
            {
                string msg = String.Format("Connection string for {0} is not defined, ignoring tests", typeof(TConn).Name);
                m_log.Error(msg);
                Assert.Ignore(msg);
            }

            // If we manage to connect to the database with the user
            // and password above it is our test database, and run
            // these tests.  If anything goes wrong, ignore these
            // tests.
            try
            {
                m_service = new TService();
                InitService(m_service);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                Assert.Ignore();
            }
        }

        [TestFixtureTearDown]
        public void Cleanup()
        {
            if (m_service != null)
            {
                if( m_service is IDisposable)
                    ((IDisposable)m_service).Dispose();
                m_service = null;
            }

            if( !String.IsNullOrEmpty(m_file) && File.Exists(m_file) )
                File.Delete(m_file);
        }

        protected virtual DbConnection Connect()
        {
            DbConnection cnn = new TConn();
            cnn.ConnectionString = m_connStr;
            cnn.Open();
            return cnn;
        }

        protected virtual void ExecuteSql(string sql)
        {
            using (DbConnection dbcon = Connect())
            {
                using (DbCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        protected delegate bool ProcessRow(IDataReader reader);

        protected virtual int ExecQuery(string sql, bool bSingleRow, ProcessRow action)
        {
            int nRecs = 0;
            using (DbConnection dbcon = Connect())
            {
                using (DbCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = sql;
                    CommandBehavior cb = bSingleRow ? CommandBehavior.SingleRow : CommandBehavior.Default;
                    using (DbDataReader rdr = cmd.ExecuteReader(cb))
                    {
                        while (rdr.Read())
                        {
                            nRecs++;
                            if (!action(rdr))
                                break;
                        }
                    }
                }
            }
            return nRecs;
        }

        /// <summary>Drop tables (listed as parameters). There is no "DROP IF EXISTS" syntax common for all
        /// databases, so we just DROP and ignore an exception.
        /// </summary>
        /// <param name="tables"></param>
        protected virtual void DropTables(params string[] tables)
        {
            foreach (string tbl in tables)
            {
                try
                {
                    ExecuteSql("DROP TABLE " + tbl + ";");
                }catch
                {
                }
            }
        }
    }
}
