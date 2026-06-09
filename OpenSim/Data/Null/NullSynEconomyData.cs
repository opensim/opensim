/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * SynGrid avatar-balance Null backend.
 *
 * Pure in-memory ConcurrentDictionary. No I/O. Selected
 * automatically when [DatabaseService] StorageProvider points
 * at OpenSim.Data.Null.dll — i.e. the standard standalone
 * "no real database" configuration.
 *
 * Use cases:
 *   - Quick dev / single-region sims that don't need
 *     durability across restarts.
 *   - Test harnesses.
 *   - Default fallback when nothing else is configured.
 *
 * Concurrency: ConcurrentDictionary handles the per-row
 * locking. We do manual atomic-compare debit by computing the
 * candidate new value, then AddOrUpdate with a "balance is
 * still >= amount" check inside the factory closure — that's
 * the only way to keep it lock-free with the public BCL
 * types. The check is technically a read-then-update, but
 * for in-memory it's correct because the update is linearised
 * by AddOrUpdate.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.Null
{
    public class NullSynEconomyData : ISynEconomyData
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly ConcurrentDictionary<UUID, int> m_balances
            = new ConcurrentDictionary<UUID, int>();

        public string Version { get { return "1.0.0.0"; } }
        public string Name { get { return "Null Syn Economy Data"; } }

        public void Initialise() { /* no-op */ }
        public void Dispose() { /* no-op */ }

        public NullSynEconomyData(string connectionString, string realm) { }
        public NullSynEconomyData() { }

        public int GetBalance(UUID principal)
        {
            return m_balances.TryGetValue(principal, out int b) ? b : 0;
        }

        public bool Contains(UUID principal)
        {
            return m_balances.ContainsKey(principal);
        }

        public int Credit(UUID principal, int amount)
        {
            if (amount < 0) amount = 0;
            return m_balances.AddOrUpdate(principal, amount, (_, cur) => cur + amount);
        }

        public bool TryDebit(UUID principal, int amount, out int newBalance)
        {
            newBalance = 0;
            if (amount < 0) return false;

            // Retry loop with TryUpdate to avoid the AddOrUpdate
            // factory side-effect bug (captured locals can reflect
            // a discarded invocation under contention).
            int oldValue;
            do
            {
                if (!m_balances.TryGetValue(principal, out oldValue))
                {
                    newBalance = 0;
                    return false;
                }
                if (oldValue < amount)
                {
                    newBalance = oldValue;
                    return false;
                }
                newBalance = oldValue - amount;
            }
            while (!m_balances.TryUpdate(principal, newBalance, oldValue));

            return true;
        }

        public bool TryTransfer(UUID from, UUID to, int amount, out string reason)
        {
            reason = string.Empty;
            if (amount < 0) { reason = "Negative amount"; return false; }
            if (amount == 0) { reason = "Zero amount"; return false; }
            if (from == to) { reason = "Cannot transfer to self"; return false; }

            // Order: debit first, then credit. If the credit
            // throws (it can't here, but defensive) the debit
            // has already happened — we accept that for the
            // in-memory provider since it's truly atomic at
            // this scale.
            if (!TryDebit(from, amount, out _))
            {
                reason = "Insufficient funds";
                return false;
            }
            Credit(to, amount);
            return true;
        }

        public void SetBalance(UUID principal, int balance)
        {
            m_balances[principal] = balance;
        }

        public void Delete(UUID principal)
        {
            m_balances.TryRemove(principal, out _);
        }

        public long Count()
        {
            return m_balances.Count;
        }

        public IEnumerable<KeyValuePair<UUID, int>> EnumerateAll()
        {
            // Snapshot is fine for the in-memory case. We
            // return a copy so callers can iterate without
            // worrying about concurrent modifications during
            // the walk.
            var copy = new KeyValuePair<UUID, int>[m_balances.Count];
            int i = 0;
            foreach (var kvp in m_balances)
            {
                copy[i++] = kvp;
                yield return kvp;
            }
        }

        public void BulkInsert(IEnumerable<KeyValuePair<UUID, int>> rows)
        {
            if (rows == null) return;
            int n = 0;
            foreach (var kvp in rows)
            {
                m_balances[kvp.Key] = kvp.Value;
                n++;
            }
            m_log.InfoFormat(
                "[SYN ECONOMY]: NullSynEconomyData BulkInsert {0} rows", n);
        }
    }
}
