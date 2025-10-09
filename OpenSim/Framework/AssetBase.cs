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
using System.Xml.Serialization;
using OpenMetaverse;

namespace OpenSim.Framework
{
    [Flags]
    // this enum is stuck, can not be changed or will break compatibilty with any version older than that change
    public enum AssetFlags
    {
        Normal = 0,         // Immutable asset
        Maptile = 1,        // What it says
        Rewritable = 2,     // Content can be rewritten
        Collectable = 4,     // Can be GC'ed after some time
    }

    /// <summary>
    /// Asset class.   All Assets are reference by this class or a class derived from this class
    /// </summary>
    [Serializable]
    public class AssetBase
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const int MAX_ASSET_NAME = 64;
        public const int MAX_ASSET_DESC = 64;

        /// <summary>
        /// Data of the Asset
        /// </summary>
        private byte[] m_data;

        /// <summary>
        /// Meta Data of the Asset
        /// </summary>
        private AssetMetadata m_metadata;

        private int m_uploadAttempts;

        // This is needed for .NET serialization!!!
        // Do NOT "Optimize" away!
        public AssetBase()
        {
            m_metadata = new AssetMetadata
            {
                FullID = UUID.Zero,
                ID = UUID.Zero.ToString(),
                Type = (sbyte)AssetType.Unknown,
                CreatorID = string.Empty
            };
        }

        public AssetBase(UUID assetID, string name, sbyte assetType, string creatorID)
        {
            /*
            if (assetType == (sbyte)AssetType.Unknown)
            {
                System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(true);
                m_log.ErrorFormat("[ASSETBASE]: Creating asset '{0}' ({1}) with an unknown asset type\n{2}",
                    name, assetID, trace.ToString());
            }
            */

            m_metadata = new AssetMetadata
            {
                FullID = assetID,
                Name = name,
                Type = assetType,
                CreatorID = creatorID
            };
        }

        public AssetBase(string assetID, string name, sbyte assetType, string creatorID)
        {
            /*
            if (assetType == (sbyte)AssetType.Unknown)
            {
                System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(true);
                m_log.ErrorFormat("[ASSETBASE]: Creating asset '{0}' ({1}) with an unknown asset type\n{2}",
                    name, assetID, trace.ToString());
            }
            */

            m_metadata = new AssetMetadata
            {
                ID = assetID,
                Name = name,
                Type = assetType,
                CreatorID = creatorID
            };
        }

        public bool ContainsReferences =>
            IsTextualAsset && (
                Type != (sbyte)AssetType.Notecard
                && Type != (sbyte)AssetType.CallingCard
                && Type != (sbyte)AssetType.LSLText
                && Type != (sbyte)AssetType.Landmark);

        public bool IsTextualAsset => !IsBinaryAsset;

        /// <summary>
        /// Checks if this asset is a binary or text asset
        /// </summary>
        public bool IsBinaryAsset =>
            Type is (sbyte)AssetType.Animation 
                or (sbyte)AssetType.Gesture 
                or (sbyte)AssetType.Simstate 
                or (sbyte)AssetType.Unknown 
                or (sbyte)AssetType.Object 
                or (sbyte)AssetType.Sound 
                or (sbyte)AssetType.SoundWAV 
                or (sbyte)AssetType.Texture 
                or (sbyte)AssetType.TextureTGA 
                or (sbyte)AssetType.Folder 
                or (sbyte)AssetType.ImageJPEG 
                or (sbyte)AssetType.ImageTGA 
                or (sbyte)AssetType.Mesh 
                or (sbyte) AssetType.LSLBytecode;

        public byte[] Data
        {
            get => m_data;
            set => m_data = value;
        }

        /// <summary>
        /// Asset UUID
        /// </summary>
        public UUID FullID
        {
            get => m_metadata.FullID;
            set => m_metadata.FullID = value;
        }

        /// <summary>
        /// Asset MetaData ID (transferring from UUID to string ID)
        /// </summary>
        public string ID
        {
            get => m_metadata.ID;
            set => m_metadata.ID = value;
        }

        public string Name
        {
            get => m_metadata.Name;
            set => m_metadata.Name = value;
        }

        public string Description
        {
            get => m_metadata.Description;
            set => m_metadata.Description = value;
        }

        /// <summary>
        /// (sbyte) AssetType enum
        /// </summary>
        public sbyte Type
        {
            get => m_metadata.Type;
            set => m_metadata.Type = value;
        }

        public int UploadAttempts
        {
            get => m_uploadAttempts;
            set => m_uploadAttempts = value;
        }

        /// <summary>
        /// Is this a region only asset, or does this exist on the asset server also
        /// </summary>
        public bool Local
        {
            get => m_metadata.Local;
            set => m_metadata.Local = value;
        }

        /// <summary>
        /// Is this asset going to be saved to the asset database?
        /// </summary>
        public bool Temporary
        {
            get => m_metadata.Temporary;
            set => m_metadata.Temporary = value;
        }

        public string CreatorID
        {
            get => m_metadata.CreatorID;
            set => m_metadata.CreatorID = value;
        }

        public AssetFlags Flags
        {
            get => m_metadata.Flags;
            set => m_metadata.Flags = value;
        }

        [XmlIgnore]
        public AssetMetadata Metadata
        {
            get => m_metadata;
            set => m_metadata = value;
        }

        public override string ToString()
        {
            return FullID.ToString();
        }
    }

    [Serializable]
    public class AssetMetadata
    {
        private UUID m_fullid;
        private string m_id;
        private string m_name = string.Empty;
        private string m_description = string.Empty;
        private DateTime m_creation_date;
        private sbyte m_type = (sbyte)AssetType.Unknown;
        private string m_content_type;
        private byte[] m_sha1;
        private bool m_local;
        private bool m_temporary;
        private string m_creatorid;
        private AssetFlags m_flags;

        public UUID FullID
        {
            get => m_fullid;
            set { m_fullid = value; m_id = m_fullid.ToString(); }
        }

        public string ID
        {
            //get { return m_fullid.ToString(); }
            //set { m_fullid = new UUID(value); }
            get
            {
                if (string.IsNullOrEmpty(m_id))
                    m_id = m_fullid.ToString();

                return m_id;
            }

            set
            {
                if (UUID.TryParse(value, out UUID uuid))
                {
                    m_fullid = uuid;
                    m_id = uuid.ToString();
                }
                else
                    m_id = value;
            }
        }

        public string Name
        {
            get => m_name;
            set => m_name = value;
        }

        public string Description
        {
            get => m_description;
            set => m_description = value;
        }

        public DateTime CreationDate
        {
            get => m_creation_date;
            set => m_creation_date = value;
        }

        public sbyte Type
        {
            get => m_type;
            set => m_type = value;
        }

        public string ContentType
        {
            get => !string.IsNullOrEmpty(m_content_type) 
                ? m_content_type : SLUtil.SLAssetTypeToContentType(m_type);
            set
            {
                m_content_type = value;

                var type = SLUtil.ContentTypeToSLAssetType(value);
                if (type != -1)
                    m_type = type;
            }
        }

        public byte[] SHA1
        {
            get => m_sha1;
            set => m_sha1 = value;
        }

        public bool Local
        {
            get => m_local;
            set => m_local = value;
        }

        public bool Temporary
        {
            get => m_temporary;
            set => m_temporary = value;
        }

        public string CreatorID
        {
            get => m_creatorid;
            set => m_creatorid = value;
        }

        public AssetFlags Flags
        {
            get => m_flags;
            set => m_flags = value;
        }
    }
}
