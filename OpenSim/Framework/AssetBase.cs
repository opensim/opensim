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
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    [Serializable]
    public class AssetBase
    {
        private byte[] _data;
        private AssetMetadata _metadata;

        public AssetBase()
        {
            Metadata = new AssetMetadata();
        }

        public AssetBase(UUID assetId, string name)
        {
            Metadata = new AssetMetadata();
            Metadata.FullID = assetId;
            Metadata.Name = name;
        }

        public virtual byte[] Data
        {
            get { return _data; }
            set { _data = value; }
        }

        public virtual AssetMetadata Metadata
        {
            get { return _metadata; }
            set { _metadata = value; }
        }
    }

    [Serializable]
    public class AssetMetadata
    {
        private UUID _fullid;
        private string _name = String.Empty;
        private string _description = String.Empty;
        private DateTime _creation_date;
        private sbyte _type;
        private string _content_type;
        private byte[] _sha1;
        private bool _local = false;
        private bool _temporary = false;
        //private Dictionary<string, Uri> _methods = new Dictionary<string, Uri>();
        //private OSDMap _extra_data;

        public UUID FullID
        {
            get { return _fullid; }
            set { _fullid = value; }
        }

        public string ID
        {
            get { return _fullid.ToString(); }
            set { _fullid = new UUID(value); }
        }

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public string Description
        {
            get { return _description; }
            set { _description = value; }
        }

        public DateTime CreationDate
        {
            get { return _creation_date; }
            set { _creation_date = value; }
        }

        public sbyte Type
        {
            get { return _type; }
            set { _type = value; }
        }

        public string ContentType
        {
            get { return _content_type; }
            set { _content_type = value; }
        }

        public byte[] SHA1
        {
            get { return _sha1; }
            set { _sha1 = value; }
        }

        public bool Local
        {
            get { return _local; }
            set { _local = value; }
        }

        public bool Temporary
        {
            get { return _temporary; }
            set { _temporary = value; }
        }

        //public Dictionary<string, Uri> Methods
        //{
        //    get { return _methods; }
        //    set { _methods = value; }
        //}

        //public OSDMap ExtraData
        //{
        //    get { return _extra_data; }
        //    set { _extra_data = value; }
        //}
    }
}
