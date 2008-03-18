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
using System.Collections.Generic;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using MoneyTransferArgs = OpenSim.Region.Environment.Scenes.EventManager.MoneyTransferArgs;

namespace OpenSim.Region.Environment.Modules
{
    public class BetaGridLikeMoneyModule: IRegionModule
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<ulong,Scene> m_scenel = new Dictionary<ulong,Scene>();

        private IConfigSource m_gConfig;

        private bool m_keepMoneyAcrossLogins = true;

        private int m_minFundsBeforeRefresh = 100;

        private int m_stipend = 1000;

        private bool m_enabled = true;

        private Dictionary<LLUUID, int> m_KnownClientFunds = new Dictionary<LLUUID, int>();

        private bool gridmode = false;

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_gConfig = config;
            ReadConfigAndPopulate();

            if (m_enabled)
            {
                lock (m_scenel)
                {

                    if (m_scenel.ContainsKey(scene.RegionInfo.RegionHandle))
                    {
                        m_scenel[scene.RegionInfo.RegionHandle] = scene;
                    }
                    else
                    {
                        m_scenel.Add(scene.RegionInfo.RegionHandle, scene);
                    }
                }
                
                scene.EventManager.OnNewClient += OnNewClient;
                scene.EventManager.OnMoneyTransfer += MoneyTransferAction;
                scene.EventManager.OnClientClosed += ClientClosed;
            }
        }

        private void ReadConfigAndPopulate()
        {
            IConfig startupConfig = m_gConfig.Configs["Startup"];
            gridmode = startupConfig.GetBoolean("gridmode", false);
            m_enabled = (startupConfig.GetString("moneymodule", "BetaGridLikeMoneyModule") == "BetaGridLikeMoneyModule");
        }

        private void OnNewClient(IClientAPI client)
        {
            // Here we check if we're in grid mode
            // I imagine that the 'check balance' 
            // function for the client should be here or shortly after

            if (gridmode)
            {
                CheckExistAndRefreshFunds(client.AgentId);
            }
            else
            {
                CheckExistAndRefreshFunds(client.AgentId);
            }
            
            // Subscribe to Money messages
            client.OnMoneyBalanceRequest += SendMoneyBalance;
            client.OnLogout += ClientClosed;
        }

        public void ClientClosed(LLUUID AgentID)
        {
            lock (m_KnownClientFunds)
            {
                if (!m_keepMoneyAcrossLogins)
                    m_KnownClientFunds.Remove(AgentID);
            }
        }

        private void MoneyTransferAction (Object osender, MoneyTransferArgs e)
        {
            IClientAPI sender = null;
            IClientAPI receiver = null;
            
            sender = LocateClientObject(e.sender);
            if (sender != null)
            {
                receiver = LocateClientObject(e.reciever);
                bool transactionresult = doMoneyTranfer(e.sender, e.reciever, e.amount);

                if (e.sender != e.reciever)
                {
                    if (sender != null)
                    {
                        sender.SendMoneyBalance(LLUUID.Random(), transactionresult, Helpers.StringToField(e.description), GetFundsForAgentID(e.sender));
                    }
                }

                if (receiver != null)
                {
                    receiver.SendMoneyBalance(LLUUID.Random(), transactionresult, Helpers.StringToField(e.description), GetFundsForAgentID(e.reciever));
                }
                

            }
            else
            {
                m_log.Warn("[MONEY]: Potential Fraud Warning, got money transfer request for avatar that isn't in this simulator - Details; Sender:" + e.sender.ToString() + " Reciver: " + e.reciever.ToString() + " Amount: " + e.amount.ToString());
            }
        }

        private bool doMoneyTranfer(LLUUID Sender, LLUUID Receiver, int amount)
        {
            bool result = false;
            if (amount >= 0)
            {
                lock (m_KnownClientFunds)
                {
                    // If we don't know about the sender, then the sender can't 
                    // actually be here and therefore this is likely fraud or outdated.
                    if (m_KnownClientFunds.ContainsKey(Sender))
                    {
                        // Does the sender have enough funds to give?
                        if (m_KnownClientFunds[Sender] >= amount)
                        {
                            // Subtract the funds from the senders account
                            m_KnownClientFunds[Sender] -= amount;

                            // do we know about the receiver?
                            if (!m_KnownClientFunds.ContainsKey(Receiver))
                            {
                                // Make a record for them so they get the updated balance when they login
                                CheckExistAndRefreshFunds(Receiver);
                            }

                            //Add the amount to the Receiver's funds
                            m_KnownClientFunds[Receiver] += amount;
                            result = true;
                        }
                        else
                        {
                            // These below are redundant to make this clearer to read
                            result = false;
                        }
                    }
                    else
                    {
                        result = false;
                    }
                }
            }
            return result;
        }

        private IClientAPI LocateClientObject(LLUUID AgentID)
        {
            ScenePresence tPresence = null;
            IClientAPI rclient = null;

            lock (m_scenel)
            {
                foreach (Scene _scene in m_scenel.Values)
                {
                    tPresence = _scene.GetScenePresence(AgentID);
                    if (tPresence != null)
                    {
                        if (!tPresence.IsChildAgent)
                        {
                            rclient = tPresence.ControllingClient;
                        }
                    }
                    if (rclient != null)
                    {
                        return rclient;
                    }
                }

            }
            return null;
        }

        public void ClientClosed(IClientAPI client)
        {
            ClientClosed(client.AgentId);
        }

        public void SendMoneyBalance(IClientAPI client, LLUUID agentID, LLUUID SessionID, LLUUID TransactionID)
        {
            if (client.AgentId == agentID && client.SessionId == SessionID)
            {
                int returnfunds = 0;
                
                try
                {
                    returnfunds = GetFundsForAgentID(agentID);
                }
                catch (System.Exception e)
                {
                    client.SendAlertMessage(e.Message + " ");
                }
                
                client.SendMoneyBalance(TransactionID, true, new byte[0], returnfunds);
            }
            else
            {
                client.SendAlertMessage("Unable to send your money balance to you!");
            }
        }

        private void CheckExistAndRefreshFunds(LLUUID agentID)
        {
            lock (m_KnownClientFunds)
            {
                if (!m_KnownClientFunds.ContainsKey(agentID))
                {
                    m_KnownClientFunds.Add(agentID, m_stipend);
                }
                else
                {
                    if (m_KnownClientFunds[agentID] <= m_minFundsBeforeRefresh)
                    {
                        m_KnownClientFunds[agentID] = m_stipend;
                    }
                }
            }
        }

        private int GetFundsForAgentID(LLUUID AgentID)
        {
            int returnfunds = 0;
            lock (m_KnownClientFunds)
            {
                if (m_KnownClientFunds.ContainsKey(AgentID))
                {
                    returnfunds = m_KnownClientFunds[AgentID];
                }
                else
                {
                    throw new Exception("Unable to get funds.");
                }
            }
            return returnfunds;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "BetaGridLikeMoneyModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }
    }
}
