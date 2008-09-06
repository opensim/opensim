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
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using log4net;
using Nini.Config;
using DotNetOpenMail;
using DotNetOpenMail.SmtpAuth;

namespace OpenSim.Region.Environment.Modules.Scripting.EmailModules
{
    public class EmailModule : IEmailModule
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

        // Scenes by Region Handle
        private Dictionary<ulong, Scene> m_Scenes =
            new Dictionary<ulong, Scene>();

        private bool m_Enabled = false;

        public void Initialise(Scene scene, IConfigSource config)
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
                    m_log.InfoFormat("[SMTP] SMTP server not configured");
                    m_Enabled = false;
                    return;
                }

                if (!SMTPConfig.GetBoolean("enabled", false))
                {
                    m_log.InfoFormat("[SMTP] module disabled in configuration");
                    m_Enabled = false;
                    return;
                }

                m_HostName = SMTPConfig.GetString("host_domain_header_from", m_HostName);
                SMTP_SERVER_HOSTNAME = SMTPConfig.GetString("SMTP_SERVER_HOSTNAME",SMTP_SERVER_HOSTNAME);
                SMTP_SERVER_PORT = SMTPConfig.GetInt("SMTP_SERVER_PORT", SMTP_SERVER_PORT);
                SMTP_SERVER_LOGIN = SMTPConfig.GetString("SMTP_SERVER_LOGIN", SMTP_SERVER_LOGIN);
                SMTP_SERVER_PASSWORD = SMTPConfig.GetString("SMTP_SERVER_PASSWORD", SMTP_SERVER_PASSWORD);
            }
            catch (Exception e)
            {
                m_log.Error("[EMAIL] DefaultEmailModule not configured: "+ e.Message);
                m_Enabled = false;
                return;
            }

            // It's a go!
            if (m_Enabled)
            {
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

                m_log.Info("[EMAIL] Activated DefaultEmailModule");
            }
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

        public bool IsSharedModule
        {
            get { return true; }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="seconds"></param>
        private void DelayInSeconds(int seconds)
        {
            TimeSpan DiffDelay = new TimeSpan(0, 0, seconds);
            DateTime EndDelay = DateTime.Now.Add(DiffDelay);
            while (DateTime.Now < EndDelay)
            {
                ;//Do nothing!!
            }
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
                        return part;
                    }
                }
            }
            ObjectRegionName = string.Empty;
            return null;
        }

        private void resolveNamePositionRegionName(UUID objectID, out string ObjectName, out string ObjectAbsolutePosition, out string ObjectRegionName)
        {
            string m_ObjectRegionName;
            SceneObjectPart part = findPrim(objectID, out m_ObjectRegionName);
            if (part != null)
            {
                 ObjectAbsolutePosition = part.AbsolutePosition.ToString();
                 ObjectName = part.Name;
                 ObjectRegionName = m_ObjectRegionName;
                 return;
            }
            ObjectAbsolutePosition = part.AbsolutePosition.ToString();
            ObjectName = part.Name;
            ObjectRegionName = m_ObjectRegionName;
            return;
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
                m_log.Error("[EMAIL] REGEX Problem in EMail Address: "+address);
                return;
            }
            //FIXME:Check if subject + body = 4096 Byte
            if ((subject.Length + body.Length) > 1024)
            {
                m_log.Error("[EMAIL] subject + body > 1024 Byte");
                return;
            }

            try
            {
                string LastObjectName = string.Empty;
                string LastObjectPosition = string.Empty;
                string LastObjectRegionName = string.Empty;
                //DONE: Message as Second Life style
                //20 second delay - AntiSpam System - for now only 10 seconds
                DelayInSeconds(10);
                //Creation EmailMessage
                EmailMessage emailMessage = new EmailMessage();
                //From
                emailMessage.FromAddress = new EmailAddress(objectID.ToString()+"@"+m_HostName);
                //To - Only One
                emailMessage.AddToAddress(new EmailAddress(address));
                //Subject
                emailMessage.Subject = subject;
                //TEXT Body
                resolveNamePositionRegionName(objectID, out LastObjectName, out LastObjectPosition, out LastObjectRegionName);
                emailMessage.TextPart = new TextAttachment("Object-Name: " + LastObjectName +
                                                           "\r\nRegion: " + LastObjectRegionName + "\r\nLocal-Position: " +
                                                           LastObjectPosition+"\r\n\r\n\r\n" + body);
                //HTML Body
                emailMessage.HtmlPart = new HtmlAttachment("<html><body><p>" +
                                                           "<BR>Object-Name: " + LastObjectName +
                                                           "<BR>Region: " + LastObjectRegionName +
                                                           "<BR>Local-Position: " + LastObjectPosition + "<BR><BR><BR>"
                                                           +body+"\r\n</p></body><html>");

                //Set SMTP SERVER config
                SmtpServer smtpServer=new SmtpServer(SMTP_SERVER_HOSTNAME,SMTP_SERVER_PORT);
                //Authentication
                smtpServer.SmtpAuthToken=new SmtpAuthToken(SMTP_SERVER_LOGIN, SMTP_SERVER_PASSWORD);
                //Send Email Message
                emailMessage.Send(smtpServer);
                //Log
                m_log.Info("[EMAIL] EMail sent to: " + address + " from object: " + objectID.ToString());
            }
            catch (Exception e)
            {
                m_log.Error("[EMAIL] DefaultEmailModule Exception: "+e.Message);
                return;
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
            return null;
        }
    }
}
