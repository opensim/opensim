/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * SynGrid avatar-balance MySQL backend.
 *
 * Schema (auto-created on first connect via CREATE TABLE IF
 * NOT EXISTS):
 *
 *   CREATE TABLE syn_economy (
 *       PrincipalID CHAR(36) NOT NULL PRIMARY KEY,
 *       Balance     INT       NOT NULL DEFAULT 0,
 *       UpdatedAt   TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
 *                                  ON UPDATE CURRENT_TIMESTAMP
 *   ) ENGINE=InnoDB;
 *
 * Concurrency model:
 *   - GetBalance:   single SELECT, no transaction needed.
 *   - Credit:       INSERT ... ON DUPLICATE KEY UPDATE
 *                   Balance = Balance + VALUES(Balance). The
 *                   server serialises the per-row update so
 *                   two concurrent credits can't lose one.
 *   - TryDebit:     conditional UPDATE
 *                   Balance = Balance - ?amt
 *                   WHERE PrincipalID = ?id AND Balance >= ?amt
 *                   Examines affected_rows to detect the
 *                   "insufficient funds" case. One row, no
 *                   SELECT roundtrip.
 *   - TryTransfer:  BEGIN; conditional debit; credit receiver;
 *                   COMMIT;. On ER_LOCK_DEADLOCK retry once
 *                   after a small backoff — the only realistic
 *                   deadlock in this workload is a reverse
 *                   pair of transfers between the same two
 *                   accounts, which is a one-shot retry.
 *   - EnumerateAll: SELECT ... with a forward-only reader,
 *                   yields one row at a time.
 *
 * Realm parameter (passed from the addon's [Economy] config)
 * lets operators point multiple SynEconomy addons at the
 * same database with non-overlapping table names — useful for
 * multi-region testing.
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using log4net;
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.MySQL
{
    public class MySQLSynEconomyData : ISynEconomyData
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly string m_connectionString;
        private readonly string m_realm;
        private readonly string m_table;

        public string Version { get { return "1.0.0.0"; } }
        public string Name { get { return "MySQL Syn Economy Data"; } }

        public void Initialise() { /* connection string set in ctor */ }
        public void Dispose() { /* MySqlFramework opens per-call connections */ }

        public MySQLSynEconomyData(string connectionString, string realm)
        {
            m_connectionString = connectionString;
            // Realm is normally "SynEconomy" (or a per-region variant).
            // We use it as the table name verbatim, with light validation.
            if (string.IsNullOrEmpty(realm))
                realm = "SynEconomy";
            // Realm can carry only [A-Za-z0-9_]; we accept the standard
            // OpenSim convention "XStore" without quoting.
            m_realm = realm;
            m_table = SanitizeIdentifier(realm);

            EnsureSchema();
        }

        public MySQLSynEconomyData(string connectionString) : this(connectionString, "SynEconomy") { }

        private void EnsureSchema()
        {
            string sql = string.Format(@"
                CREATE TABLE IF NOT EXISTS `{0}` (
                    PrincipalID CHAR(36)     NOT NULL PRIMARY KEY,
                    Balance     INT           NOT NULL DEFAULT 0,
                    UpdatedAt   TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP
                                                     ON UPDATE CURRENT_TIMESTAMP
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", m_table);

            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(sql, dbcon))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[SYN ECONOMY]: MySQLSynEconomyData EnsureSchema failed for {0}: {1}",
                    m_table, e.Message);
                throw;
            }
        }

        public int GetBalance(UUID principal)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    string.Format("SELECT Balance FROM `{0}` WHERE PrincipalID = ?id", m_table), dbcon))
                {
                    cmd.Parameters.AddWithValue("?id", principal.ToString());
                    object v = cmd.ExecuteScalar();
                    if (v == null || v is DBNull) return 0;
                    return Convert.ToInt32(v);
                }
            }
        }

        public bool Contains(UUID principal)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    string.Format("SELECT 1 FROM `{0}` WHERE PrincipalID = ?id LIMIT 1", m_table), dbcon))
                {
                    cmd.Parameters.AddWithValue("?id", principal.ToString());
                    object v = cmd.ExecuteScalar();
                    return v != null;
                }
            }
        }

        public int Credit(UUID principal, int amount)
        {
            if (amount < 0) amount = 0;
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                // INSERT ... ON DUPLICATE KEY UPDATE is atomic on a single
                // row and avoids the read-modify-write race.
                using (MySqlCommand cmd = new MySqlCommand(string.Format(@"
                    INSERT INTO `{0}` (PrincipalID, Balance) VALUES (?id, ?amt)
                    ON DUPLICATE KEY UPDATE Balance = Balance + VALUES(Balance)", m_table), dbcon))
                {
                    cmd.Parameters.AddWithValue("?id", principal.ToString());
                    cmd.Parameters.AddWithValue("?amt", amount);
                    cmd.ExecuteNonQuery();
                }
            }
            return GetBalance(principal);
        }

        public bool TryDebit(UUID principal, int amount, out int newBalance)
        {
            newBalance = 0;
            if (amount < 0) return false;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                // Atomic conditional debit. affected_rows = 0 means
                // either the row doesn't exist or balance < amount.
                using (MySqlCommand cmd = new MySqlCommand(string.Format(@"
                    UPDATE `{0}` SET Balance = Balance - ?amt
                    WHERE PrincipalID = ?id AND Balance >= ?amt", m_table), dbcon))
                {
                    cmd.Parameters.AddWithValue("?id", principal.ToString());
                    cmd.Parameters.AddWithValue("?amt", amount);
                    int n = cmd.ExecuteNonQuery();
                    if (n == 0) return false;
                }
            }
            newBalance = GetBalance(principal);
            return true;
        }

        public bool TryTransfer(UUID from, UUID to, int amount, out string reason)
        {
            reason = string.Empty;
            if (amount < 0) { reason = "Negative amount"; return false; }
            if (amount == 0) { reason = "Zero amount"; return false; }
            if (from == to) { reason = "Cannot transfer to self"; return false; }

            // Two retry attempts are enough — three indicates a
            // pathological workload and we let it surface.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                    {
                        dbcon.Open();
                        using (MySqlTransaction trans = dbcon.BeginTransaction(IsolationLevel.ReadCommitted))
                        {
                            // 1. Conditional debit
                            using (MySqlCommand cmd = new MySqlCommand(string.Format(@"
                                UPDATE `{0}` SET Balance = Balance - ?amt
                                WHERE PrincipalID = ?from AND Balance >= ?amt", m_table), dbcon, trans))
                            {
                                cmd.Parameters.AddWithValue("?from", from.ToString());
                                cmd.Parameters.AddWithValue("?amt", amount);
                                int n = cmd.ExecuteNonQuery();
                                if (n == 0)
                                {
                                    trans.Rollback();
                                    reason = "Insufficient funds";
                                    return false;
                                }
                            }

                            // 2. Credit receiver (create row if missing)
                            using (MySqlCommand cmd = new MySqlCommand(string.Format(@"
                                INSERT INTO `{0}` (PrincipalID, Balance) VALUES (?to, ?amt)
                                ON DUPLICATE KEY UPDATE Balance = Balance + VALUES(Balance)", m_table), dbcon, trans))
                            {
                                cmd.Parameters.AddWithValue("?to", to.ToString());
                                cmd.Parameters.AddWithValue("?amt", amount);
                                cmd.ExecuteNonQuery();
                            }

                            trans.Commit();
                            return true;
                        }
                    }
                }
                catch (MySqlException ex) when (ex.Number == 1213 /* ER_LOCK_DEADLOCK */ && attempt < 2)
                {
                    // One-shot retry on deadlock. Sleep backoff grows.
                    System.Threading.Thread.Sleep(5 * (attempt + 1));
                    continue;
                }
            }

            reason = "Transfer failed after retries";
            return false;
        }

        public void SetBalance(UUID principal, int balance)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                using (MySqlCommand cmd = new MySqlCommand(string.Format(@"
                    INSERT INTO `{0}` (PrincipalID, Balance) VALUES (?id, ?bal)
                    ON DUPLICATE KEY UPDATE Balance = VALUES(Balance)", m_table), dbcon))
                {
                    cmd.Parameters.AddWithValue("?id", principal.ToString());
                    cmd.Parameters.AddWithValue("?bal", balance);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Delete(UUID principal)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    string.Format("DELETE FROM `{0}` WHERE PrincipalID = ?id", m_table), dbcon))
                {
                    cmd.Parameters.AddWithValue("?id", principal.ToString());
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public long Count()
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    string.Format("SELECT COUNT(*) FROM `{0}`", m_table), dbcon))
                {
                    return Convert.ToInt64(cmd.ExecuteScalar());
                }
            }
        }

        public IEnumerable<KeyValuePair<UUID, int>> EnumerateAll()
        {
            // Forward-only MySqlDataReader + yield return. The reader
            // and connection live for the entire iteration. Callers
            // must consume the enumerator promptly; we yield row by
            // row from the open reader.
            MySqlConnection dbcon = new MySqlConnection(m_connectionString);
            MySqlCommand cmd = null;
            MySqlDataReader reader = null;
            try
            {
                dbcon.Open();
                cmd = new MySqlCommand(
                    string.Format("SELECT PrincipalID, Balance FROM `{0}` ORDER BY PrincipalID", m_table), dbcon);
                reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess);
                while (reader.Read())
                {
                    string s = reader.GetString(0);
                    int b = reader.GetInt32(1);
                    if (UUID.TryParse(s, out UUID id))
                        yield return new KeyValuePair<UUID, int>(id, b);
                }
            }
            finally
            {
                try { if (reader != null) reader.Close(); } catch { }
                try { if (cmd != null) cmd.Dispose(); } catch { }
                try { dbcon.Close(); dbcon.Dispose(); } catch { }
            }
        }

        public void BulkInsert(IEnumerable<KeyValuePair<UUID, int>> rows)
        {
            if (rows == null) return;
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                using (MySqlTransaction trans = dbcon.BeginTransaction())
                {
                    using (MySqlCommand cmd = new MySqlCommand(string.Format(@"
                        INSERT INTO `{0}` (PrincipalID, Balance) VALUES (?id, ?bal)
                        ON DUPLICATE KEY UPDATE Balance = VALUES(Balance)", m_table), dbcon, trans))
                    {
                        var pId = cmd.Parameters.Add("?id", MySqlDbType.VarChar, 36);
                        var pBal = cmd.Parameters.Add("?bal", MySqlDbType.Int32);
                        cmd.Prepare();
                        int n = 0;
                        foreach (var kvp in rows)
                        {
                            pId.Value = kvp.Key.ToString();
                            pBal.Value = kvp.Value;
                            cmd.ExecuteNonQuery();
                            n++;
                        }
                        trans.Commit();
                        m_log.InfoFormat(
                            "[SYN ECONOMY]: BulkInsert {0} rows into {1}", n, m_table);
                    }
                }
            }
        }

        // Realm is concatenated into SQL identifiers, so the only
        // safe thing to do is strip everything that isn't a
        // letter, digit, or underscore. OpenSim's existing data
        // realms all follow this convention already.
        private static string SanitizeIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s)) return "SynEconomy";
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                      (c >= '0' && c <= '9') || c == '_'))
                    chars[i] = '_';
            }
            return new string(chars);
        }
    }
}
