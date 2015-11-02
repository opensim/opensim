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
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Avatar.Chat
{

    // An instance of this class exists for each unique combination of 
    // IRC chat interface characteristics, as determined by the supplied
    // configuration file.

    internal class ChannelState
    {

        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static Regex arg = new Regex(@"(?<!\\)\[[^\[\]]*(?<!\\)\]");
        private static int _idk_ = 0;
        private static int DEBUG_CHANNEL = 2147483647;

        // These are the IRC Connector configurable parameters with hard-wired
        // default values (retained for compatability).

        internal string Server = null;
        internal string Password = null;
        internal string IrcChannel = null;
        internal string BaseNickname = "OSimBot";
        internal uint Port = 6667;
        internal string User = null;

        internal bool ClientReporting = true;
        internal bool RelayChat = true;
        internal bool RelayPrivateChannels = false;
        internal int RelayChannel = 1;
        internal List<int> ValidInWorldChannels = new List<int>();

        // Connector agnostic parameters. These values are NOT shared with the
        // connector and do not differentiate at an IRC level

        internal string PrivateMessageFormat = "PRIVMSG {0} :<{2}> {1} {3}";
        internal string NoticeMessageFormat = "PRIVMSG {0} :<{2}> {3}";
        internal int RelayChannelOut = -1;
        internal bool RandomizeNickname = true;
        internal bool CommandsEnabled = false;
        internal int CommandChannel = -1;
        internal int ConnectDelay = 10;
        internal int PingDelay = 15;
        internal string DefaultZone = "Sim";

        internal string _accessPassword = String.Empty;
        internal Regex AccessPasswordRegex = null;
        internal List<string> ExcludeList = new List<string>();
        internal string AccessPassword
        {
            get { return _accessPassword; }
            set
            {
                _accessPassword = value;
                AccessPasswordRegex = new Regex(String.Format(@"^{0},\s*(?<avatar>[^,]+),\s*(?<message>.+)$", _accessPassword),
                                                RegexOptions.Compiled);
            }
        }



        // IRC connector reference

        internal IRCConnector irc = null;

        internal int idn = _idk_++;

        // List of regions dependent upon this connection

        internal List<RegionState> clientregions = new List<RegionState>();

        // Needed by OpenChannel

        internal ChannelState()
        {
        }

        // This constructor is used by the Update* methods. A copy of the
        // existing channel state is created, and distinguishing characteristics
        // are copied across.

        internal ChannelState(ChannelState model)
        {
            Server = model.Server;
            Password = model.Password;
            IrcChannel = model.IrcChannel;
            Port = model.Port;
            BaseNickname = model.BaseNickname;
            RandomizeNickname = model.RandomizeNickname;
            User = model.User;
            CommandsEnabled = model.CommandsEnabled;
            CommandChannel = model.CommandChannel;
            RelayChat = model.RelayChat;
            RelayPrivateChannels = model.RelayPrivateChannels;
            RelayChannelOut = model.RelayChannelOut;
            RelayChannel = model.RelayChannel;
            ValidInWorldChannels = model.ValidInWorldChannels;
            PrivateMessageFormat = model.PrivateMessageFormat;
            NoticeMessageFormat = model.NoticeMessageFormat;
            ClientReporting = model.ClientReporting;
            AccessPassword = model.AccessPassword;
            DefaultZone = model.DefaultZone;
            ConnectDelay = model.ConnectDelay;
            PingDelay = model.PingDelay;
        }

        // Read the configuration file, performing variable substitution and any
        // necessary aliasing. See accompanying documentation for how this works.
        // If you don't need variables, then this works exactly as before.
        // If either channel or server are not specified, the request fails.

        internal static void OpenChannel(RegionState rs, IConfig config)
        {

            // Create a new instance of a channel. This may not actually
            // get used if an equivalent channel already exists.

            ChannelState cs = new ChannelState();

            // Read in the configuration file and filter everything for variable
            // subsititution.

            m_log.DebugFormat("[IRC-Channel-{0}] Initial request by Region {1} to connect to IRC", cs.idn, rs.Region);

            cs.Server = Substitute(rs, config.GetString("server", null));
            m_log.DebugFormat("[IRC-Channel-{0}] Server : <{1}>", cs.idn, cs.Server);
            cs.Password = Substitute(rs, config.GetString("password", null));
            // probably not a good idea to put a password in the log file
            cs.User = Substitute(rs, config.GetString("user", null));
            cs.IrcChannel = Substitute(rs, config.GetString("channel", null));
            m_log.DebugFormat("[IRC-Channel-{0}] IrcChannel : <{1}>", cs.idn, cs.IrcChannel);
            cs.Port = Convert.ToUInt32(Substitute(rs, config.GetString("port", Convert.ToString(cs.Port))));
            m_log.DebugFormat("[IRC-Channel-{0}] Port : <{1}>", cs.idn, cs.Port);
            cs.BaseNickname = Substitute(rs, config.GetString("nick", cs.BaseNickname));
            m_log.DebugFormat("[IRC-Channel-{0}] BaseNickname : <{1}>", cs.idn, cs.BaseNickname);
            cs.RandomizeNickname = Convert.ToBoolean(Substitute(rs, config.GetString("randomize_nick", Convert.ToString(cs.RandomizeNickname))));
            m_log.DebugFormat("[IRC-Channel-{0}] RandomizeNickname : <{1}>", cs.idn, cs.RandomizeNickname);
            cs.RandomizeNickname = Convert.ToBoolean(Substitute(rs, config.GetString("nicknum", Convert.ToString(cs.RandomizeNickname))));
            m_log.DebugFormat("[IRC-Channel-{0}] RandomizeNickname : <{1}>", cs.idn, cs.RandomizeNickname);
            cs.User = Substitute(rs, config.GetString("username", cs.User));
            m_log.DebugFormat("[IRC-Channel-{0}] User : <{1}>", cs.idn, cs.User);
            cs.CommandsEnabled = Convert.ToBoolean(Substitute(rs, config.GetString("commands_enabled", Convert.ToString(cs.CommandsEnabled))));
            m_log.DebugFormat("[IRC-Channel-{0}] CommandsEnabled : <{1}>", cs.idn, cs.CommandsEnabled);
            cs.CommandChannel = Convert.ToInt32(Substitute(rs, config.GetString("commandchannel", Convert.ToString(cs.CommandChannel))));
            m_log.DebugFormat("[IRC-Channel-{0}] CommandChannel : <{1}>", cs.idn, cs.CommandChannel);
            cs.CommandChannel = Convert.ToInt32(Substitute(rs, config.GetString("command_channel", Convert.ToString(cs.CommandChannel))));
            m_log.DebugFormat("[IRC-Channel-{0}] CommandChannel : <{1}>", cs.idn, cs.CommandChannel);
            cs.RelayChat = Convert.ToBoolean(Substitute(rs, config.GetString("relay_chat", Convert.ToString(cs.RelayChat))));
            m_log.DebugFormat("[IRC-Channel-{0}] RelayChat           : <{1}>", cs.idn, cs.RelayChat);
            cs.RelayPrivateChannels = Convert.ToBoolean(Substitute(rs, config.GetString("relay_private_channels", Convert.ToString(cs.RelayPrivateChannels))));
            m_log.DebugFormat("[IRC-Channel-{0}] RelayPrivateChannels : <{1}>", cs.idn, cs.RelayPrivateChannels);
            cs.RelayPrivateChannels = Convert.ToBoolean(Substitute(rs, config.GetString("useworldcomm", Convert.ToString(cs.RelayPrivateChannels))));
            m_log.DebugFormat("[IRC-Channel-{0}] RelayPrivateChannels : <{1}>", cs.idn, cs.RelayPrivateChannels);
            cs.RelayChannelOut = Convert.ToInt32(Substitute(rs, config.GetString("relay_private_channel_out", Convert.ToString(cs.RelayChannelOut))));
            m_log.DebugFormat("[IRC-Channel-{0}] RelayChannelOut : <{1}>", cs.idn, cs.RelayChannelOut);
            cs.RelayChannel = Convert.ToInt32(Substitute(rs, config.GetString("relay_private_channel_in", Convert.ToString(cs.RelayChannel))));
            m_log.DebugFormat("[IRC-Channel-{0}] RelayChannel : <{1}>", cs.idn, cs.RelayChannel);
            cs.RelayChannel = Convert.ToInt32(Substitute(rs, config.GetString("inchannel", Convert.ToString(cs.RelayChannel))));
            m_log.DebugFormat("[IRC-Channel-{0}] RelayChannel : <{1}>", cs.idn, cs.RelayChannel);
            cs.PrivateMessageFormat = Substitute(rs, config.GetString("msgformat", cs.PrivateMessageFormat));
            m_log.DebugFormat("[IRC-Channel-{0}] PrivateMessageFormat : <{1}>", cs.idn, cs.PrivateMessageFormat);
            cs.NoticeMessageFormat = Substitute(rs, config.GetString("noticeformat", cs.NoticeMessageFormat));
            m_log.DebugFormat("[IRC-Channel-{0}] NoticeMessageFormat : <{1}>", cs.idn, cs.NoticeMessageFormat);
            cs.ClientReporting = Convert.ToInt32(Substitute(rs, config.GetString("verbosity", cs.ClientReporting ? "1" : "0"))) > 0;
            m_log.DebugFormat("[IRC-Channel-{0}] ClientReporting : <{1}>", cs.idn, cs.ClientReporting);
            cs.ClientReporting = Convert.ToBoolean(Substitute(rs, config.GetString("report_clients", Convert.ToString(cs.ClientReporting))));
            m_log.DebugFormat("[IRC-Channel-{0}] ClientReporting : <{1}>", cs.idn, cs.ClientReporting);
            cs.DefaultZone = Substitute(rs, config.GetString("fallback_region", cs.DefaultZone));
            m_log.DebugFormat("[IRC-Channel-{0}] DefaultZone : <{1}>", cs.idn, cs.DefaultZone);
            cs.ConnectDelay = Convert.ToInt32(Substitute(rs, config.GetString("connect_delay", Convert.ToString(cs.ConnectDelay))));
            m_log.DebugFormat("[IRC-Channel-{0}] ConnectDelay : <{1}>", cs.idn, cs.ConnectDelay);
            cs.PingDelay = Convert.ToInt32(Substitute(rs, config.GetString("ping_delay", Convert.ToString(cs.PingDelay))));
            m_log.DebugFormat("[IRC-Channel-{0}] PingDelay : <{1}>", cs.idn, cs.PingDelay);
            cs.AccessPassword = Substitute(rs, config.GetString("access_password", cs.AccessPassword));
            m_log.DebugFormat("[IRC-Channel-{0}] AccessPassword : <{1}>", cs.idn, cs.AccessPassword);
            string[] excludes = config.GetString("exclude_list", "").Trim().Split(new Char[] { ',' });
            cs.ExcludeList = new List<string>(excludes.Length);
            foreach (string name in excludes)
            {
                cs.ExcludeList.Add(name.Trim().ToLower());
            }

            // Fail if fundamental information is still missing

            if (cs.Server == null)
                throw new Exception(String.Format("[IRC-Channel-{0}] Invalid configuration for region {1}: server missing", cs.idn, rs.Region));
            else if (cs.IrcChannel == null)
                throw new Exception(String.Format("[IRC-Channel-{0}] Invalid configuration for region {1}: channel missing", cs.idn, rs.Region));
            else if (cs.BaseNickname == null)
                throw new Exception(String.Format("[IRC-Channel-{0}] Invalid configuration for region {1}: nick missing", cs.idn, rs.Region));
            else if (cs.User == null)
                throw new Exception(String.Format("[IRC-Channel-{0}] Invalid configuration for region {1}: user missing", cs.idn, rs.Region));

            m_log.InfoFormat("[IRC-Channel-{0}] Configuration for Region {1} is valid", cs.idn, rs.Region);
            m_log.InfoFormat("[IRC-Channel-{0}]    Server = {1}", cs.idn, cs.Server);
            m_log.InfoFormat("[IRC-Channel-{0}]   Channel = {1}", cs.idn, cs.IrcChannel);
            m_log.InfoFormat("[IRC-Channel-{0}]      Port = {1}", cs.idn, cs.Port);
            m_log.InfoFormat("[IRC-Channel-{0}]  Nickname = {1}", cs.idn, cs.BaseNickname);
            m_log.InfoFormat("[IRC-Channel-{0}]      User = {1}", cs.idn, cs.User);

            // Set the channel state for this region

            if (cs.RelayChat)
            {
                cs.ValidInWorldChannels.Add(0);
                cs.ValidInWorldChannels.Add(DEBUG_CHANNEL);
            }

            if (cs.RelayPrivateChannels)
                cs.ValidInWorldChannels.Add(cs.RelayChannelOut);

            rs.cs = Integrate(rs, cs);

        }

        // An initialized channel state instance is passed in. If an identical
        // channel state instance already exists, then the existing instance
        // is used to replace the supplied value.
        // If the instance matches with respect to IRC, then the underlying
        // IRCConnector is assigned to the supplied channel state and the
        // updated value is returned.
        // If there is no match, then the supplied instance is completed by
        // creating and assigning an instance of an IRC connector.

        private static ChannelState Integrate(RegionState rs, ChannelState p_cs)
        {

            ChannelState cs = p_cs;

            // Check to see if we have an existing server/channel setup that can be used
            // In the absence of variable substitution this will always resolve to the 
            // same ChannelState instance, and the table will only contains a single 
            // entry, so the performance considerations for the existing behavior are 
            // zero. Only the IRC connector is shared, the ChannelState still contains
            // values that, while independent of the IRC connetion, do still distinguish 
            // this region's behavior.

            lock (IRCBridgeModule.m_channels)
            {

                foreach (ChannelState xcs in IRCBridgeModule.m_channels)
                {
                    if (cs.IsAPerfectMatchFor(xcs))
                    {
                        m_log.DebugFormat("[IRC-Channel-{0}]  Channel state matched", cs.idn);
                        cs = xcs;
                        break;
                    }
                    if (cs.IsAConnectionMatchFor(xcs))
                    {
                        m_log.DebugFormat("[IRC-Channel-{0}]  Channel matched", cs.idn);
                        cs.irc = xcs.irc;
                        break;
                    }
                }

            }

            // No entry was found, so this is going to be a new entry.

            if (cs.irc == null)
            {

                m_log.DebugFormat("[IRC-Channel-{0}]  New channel required", cs.idn);

                if ((cs.irc = new IRCConnector(cs)) != null)
                {

                    IRCBridgeModule.m_channels.Add(cs);

                    m_log.InfoFormat("[IRC-Channel-{0}] New channel initialized for {1}, nick: {2}, commands {3}, private channels {4}",
                                 cs.idn, rs.Region, cs.DefaultZone,
                                 cs.CommandsEnabled ? "enabled" : "not enabled",
                                 cs.RelayPrivateChannels ? "relayed" : "not relayed");
                }
                else
                {
                    string txt = String.Format("[IRC-Channel-{0}] Region {1} failed to connect to channel {2} on server {3}:{4}",
                            cs.idn, rs.Region, cs.IrcChannel, cs.Server, cs.Port);
                    m_log.Error(txt);
                    throw new Exception(txt);
                }
            }
            else
            {
                m_log.InfoFormat("[IRC-Channel-{0}] Region {1} reusing existing connection to channel {2} on server {3}:{4}",
                        cs.idn, rs.Region, cs.IrcChannel, cs.Server, cs.Port);
            }

            m_log.InfoFormat("[IRC-Channel-{0}] Region {1} associated with channel {2} on server {3}:{4}",
                        cs.idn, rs.Region, cs.IrcChannel, cs.Server, cs.Port);

            // We're finally ready to commit ourselves


            return cs;

        }

        // These routines allow differentiating changes to 
        // the underlying channel state. If necessary, a
        // new channel state will be created.

        internal ChannelState UpdateServer(RegionState rs, string server)
        {
            RemoveRegion(rs);
            ChannelState cs = new ChannelState(this);
            cs.Server = server;
            cs = Integrate(rs, cs);
            cs.AddRegion(rs);
            return cs;
        }

        internal ChannelState UpdatePort(RegionState rs, string port)
        {
            RemoveRegion(rs);
            ChannelState cs = new ChannelState(this);
            cs.Port = Convert.ToUInt32(port);
            cs = Integrate(rs, cs);
            cs.AddRegion(rs);
            return cs;
        }

        internal ChannelState UpdateChannel(RegionState rs, string channel)
        {
            RemoveRegion(rs);
            ChannelState cs = new ChannelState(this);
            cs.IrcChannel = channel;
            cs = Integrate(rs, cs);
            cs.AddRegion(rs);
            return cs;
        }

        internal ChannelState UpdateNickname(RegionState rs, string nickname)
        {
            RemoveRegion(rs);
            ChannelState cs = new ChannelState(this);
            cs.BaseNickname = nickname;
            cs = Integrate(rs, cs);
            cs.AddRegion(rs);
            return cs;
        }

        internal ChannelState UpdateClientReporting(RegionState rs, string cr)
        {
            RemoveRegion(rs);
            ChannelState cs = new ChannelState(this);
            cs.ClientReporting = Convert.ToBoolean(cr);
            cs = Integrate(rs, cs);
            cs.AddRegion(rs);
            return cs;
        }

        internal ChannelState UpdateRelayIn(RegionState rs, string channel)
        {
            RemoveRegion(rs);
            ChannelState cs = new ChannelState(this);
            cs.RelayChannel = Convert.ToInt32(channel);
            cs = Integrate(rs, cs);
            cs.AddRegion(rs);
            return cs;
        }

        internal ChannelState UpdateRelayOut(RegionState rs, string channel)
        {
            RemoveRegion(rs);
            ChannelState cs = new ChannelState(this);
            cs.RelayChannelOut = Convert.ToInt32(channel);
            cs = Integrate(rs, cs);
            cs.AddRegion(rs);
            return cs;
        }

        // Determine whether or not this is a 'new' channel. Only those
        // attributes that uniquely distinguish an IRC connection should
        // be included here (and only those attributes should really be
        // in the ChannelState structure)

        private bool IsAConnectionMatchFor(ChannelState cs)
        {
            return (
                Server == cs.Server &&
                IrcChannel == cs.IrcChannel &&
                Port == cs.Port &&
                BaseNickname == cs.BaseNickname &&
                User == cs.User
            );
        }

        // This level of obsessive matching allows us to produce
        // a minimal overhead int he case of a server which does 
        // need to differentiate IRC at a region level.

        private bool IsAPerfectMatchFor(ChannelState cs)
        {
            return (IsAConnectionMatchFor(cs) &&
                     RelayChannelOut == cs.RelayChannelOut &&
                     PrivateMessageFormat == cs.PrivateMessageFormat &&
                     NoticeMessageFormat == cs.NoticeMessageFormat &&
                     RandomizeNickname == cs.RandomizeNickname &&
                     AccessPassword == cs.AccessPassword &&
                     CommandsEnabled == cs.CommandsEnabled &&
                     CommandChannel == cs.CommandChannel &&
                     DefaultZone == cs.DefaultZone &&
                     RelayPrivateChannels == cs.RelayPrivateChannels &&
                     RelayChannel == cs.RelayChannel &&
                     RelayChat == cs.RelayChat &&
                     ClientReporting == cs.ClientReporting
            );
        }

        // This function implements the variable substitution mechanism 
        // for the configuration values. Each string read from the 
        // configuration file is scanned for '[...]' enclosures. Each
        // one that is found is replaced by either a runtime variable
        // (%xxx) or an existing configuration key. When no further
        // substitution is possible, the remaining string is returned
        // to the caller. This allows for arbitrarily nested
        // enclosures.

        private static string Substitute(RegionState rs, string instr)
        {

            string result = instr;

            if (string.IsNullOrEmpty(result))
                return result;

            // Repeatedly scan the string until all possible
            // substitutions have been performed.

            // m_log.DebugFormat("[IRC-Channel] Parse[1]: {0}", result);

            while (arg.IsMatch(result))
            {

                string vvar = arg.Match(result).ToString();
                string var = vvar.Substring(1, vvar.Length - 2).Trim();

                switch (var.ToLower())
                {
                    case "%region":
                        result = result.Replace(vvar, rs.Region);
                        break;
                    case "%host":
                        result = result.Replace(vvar, rs.Host);
                        break;
                    case "%locx":
                        result = result.Replace(vvar, rs.LocX);
                        break;
                    case "%locy":
                        result = result.Replace(vvar, rs.LocY);
                        break;
                    case "%k":
                        result = result.Replace(vvar, rs.IDK);
                        break;
                    default:
                        result = result.Replace(vvar, rs.config.GetString(var, var));
                        break;
                }
                // m_log.DebugFormat("[IRC-Channel] Parse[2]: {0}", result);
            }

            // Now we unescape the literal brackets
            result = result.Replace(@"\[","[").Replace(@"\]","]");

            // m_log.DebugFormat("[IRC-Channel] Parse[3]: {0}", result);
            return result;

        }

        public void Close()
        {
            m_log.InfoFormat("[IRC-Channel-{0}] Closing channel <{1}> to server <{2}:{3}>",
                             idn, IrcChannel, Server, Port);
            m_log.InfoFormat("[IRC-Channel-{0}] There are {1} active clients",
                             idn, clientregions.Count);
            irc.Close();
        }

        public void Open()
        {
            m_log.InfoFormat("[IRC-Channel-{0}] Opening channel <{1}> to server <{2}:{3}>",
                             idn, IrcChannel, Server, Port);

            irc.Open();

        }

        // These are called by each region that attaches to this channel. The call affects
        // only the relationship of the region with the channel. Not the channel to IRC
        // relationship (unless it is closed and we want it open).

        public void Open(RegionState rs)
        {
            AddRegion(rs);
            Open();
        }

        // Close is called to ensure that the IRC session is terminated if this is the
        // only client.

        public void Close(RegionState rs)
        {
            RemoveRegion(rs);
            lock (IRCBridgeModule.m_channels)
            {
                if (clientregions.Count == 0)
                {
                    Close();
                    IRCBridgeModule.m_channels.Remove(this);
                    m_log.InfoFormat("[IRC-Channel-{0}] Region {1} is last user of channel <{2}> to server <{3}:{4}>",
                             idn, rs.Region, IrcChannel, Server, Port);
                    m_log.InfoFormat("[IRC-Channel-{0}] Removed", idn);
                }
            }
        }

        // Add a client region to this channel if it is not already known

        public void AddRegion(RegionState rs)
        {
            m_log.InfoFormat("[IRC-Channel-{0}] Adding region {1} to channel <{2}> to server <{3}:{4}>",
                             idn, rs.Region, IrcChannel, Server, Port);
            if (!clientregions.Contains(rs))
            {
                clientregions.Add(rs);
                lock (irc) irc.depends++;
            }
        }

        // Remove a client region from the channel. If this is the last
        // region, then clean up the channel. The connector will clean itself
        // up if it finds itself about to be GC'd.

        public void RemoveRegion(RegionState rs)
        {

            m_log.InfoFormat("[IRC-Channel-{0}] Removing region {1} from channel <{2} to server <{3}:{4}>",
                             idn, rs.Region, IrcChannel, Server, Port);

            if (clientregions.Contains(rs))
            {
                clientregions.Remove(rs);
                lock (irc) irc.depends--;
            }

        }

        // This function is lifted from the IRCConnector because it 
        // contains information that is not differentiating from an
        // IRC point-of-view.

        public static void OSChat(IRCConnector p_irc, OSChatMessage c, bool cmsg)
        {

            // m_log.DebugFormat("[IRC-OSCHAT] from {0}:{1}", p_irc.Server, p_irc.IrcChannel);

            try
            {

                // Scan through the set of unique channel configuration for those
                // that belong to this connector. And then forward the message to 
                // all regions known to those channels.
                // Note that this code is responsible for completing some of the
                // settings for the inbound OSChatMessage

                lock (IRCBridgeModule.m_channels)
                {
                    foreach (ChannelState cs in IRCBridgeModule.m_channels)
                    {
                        if (p_irc == cs.irc)
                        {

                            // This non-IRC differentiator moved to here

                            if (cmsg && !cs.ClientReporting)
                                continue;

                            // This non-IRC differentiator moved to here

                            c.Channel = (cs.RelayPrivateChannels ? cs.RelayChannel : 0);

                            foreach (RegionState region in cs.clientregions)
                            {
                                region.OSChat(cs.irc, c);
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat("[IRC-OSCHAT]: BroadcastSim Exception: {0}", ex.Message);
                m_log.Debug(ex);
            }
        }
    }
}
