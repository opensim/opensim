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
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using libsecondlife;
using log4net;

namespace OpenSim.Framework
{
    /// <summary>
    /// A dictionary for task inventory.
    ///
    /// This class is not thread safe.  Callers must synchronize on Dictionary methods.
    /// </summary>
    public class TaskInventoryDictionary : Dictionary<LLUUID, TaskInventoryItem>,
                                           ICloneable, IXmlSerializable
    {
        // private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static XmlSerializer tiiSerializer = new XmlSerializer(typeof (TaskInventoryItem));

        #region ICloneable Members

        public Object Clone()
        {
            TaskInventoryDictionary clone = new TaskInventoryDictionary();

            lock (this)
            {
                foreach (LLUUID uuid in Keys)
                {
                    clone.Add(uuid, (TaskInventoryItem) this[uuid].Clone());
                }
            }

            return clone;
        }

        #endregion

        // The alternative of simply serializing the list doesn't appear to work on mono, since
        // we get a
        //
        // System.TypeInitializationException: An exception was thrown by the type initializer for OpenSim.Framework.TaskInventoryDictionary ---> System.ArgumentOutOfRangeException: < 0
        // Parameter name: length
        //   at System.String.Substring (Int32 startIndex, Int32 length) [0x00088] in /build/buildd/mono-1.2.4/mcs/class/corlib/System/String.cs:381
        //   at System.Xml.Serialization.TypeTranslator.GetTypeData (System.Type runtimeType, System.String xmlDataType) [0x001f6] in /build/buildd/mono-1.2.4/mcs/class/System.XML/System.Xml.Serialization/TypeTranslator.cs:217
        // ...
//        private static XmlSerializer tiiSerializer
//            = new XmlSerializer(typeof(Dictionary<LLUUID, TaskInventoryItem>.ValueCollection));

        // see IXmlSerializable

        #region IXmlSerializable Members

        public XmlSchema GetSchema()
        {
            return null;
        }

        // see IXmlSerializable
        public void ReadXml(XmlReader reader)
        {
            // m_log.DebugFormat("[TASK INVENTORY]: ReadXml current node before actions, {0}", reader.Name);

            if (!reader.IsEmptyElement)
            {
                reader.Read();
                while (tiiSerializer.CanDeserialize(reader))
                {
                    TaskInventoryItem item = (TaskInventoryItem) tiiSerializer.Deserialize(reader);
                    Add(item.ItemID, item);

                    //m_log.DebugFormat("[TASK INVENTORY]: Instanted prim item {0}, {1} from xml", item.Name, item.ItemID);
                }

               // m_log.DebugFormat("[TASK INVENTORY]: Instantiated {0} prim items in total from xml", Count);
            }
            // else
            // {
            //     m_log.DebugFormat("[TASK INVENTORY]: Skipping empty element {0}", reader.Name);
            // }

            // For some .net implementations, this last read is necessary so that we advance beyond the end tag
            // of the element wrapping this object so that the rest of the serialization can complete normally.
            reader.Read();

            // m_log.DebugFormat("[TASK INVENTORY]: ReadXml current node after actions, {0}", reader.Name);
        }

        // see IXmlSerializable
        public void WriteXml(XmlWriter writer)
        {
            lock (this)
            {
                foreach (TaskInventoryItem item in Values)
                {
                    tiiSerializer.Serialize(writer, item);
                }
            }

            //tiiSerializer.Serialize(writer, Values);
        }

        #endregion

        // see ICloneable
    }

    /// <summary>
    /// Represents an item in a task inventory
    /// </summary>
    public class TaskInventoryItem : ICloneable
    {
        /// <summary>
        /// XXX This should really be factored out into some constants class.
        /// </summary>
        private const uint FULL_MASK_PERMISSIONS_GENERAL = 2147483647;

        /// <summary>
        /// Inventory types
        /// </summary>
        public static string[] InvTypes = new string[]
            {
                "texture",
                "sound",
                "calling_card",
                "landmark",
                String.Empty,
                String.Empty,
                "object",
                "notecard",
                String.Empty,
                String.Empty,
                "lsl_text",
                String.Empty,
                String.Empty,
                "bodypart",
                String.Empty,
                "snapshot",
                String.Empty,
                String.Empty,
                "wearable",
                "animation",
                "gesture"
            };

