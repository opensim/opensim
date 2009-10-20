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

using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.CoreModules.Agent.TextureDownload
{
    /// <summary>
    /// Sends a 'texture not found' packet back to the client
    /// </summary>
    public class TextureNotFoundSender : ITextureSender
    {
        //        private static readonly log4net.ILog m_log
        //            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        //        private IClientAPI m_client;
        //        private UUID m_textureId;

        public TextureNotFoundSender(IClientAPI client, UUID textureID)
        {
            //m_client = client;
            //m_textureId = textureID;
        }

        #region ITextureSender Members

        public bool Sending
        {
            get { return false; }
            set { }
        }

        public bool Cancel
        {
            get { return false; }
            set { }
        }

        // See ITextureSender
        public void UpdateRequest(int discardLevel, uint packetNumber)
        {
            // No need to implement since priority changes don't affect this operation
        }

        // See ITextureSender
        public bool SendTexturePacket()
        {
            //            m_log.DebugFormat(
            //                "[TEXTURE NOT FOUND SENDER]: Informing the client that texture {0} cannot be found",
            //                m_textureId);

            // XXX Temporarily disabling as this appears to be causing client crashes on at least
            // 1.19.0(5) of the Linden Second Life client.
            //            m_client.SendImageNotFound(m_textureId);

            return true;
        }

        #endregion
    }
}
