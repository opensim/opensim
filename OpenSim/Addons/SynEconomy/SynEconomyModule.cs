/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * SynGrid avatar-balance money module.
 *
 * Standalone addon IMoneyModule implementation backed by OpenSim's
 * standard ISynEconomyData storage interface. The actual backend
 * (MySQL, Null, ...) is whatever the sim's [DatabaseService]
 * StorageProvider points at, or whatever [Economy]
 * SynEconomyStorageProvider overrides it to.
 *
 * Rejects LSL llGiveMoney and viewer "Pay" requests when the
 * sender has insufficient balance.
 *
 * Configuration (OpenSim.ini, [Economy] section):
 *   economymodule = SynEconomyModule
 *   SynEconomyStorageProvider = OpenSim.Data.MySQL.dll     ; optional override
 *   SynEconomyConnectionString = Data Source=...;...       ; optional
 *   SynEconomyRealm = SynEconomy                        ; optional
 *
 * Console commands (region console):
 *   money balance <name|uuid>
 *   money credit <name|uuid> <amount>
 *   money debit  <name|uuid> <amount>
 *   money set    <name|uuid> <amount>
 *   money delete <name|uuid>
 *   money list
 *   money give   <from> <to> <amount> [description]
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Addons.SynEconomy
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SynEconomyModule")]
    public class SynEconomyModule : IMoneyModule, ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public string Name { get { return "SynEconomyModule"; } }
        public Type ReplaceableInterface { get { return typeof(IMoneyModule); } }

        private bool m_enabled = true;
        private SynEconomyStore m_store;
        private string m_currencySymbol = "L$";
        private IConfigSource m_gConfig;

        private readonly Dictionary<ulong, Scene> m_scenes = new Dictionary<ulong, Scene>();

#pragma warning disable 0067
        public event ObjectPaid OnObjectPaid;
