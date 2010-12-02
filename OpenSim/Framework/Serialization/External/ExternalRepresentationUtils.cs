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
using System.IO;
using System.Xml;

using OpenMetaverse;
using OpenSim.Services.Interfaces;

namespace OpenSim.Framework.Serialization.External
{
    /// <summary>
    /// Utilities for manipulating external representations of data structures in OpenSim
    /// </summary>
    public class ExternalRepresentationUtils
    {
        /// <summary>
        /// Takes a XML representation of a SceneObjectPart and returns another XML representation
        /// with creator data added to it.
        /// </summary>
        /// <param name="xml">The SceneObjectPart represented in XML2</param>
        /// <param name="profileURL">The URL of the profile service for the creator</param>
        /// <param name="userService">The service for retrieving user account information</param>
        /// <param name="scopeID">The scope of the user account information (Grid ID)</param>
        /// <returns>The SceneObjectPart represented in XML2</returns>
        public static string RewriteSOP(string xml, string profileURL, IUserAccountService userService, UUID scopeID)
        {
            if (xml == string.Empty || profileURL == string.Empty || userService == null)
                return xml;

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNodeList sops = doc.GetElementsByTagName("SceneObjectPart");

            foreach (XmlNode sop in sops)
            {
                UserAccount creator = null;
                bool hasCreatorData = false;
                XmlNodeList nodes = sop.ChildNodes;
                foreach (XmlNode node in nodes)
                {
                    if (node.Name == "CreatorID")
                    {
                        UUID uuid = UUID.Zero;
                        UUID.TryParse(node.InnerText, out uuid);
                        creator = userService.GetUserAccount(scopeID, uuid);
                    }
                    if (node.Name == "CreatorData" && node.InnerText != null && node.InnerText != string.Empty)
                        hasCreatorData = true;

                    //if (node.Name == "OwnerID")
                    //{
                    //    UserAccount owner = GetUser(node.InnerText);
                    //    if (owner != null)
                    //        node.InnerText = m_ProfileServiceURL + "/" + node.InnerText + "/" + owner.FirstName + " " + owner.LastName;
                    //}
                }
                if (!hasCreatorData && creator != null)
                {
                    XmlElement creatorData = doc.CreateElement("CreatorData");
                    creatorData.InnerText = profileURL + "/" + creator.PrincipalID + ";" + creator.FirstName + " " + creator.LastName;
                    sop.AppendChild(creatorData);
                }
            }

            using (StringWriter wr = new StringWriter())
            {
                doc.Save(wr);
                return wr.ToString();
            }

        }
    }
}