        /// <summary>
        /// Asset types
        /// </summary>
        public static string[] Types = new string[]
            {
                "texture",
                "sound",
                "callcard",
                "landmark",
                "clothing", // Deprecated
                "clothing",
                "object",
                "notecard",
                "category",
                "root",
                "lsltext",
                "lslbyte",
                "txtr_tga",
                "bodypart",
                "trash",
                "snapshot",
                "lstndfnd",
                "snd_wav",
                "img_tga",
                "jpeg",
                "animatn",
                "gesture"
            };

        private LLUUID _assetID = LLUUID.Zero;

        private uint _baseMask = FULL_MASK_PERMISSIONS_GENERAL;
        private uint _creationDate = 0;
        private LLUUID _creatorID = LLUUID.Zero;
        private string _description = String.Empty;
        private uint _everyoneMask = FULL_MASK_PERMISSIONS_GENERAL;
        private uint _flags = 0;
        private LLUUID _groupID = LLUUID.Zero;
        private uint _groupMask = FULL_MASK_PERMISSIONS_GENERAL;

        private int _invType = 0;
        private LLUUID _itemID = LLUUID.Zero;
        private LLUUID _lastOwnerID = LLUUID.Zero;
        private string _name = String.Empty;
        private uint _nextOwnerMask = FULL_MASK_PERMISSIONS_GENERAL;
        private LLUUID _ownerID = LLUUID.Zero;
        private uint _ownerMask = FULL_MASK_PERMISSIONS_GENERAL;
        private LLUUID _parentID = LLUUID.Zero; //parent folder id
        private LLUUID _parentPartID = LLUUID.Zero; // SceneObjectPart this is inside
        private LLUUID _permsGranter;
        private int _permsMask;
        private int _type = 0;

        public LLUUID AssetID {
            get {
                return _assetID;
            }
            set {
                _assetID = value;
            }
        }

        public uint BasePermissions {
            get {
                return _baseMask;
            }
            set {
                _baseMask = value;
            }
        }

        public uint CreationDate {
            get {
                return _creationDate;
            }
            set {
                _creationDate = value;
            }
        }

        public LLUUID CreatorID {
            get {
                return _creatorID;
            }
            set {
                _creatorID = value;
            }
        }

        public string Description {
            get {
                return _description;
            }
            set {
                _description = value;
            }
        }

        public uint EveryonePermissions {
            get {
                return _everyoneMask;
            }
            set {
                _everyoneMask = value;
            }
        }

        public uint Flags {
            get {
                return _flags;
            }
            set {
                _flags = value;
            }
        }

        public LLUUID GroupID {
            get {
                return _groupID;
            }
            set {
                _groupID = value;
            }
        }

        public uint GroupPermissions {
            get {
                return _groupMask;
            }
            set {
                _groupMask = value;
            }
        }

        public int InvType {
            get {
                return _invType;
            }
            set {
                _invType = value;
            }
        }

        public LLUUID ItemID {
            get {
                return _itemID;
            }
            set {
                _itemID = value;
            }
        }

        public LLUUID LastOwnerID {
            get {
                return _lastOwnerID;
            }
            set {
                _lastOwnerID = value;
            }
        }

        public string Name {
            get {
                return _name;
            }
            set {
                _name = value;
            }
        }

        public uint NextPermissions {
            get {
                return _nextOwnerMask;
            }
            set {
                _nextOwnerMask = value;
            }
        }

        public LLUUID OwnerID {
            get {
                return _ownerID;
            }
            set {
                _ownerID = value;
            }
        }

        public uint CurrentPermissions {
            get {
                return _ownerMask;
            }
            set {
                _ownerMask = value;
            }
        }

        public LLUUID ParentID {
            get {
                return _parentID;
            }
            set {
                _parentID = value;
            }
        }

        public LLUUID ParentPartID {
            get {
                return _parentPartID;
            }
            set {
                _parentPartID = value;
            }
        }

        public LLUUID PermsGranter {
            get {
                return _permsGranter;
            }
            set {
                _permsGranter = value;
            }
        }

        public int PermsMask {
            get {
                return _permsMask;
            }
            set {
                _permsMask = value;
            }
        }

        public int Type {
            get {
                return _type;
            }
            set {
                _type = value;
            }
        }

        // See ICloneable

        #region ICloneable Members

        public Object Clone()
        {
            return MemberwiseClone();
        }

        #endregion

        /// <summary>
        /// Reset the LLUUIDs for this item.
        /// </summary>
        /// <param name="partID">The new part ID to which this item belongs</param>
        public void ResetIDs(LLUUID partID)
        {
            _itemID = LLUUID.Random();
            _parentPartID = partID;
        }
    }
}
