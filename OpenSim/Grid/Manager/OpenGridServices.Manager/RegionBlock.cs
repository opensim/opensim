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
using System.Xml;
using libsecondlife;
using OpenSim.Framework.Utilities;

namespace OpenGridServices.Manager
{
    public class RegionBlock
    {
        public uint regloc_x;
        public uint regloc_y;

        public string httpd_url;

        public string region_name;

        public ulong regionhandle {
            get { return Util.UIntsToLong(regloc_x*256,regloc_y*256); }
        }

        public Gdk.Pixbuf MiniMap;

        public RegionBlock()
        {
        }

        public void LoadFromXmlNode(XmlNode sourcenode)
        {
            this.regloc_x=Convert.ToUInt32(sourcenode.Attributes.GetNamedItem("loc_x").Value);
            this.regloc_y=Convert.ToUInt32(sourcenode.Attributes.GetNamedItem("loc_y").Value);
            this.region_name=sourcenode.Attributes.GetNamedItem("region_name").Value;
            this.httpd_url=sourcenode.Attributes.GetNamedItem("httpd_url").Value;
        }
    }
}
