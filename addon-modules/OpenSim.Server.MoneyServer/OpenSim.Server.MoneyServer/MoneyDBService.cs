/*
 * Copyright (c) Contributors, http://opensimulator.org/, http://www.nsl.tuis.ac.jp/
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
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Data.MySQL.MoneyData;
using OpenSim.Region.OptionalModules.Currency;
using log4net;
using System.Reflection;
using OpenMetaverse;


namespace OpenSim.Server.MoneyServer
{
    class MoneyDBService : IMoneyDBService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private string m_connect;
        //private MySQLMoneyManager m_moneyManager;
        private long TicksToEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        // DB manager pool
        protected Dictionary<int, MySQLSuperManager> m_dbconnections = new Dictionary<int, MySQLSuperManager>();	// with Lock
        private int m_maxConnections;

        public int m_lastConnect = 0;


        public MoneyDBService(string connect)
        {
            m_connect = connect;
            Initialise(m_connect, 10);
        }


        public MoneyDBService()
        {
        }


        public void Initialise(string connectionString, int maxDBConnections)
        {
            m_connect = connectionString;
            m_maxConnections = maxDBConnections;
            if (connectionString != string.Empty)
            {
                //m_moneyManager = new MySQLMoneyManager(connectionString);

                //m_log.Info("Creating " + m_maxConnections + " DB connections...");
                for (int i = 0; i < m_maxConnections; i++)
                {
                    //m_log.Info("Connecting to DB... [" + i + "]");
                    MySQLSuperManager msm = new MySQLSuperManager();
                    msm.Manager = new MySQLMoneyManager(connectionString);
                    m_dbconnections.Add(i, msm);
                }
            }
            else
            {
                m_log.Error("[MONEY DB]: Connection string is null, initialise database failed");
                throw new Exception("Failed to initialise MySql database");
            }
        }


        public void Reconnect()
        {
            for (int i = 0; i < m_maxConnections; i++)
            {
                MySQLSuperManager msm = m_dbconnections[i];
                msm.Manager.Reconnect();
            }
        }


        private MySQLSuperManager GetLockedConnection()
        {
            int lockedCons = 0;
            while (true)
            {
                m_lastConnect++;

                // Overflow protection
                if (m_lastConnect == int.MaxValue) m_lastConnect = 0;

                MySQLSuperManager msm = m_dbconnections[m_lastConnect % m_maxConnections];
                if (!msm.Locked)
                {
                    msm.GetLock();
                    return msm;
                }

                lockedCons++;
                if (lockedCons > m_maxConnections)
                {
                    lockedCons = 0;
                    System.Threading.Thread.Sleep(1000); // Wait some time before searching them again.
                    m_log.Debug("WARNING: All threads are in use. Probable cause: Something didnt release a mutex properly, or high volume of requests inbound.");
                }
            }
        }


        public int getBalance(string userID)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                return dbm.Manager.getBalance(userID);
            }
#pragma warning disable CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
#pragma warning restore CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
                dbm.Manager.Reconnect();
                return dbm.Manager.getBalance(userID);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return 0;
            }
            finally
            {
                dbm.Release();
            }
        }


        public bool withdrawMoney(UUID transactionID, string senderID, int amount)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                return dbm.Manager.withdrawMoney(transactionID, senderID, amount);
            }
#pragma warning disable CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
#pragma warning restore CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
                dbm.Manager.Reconnect();
                return dbm.Manager.withdrawMoney(transactionID, senderID, amount);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }
            finally
            {
                dbm.Release();
            }
        }


        public bool giveMoney(UUID transactionID, string receiverID, int amount)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                return dbm.Manager.giveMoney(transactionID, receiverID, amount);
            }
#pragma warning disable CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
#pragma warning restore CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
                dbm.Manager.Reconnect();
                return dbm.Manager.giveMoney(transactionID, receiverID, amount);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }
            finally
            {
                dbm.Release();
            }
        }


        public bool setTotalSale(TransactionData transaction)
        {
            if (transaction.Receiver == transaction.Sender) return false;
            if (transaction.Sender == UUID.Zero.ToString()) return false;

            MySQLSuperManager dbm = GetLockedConnection();

            int time = (int)((DateTime.UtcNow.Ticks - TicksToEpoch) / 10000000);
            try
            {
                return dbm.Manager.setTotalSale(transaction.Receiver, transaction.ObjectUUID, transaction.Type, 1, transaction.Amount, time);
            }
#pragma warning disable CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
#pragma warning restore CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
                dbm.Manager.Reconnect();
                return dbm.Manager.setTotalSale(transaction.Receiver, transaction.ObjectUUID, transaction.Type, 1, transaction.Amount, time);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }
            finally
            {
                dbm.Release();
            }
        }


        public bool addTransaction(TransactionData transaction)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                return dbm.Manager.addTransaction(transaction);
            }
#pragma warning disable CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
#pragma warning restore CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
                dbm.Manager.Reconnect();
                return dbm.Manager.addTransaction(transaction);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }
            finally
            {
                dbm.Release();
            }
        }


        public bool addUser(string userID, int balance, int status, int type)
        {
            TransactionData transaction = new TransactionData();
            transaction.TransUUID = UUID.Random();
            transaction.Sender = UUID.Zero.ToString();
            transaction.Receiver = userID;
            transaction.Amount = balance;
            transaction.ObjectUUID = UUID.Zero.ToString();
            transaction.ObjectName = string.Empty;
            transaction.RegionHandle = string.Empty;
            transaction.Type = (int)TransactionType.BirthGift;
            transaction.Time = (int)((DateTime.UtcNow.Ticks - TicksToEpoch) / 10000000); ;
            transaction.Status = (int)Status.PENDING_STATUS;
            transaction.SecureCode = UUID.Random().ToString();
            transaction.CommonName = string.Empty;
            transaction.Description = "addUser " + DateTime.UtcNow.ToString();

            bool ret = addTransaction(transaction);
            if (!ret) return false;

            //
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                ret = dbm.Manager.addUser(userID, 0, status, type);		// make Balance Table
            }
#pragma warning disable CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
#pragma warning restore CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
                dbm.Manager.Reconnect();
                ret = dbm.Manager.addUser(userID, 0, status, type);     // make Balance Table
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }
            finally
            {
                dbm.Release();
            }

            //
            if (ret) ret = giveMoney(transaction.TransUUID, userID, balance);
            return ret;
        }


        public bool updateTransactionStatus(UUID transactionID, int status, string description)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                return dbm.Manager.updateTransactionStatus(transactionID, status, description);
            }
#pragma warning disable CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
#pragma warning restore CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
                dbm.Manager.Reconnect();
                return dbm.Manager.updateTransactionStatus(transactionID, status, description);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }
            finally
            {
                dbm.Release();
            }
        }


        public bool SetTransExpired(int deadTime)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                return dbm.Manager.SetTransExpired(deadTime);
            }
#pragma warning disable CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
#pragma warning restore CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
                dbm.Manager.Reconnect();
                return dbm.Manager.SetTransExpired(deadTime);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }
            finally
            {
                dbm.Release();
            }
        }


        public bool ValidateTransfer(string secureCode, UUID transactionID)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                return dbm.Manager.ValidateTransfer(secureCode, transactionID);
            }
#pragma warning disable CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
#pragma warning restore CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
                dbm.Manager.Reconnect();
                return dbm.Manager.ValidateTransfer(secureCode, transactionID);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }
            finally
            {
                dbm.Release();
            }
        }


        public TransactionData FetchTransaction(UUID transactionID)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                return dbm.Manager.FetchTransaction(transactionID);
            }
#pragma warning disable CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
#pragma warning restore CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
                dbm.Manager.Reconnect();
                return dbm.Manager.FetchTransaction(transactionID);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
            finally
            {
                dbm.Release();
            }
        }


        public TransactionData FetchTransaction(string userID, int startTime, int endTime, int lastIndex)
        {
            MySQLSuperManager dbm = GetLockedConnection();
            TransactionData[] arrTransaction;

            uint index = 0;
            if (lastIndex >= 0) index = Convert.ToUInt32(lastIndex) + 1;

            try
            {
                arrTransaction = dbm.Manager.FetchTransaction(userID, startTime, endTime, index, 1);
            }
#pragma warning disable CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
#pragma warning restore CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
                dbm.Manager.Reconnect();
                arrTransaction = dbm.Manager.FetchTransaction(userID, startTime, endTime, index, 1);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
            finally
            {
                dbm.Release();
            }

            //
            if (arrTransaction.Length > 0)
            {
                return arrTransaction[0];
            }
            else
            {
                return null;
            }
        }


        public bool DoTransfer(UUID transactionUUID)
        {
            bool do_trans = false;

            TransactionData transaction = new TransactionData();
            transaction = FetchTransaction(transactionUUID);

            if (transaction != null && transaction.Status == (int)Status.PENDING_STATUS)
            {
                int balance = getBalance(transaction.Sender);

                //check the amount
                if (transaction.Amount >= 0 && balance >= transaction.Amount)
                {
                    if (withdrawMoney(transactionUUID, transaction.Sender, transaction.Amount))
                    {
                        //If receiver not found, add it to DB.
                        if (getBalance(transaction.Receiver) == -1)
                        {
                            m_log.ErrorFormat("[MONEY DB]: DoTransfer: Receiver not found in balances DB. {0}", transaction.Receiver);
                            return false;
                        }

                        if (giveMoney(transactionUUID, transaction.Receiver, transaction.Amount))
                        {
                            do_trans = true;
                        }
                        else
                        {	// give money to receiver failed. Refund Processing
                            m_log.ErrorFormat("[MONEY DB]: Give money to receiver {0} failed", transaction.Receiver);
                            //Return money to sender
                            if (giveMoney(transactionUUID, transaction.Sender, transaction.Amount))
                            {
                                m_log.ErrorFormat("[MONEY DB]: give money to receiver {0} failed but return it to sender {1} successfully",
                                                        transaction.Receiver, transaction.Sender);
                                updateTransactionStatus(transactionUUID, (int)Status.FAILED_STATUS, "give money to receiver failed but return it to sender successfully");
                            }
                            else
                            {
                                m_log.ErrorFormat("[MONEY DB]: FATAL ERROR: Money withdrawn from sender: {0}, but failed to be given to receiver {1}",
                                                        transaction.Sender, transaction.Receiver);
                                updateTransactionStatus(transactionUUID, (int)Status.ERROR_STATUS, "give money to receiver failed, and return it to sender unsuccessfully!!!");
                            }
                        }
                    }
                    else
                    {	// withdraw money failed
                        m_log.ErrorFormat("[MONEY DB]: Withdraw money from sender {0} failed", transaction.Sender);
                    }
                }
                else
                {	// not enough balance to finish the transaction
                    m_log.ErrorFormat("[MONEY DB]: Not enough balance for user: {0} to apply the transaction.", transaction.Sender);
                }
            }
            else
            {	// Can not fetch the transaction or it has expired
                m_log.ErrorFormat("[MONEY DB]: The transaction:{0} has expired", transactionUUID.ToString());
            }

            //
            if (do_trans)
            {
                setTotalSale(transaction);
            }

            return do_trans;
        }


        // by Fumi.Iseki
        public bool DoAddMoney(UUID transactionUUID)
        {
            TransactionData transaction = new TransactionData();
            transaction = FetchTransaction(transactionUUID);

            if (transaction != null && transaction.Status == (int)Status.PENDING_STATUS)
            {
                //If receiver not found, add it to DB.
                if (getBalance(transaction.Receiver) == -1)
                {
                    m_log.ErrorFormat("[MONEY DB]: DoAddMoney: Receiver not found in balances DB. {0}", transaction.Receiver);
                    return false;
                }
                //
                if (giveMoney(transactionUUID, transaction.Receiver, transaction.Amount))
                {
                    setTotalSale(transaction);
                    return true;
                }
                else
                {	// give money to receiver failed.
                    m_log.ErrorFormat("[MONEY DB]: Add money to receiver {0} failed", transaction.Receiver);
                    updateTransactionStatus(transactionUUID, (int)Status.FAILED_STATUS, "add money to receiver failed");
                }
            }
            else
            {	// Can not fetch the transaction or it has expired
                m_log.ErrorFormat("[MONEY DB]: The transaction:{0} has expired", transactionUUID.ToString());
            }

            return false;
        }


        ///////////////////////////////////////////////////////////////////////////////////////////////////////
        //
        // userinfo
        //

        public bool TryAddUserInfo(UserInfo user)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            UserInfo userInfo = null;

            try
            {
                userInfo = dbm.Manager.fetchUserInfo(user.UserID);
            }
#pragma warning disable CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
#pragma warning restore CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
                dbm.Manager.Reconnect();
                userInfo = dbm.Manager.fetchUserInfo(user.UserID);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                dbm.Release();
                return false;
            }

            try
            {
                if (userInfo != null)
                {
                    //m_log.InfoFormat("[MONEY DB]: Found user \"{0}\", now update information", user.Avatar);
                    if (dbm.Manager.updateUserInfo(user)) return true;
                }
                else if (dbm.Manager.addUserInfo(user))
                {
                    //m_log.InfoFormat("[MONEY DB]: Unable to find user \"{0}\", add it to DB successfully", user.Avatar);
                    return true;
                }
                m_log.InfoFormat("[MONEY DB]: WARNNING: TryAddUserInfo: Unable to TryAddUserInfo.");
                return false;
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }
            finally
            {
                dbm.Release();
            }
        }


        public UserInfo FetchUserInfo(string userID)
        {
            UserInfo userInfo = null;
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                userInfo = dbm.Manager.fetchUserInfo(userID);
                return userInfo;
            }
#pragma warning disable CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
#pragma warning restore CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
                dbm.Manager.Reconnect();
                userInfo = dbm.Manager.fetchUserInfo(userID);
                return userInfo;
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
            finally
            {
                dbm.Release();
            }
        }


        public int getTransactionNum(string userID, int startTime, int endTime)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                return dbm.Manager.getTransactionNum(userID, startTime, endTime);
            }
#pragma warning disable CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
#pragma warning restore CS0168 // Variable ist deklariert, wird jedoch niemals verwendet
                dbm.Manager.Reconnect();
                return dbm.Manager.getTransactionNum(userID, startTime, endTime);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return -1;
            }
            finally
            {
                dbm.Release();
            }
        }
    }
}
