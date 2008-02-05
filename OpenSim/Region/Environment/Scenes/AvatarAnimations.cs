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
* 
*/
using System;
using System.Collections.Generic;
using System.Xml;
using libsecondlife;
using OpenSim.Framework.Console;

namespace OpenSim.Region.Environment.Scenes
{
    public class AvatarAnimations
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Dictionary<string, LLUUID> AnimsLLUUID = new Dictionary<string, LLUUID>();
        public Dictionary<LLUUID, string> AnimsNames = new Dictionary<LLUUID, string>();

        public AvatarAnimations()
        {
        }

        public void LoadAnims()
        {
            //m_log.Info("[CLIENT]: Loading avatar animations");
            using (XmlTextReader reader = new XmlTextReader("data/avataranimations.xml"))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(reader);
                foreach (XmlNode nod in doc.DocumentElement.ChildNodes)
                {
                    if (nod.Attributes["name"] != null)
                    {
                        AnimsLLUUID.Add((string)nod.Attributes["name"].Value, (LLUUID)nod.InnerText);
                    }
                }
            }

            // m_log.Info("[CLIENT]: Loaded " + AnimsLLUUID.Count.ToString() + " animation(s)");

            try
            {
                //Mantis: 0000224: 2755 - Enumeration Operation may not execute [immediate crash] (ODE/2750/WIN2003) 
                foreach (KeyValuePair<string, LLUUID> kp in ScenePresence.Animations.AnimsLLUUID)
                {
                    AnimsNames.Add(kp.Value, kp.Key);
                }
            }
            catch (InvalidOperationException)
            {
                m_log.Warn("[AVATAR]: Unable to load animation names for an Avatar");
            }
        }
    }
}