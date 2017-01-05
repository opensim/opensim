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

using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using log4net;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Scenes.Animation
{
    public class DefaultAvatarAnimations
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static readonly string DefaultAnimationsPath = "data/avataranimations.xml";

        public static Dictionary<string, UUID> AnimsUUID = new Dictionary<string, UUID>();
        public static Dictionary<UUID, string> AnimsNames = new Dictionary<UUID, string>();
        public static Dictionary<UUID, string> AnimStateNames = new Dictionary<UUID, string>();

        static DefaultAvatarAnimations()
        {
            LoadAnimations(DefaultAnimationsPath);
        }

        /// <summary>
        /// Load the default SL avatar animations.
        /// </summary>
        /// <returns></returns>
        private static void LoadAnimations(string path)
        {
//            Dictionary<string, UUID> animations = new Dictionary<string, UUID>();

            using (XmlTextReader reader = new XmlTextReader(path))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(reader);
//                if (doc.DocumentElement != null)
//                {
                    foreach (XmlNode nod in doc.DocumentElement.ChildNodes)
                    {
                        if (nod.Attributes["name"] != null)
                        {
                            string name = nod.Attributes["name"].Value;
                            UUID id = (UUID)nod.InnerText;
                            string animState = (string)nod.Attributes["state"].Value;

                            AnimsUUID.Add(name, id);
                            AnimsNames.Add(id, name);
                            if (animState != "")
                                AnimStateNames.Add(id, animState);

//                            m_log.DebugFormat("[AVATAR ANIMATIONS]: Loaded {0} {1} {2}", id, name, animState);
                        }
                    }
//                }
            }

//            return animations;
        }

        /// <summary>
        /// Get the default avatar animation with the given name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static UUID GetDefaultAnimation(string name)
        {
//            m_log.DebugFormat(
//                "[AVATAR ANIMATIONS]: Looking for default avatar animation with name {0}", name);

            if (AnimsUUID.ContainsKey(name))
            {
//                m_log.DebugFormat(
//                    "[AVATAR ANIMATIONS]: Found {0} {1} in GetDefaultAvatarAnimation()", AnimsUUID[name], name);

                return AnimsUUID[name];
            }

            return UUID.Zero;
        }

        /// <summary>
        /// Get the name of the animation given a UUID. If there is no matching animation
        ///    return the UUID as a string.
        /// </summary>
        public static string GetDefaultAnimationName(UUID uuid)
        {
            string ret = "unknown";
            if (AnimsUUID.ContainsValue(uuid))
            {
                foreach (KeyValuePair<string, UUID> kvp in AnimsUUID)
                {
                    if (kvp.Value == uuid)
                    {
                        ret = kvp.Key;
                        break;
                    }
                }
            }
            else
            {
                ret = uuid.ToString();
            }

            return ret;
        }
    }
}