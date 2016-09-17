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
using DotNetOpenMail;
using DotNetOpenMail.SmtpAuth;
using log4net;
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
        //
        // Log
        //
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //
        // Module vars
        //
        private IConfigSource m_Config;
        private string m_HostName = string.Empty;
        //private string m_RegionName = string.Empty;
        private string SMTP_SERVER_HOSTNAME = string.Empty;
        private int SMTP_SERVER_PORT = 25;
        private string SMTP_SERVER_LOGIN = string.Empty;
        private string SMTP_SERVER_PASSWORD = string.Empty;

        private int m_MaxQueueSize = 50; // maximum size of an object mail queue
        private Dictionary<UUID, List<Email>> m_MailQueues = new Dictionary<UUID, List<Email>>();
        private Dictionary<UUID, DateTime> m_LastGetEmailCall = new Dictionary<UUID, DateTime>();
        private TimeSpan m_QueueTimeout = new TimeSpan(2, 0, 0); // 2 hours without llGetNextEmail drops the queue
        private string m_InterObjectHostname = "lsl.opensim.local";

        private int m_MaxEmailSize = 4096;  // largest email allowed by default, as per lsl docs.

        // Scenes by Region Handle
        private Dictionary<ulong, Scene> m_Scenes =
            new Dictionary<ulong, Scene>();

        private bool m_Enabled = false;

        #region ISharedRegionModule

        public void Initialise(IConfigSource config)
        {
            m_Config = config;
            IConfig SMTPConfig;

            //FIXME: RegionName is correct??
            //m_RegionName = scene.RegionInfo.RegionName;

            IConfig startupConfig = m_Config.Configs["Startup"];

            m_Enabled = (startupConfig.GetString("emailmodule", "DefaultEmailModule") == "DefaultEmailModule");

            //Load SMTP SERVER config
            try
            {
                if ((SMTPConfig = m_Config.Configs["SMTP"]) == null)
                {
                    m_Enabled = false;
                    return;
                }

                if (!SMTPConfig.GetBoolean("enabled", false))
                {
                    m_Enabled = false;
                    return;
                }

                m_HostName = SMTPConfig.GetString("host_domain_header_from", m_HostName);
                m_InterObjectHostname = SMTPConfig.GetString("internal_object_host", m_InterObjectHostname);
                SMTP_SERVER_HOSTNAME = SMTPConfig.GetString("SMTP_SERVER_HOSTNAME", SMTP_SERVER_HOSTNAME);
                SMTP_SERVER_PORT = SMTPConfig.GetInt("SMTP_SERVER_PORT", SMTP_SERVER_PORT);
                SMTP_SERVER_LOGIN = SMTPConfig.GetString("SMTP_SERVER_LOGIN", SMTP_SERVER_LOGIN);
                SMTP_SERVER_PASSWORD = SMTPConfig.GetString("SMTP_SERVER_PASSWORD", SMTP_SERVER_PASSWORD);
                m_MaxEmailSize = SMTPConfig.GetInt("email_max_size", m_MaxEmailSize);
            }
            catch (Exception e)
            {
                m_log.Error("[EMAIL]: DefaultEmailModule not configured: " + e.Message);
                m_Enabled = false;
                return;
            }

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
                if (m_Scenes.ContainsKey(scene.RegionInfo.RegionHandle))
                {
                    m_Scenes[scene.RegionInfo.RegionHandle] = scene;
                }
                else
                {
                    m_Scenes.Add(scene.RegionInfo.RegionHandle, scene);
                }
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

        public void InsertEmail(UUID to, Email email)
        {
            // It's tempting to create the queue here.  Don't; objects which have
            // not yet called GetNextEmail should have no queue, and emails to them
            // should be silently dropped.

            lock (m_MailQueues)
            {
                if (m_MailQueues.ContainsKey(to))
                {
                    if (m_MailQueues[to].Count >= m_MaxQueueSize)
                    {
                        // fail silently
                        return;
                    }

                    lock (m_MailQueues[to])
                    {
                        m_MailQueues[to].Add(email);
                    }
                }
            }
        }

        private bool IsLocal(UUID objectID)
        {
            string unused;
            return (null != findPrim(objectID, out unused));
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
                        ObjectRegionName = s.RegionInfo.RegionName;
                        uint localX = s.RegionInfo.WorldLocX;
                        uint localY = s.RegionInfo.WorldLocY;
                        ObjectRegionName = ObjectRegionName + " (" + localX + ", " + localY + ")";
                        return part;
                    }
                }
            }
            ObjectRegionName = string.Empty;
            return null;
        }

        private bool resolveNamePositionRegionName(UUID objectID, out string ObjectName, out string ObjectAbsolutePosition, out string ObjectRegionName)
        {
            ObjectName = ObjectAbsolutePosition = ObjectRegionName = String.Empty;
            string m_ObjectRegionName;
            int objectLocX;
            int objectLocY;
            int objectLocZ;
            SceneObjectPart part = findPrim(objectID, out m_ObjectRegionName);
            if (part != null)
            {
                objectLocX = (int)part.AbsolutePosition.X;
                objectLocY = (int)part.AbsolutePosition.Y;
                objectLocZ = (int)part.AbsolutePosition.Z;
                ObjectAbsolutePosition = "(" + objectLocX + ", " + objectLocY + ", " + objectLocZ + ")";
                ObjectName = part.Name;
                ObjectRegionName = m_ObjectRegionName;
                return true;
            }
            return false;
        }

        /// <summary>
        /// SendMail function utilized by llEMail
        /// </summary>
        /// <param name="objectID"></param>
        /// <param name="address"></param>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        public void SendEmail(UUID objectID, string address, string subject, string body)
        {
            //Check if address is empty
            if (address == string.Empty)
                return;

            //FIXED:Check the email is correct form in REGEX
            string EMailpatternStrict = @"^(([^<>()[\]\\.,;:\s@\""]+"
                + @"(\.[^<>()[\]\\.,;:\s@\""]+)*)|(\"".+\""))@"
                + @"((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}"
                + @"\.[0-9]{1,3}\])|(([a-zA-Z\-0-9]+\.)+"
                + @"[a-zA-Z]{2,}))$";
            Regex EMailreStrict = new Regex(EMailpatternStrict);
            bool isEMailStrictMatch = EMailreStrict.IsMatch(address);
            if (!isEMailStrictMatch)
            {
                m_log.Error("[EMAIL]: REGEX Problem in EMail Address: "+address);
                return;
            }
            if ((subject.Length + body.Length) > m_MaxEmailSize)
            {
                m_log.Error("[EMAIL]: subject + body larger than limit of " + m_MaxEmailSize + " bytes");
                return;
            }

            string LastObjectName = string.Empty;
            string LastObjectPosition = string.Empty;
            string LastObjectRegionName = string.Empty;

            if (!resolveNamePositionRegionName(objectID, out LastObjectName, out LastObjectPosition, out LastObjectRegionName))
                return;

            if (!address.EndsWith(m_InterObjectHostname))
            {
                // regular email, send it out
                try
                {
                    //Creation EmailMessage
                    EmailMessage emailMessage = new EmailMessage();
                    //From
                    emailMessage.FromAddress = new EmailAddress(objectID.ToString() + "@" + m_HostName);
                    //To - Only One
                    emailMessage.AddToAddress(new EmailAddress(address));
                    //Subject
                    emailMessage.Subject = subject;
                    //TEXT Body
                    if (!resolveNamePositionRegionName(objectID, out LastObjectName, out LastObjectPosition, out LastObjectRegionName))
                        return;
                    emailMessage.BodyText = "Object-Name: " + LastObjectName +
                              "\nRegion: " + LastObjectRegionName + "\nLocal-Position: " +
                              LastObjectPosition + "\n\n" + body;

                    //Config SMTP Server
                    //Set SMTP SERVER config
                    SmtpServer smtpServer=new SmtpServer(SMTP_SERVER_HOSTNAME,SMTP_SERVER_PORT);
                    // Add authentication only when requested
                    //
                    if (SMTP_SERVER_LOGIN != String.Empty && SMTP_SERVER_PASSWORD != String.Empty)
                    {
                        //Authentication
                        smtpServer.SmtpAuthToken=new SmtpAuthToken(SMTP_SERVER_LOGIN, SMTP_SERVER_PASSWORD);
                    }
                    //Send Email Message
                    emailMessage.Send(smtpServer);

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
                // inter object email, keep it in the family
                Email email = new Email();
                email.time = ((int)((DateTime.UtcNow - new DateTime(1970,1,1,0,0,0)).TotalSeconds)).ToString();
                email.subject = subject;
                email.sender = objectID.ToString() + "@" + m_InterObjectHostname;
                email.message = "Object-Name: " + LastObjectName +
                              "\nRegion: " + LastObjectRegionName + "\nLocal-Position: " +
                              LastObjectPosition + "\n\n" + body;

                string guid = address.Substring(0, address.IndexOf("@"));
                UUID toID = new UUID(guid);

                if (IsLocal(toID)) // TODO FIX check to see if it is local
                {
                    // object in this region
                    InsertEmail(toID, email);
                }
                else
                {
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
            List<Email> queue = null;

            lock (m_LastGetEmailCall)
            {
                if (m_LastGetEmailCall.ContainsKey(objectID))
                {
                    m_LastGetEmailCall.Remove(objectID);
                }

                m_LastGetEmailCall.Add(objectID, DateTime.Now);

                // Hopefully this isn't too time consuming.  If it is, we can always push it into a worker thread.
                DateTime now = DateTime.Now;
                List<UUID> removal = new List<UUID>();
                foreach (UUID uuid in m_LastGetEmailCall.Keys)
                {
                    if ((now - m_LastGetEmailCall[uuid]) > m_QueueTimeout)
                    {
                        removal.Add(uuid);
                    }
                }

                foreach (UUID remove in removal)
                {
                    m_LastGetEmailCall.Remove(remove);
                    lock (m_MailQueues)
                    {
                        m_MailQueues.Remove(remove);
                    }
                }
            }

            lock (m_MailQueues)
            {
                if (m_MailQueues.ContainsKey(objectID))
                {
                    queue = m_MailQueues[objectID];
                }
            }

            if (queue != null)
            {
                lock (queue)
                {
                    if (queue.Count > 0)
                    {
                        int i;

                        for (i = 0; i < queue.Count; i++)
                        {
                            if ((sender == null || sender.Equals("") || sender.Equals(queue[i].sender)) &&
                                (subject == null || subject.Equals("") || subject.Equals(queue[i].subject)))
                            {
                                break;
                            }
                        }

                        if (i != queue.Count)
                        {
                            Email ret = queue[i];
                            queue.Remove(ret);
                            ret.numLeft = queue.Count;
                            return ret;
                        }
                    }
                }
            }
            else
            {
                lock (m_MailQueues)
                {
                    m_MailQueues.Add(objectID, new List<Email>());
                }
            }

            return null;
        }
    }
}
