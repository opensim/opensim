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

using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public class SPAvatarAttachment : IAvatarAttachment
    {
        private readonly Scene m_rootScene;
        //private readonly IAvatar m_parent;
        private readonly int m_location;
        //private readonly UUID m_itemId;
        private readonly UUID m_assetId;

        private readonly ISecurityCredential m_security;

        public SPAvatarAttachment(Scene rootScene, IAvatar self, int location, UUID itemId, UUID assetId, ISecurityCredential security)
        {
            m_rootScene = rootScene;
            m_security = security;
            //m_parent = self;
            m_location = location;
            //m_itemId = itemId;
            m_assetId = assetId;
        }

        public int Location { get { return m_location; } }

        public IObject Asset
        {
            get
            {
                return new SOPObject(m_rootScene, m_rootScene.GetSceneObjectPart(m_assetId).LocalId, m_security);
            }
        }
    }
}
