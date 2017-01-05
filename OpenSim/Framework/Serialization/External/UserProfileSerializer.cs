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

using System.IO;
using System.Xml;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Framework.Serialization.External
{
    /// <summary>
    /// Serialize and deserialize user profiles as an external format.
    /// </summary>
    /// <remarks>
    /// Currently UNUSED.
    /// </remarks>
    public class UserProfileSerializer
    {
        public const int MAJOR_VERSION = 0;
        public const int MINOR_VERSION = 1;

        public static string Serialize(UUID userID, string firstName, string lastName)
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter xtw = new XmlTextWriter(sw);
            xtw.Formatting = Formatting.Indented;
            xtw.WriteStartDocument();

            xtw.WriteStartElement("user_profile");
            xtw.WriteAttributeString("major_version", MAJOR_VERSION.ToString());
            xtw.WriteAttributeString("minor_version", MINOR_VERSION.ToString());

            xtw.WriteElementString("name", firstName + " " + lastName);
            xtw.WriteElementString("id", userID.ToString());
            xtw.WriteElementString("about", "");

            // Not sure if we're storing this yet, need to take a look
//            xtw.WriteElementString("Url", profile.Url);
            // or, indeed, interests

            xtw.WriteEndElement();

            xtw.Close();
            sw.Close();

            return sw.ToString();
        }
    }
}