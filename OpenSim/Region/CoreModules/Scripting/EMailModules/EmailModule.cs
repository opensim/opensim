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
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using log4net;
using MailKit;
using MailKit.Net.Smtp;
using MimeKit;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Mono.Addins;

namespace OpenSim.Region.CoreModules.Scripting.EmailModules
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "EmailModule")]
    public class EmailModule : ISharedRegionModule, IEmailModule
    {
        public class throttleControlInfo
        {
            public double lastTime;
            public double count;
        }

        //
        // Log
        //
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //
        // Module vars
        //
        private string m_HostName = string.Empty;
        private bool SMTP_SERVER_TLS = false;
        private string SMTP_SERVER_HOSTNAME = null;
        private int SMTP_SERVER_PORT = 25;
        private MailboxAddress SMTP_MAIL_FROM = null;
        private string SMTP_SERVER_LOGIN = null;
        private string SMTP_SERVER_PASSWORD = null;

        private bool m_enableEmailToExternalObjects = true;
        private bool m_enableEmailToSMTP = true;

        private ParserOptions m_mailParseOptions;

        private int m_MaxQueueSize = 50; // maximum size of an object mail queue
        private Dictionary<UUID, List<Email>> m_MailQueues = new Dictionary<UUID, List<Email>>();
        private Dictionary<UUID, double> m_LastGetEmailCall = new Dictionary<UUID, double>();
        private Dictionary<UUID, throttleControlInfo> m_ownerThrottles = new Dictionary<UUID, throttleControlInfo>();
        private Dictionary<string, throttleControlInfo> m_primAddressThrottles = new Dictionary<string, throttleControlInfo>();
        private Dictionary<string, throttleControlInfo> m_SMPTAddressThrottles = new Dictionary<string, throttleControlInfo>();
        private double m_QueueTimeout = 30 * 60; // 15min;
        private double m_nextQueuesExpire;
        private double m_nextOwnerThrottlesExpire;
        private double m_nextPrimAddressThrottlesExpire;
        private double m_nextSMTPAddressThrottlesExpire;
        private string m_InterObjectHostname = "lsl.opensim.local";

        private int m_SMTP_MailsPerDay = 100;
        private double m_SMTP_MailsRate = 100.0 / 86400.0;
        private double m_SMTPLastTime;
        private double m_SMTPCount;

        private int m_MailsToPrimAddressPerHour = 50;
        private double m_MailsToPrimAddressRate = 50.0 / 3600.0;
        private int m_MailsToSMTPAddressPerHour = 10;
        private double m_MailsToSMTPAddressRate = 20.0 / 3600.0;

        private int m_MailsFromOwnerPerHour = 500;
        private double m_MailsFromOwnerRate = 500.0 / 3600.0;

        private int m_MaxEmailSize = 4096;  // largest email allowed by default, as per lsl docs.

        private static SslPolicyErrors m_SMTP_SslPolicyErrorsMask;
        private bool m_checkSpecName;

        private object m_queuesLock = new object();

        // Scenes by Region Handle
        private Dictionary<ulong, Scene> m_Scenes = new Dictionary<ulong, Scene>();

        private bool m_Enabled = false;

        #region ISharedRegionModule


        public void Initialise(IConfigSource config)
        {
            IConfig startupConfig = config.Configs["Startup"];
            if(startupConfig == null)
                return;

            if(startupConfig.GetString("emailmodule", "DefaultEmailModule") != "DefaultEmailModule")
                return;

            //Load SMTP SERVER config
            try
            {
                IConfig SMTPConfig = config.Configs["SMTP"];
                if (SMTPConfig  == null)
                    return;

                if(!SMTPConfig.GetBoolean("enabled", false))
                    return;

                m_enableEmailToExternalObjects = SMTPConfig.GetBoolean("enableEmailToExternalObjects", m_enableEmailToExternalObjects);
                m_enableEmailToSMTP = SMTPConfig.GetBoolean("enableEmailToSMTP", m_enableEmailToSMTP);

                m_MailsToPrimAddressPerHour = SMTPConfig.GetInt("MailsToPrimAddressPerHour", m_MailsToPrimAddressPerHour);
                m_MailsToPrimAddressRate = m_MailsToPrimAddressPerHour / 3600.0;
                m_MailsFromOwnerPerHour = SMTPConfig.GetInt("MailsFromOwnerPerHour", m_MailsFromOwnerPerHour);
                m_MailsFromOwnerRate = m_MailsFromOwnerPerHour / 3600.0;

                m_mailParseOptions = new ParserOptions()
                {
                    AllowAddressesWithoutDomain = false,
                };

                m_InterObjectHostname = SMTPConfig.GetString("internal_object_host", m_InterObjectHostname);
                m_checkSpecName = !m_InterObjectHostname.Equals("lsl.secondlife.com");

                if (m_enableEmailToSMTP)
                {
                    m_SMTP_MailsPerDay = SMTPConfig.GetInt("SMTP_MailsPerDay", m_SMTP_MailsPerDay);
                    m_SMTP_MailsRate = m_SMTP_MailsPerDay / 86400.0;
                    m_MailsToSMTPAddressPerHour = SMTPConfig.GetInt("MailsToSMTPAddressPerHour", m_MailsToPrimAddressPerHour);
                    m_MailsToSMTPAddressRate = m_MailsToPrimAddressPerHour / 3600.0;

                    SMTP_SERVER_HOSTNAME = SMTPConfig.GetString("SMTP_SERVER_HOSTNAME", SMTP_SERVER_HOSTNAME);
                    OSHHTPHost hosttmp = new OSHHTPHost(SMTP_SERVER_HOSTNAME, true);
                    if(!hosttmp.IsResolvedHost)
                    {
                        m_log.ErrorFormat("[EMAIL]: could not resolve SMTP_SERVER_HOSTNAME {0}", SMTP_SERVER_HOSTNAME);
                        return;
                    }

                    SMTP_SERVER_PORT = SMTPConfig.GetInt("SMTP_SERVER_PORT", SMTP_SERVER_PORT);
                    SMTP_SERVER_TLS = SMTPConfig.GetBoolean("SMTP_SERVER_TLS", SMTP_SERVER_TLS);

                    string smtpfrom = SMTPConfig.GetString("SMTP_SERVER_FROM", string.Empty);
                    m_HostName = SMTPConfig.GetString("host_domain_header_from", m_HostName);
                    if (!string.IsNullOrEmpty(smtpfrom) && !MailboxAddress.TryParse(m_mailParseOptions, smtpfrom, out SMTP_MAIL_FROM))
                    {
                        m_log.ErrorFormat("[EMAIL]: Invalid SMTP_SERVER_FROM {0}", smtpfrom);
                        return;
                    }

                    SMTP_SERVER_LOGIN = SMTPConfig.GetString("SMTP_SERVER_LOGIN", SMTP_SERVER_LOGIN);
                    SMTP_SERVER_PASSWORD = SMTPConfig.GetString("SMTP_SERVER_PASSWORD", SMTP_SERVER_PASSWORD);

                    bool VerifyCertChain = SMTPConfig.GetBoolean("SMTP_VerifyCertChain", true);
                    bool VerifyCertNames = SMTPConfig.GetBoolean("SMTP_VerifyCertNames", true);
                    m_SMTP_SslPolicyErrorsMask = VerifyCertChain ? 0 : SslPolicyErrors.RemoteCertificateChainErrors;
                    if (!VerifyCertNames)
                        m_SMTP_SslPolicyErrorsMask |= SslPolicyErrors.RemoteCertificateNameMismatch;
                    m_SMTP_SslPolicyErrorsMask = ~m_SMTP_SslPolicyErrorsMask;
                }
                else
                {
                    m_SMTP_SslPolicyErrorsMask = ~SslPolicyErrors.None;
                    m_log.Warn("[EMAIL]: SMTP disabled, set enableEmailSMTP to enable");
                }

                m_MaxEmailSize = SMTPConfig.GetInt("email_max_size", m_MaxEmailSize);
                if(m_MaxEmailSize < 256 || m_MaxEmailSize > 1000000)
                {
                    m_log.Warn("[EMAIL]: email_max_size out of range [256, 1000000], Changed to default 4096");
                    m_MaxEmailSize = 4096;
                }

            }
            catch (Exception e)
            {
                m_log.Error("[EMAIL]: DefaultEmailModule not configured: " + e.Message);
                return;
            }

            double now = Util.GetTimeStamp();
            m_nextQueuesExpire = now + m_QueueTimeout;
            m_nextOwnerThrottlesExpire = now + 3600;
            m_nextPrimAddressThrottlesExpire = now + 3600;
            m_nextSMTPAddressThrottlesExpire = now + 3600;

            m_SMTPLastTime = now;
            m_SMTPCount = m_SMTP_MailsPerDay;

            m_Enabled = true;
    }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

        // It's a go!
            lock (m_Scenes)
            {
                // Claim the interface slot
                scene.RegisterModuleInterface<IEmailModule>(this);

                // Add to scene list
                m_Scenes[scene.RegionInfo.RegionHandle] = scene;
            }

            m_log.Info("[EMAIL]: Activated DefaultEmailModule");
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "DefaultEmailModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        #endregion

        public void AddPartMailBox(UUID objectID)
        {
            if (m_Enabled)
            {
                lock (m_queuesLock)
                {
                    if (!m_MailQueues.TryGetValue(objectID, out List<Email> elist))
                    {
                        m_MailQueues[objectID] = null;
                        //TODO external global
                    }
                }
            }
        }

        public void RemovePartMailBox(UUID objectID)
        {
            if (m_Enabled)
            {
                lock (m_queuesLock)
                {
                    m_LastGetEmailCall.Remove(objectID);
                    if (m_MailQueues.Remove(objectID))
                    {
                        //TODO external global
                    }
                }
            }
        }

        public void InsertEmail(UUID to, Email email)
        {
            lock (m_queuesLock)
            {
                if (m_MailQueues.TryGetValue(to, out List<Email> elist))
                {
                    if(elist == null)
                    {
                        elist = new List<Email>();
                        elist.Add(email);
                        m_MailQueues[to] = elist;
                    }
                    else
                    {
                        if (elist.Count >= m_MaxQueueSize)
                            return;
                        lock (elist)
                            elist.Add(email);
                    }
                    m_LastGetEmailCall[to] = Util.GetTimeStamp() + m_QueueTimeout;
                }
            }
        }

        private bool IsLocal(UUID objectID)
        {
            lock (m_Scenes)
            {
                foreach (Scene s in m_Scenes.Values)
                {
                    if (s.GetSceneObjectPart(objectID) != null)
                        return true;
                }
            }
            return false;
        }

        private SceneObjectPart findPrim(UUID objectID, out string ObjectRegionName)
        {
            lock (m_Scenes)
            {
                foreach (Scene s in m_Scenes.Values)
                {
                    SceneObjectPart part = s.GetSceneObjectPart(objectID);
                    if (part != null)
                    {
                        RegionInfo sri = s.RegionInfo;
                        ObjectRegionName = sri.RegionName;
                        ObjectRegionName = ObjectRegionName + " (" + sri.WorldLocX + ", " + sri.WorldLocY + ")";
                        return part;
                    }
                }
            }
            ObjectRegionName = string.Empty;
            return null;
        }

        private bool resolveNamePositionRegionName(UUID objectID, out string ObjectName, out string ObjectAbsolutePosition, out string ObjectRegionName)
        {
            SceneObjectPart part = findPrim(objectID, out ObjectRegionName);
            if (part != null)
            {
                Vector3 pos = part.AbsolutePosition;
                ObjectAbsolutePosition = "(" + (int)pos.X + ", " + (int)pos.Y + ", " + (int)pos.Z + ")";
                ObjectName = part.Name;
                return true;
            }
            ObjectName = ObjectAbsolutePosition = ObjectRegionName = string.Empty;
            return false;
        }

        public static bool smptValidateServerCertificate(object sender, X509Certificate certificate,
                X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return (sslPolicyErrors & m_SMTP_SslPolicyErrorsMask) == SslPolicyErrors.None;
        }

        /// <summary>
        /// SendMail function utilized by llEMail
        /// </summary>
        /// <param name="objectID"></param>
        /// <param name="address"></param>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        public void SendEmail(UUID objectID, UUID ownerID, string address, string subject, string body)
        {
            //Check if address is empty or too large
            if(string.IsNullOrEmpty(address))
                return;
            address = address.Trim();
            if (address.Length == 0 || address.Length > 320)
                return;

            double now = Util.GetTimeStamp();
            throttleControlInfo tci;
            lock (m_ownerThrottles)
            {
                if (m_ownerThrottles.TryGetValue(ownerID, out tci))
                {
                    tci.count += (now - tci.lastTime) * m_MailsFromOwnerRate;
                    tci.lastTime = now;
                    if (tci.count > m_MailsFromOwnerPerHour)
                        tci.count = m_MailsFromOwnerPerHour;
                    else if (tci.count <= 0)
                        return;
                    --tci.count;
                }
                else
                {
                    tci = new throttleControlInfo
                    {
                        lastTime = now,
                        count = m_MailsFromOwnerPerHour
                    };
                    m_ownerThrottles[ownerID] = tci;
                }
            }

            string addressLower = address.ToLower();

            if (!MailboxAddress.TryParse(address, out MailboxAddress mailTo))
            {
                m_log.ErrorFormat("[EMAIL]: invalid TO email address {0}",address);
                return;
            }

            if ((subject.Length + body.Length) > m_MaxEmailSize)
            {
                m_log.Error("[EMAIL]: subject + body larger than limit of " + m_MaxEmailSize + " bytes");
                return;
            }

            if (!resolveNamePositionRegionName(objectID, out string LastObjectName, out string LastObjectPosition, out string LastObjectRegionName))
                return;

            string objectIDstr = objectID.ToString();
            if (!address.EndsWith(m_InterObjectHostname, StringComparison.InvariantCultureIgnoreCase) &&
                !(m_checkSpecName && address.EndsWith("lsl.secondlife.com", StringComparison.InvariantCultureIgnoreCase)))
            {
                if(!m_enableEmailToSMTP)
                    return; //smtp disabled

                m_SMTPCount += (m_SMTPLastTime - now) * m_SMTP_MailsRate;
                m_SMTPLastTime = now;
                if (m_SMTPCount > m_SMTP_MailsPerDay)
                    m_SMTPCount = m_SMTP_MailsPerDay;
                else if (m_SMTPCount <= 0)
                    return;
                --m_SMTPCount;

                lock (m_SMPTAddressThrottles)
                {
                    if (m_SMPTAddressThrottles.TryGetValue(addressLower, out tci))
                    {
                        tci.count += (now - tci.lastTime) * m_MailsToSMTPAddressRate;
                        tci.lastTime = now;
                        if (tci.count > m_MailsToSMTPAddressPerHour)
                            tci.count = m_MailsToSMTPAddressPerHour;
                        else if (tci.count <= 0)
                            return;
                        --tci.count;
                    }
                    else
                    {
                        tci = new throttleControlInfo
                        {
                            lastTime = now,
                            count = m_MailsToSMTPAddressPerHour
                        };
                        m_SMPTAddressThrottles[addressLower] = tci;
                    }
                }

                // regular email, send it out
                try
                {
                    //Creation EmailMessage
                    MimeMessage mmsg = new MimeMessage();

                    if(SMTP_MAIL_FROM != null)
                    {
                        mmsg.From.Add(SMTP_MAIL_FROM);
                        mmsg.Subject = "(OSObj" + objectIDstr + ") " + subject;
                    }
                    else
                    {
                        mmsg.From.Add(MailboxAddress.Parse(objectIDstr + "@" + m_HostName));
                        mmsg.Subject = subject;
                    }

                    mmsg.To.Add(mailTo);
                    mmsg.Headers["X-Owner-ID"] = ownerID.ToString();
                    mmsg.Headers["X-Task-ID"] = objectIDstr;
                    mmsg.Body = new TextPart("plain") {
                        Text = "Object-Name: " + LastObjectName +
                              "\nRegion: " + LastObjectRegionName + "\nLocal-Position: " +
                              LastObjectPosition + "\n\n" + body
                        };

                    using (var client = new SmtpClient())
                    {
                        if (SMTP_SERVER_TLS)
                        {
                            client.ServerCertificateValidationCallback = smptValidateServerCertificate;
                            client.Connect(SMTP_SERVER_HOSTNAME, SMTP_SERVER_PORT, MailKit.Security.SecureSocketOptions.StartTls);
                        }
                        else
                            client.Connect(SMTP_SERVER_HOSTNAME, SMTP_SERVER_PORT);

                        if (!string.IsNullOrEmpty(SMTP_SERVER_LOGIN) && !string.IsNullOrEmpty(SMTP_SERVER_PASSWORD))
                            client.Authenticate(SMTP_SERVER_LOGIN, SMTP_SERVER_PASSWORD);

                        client.Send(mmsg);
                        client.Disconnect(true);
                    }

                    //Log
                    m_log.Info("[EMAIL]: EMail sent to: " + address + " from object: " + objectID.ToString() + "@" + m_HostName);
                }
                catch (Exception e)
                {
                    m_log.Error("[EMAIL]: DefaultEmailModule Exception: " + e.Message);
                }
            }
            else
            {
                lock (m_primAddressThrottles)
                {
                    if (m_primAddressThrottles.TryGetValue(addressLower, out tci))
                    {
                        tci.count += (now - tci.lastTime) * m_MailsToPrimAddressRate;
                        tci.lastTime = now;
                        if (tci.count > m_MailsToPrimAddressPerHour)
                            tci.count = m_MailsToPrimAddressPerHour;
                        else if (tci.count <= 0)
                            return;
                        --tci.count;
                    }
                    else
                    {
                        tci = new throttleControlInfo
                        {
                            lastTime = now,
                            count = m_MailsToPrimAddressPerHour
                        };
                        m_primAddressThrottles[addressLower] = tci;
                    }
                }

                // inter object email
                int indx = address.IndexOf('@');
                if (indx < 0)
                    return;
                if (!UUID.TryParse(address.Substring(0, indx), out UUID toID))
                    return;

                Email email = new Email();
                email.time = Util.UnixTimeSinceEpoch().ToString();
                email.subject = subject;
                email.sender = objectID.ToString() + "@" + m_InterObjectHostname;
                email.message = "Object-Name: " + LastObjectName +
                              "\nRegion: " + LastObjectRegionName + "\nLocal-Position: " +
                              LastObjectPosition + "\n\n" + body;

                if (IsLocal(toID))
                {
                    // object in this instance
                    InsertEmail(toID, email);
                }
                else
                {
                    if (!m_enableEmailToExternalObjects)
                        return;
                    // object on another region
                    // TODO FIX
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="objectID"></param>
        /// <param name="sender"></param>
        /// <param name="subject"></param>
        /// <returns></returns>
        public Email GetNextEmail(UUID objectID, string sender, string subject)
        {
            double now = Util.GetTimeStamp();
            double lasthour = now - 3600;
            lock (m_ownerThrottles)
            {
                if (m_ownerThrottles.Count > 0 && now > m_nextOwnerThrottlesExpire)
                {
                    List<UUID> removal = new List<UUID>(m_ownerThrottles.Count);
                    foreach (KeyValuePair<UUID, throttleControlInfo> kpv in m_ownerThrottles)
                    {
                        if (kpv.Value.lastTime < lasthour)
                            removal.Add(kpv.Key);
                    }

                    foreach (UUID remove in removal)
                        m_ownerThrottles.Remove(remove);

                    m_nextOwnerThrottlesExpire = now + 3600;
                }
            }

            lock (m_primAddressThrottles)
            {
                if (m_primAddressThrottles.Count > 0 && now > m_nextPrimAddressThrottlesExpire)
                {
                    List<string> removal = new List<string>(m_primAddressThrottles.Count);
                    foreach (KeyValuePair<string, throttleControlInfo> kpv in m_primAddressThrottles)
                    {
                        if (kpv.Value.lastTime < lasthour)
                            removal.Add(kpv.Key);
                    }

                    foreach (string remove in removal)
                        m_primAddressThrottles.Remove(remove);

                    m_nextPrimAddressThrottlesExpire = now + 3600;
                }
            }

            lock (m_SMPTAddressThrottles)
            {
                if (m_SMPTAddressThrottles.Count > 0 && now > m_nextSMTPAddressThrottlesExpire)
                {
                    List<string> removal = new List<string>(m_SMPTAddressThrottles.Count);
                    foreach (KeyValuePair<string, throttleControlInfo> kpv in m_SMPTAddressThrottles)
                    {
                        if (kpv.Value.lastTime < lasthour)
                            removal.Add(kpv.Key);
                    }

                    foreach (string remove in removal)
                        m_SMPTAddressThrottles.Remove(remove);

                    m_nextSMTPAddressThrottlesExpire = now + 3600;
                }
            }

            lock (m_queuesLock)
            {
                m_LastGetEmailCall[objectID] = now + m_QueueTimeout;

                if(m_LastGetEmailCall.Count > 1 && now > m_nextQueuesExpire)
                {
                    List<UUID> removal = new List<UUID>(m_LastGetEmailCall.Count);
                    foreach (KeyValuePair<UUID, double> kpv in m_LastGetEmailCall)
                    {
                        if (kpv.Value < now)
                            removal.Add(kpv.Key);
                    }

                    foreach (UUID remove in removal)
                    {
                        m_LastGetEmailCall.Remove(remove);
                        m_MailQueues[remove] = null;
                    }
                    m_nextQueuesExpire = now + m_QueueTimeout;
                }

                m_MailQueues.TryGetValue(objectID, out List<Email> queue);
                if (queue != null)
                {
                    lock (queue)
                    {
                        if (queue.Count > 0)
                        {
                            bool emptySender = string.IsNullOrEmpty(sender);
                            bool emptySubject = string.IsNullOrEmpty(subject);

                            int i;
                            Email ret;
                            for (i = 0; i < queue.Count; i++)
                            {
                                ret = queue[i];
                                if (emptySender || sender.Equals(ret.sender, StringComparison.InvariantCultureIgnoreCase) &&
                                   (emptySubject || subject.Equals(ret.subject, StringComparison.InvariantCultureIgnoreCase)))
                                {
                                    if (queue.Count == 1)
                                    {
                                        m_MailQueues[objectID] = null;
                                        m_LastGetEmailCall.Remove(objectID);
                                        ret.numLeft = 0;
                                    }
                                    else
                                    {
                                        queue.RemoveAt(i);
                                        ret.numLeft = queue.Count;
                                    }
                                    return ret;
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }
    }
}
