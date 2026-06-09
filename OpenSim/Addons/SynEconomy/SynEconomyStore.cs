/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * SynGrid avatar-balance store facade.
 *
 * Thin wrapper over ISynEconomyData. The store itself does
 * not load balances into memory at startup; every operation
 * hits one row in the configured backend (MySQL or Null by
 * default). No in-memory mirror of the table.
 *
 * Public API is preserved so the SynEconomyModule can keep
 * the same call sites.
 *
 * No whole-file rewrites. No in-memory mirror of the table.
 *
 * Plugin loading uses the same StorageProvider key the rest
 * of OpenSim uses. If [Economy] SynEconomyStorageProvider
 * is set, that wins; otherwise the global [DatabaseService]
 * StorageProvider is used so the addon follows whatever
 * backend the sim is configured with.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Services.Base;

namespace OpenSim.Addons.SynEconomy
{
    internal class SynEconomyStore : ServiceBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(typeof(SynEconomyStore));

        // The actual backend. Null only if the plugin failed to
        // load (in which case every public method short-circuits
        // to a sensible default).
        private ISynEconomyData m_data;

        public SynEconomyStore(IConfigSource config)
            : base(config)
        {

            // Resolve the storage plugin to use, following the
            // standard OpenSim convention:
            //   1. [Economy] SynEconomyStorageProvider (override)
            //   2. [Economy] StorageProvider            (addon override)
            //   3. [DatabaseService] StorageProvider     (global)
            // The connection string follows the same precedence.
            IConfig econ = config.Configs["Economy"];
            IConfig db = config.Configs["DatabaseService"];

            string dllName = string.Empty;
            string connString = string.Empty;
            string realm = "SynEconomy";

            if (econ != null)
            {
                string overrideProv = econ.GetString("SynEconomyStorageProvider", null);
                if (!string.IsNullOrEmpty(overrideProv))
                    dllName = overrideProv;
                string overrideConn = econ.GetString("SynEconomyConnectionString", null);
                if (!string.IsNullOrEmpty(overrideConn))
                    connString = overrideConn;
                realm = econ.GetString("SynEconomyRealm", realm);
            }

            if (string.IsNullOrEmpty(dllName) && econ != null)
                dllName = econ.GetString("StorageProvider", string.Empty);
            if (string.IsNullOrEmpty(connString) && econ != null)
                connString = econ.GetString("ConnectionString", string.Empty);

            if (string.IsNullOrEmpty(dllName) && db != null)
                dllName = db.GetString("StorageProvider", string.Empty);
            if (string.IsNullOrEmpty(connString) && db != null)
                connString = db.GetString("ConnectionString", string.Empty);

            if (string.IsNullOrEmpty(dllName))
            {
                m_log.Warn("[SYN ECONOMY]: No StorageProvider configured anywhere. " +
                    "The store will not function. Set [DatabaseService] StorageProvider " +
                    "or [Economy] SynEconomyStorageProvider.");
                return;
            }

            try
            {
                m_data = LoadPlugin<ISynEconomyData>(dllName,
                    new Object[] { connString, realm });
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[SYN ECONOMY]: LoadPlugin<ISynEconomyData> threw: {0}", e.Message);
                m_data = null;
                return;
            }

            if (m_data == null)
            {
                m_log.ErrorFormat(
                    "[SYN ECONOMY]: Could not find ISynEconomyData in {0}. " +
                    "Ensure OpenSim.Data.MySQL.dll (or Null) is in bin/.",
                    dllName);
                return;
            }

            try
            {
                m_data.Initialise();
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[SYN ECONOMY]: Backend Initialise failed: {0}", e.Message);
                m_data = null;
                return;
            }

            m_log.InfoFormat(
                "[SYN ECONOMY]: Loaded backend {0} from {1} (realm={2}, connString={3})",
                m_data.Name, dllName, realm,
                string.IsNullOrEmpty(connString) ? "<default>" : "<set>");
        }

        public string BackendName
        {
            get { return m_data != null ? m_data.Name : "<uninitialised>"; }
        }

        // ---- public API ----

        public bool Contains(UUID id)
        {
            return m_data != null && m_data.Contains(id);
        }

        public int GetBalance(UUID id)
        {
            return m_data != null ? m_data.GetBalance(id) : 0;
        }

        public int Add(UUID id, int amount)
        {
            if (m_data == null) return 0;
            if (amount < 0) amount = 0;
            return m_data.Credit(id, amount);
        }

        public bool TrySubtract(UUID id, int amount, out int newBalance)
        {
            newBalance = 0;
            if (m_data == null) return false;
            return m_data.TryDebit(id, amount, out newBalance);
        }

        public bool TryTransfer(UUID from, UUID to, int amount, out string reason)
        {
            reason = string.Empty;
            if (m_data == null)
            {
                reason = "Store not initialised";
                return false;
            }
            return m_data.TryTransfer(from, to, amount, out reason);
        }

        public void SetBalance(UUID id, int balance)
        {
            if (m_data == null) return;
            m_data.SetBalance(id, balance);
        }

        public void Delete(UUID id)
        {
            if (m_data == null) return;
            m_data.Delete(id);
        }

        /// <summary>
        /// One-shot materialisation of the entire table. Streams
        /// via the backend's EnumerateAll so the caller doesn't
        /// have to pre-allocate anything; the resulting dictionary
        /// is just a convenience for the `money list` console
        /// command which prints everything in one go anyway.
        /// </summary>
        public Dictionary<UUID, int> Snapshot()
        {
            var snap = new Dictionary<UUID, int>();
            if (m_data == null) return snap;
            foreach (var kvp in m_data.EnumerateAll())
                snap[kvp.Key] = kvp.Value;
            return snap;
        }

    }
}