#pragma warning restore 0067

        public int UploadCharge { get { return 0; } }
        public int GroupCreationCharge { get { return 0; } }

        #region ISharedRegionModule

        public void Initialise(IConfigSource source)
        {
            m_gConfig = source;

            IConfig startupConfig = source.Configs["Startup"];
            IConfig economyConfig = source.Configs["Economy"];

            string mmodule = null;
            if (startupConfig != null)
                mmodule = startupConfig.GetString("economymodule", null);
            if (string.IsNullOrEmpty(mmodule) && economyConfig != null)
                mmodule = economyConfig.GetString("economymodule", null);
            if (string.IsNullOrEmpty(mmodule) && economyConfig != null)
                mmodule = economyConfig.GetString("EconomyModule", null);

            if (!string.IsNullOrEmpty(mmodule) && mmodule != Name)
            {
                m_log.InfoFormat(
                    "[SYN ECONOMY]: Disabled because another money module is selected ({0})", mmodule);
                m_enabled = false;
                return;
            }

            m_currencySymbol = economyConfig != null
                ? economyConfig.GetString("SynEconomyCurrencySymbol", "L$")
                : "L$";

            m_store = new SynEconomyStore(source);
            m_log.InfoFormat(
                "[SYN ECONOMY]: Initialised. symbol={0} backend={1}",
                m_currencySymbol,
                m_store != null ? m_store.BackendName : "<uninitialised>");
        }

        public void AddRegion(Scene scene)
        {
            if (!m_enabled) return;

            scene.RegisterModuleInterface<IMoneyModule>(this);

            lock (m_scenes)
            {
                if (!m_scenes.ContainsKey(scene.RegionInfo.RegionHandle))
                    m_scenes.Add(scene.RegionInfo.RegionHandle, scene);
                else
                    m_scenes[scene.RegionInfo.RegionHandle] = scene;
            }

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += OnClientClosed;
            scene.EventManager.OnMoneyTransfer += OnMoneyTransferEvent;
            scene.EventManager.OnValidateLandBuy += OnValidateLandBuy;
            scene.EventManager.OnLandBuy += OnLandBuy;
            // Fires exactly once per "agent becomes root agent in a scene":
            //   - initial login
            //   - cross-region TP
            //   - hypergrid return
            // Does NOT fire on local walk, double-click between parcels, or
            // child-agent establishment, so no event spam. Used to push the
            // DB-stored balance to the viewer on TP arrival, since the
            // viewer's cached L$ indicator would otherwise stay at the
            // last-known value (often 0 after a hypergrid hop).
            scene.EventManager.OnSetRootAgentScene += OnSetRootAgentScene;
        }

        public void RemoveRegion(Scene scene)
        {
            lock (m_scenes)
            {
                if (m_scenes.ContainsKey(scene.RegionInfo.RegionHandle))
                    m_scenes.Remove(scene.RegionInfo.RegionHandle);
            }
            scene.EventManager.OnMoneyTransfer -= OnMoneyTransferEvent;
            scene.EventManager.OnValidateLandBuy -= OnValidateLandBuy;
            scene.EventManager.OnLandBuy -= OnLandBuy;
            scene.EventManager.OnSetRootAgentScene -= OnSetRootAgentScene;
        }

        public void RegionLoaded(Scene scene) { }

        public void PostInitialise()
        {
            if (!m_enabled) return;
            m_log.Info("[SYN ECONOMY]: Module post-initialised.");
            if (MainConsole.Instance != null)
                RegisterConsoleCommands();
        }

        public void Close()
        {
            if (!m_enabled) return;
            m_log.Info("[SYN ECONOMY]: Module closed.");
        }

        #endregion

        #region IMoneyModule

        public int GetBalance(UUID agentID)
        {
            return m_store != null ? m_store.GetBalance(agentID) : 0;
        }

        public bool UploadCovered(UUID agentID, int amount)
        {
            return AmountCovered(agentID, amount);
        }

        public bool AmountCovered(UUID agentID, int amount)
        {
            if (amount <= 0) return true;
            return GetBalance(agentID) >= amount;
        }

        public void ApplyCharge(UUID agentID, int amount, MoneyTransactionType type, string extraData)
        {
            if (m_store == null || amount <= 0) return;
            if (m_store.TrySubtract(agentID, amount, out int newBal))
            {
                m_log.DebugFormat(
                    "[SYN ECONOMY]: ApplyCharge agent={0} amt={1} type={2} newBal={3}",
                    agentID, amount, type, newBal);
            }
        }

        public void ApplyCharge(UUID agentID, int amount, MoneyTransactionType type)
        {
            ApplyCharge(agentID, amount, type, string.Empty);
        }

        public void ApplyUploadCharge(UUID agentID, int amount, string text)
        {
            ApplyCharge(agentID, amount, MoneyTransactionType.UploadCharge, text);
        }

        public void MoveMoney(UUID fromUser, UUID toUser, int amount, string text)
        {
            MoveMoney(fromUser, toUser, amount, MoneyTransactionType.Gift, text);
        }

        public bool MoveMoney(UUID fromUser, UUID toUser, int amount, MoneyTransactionType type, string text)
        {
            if (m_store == null) return false;
            if (amount <= 0) return false;

            if (fromUser == UUID.Zero)
            {
                m_store.Add(toUser, amount);
                PushBalanceUpdate(toUser, true, text ?? string.Empty);
                return true;
            }

            if (!m_store.TryTransfer(fromUser, toUser, amount, out string reason))
            {
                m_log.WarnFormat(
                    "[SYN ECONOMY]: MoveMoney FAILED from={0} to={1} amt={2} reason={3}",
                    fromUser, toUser, amount, reason);
                PushBalanceUpdate(fromUser, false, reason);
                return false;
            }

            m_log.InfoFormat(
                "[SYN ECONOMY]: MoveMoney OK from={0} to={1} amt={2} type={3} text={4}",
                fromUser, toUser, amount, type, text);
            PushBalanceUpdate(fromUser, true, text ?? string.Empty);
            if (toUser != fromUser)
                PushBalanceUpdate(toUser, true, text ?? string.Empty);
            return true;
        }

        public bool ObjectGiveMoney(UUID objectID, UUID fromID, UUID toID, int amount, UUID txn, out string reason)
        {
            reason = string.Empty;
            if (m_store == null) { reason = "Money module not initialised"; return false; }
            if (amount <= 0) { reason = "Non-positive amount"; return false; }

            UUID sender = fromID;
            if (sender == UUID.Zero) { reason = "Sender has no UUID"; return false; }

            if (!m_store.TryTransfer(sender, toID, amount, out string why))
            {
                reason = why;
                m_log.WarnFormat(
                    "[SYN ECONOMY]: ObjectGiveMoney REJECTED from={0} to={1} amt={2} reason={3}",
                    sender, toID, amount, why);
                PushBalanceUpdate(sender, false, why);
                return false;
            }

            m_log.InfoFormat(
                "[SYN ECONOMY]: ObjectGiveMoney OK from={0} to={1} amt={2} txn={3}",
                sender, toID, amount, txn);

            string desc = string.Format("Pay object {0}", objectID);
            PushBalanceUpdate(sender, true, desc);
            if (toID != sender)
                PushBalanceUpdate(toID, true, desc);

            string senderName = ResolveName(sender);
            string recipientName = ResolveName(toID);
            Notify(sender,
                string.Format("You paid {0}{1} to {2}.", m_currencySymbol, amount, recipientName));
            Notify(toID,
                string.Format("{0} paid you {1}{2}.", senderName, m_currencySymbol, amount));

            try { OnObjectPaid?.Invoke(objectID, sender, amount); }
            catch (Exception e) { m_log.Warn("[SYN ECONOMY]: OnObjectPaid handler threw: " + e.Message); }
            return true;
        }

        #endregion

        #region Client event hooks

        private void OnNewClient(IClientAPI client)
        {
            client.OnMoneyBalanceRequest += SendMoneyBalance;
            client.OnEconomyDataRequest += EconomyDataRequestHandler;
            client.OnRequestPayPrice += OnRequestPayPrice;
            client.OnObjectBuy += OnObjectBuy;
            client.OnLogout += ClientLoggedOut;
        }

        private void OnClientClosed(UUID agentID, Scene scene) { }

        private void ClientLoggedOut(IClientAPI client)
        {
            if (client == null) return;
            client.OnObjectBuy -= OnObjectBuy;
            client.OnRequestPayPrice -= OnRequestPayPrice;
        }

        private void SendMoneyBalance(IClientAPI client, UUID agentID, UUID sessionID, UUID transactionID)
        {
            if (client == null) return;
            if (client.AgentId != agentID || client.SessionId != sessionID)
            {
                client.SendAlertMessage("Unable to send your money balance to you!");
                return;
            }

            int bal = GetBalance(agentID);
            try
            {
                client.SendMoneyBalance(
                    transactionID, true, Array.Empty<byte>(), bal, 0,
                    UUID.Zero, false, UUID.Zero, false, 0, string.Empty);
            }
            catch (Exception e)
            {
                m_log.Warn("[SYN ECONOMY]: SendMoneyBalance failed: " + e.Message);
            }
        }

        private void EconomyDataRequestHandler(IClientAPI user)
        {
            try
            {
                user.SendEconomyData(1f, 0, 0, 0, 0, 0, 0f, 0f, 0, 0f, 0, 0, 0, 0, 0, 0, 0f);
            }
            catch { }
        }

        #endregion

        #region Viewer Pay / Buy / Land event handlers

        // Fired by Scene.ProcessMoneyTransferRequest when the user picks
        // "Pay" in the viewer and selects a target avatar. The LSL
        // llGiveMoney path goes through ObjectGiveMoney instead.
        private void OnMoneyTransferEvent(Object osender, EventManager.MoneyTransferArgs e)
        {
            if (m_store == null) return;
            if (e.amount <= 0) return;

            MoneyTransactionType type = MoneyTransactionType.Gift;
            try
            {
                // 0=SystemGenerated, 1=RegionMoneyRequest, 2=Gift, 3=Purchase
                type = (MoneyTransactionType)e.transactiontype;
            }
            catch { }

            string desc = string.IsNullOrEmpty(e.description)
                ? string.Format("Pay: {0} -> {1}", ResolveName(e.sender), ResolveName(e.receiver))
                : e.description;

            m_log.InfoFormat(
                "[SYN ECONOMY]: OnMoneyTransferEvent from={0} to={1} amt={2} type={3} desc={4}",
                e.sender, e.receiver, e.amount, type, desc);

            bool ok = MoveMoney(e.sender, e.receiver, e.amount, type, desc);

            if (e.sender == UUID.Zero) return;
            string senderName = ResolveName(e.sender);
            string recipientName = ResolveName(e.receiver);
            // e.description carries the SL Pay dialog's optional note.
            // Surface it inline in the toast only — no bluebox, to avoid
            // a duplicate line in the viewer's chat log.
            string userMsg = string.IsNullOrEmpty(e.description)
                ? string.Empty
                : string.Format(" Message: \"{0}\"", e.description);
            if (ok)
            {
                Notify(e.sender,
                    string.Format("You paid {0}{1} to {2}.{3}",
                        m_currencySymbol, e.amount, recipientName, userMsg));
                Notify(e.receiver,
                    string.Format("{0} paid you {1}{2}.{3}",
                        senderName, m_currencySymbol, e.amount, userMsg));
            }
            else
            {
                Notify(e.sender,
                    string.Format("Payment of {0}{1} to {2} failed: insufficient funds.",
                        m_currencySymbol, e.amount, recipientName));
            }
        }

        // Fired when the user clicks "Buy" on a prim in the viewer.
        // The delivery notification (bluebox + buyer toast) always fires
        // for a successful purchase, regardless of whether money moved
        // (free objects, self-buys, paid buys). The "you paid" toasts
        // only fire when money actually moves.
        private void OnObjectBuy(IClientAPI remoteClient, UUID agentID, UUID sessionID,
                UUID groupID, UUID categoryID, uint localID, byte saleType, int salePrice)
        {
            if (remoteClient == null) return;
            if (m_store == null)
            {
                remoteClient.SendAgentAlertMessage("Money module not initialised.", false);
                return;
            }

            Scene scene = FindSceneForClient(remoteClient);
            if (scene == null)
            {
                m_log.Warn("[SYN ECONOMY]: OnObjectBuy: no scene for client");
                return;
            }

            SceneObjectPart part = scene.GetSceneObjectPart(localID);
            if (part == null) return;
            if (!part.IsRoot) return;

            SceneObjectPart root = part.ParentGroup != null
                ? part.ParentGroup.RootPart
                : part;

            if (root == null || root.ParentGroup == null || root.ParentGroup.IsDeleted)
            {
                remoteClient.SendAgentAlertMessage("Unable to buy now. The object was not found.", false);
                return;
            }

            if (root.ObjectSaleType == (byte)SaleType.Not)
            {
                remoteClient.SendAgentAlertMessage(
                    string.Format("Object {0} is not for sale", root.Name), false);
                return;
            }

            if (root.SalePrice != salePrice)
            {
                remoteClient.SendAgentAlertMessage(
                    string.Format("Object {0} price does not match selected price", root.Name), false);
                return;
            }
            if (root.ObjectSaleType != saleType)
            {
                remoteClient.SendAgentAlertMessage(
                    string.Format("Object {0} sell type does not match selected type", root.Name), false);
                return;
            }

            UUID seller = root.OwnerID;
            UUID buyer = agentID;
            string displayName = string.IsNullOrEmpty(root.Name)
                ? ("prim#" + root.UUID)
                : root.Name;
            string sellerName = ResolveName(seller);
            string buyerName = ResolveName(buyer);

            // Move money only for paid, non-self purchases. Free /
            // self-buy are still valid purchases; the buyer just
            // gets their item and a "received" notification.
            bool moneyMoved = false;
            if (salePrice > 0 && seller != buyer)
            {
                if (m_store.GetBalance(buyer) < salePrice)
                {
                    remoteClient.SendAgentAlertMessage(
                        string.Format("You do not have enough {0} to buy {1} (cost {2})",
                            m_currencySymbol, root.Name, salePrice), false);
                    m_log.InfoFormat(
                        "[SYN ECONOMY]: OnObjectBuy REJECTED buyer={0} seller={1} amt={2} obj={3} (insufficient funds)",
                        buyer, seller, salePrice, root.Name);
                    return;
                }

                string desc = string.Format("Buy: {0}", displayName);
                if (!MoveMoney(buyer, seller, salePrice, MoneyTransactionType.ObjectSale, desc))
                {
                    remoteClient.SendAgentAlertMessage("Payment failed; purchase aborted.", false);
                    return;
                }
                moneyMoved = true;

                Notify(buyer,
                    string.Format("You paid {0}{1} to {2} for {3}.",
                        m_currencySymbol, salePrice, sellerName, displayName));
                if (TryGetClient(seller, true, out _))
                {
                    Notify(seller,
                        string.Format("{0} paid you {1}{2} for {3}.",
                            buyerName, m_currencySymbol, salePrice, displayName));
                }

                try { OnObjectPaid?.Invoke(root.UUID, buyer, salePrice); }
                catch (Exception ex)
                {
                    m_log.Warn("[SYN ECONOMY]: OnObjectPaid handler threw: " + ex.Message);
                }
            }

            IBuySellModule bsModule = scene.RequestModuleInterface<IBuySellModule>();
            if (bsModule == null)
            {
                m_log.Warn("[SYN ECONOMY]: IBuySellModule not available; object not delivered.");
                remoteClient.SendAgentAlertMessage(
                    "Object delivery failed. Please contact support.", false);
                return;
            }
            bsModule.BuyObject(remoteClient, categoryID, localID, saleType, salePrice);

            // Delivery notification — always fires after a successful
            // purchase, regardless of whether money moved. Bluebox
            // only, so the viewer shows exactly one line (not two).
            if (moneyMoved)
            {
                NotifyBluebox(buyer, seller, sellerName,
                    string.Format("Object '{0}' has given you an item named '{0}'.",
                        displayName));
            }
            else
            {
                NotifyBluebox(buyer, buyer, buyerName,
                    string.Format("You received '{0}'.", displayName));
            }
        }

        private void OnRequestPayPrice(IClientAPI client, UUID objectID)
        {
            if (client == null) return;
            Scene scene = FindSceneForClient(client);
            if (scene == null) return;

            SceneObjectPart task = scene.GetSceneObjectPart(objectID);
            if (task == null) return;
            SceneObjectPart root = task.ParentGroup != null
                ? task.ParentGroup.RootPart
                : task;
            try
            {
                client.SendPayPrice(objectID, root.PayPrice);
            }
            catch (Exception ex)
            {
                m_log.DebugFormat("[SYN ECONOMY]: SendPayPrice failed: {0}", ex.Message);
            }
        }

        private void OnValidateLandBuy(Object osender, EventManager.LandBuyArgs e)
        {
            lock (e)
            {
                e.economyValidated = true;
            }
        }

        private void OnLandBuy(Object osender, EventManager.LandBuyArgs e)
        {
            if (m_store == null) return;
            if (!e.economyValidated) return;
            if (e.parcelPrice <= 0) return;
            if (e.parcelOwnerID == UUID.Zero) return;

            if (m_store.GetBalance(e.agentId) < e.parcelPrice)
            {
                m_log.InfoFormat(
                    "[SYN ECONOMY]: LandBuy REJECTED buyer={0} price={1} (insufficient funds)",
                    e.agentId, e.parcelPrice);
                Notify(e.agentId,
                    string.Format("You do not have enough {0} to buy this land (cost {0}{1}).",
                        m_currencySymbol, e.parcelPrice));
                return;
            }

            string desc = string.Format("Land buy: parcel {0} ({1}m\u00b2)",
                e.parcelLocalID, e.parcelArea);
            bool ok = MoveMoney(e.agentId, e.parcelOwnerID, e.parcelPrice,
                MoneyTransactionType.LandSale, desc);

            if (!ok) return;
            string buyerName = ResolveName(e.agentId);
            string sellerName = ResolveName(e.parcelOwnerID);
            Notify(e.agentId,
                string.Format("You paid {0}{1} to {2} for {3}m\u00b2 of land.",
                    m_currencySymbol, e.parcelPrice, sellerName, e.parcelArea));
            if (TryGetClient(e.parcelOwnerID, true, out _))
            {
                Notify(e.parcelOwnerID,
                    string.Format("{0} paid you {1}{2} for {3}m\u00b2 of land.",
                        buyerName, m_currencySymbol, e.parcelPrice, e.parcelArea));
            }
        }

        #endregion

        #region Console commands

        private void RegisterConsoleCommands()
        {
            MainConsole.Instance.Commands.AddCommand(
                "SynEconomy", false, "money balance",
                "money balance <name|uuid>",
                "Show an avatar's current money balance.",
                HandleMoneyBalance);

            MainConsole.Instance.Commands.AddCommand(
                "SynEconomy", false, "money credit",
                "money credit <name|uuid> <amount>",
                "Add money to an avatar's balance (creates account if missing).",
                HandleMoneyCredit);

            MainConsole.Instance.Commands.AddCommand(
                "SynEconomy", false, "money debit",
                "money debit <name|uuid> <amount>",
                "Subtract money from an avatar's balance. Fails if insufficient.",
                HandleMoneyDebit);

            MainConsole.Instance.Commands.AddCommand(
                "SynEconomy", false, "money set",
                "money set <name|uuid> <amount>",
                "Set an avatar's balance to an exact value (admin override).",
                HandleMoneySet);

            MainConsole.Instance.Commands.AddCommand(
                "SynEconomy", false, "money delete",
                "money delete <name|uuid>",
                "Delete an avatar's balance record.",
                HandleMoneyDelete);

            MainConsole.Instance.Commands.AddCommand(
                "SynEconomy", false, "money list",
                "money list",
                "List all known avatar balances.",
                HandleMoneyList);

            MainConsole.Instance.Commands.AddCommand(
                "SynEconomy", false, "money give",
                "money give <fromName|uuid> <toName|uuid> <amount> [description]",
                "Transfer money from one avatar to another.",
                HandleMoneyGive);
        }

        private void HandleMoneyBalance(string module, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Output("Usage: money balance <name|uuid>");
                return;
            }
            string query = args[2];
            if (!ResolveAgent(query, out UUID id, out string who))
            {
                MainConsole.Instance.Output(string.Format("No such avatar: {0}", query));
                return;
            }
            int bal = GetBalance(id);
            MainConsole.Instance.Output(string.Format("Balance for {0} ({1}): {2}", who, id, bal));
        }

        private void HandleMoneyCredit(string module, string[] args)
        {
            if (args.Length < 4 || !int.TryParse(args[3], out int amount))
            {
                MainConsole.Instance.Output("Usage: money credit <name|uuid> <amount>");
                return;
            }
            if (!ResolveAgent(args[2], out UUID id, out string who))
            {
                MainConsole.Instance.Output(string.Format("No such avatar: {0}", args[2]));
                return;
            }
            int newBal = m_store.Add(id, amount);
            MainConsole.Instance.Output(string.Format("Credited {0} to {1} ({2}). New balance: {3}", amount, who, id, newBal));
        }

        private void HandleMoneyDebit(string module, string[] args)
        {
            if (args.Length < 4 || !int.TryParse(args[3], out int amount))
            {
                MainConsole.Instance.Output("Usage: money debit <name|uuid> <amount>");
                return;
            }
            if (!ResolveAgent(args[2], out UUID id, out string who))
            {
                MainConsole.Instance.Output(string.Format("No such avatar: {0}", args[2]));
                return;
            }
            if (!m_store.TrySubtract(id, amount, out int newBal))
            {
                MainConsole.Instance.Output(string.Format("Debit FAILED for {0} ({1}). Insufficient funds. Current: {2}",
                    who, id, GetBalance(id)));
                return;
            }
            MainConsole.Instance.Output(string.Format("Debited {0} from {1} ({2}). New balance: {3}", amount, who, id, newBal));
        }

        private void HandleMoneySet(string module, string[] args)
        {
            if (args.Length < 4 || !int.TryParse(args[3], out int amount))
            {
                MainConsole.Instance.Output("Usage: money set <name|uuid> <amount>");
                return;
            }
            if (!ResolveAgent(args[2], out UUID id, out string who))
            {
                MainConsole.Instance.Output(string.Format("No such avatar: {0}", args[2]));
                return;
            }
            m_store.SetBalance(id, amount);
            MainConsole.Instance.Output(string.Format("Set balance for {0} ({1}) to {2}", who, id, amount));
        }

        private void HandleMoneyDelete(string module, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Output("Usage: money delete <name|uuid>");
                return;
            }
            if (!ResolveAgent(args[2], out UUID id, out string who))
            {
                MainConsole.Instance.Output(string.Format("No such avatar: {0}", args[2]));
                return;
            }
            m_store.Delete(id);
            MainConsole.Instance.Output(string.Format("Deleted balance record for {0} ({1})", who, id));
        }

        private void HandleMoneyList(string module, string[] args)
        {
            var snap = m_store.Snapshot();
            if (snap.Count == 0)
            {
                MainConsole.Instance.Output("(no balances on record)");
                return;
            }
            MainConsole.Instance.Output(string.Format("Listing {0} balance(s):", snap.Count));
            foreach (var kvp in snap)
            {
                string who = ResolveName(kvp.Key);
                MainConsole.Instance.Output(string.Format("  {0} ({1}) = {2}", who, kvp.Key, kvp.Value));
            }
        }

        private void HandleMoneyGive(string module, string[] args)
        {
            if (args.Length < 5 || !int.TryParse(args[4], out int amount))
            {
                MainConsole.Instance.Output("Usage: money give <fromName|uuid> <toName|uuid> <amount> [description]");
                return;
            }
            if (!ResolveAgent(args[2], out UUID from, out string fromWho))
            {
                MainConsole.Instance.Output(string.Format("No such avatar (from): {0}", args[2]));
                return;
            }
            if (!ResolveAgent(args[3], out UUID to, out string toWho))
            {
                MainConsole.Instance.Output(string.Format("No such avatar (to): {0}", args[3]));
                return;
            }
            string desc = args.Length >= 6
                ? string.Join(" ", args, 5, args.Length - 5)
                : string.Format("Console transfer: {0} -> {1}", fromWho, toWho);
            if (!MoveMoney(from, to, amount, MoneyTransactionType.Gift, desc))
            {
                MainConsole.Instance.Output(string.Format("Transfer FAILED: {0} -> {1} amount={2}", fromWho, toWho, amount));
                return;
            }
            MainConsole.Instance.Output(string.Format("Transfer OK: {0} -> {1} amount={2}", fromWho, toWho, amount));
        }

        #endregion

        #region helpers

        private bool ResolveAgent(string query, out UUID id, out string display)
        {
            id = UUID.Zero;
            display = query;

            if (string.IsNullOrWhiteSpace(query)) return false;

            if (UUID.TryParse(query.Trim(), out id))
            {
                display = ResolveName(id);
                return true;
            }

            string[] parts = query.Trim().Split(new[] { ' ' }, 2,
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                Scene scene = GetAnyScene();
                if (scene == null || scene.UserAccountService == null)
                {
                    m_log.Warn("[SYN ECONOMY]: No scene/UserAccountService available for name lookup");
                    return false;
                }
                UserAccount acct = scene.UserAccountService.GetUserAccount(
                    scene.RegionInfo.ScopeID, parts[0], parts[1]);
                if (acct == null) return false;
                id = acct.PrincipalID;
                display = string.Format("{0} {1}", acct.FirstName, acct.LastName);
                return true;
            }
            return false;
        }

        private string ResolveName(UUID id)
        {
            Scene scene = GetAnyScene();
            if (scene == null || scene.UserAccountService == null) return id.ToString();
            UserAccount acct = scene.UserAccountService.GetUserAccount(
                scene.RegionInfo.ScopeID, id);
            return acct != null
                ? string.Format("{0} {1}", acct.FirstName, acct.LastName)
                : id.ToString();
        }

        private Scene GetAnyScene()
        {
            lock (m_scenes)
            {
                foreach (var s in m_scenes.Values) return s;
            }
            return null;
        }

        private Scene FindSceneForClient(IClientAPI client)
        {
            if (client == null) return null;
            lock (m_scenes)
            {
                foreach (Scene s in m_scenes.Values)
                {
                    ScenePresence sp;
                    lock (s)
                    {
                        sp = s.GetScenePresence(client.AgentId);
                    }
                    if (sp != null && !sp.IsDeleted) return s;
                }
            }
            return null;
        }

        private void PushBalanceUpdate(UUID agentID, bool success, string description)
        {
            Scene scene = GetAnyScene();
            if (scene == null) return;
            ScenePresence sp;
            lock (scene)
            {
                sp = scene.GetScenePresence(agentID);
            }
            if (sp == null || sp.IsChildAgent || sp.IsDeleted) return;
            IClientAPI client = sp.ControllingClient;
            if (client == null) return;

            try
            {
                int bal = GetBalance(agentID);
                client.SendMoneyBalance(
                    UUID.Random(), success, System.Text.Encoding.UTF8.GetBytes(description ?? string.Empty),
                    bal, 0, UUID.Zero, false, UUID.Zero, false, 0, string.Empty);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[SYN ECONOMY]: PushBalanceUpdate failed for {0}: {1}",
                    agentID, e.Message);
            }
        }

        private bool TryGetClient(UUID agentID, bool useChildAgents, out IClientAPI client)
        {
            client = null;
            Scene scene = GetAnyScene();
            if (scene == null) return false;
            ScenePresence sp;
            lock (scene)
            {
                sp = scene.GetScenePresence(agentID);
            }
            if (sp == null || sp.IsDeleted) return false;
            if (!useChildAgents && sp.IsChildAgent) return false;
            client = sp.ControllingClient;
            return client != null;
        }

        // Cross-TP balance push. OnSetRootAgentScene fires once per
        // "agent becomes root agent in a scene" (login, cross-region
        // TP, hypergrid return) and never for local walk, parcel
        // crossing, or child-agent establishment, so this is the
        // correct hook — no event spam. The 300ms delay gives the
        // viewer's UDP circuit time to settle after the TP handshake;
        // we run it on the ThreadPool so we don't block the region's
        // TP-arrival pipeline. PushBalanceUpdate is a safe no-op if
        // the presence has gone away in the meantime.
        private void OnSetRootAgentScene(UUID agentID, Scene scene)
        {
            if (m_store == null) return;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    System.Threading.Thread.Sleep(300);
                    PushBalanceUpdate(agentID, true, "TP arrival");
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[SYN ECONOMY]: OnSetRootAgentScene push failed: {0}", e.Message);
                }
            });
        }

        private void Notify(UUID agentID, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (!TryGetClient(agentID, true, out IClientAPI client)) return;
            try { client.SendAlertMessage(message); }
            catch (Exception e)
            {
                m_log.DebugFormat("[SYN ECONOMY]: Notify({0}) failed: {1}", agentID, e.Message);
            }
        }

        private void NotifyBluebox(UUID agentID, UUID fromAgentID, string fromName, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (!TryGetClient(agentID, true, out IClientAPI client)) return;
            try { client.SendBlueBoxMessage(fromAgentID, fromName ?? string.Empty, message); }
            catch (Exception e)
            {
                m_log.DebugFormat("[SYN ECONOMY]: NotifyBluebox({0}) failed: {1}", agentID, e.Message);
            }
        }

        #endregion
    }
}
