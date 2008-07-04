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
using libsecondlife;

namespace OpenSim.Framework
{
    [Serializable]
    public class AssetBase
    {
        private byte[] _data;
        private string _description = String.Empty;
        private LLUUID _fullid;
        private bool _local = false;
        private string _name = String.Empty;
        private bool _temporary = false;
        private sbyte _type;

        public AssetBase()
        {
        }

        public AssetBase(LLUUID assetId, string name)
        {
            FullID = assetId;
            Name = name;
        }

        public virtual LLUUID FullID
        {
            get { return _fullid; }
            set { _fullid = value; }
        }

        public virtual string ID
        {
            get { return _fullid.ToString(); }
            set { _fullid = new LLUUID(value); }
        }

        public virtual byte[] Data
        {
            get { return _data; }
            set { _data = value; }
        }

        public virtual sbyte Type
        {
            get { return _type; }
            set { _type = value; }
        }

        public virtual string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public virtual string Description
        {
            get { return _description; }
            set { _description = value; }
        }

        public virtual bool Local
        {
            get { return _local; }
            set { _local = value; }
        }

        public virtual bool Temporary
        {
            get { return _temporary; }
            set { _temporary = value; }
        }
    }
}
