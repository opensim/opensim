/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * SynGrid avatar-balance data interface.
 *
 * Storage abstraction for the SynEconomy addon. The addon
 * itself does not talk to a database directly — it delegates
 * to an ISynEconomyData implementation loaded via the same
 * StorageProvider pattern the rest of OpenSim uses (MySQL,
 * Null, PGSQL, ...).
 *
 * Every operation hits exactly one row, so the addon does not
 * need (or pay for) an in-memory cache of the whole table.
 * EnumerateAll() streams via `yield return` so a `money list`
 * that walks a million rows does not materialise a million-row
 * list in memory.
 *
 * The plugin contract matches the rest of the OpenSim data
 * layer:
 *   - Parameterless / (connectionString, realm) ctor pattern
 *     is supported by ServiceBase.LoadPlugin<T> for explicit
 *     ctors; we use that for the MySQL implementation.
 *   - The Null implementation has a (string connectionString,
 *     string realm) ctor too so the same load path works.
 */

using System;
using System.Collections.Generic;
using OpenMetaverse;

using OpenSim.Framework;

namespace OpenSim.Data
{
    /// <summary>
    /// Persistent store for per-avatar L$ balances used by the
    /// SynEconomy addon.
    ///
    /// Implementations: MySQLSynEconomyData, NullSynEconomyData.
    /// </summary>
    public interface ISynEconomyData : IPlugin
    {
        /// <summary>
        /// Read the current balance for an avatar. Returns 0 if
        /// the account doesn't exist (callers treat that as a
        /// freshly-created, never-funded account).
        /// </summary>
        int GetBalance(UUID principal);

        /// <summary>
        /// Atomically add `amount` to an account. Creates the
        /// account if missing. Always succeeds (the credit is
        /// not conditional). Returns the new balance.
        /// </summary>
        int Credit(UUID principal, int amount);

        /// <summary>
        /// Atomically subtract `amount` from an account. Fails
        /// (returns false) if the account doesn't exist or its
        /// balance is below `amount`. On success, outNewBalance
        /// holds the post-debit value.
        /// </summary>
        bool TryDebit(UUID principal, int amount, out int newBalance);

        /// <summary>
        /// Atomically move `amount` from one account to another.
        /// Fails (returns false) if `from` has insufficient
        /// funds or the accounts are the same. The receiver is
        /// created with 0 if missing.
        /// </summary>
        bool TryTransfer(UUID from, UUID to, int amount, out string reason);

        /// <summary>
        /// Admin override: set an account's balance to an
        /// absolute value. Creates the account if missing.
        /// </summary>
        void SetBalance(UUID principal, int balance);

        /// <summary>
        /// Remove the account row entirely. A subsequent
        /// GetBalance returns 0.
        /// </summary>
        void Delete(UUID principal);

        /// <summary>
        /// True if a row exists for the given principal. Cheap
        /// existence check, used by startup / migration logic.
        /// </summary>
        bool Contains(UUID principal);

        /// <summary>
        /// Stream every (principal, balance) pair in the table.
        /// Implementations should yield one row at a time —
        /// callers may iterate over millions of accounts and
        /// must not have to materialise the whole set.
        /// </summary>
        IEnumerable<KeyValuePair<UUID, int>> EnumerateAll();

        /// <summary>
        /// One-shot migration helper: insert a batch of rows in
        /// a single transaction. Used by the addon to bulk-load
        /// the legacy balances.json into the new backend on
        /// first run. The order of pairs is irrelevant.
        /// </summary>
        void BulkInsert(IEnumerable<KeyValuePair<UUID, int>> rows);

        /// <summary>
        /// Total number of rows. Used for the empty-store
        /// detection that triggers the JSON -> backend
        /// migration. Avoid calling in hot paths; it's a full
        /// COUNT(*).
        /// </summary>
        long Count();
    }
}
