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
using System;
using System.Collections.Generic;

namespace OpenSim.Framework
{
    public static class SLUtil
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Asset types used only in OpenSim.
        /// To avoid clashing with the code numbers used in Second Life, use only negative numbers here.
        /// </summary>
        public enum OpenSimAssetType : sbyte
        {
            Material = -2
        }


        #region SL / file extension / content-type conversions

        /// <summary>
        /// Returns the Enum entry corresponding to the given code, regardless of whether it belongs
        /// to the AssetType or OpenSimAssetType enums.
        /// </summary>
        public static object AssetTypeFromCode(sbyte assetType)
        {
            if (Enum.IsDefined(typeof(OpenMetaverse.AssetType), assetType))
                return (OpenMetaverse.AssetType)assetType;
            else if (Enum.IsDefined(typeof(OpenSimAssetType), assetType))
                return (OpenSimAssetType)assetType;
            else
                return OpenMetaverse.AssetType.Unknown;
        }

        private class TypeMapping
        {
            private sbyte assetType;
            private sbyte inventoryType;
            private string contentType;
            private string contentType2;
            private string extension;

            public sbyte AssetTypeCode
            {
                get { return assetType; }
            }

            public object AssetType
            {
                get { return AssetTypeFromCode(assetType); }
            }

            public sbyte InventoryType
            {
                get { return inventoryType; }
            }

            public string ContentType
            {
                get { return contentType; }
            }

            public string ContentType2
            {
                get { return contentType2; }
            }

            public string Extension
            {
                get { return extension; }
            }

            private TypeMapping(sbyte assetType, sbyte inventoryType, string contentType, string contentType2, string extension)
            {
                this.assetType = assetType;
                this.inventoryType = inventoryType;
                this.contentType = contentType;
                this.contentType2 = contentType2;
                this.extension = extension;
            }

            public TypeMapping(AssetType assetType, sbyte inventoryType, string contentType, string contentType2, string extension)
                : this((sbyte)assetType, inventoryType, contentType, contentType2, extension)
            {
            }

            public TypeMapping(AssetType assetType, InventoryType inventoryType, string contentType, string contentType2, string extension)
                : this((sbyte)assetType, (sbyte)inventoryType, contentType, contentType2, extension)
            {
            }

            public TypeMapping(AssetType assetType, InventoryType inventoryType, string contentType, string extension)
                : this((sbyte)assetType, (sbyte)inventoryType, contentType, null, extension)
            {
            }

            public TypeMapping(AssetType assetType, FolderType inventoryType, string contentType, string extension)
                : this((sbyte)assetType, (sbyte)inventoryType, contentType, null, extension)
            {
            }

            public TypeMapping(OpenSimAssetType assetType, InventoryType inventoryType, string contentType, string extension)
                : this((sbyte)assetType, (sbyte)inventoryType, contentType, null, extension)
            {
            }
        }

        /// <summary>
        /// Maps between AssetType, InventoryType and Content-Type.
        /// Where more than one possibility exists, the first one takes precedence. E.g.:
        ///   AssetType "AssetType.Texture" -> Content-Type "image-xj2c"
        ///   Content-Type "image/x-j2c" -> InventoryType "InventoryType.Texture"
        /// </summary>
        private static TypeMapping[] MAPPINGS = new TypeMapping[] {
            new TypeMapping(AssetType.Unknown, InventoryType.Unknown, "application/octet-stream", "bin"),
            new TypeMapping(AssetType.Texture, InventoryType.Texture, "image/x-j2c", "image/jp2", "j2c"),
            new TypeMapping(AssetType.Texture, InventoryType.Snapshot, "image/x-j2c", "image/jp2", "j2c"),
            new TypeMapping(AssetType.TextureTGA, InventoryType.Texture, "image/tga", "tga"),
            new TypeMapping(AssetType.ImageTGA, InventoryType.Texture, "image/tga", "tga"),
            new TypeMapping(AssetType.ImageJPEG, InventoryType.Texture, "image/jpeg", "jpg"),
            new TypeMapping(AssetType.Sound, InventoryType.Sound, "audio/ogg", "application/ogg", "ogg"),
            new TypeMapping(AssetType.SoundWAV, InventoryType.Sound, "audio/x-wav", "wav"),
            new TypeMapping(AssetType.CallingCard, InventoryType.CallingCard, "application/vnd.ll.callingcard", "application/x-metaverse-callingcard", "callingcard"),
            new TypeMapping(AssetType.Landmark, InventoryType.Landmark, "application/vnd.ll.landmark", "application/x-metaverse-landmark", "landmark"),
            new TypeMapping(AssetType.Clothing, InventoryType.Wearable, "application/vnd.ll.clothing", "application/x-metaverse-clothing", "clothing"),
            new TypeMapping(AssetType.Object, InventoryType.Object, "application/vnd.ll.primitive", "application/x-metaverse-primitive", "primitive"),
            new TypeMapping(AssetType.Object, InventoryType.Attachment, "application/vnd.ll.primitive", "application/x-metaverse-primitive", "primitive"),
            new TypeMapping(AssetType.Notecard, InventoryType.Notecard, "application/vnd.ll.notecard", "application/x-metaverse-notecard", "notecard"),
            new TypeMapping(AssetType.LSLText, InventoryType.LSL, "application/vnd.ll.lsltext", "application/x-metaverse-lsl", "lsl"),
            new TypeMapping(AssetType.LSLBytecode, InventoryType.LSL, "application/vnd.ll.lslbyte", "application/x-metaverse-lso", "lso"),
            new TypeMapping(AssetType.Bodypart, InventoryType.Wearable, "application/vnd.ll.bodypart", "application/x-metaverse-bodypart", "bodypart"),
            new TypeMapping(AssetType.Animation, InventoryType.Animation, "application/vnd.ll.animation", "application/x-metaverse-animation", "animation"),
            new TypeMapping(AssetType.Gesture, InventoryType.Gesture, "application/vnd.ll.gesture", "application/x-metaverse-gesture", "gesture"),
            new TypeMapping(AssetType.Simstate, InventoryType.Snapshot, "application/x-metaverse-simstate", "simstate"),
            new TypeMapping(AssetType.Link, InventoryType.Unknown, "application/vnd.ll.link", "link"),
            new TypeMapping(AssetType.LinkFolder, InventoryType.Unknown, "application/vnd.ll.linkfolder", "linkfolder"),
            new TypeMapping(AssetType.Mesh, InventoryType.Mesh, "application/vnd.ll.mesh", "llm"),

            // The next few items are about inventory folders
            new TypeMapping(AssetType.Folder, FolderType.None, "application/vnd.ll.folder", "folder"),
            new TypeMapping(AssetType.Folder, FolderType.Root, "application/vnd.ll.rootfolder", "rootfolder"),
            new TypeMapping(AssetType.Folder, FolderType.Trash, "application/vnd.ll.trashfolder", "trashfolder"),
            new TypeMapping(AssetType.Folder, FolderType.Snapshot, "application/vnd.ll.snapshotfolder", "snapshotfolder"),
            new TypeMapping(AssetType.Folder, FolderType.LostAndFound, "application/vnd.ll.lostandfoundfolder", "lostandfoundfolder"),
            new TypeMapping(AssetType.Folder, FolderType.Favorites, "application/vnd.ll.favoritefolder", "favoritefolder"),
            new TypeMapping(AssetType.Folder, FolderType.CurrentOutfit, "application/vnd.ll.currentoutfitfolder", "currentoutfitfolder"),
            new TypeMapping(AssetType.Folder, FolderType.Outfit, "application/vnd.ll.outfitfolder", "outfitfolder"),
            new TypeMapping(AssetType.Folder, FolderType.MyOutfits, "application/vnd.ll.myoutfitsfolder", "myoutfitsfolder"),

            // This next mappping is an asset to inventory item mapping.
            // Note: LL stores folders as assets of type Folder = 8, and it has a corresponding InventoryType = 8
            // OpenSim doesn't store folders as assets, so this mapping should only be used when parsing things from the viewer to the server
            new TypeMapping(AssetType.Folder, InventoryType.Folder, "application/vnd.ll.folder", "folder"),

            // OpenSim specific
            new TypeMapping(OpenSimAssetType.Material, InventoryType.Unknown, "application/llsd+xml", "material")
        };

        private static Dictionary<sbyte, string> asset2Content;
        private static Dictionary<sbyte, string> asset2Extension;
        private static Dictionary<sbyte, string> inventory2Content;
        private static Dictionary<string, sbyte> content2Asset;
        private static Dictionary<string, sbyte> content2Inventory;

        static SLUtil()
        {
            asset2Content = new Dictionary<sbyte, string>();
            asset2Extension = new Dictionary<sbyte, string>();
            inventory2Content = new Dictionary<sbyte, string>();
            content2Asset = new Dictionary<string, sbyte>();
            content2Inventory = new Dictionary<string, sbyte>();

            foreach (TypeMapping mapping in MAPPINGS)
            {
                sbyte assetType = mapping.AssetTypeCode;
                if (!asset2Content.ContainsKey(assetType))
                    asset2Content.Add(assetType, mapping.ContentType);

                if (!asset2Extension.ContainsKey(assetType))
                    asset2Extension.Add(assetType, mapping.Extension);

                if (!inventory2Content.ContainsKey(mapping.InventoryType))
                    inventory2Content.Add(mapping.InventoryType, mapping.ContentType);

                if (!content2Asset.ContainsKey(mapping.ContentType))
                    content2Asset.Add(mapping.ContentType, assetType);

                if (!content2Inventory.ContainsKey(mapping.ContentType))
                    content2Inventory.Add(mapping.ContentType, mapping.InventoryType);

                if (mapping.ContentType2 != null)
                {
                    if (!content2Asset.ContainsKey(mapping.ContentType2))
                        content2Asset.Add(mapping.ContentType2, assetType);
                    if (!content2Inventory.ContainsKey(mapping.ContentType2))
                        content2Inventory.Add(mapping.ContentType2, mapping.InventoryType);
                }
            }
        }

        public static string SLAssetTypeToContentType(int assetType)
        {
            string contentType;
            if (!asset2Content.TryGetValue((sbyte)assetType, out contentType))
                contentType = asset2Content[(sbyte)AssetType.Unknown];
            return contentType;
        }

        public static string SLInvTypeToContentType(int invType)
        {
            string contentType;
            if (!inventory2Content.TryGetValue((sbyte)invType, out contentType))
                contentType = inventory2Content[(sbyte)InventoryType.Unknown];
            return contentType;
        }

        public static sbyte ContentTypeToSLAssetType(string contentType)
        {
            sbyte assetType;
            if (!content2Asset.TryGetValue(contentType, out assetType))
                assetType = (sbyte)AssetType.Unknown;
            return (sbyte)assetType;
        }

        public static sbyte ContentTypeToSLInvType(string contentType)
        {
            sbyte invType;
            if (!content2Inventory.TryGetValue(contentType, out invType))
                invType = (sbyte)InventoryType.Unknown;
            return (sbyte)invType;
        }

        public static string SLAssetTypeToExtension(int assetType)
        {
            string extension;
            if (!asset2Extension.TryGetValue((sbyte)assetType, out extension))
                extension = asset2Extension[(sbyte)AssetType.Unknown];
            return extension;
        }

        #endregion SL / file extension / content-type conversions

        private class NotecardReader
        {
            private string rawInput;
            private int lineNumber;

            public int LineNumber
            {
                get
                {
                    return lineNumber;
                }
            }

            public NotecardReader(string _rawInput)
            {
                rawInput = (string)_rawInput.Clone();
                lineNumber = 0;
            }

            public string getLine()
            {
                if(rawInput.Length == 0)
                {
                    throw new NotANotecardFormatException(lineNumber + 1);
                }

                int pos = rawInput.IndexOf('\n');
                if(pos < 0)
                {
                    pos = rawInput.Length;
                }

                /* cut line from rest */
                ++lineNumber;
                string line = rawInput.Substring(0, pos);
                if (pos + 1 >= rawInput.Length)
                {
                    rawInput = string.Empty;
                }
                else
                {
                    rawInput = rawInput.Substring(pos + 1);
                }
                /* clean up line from double spaces and tabs */
                line = line.Replace("\t", " ");
                while(line.IndexOf("  ") >= 0)
                {
                    line = line.Replace("  ", " ");
                }
                return line.Replace("\r", "").Trim();
            }

            public string getBlock(int length)
            {
                /* cut line from rest */
                if(length > rawInput.Length)
                {
                    throw new NotANotecardFormatException(lineNumber);
                }
                string line = rawInput.Substring(0, length);
                rawInput = rawInput.Substring(length);
                return line;
            }
        }

        public class NotANotecardFormatException : Exception
        {
            public int lineNumber;
            public NotANotecardFormatException(int _lineNumber)
                : base()
            {
               lineNumber = _lineNumber;
            }
        }

        private static void skipSection(NotecardReader reader)
        {
            if (reader.getLine() != "{")
                throw new NotANotecardFormatException(reader.LineNumber);

            string line;
            while ((line = reader.getLine()) != "}")
            {
                if(line.IndexOf('{')>=0)
                {
                    throw new NotANotecardFormatException(reader.LineNumber);
                }
            }
        }

        private static void skipInventoryItem(NotecardReader reader)
        {
            if (reader.getLine() != "{")
                throw new NotANotecardFormatException(reader.LineNumber);

            string line;
            while((line = reader.getLine()) != "}")
            {
                string[] data = line.Split(' ');
                if(data.Length == 0)
                {
                    continue;
                }
                if(data[0] == "permissions")
                {
                    skipSection(reader);
                }
                else if(data[0] == "sale_info")
                {
                    skipSection(reader);
                }
                else if (line.IndexOf('{') >= 0)
                {
                    throw new NotANotecardFormatException(reader.LineNumber);
                }
            }
        }

        private static void skipInventoryItems(NotecardReader reader)
        {
            if(reader.getLine() != "{")
            {
                throw new NotANotecardFormatException(reader.LineNumber);
            }

            string line;
            while((line = reader.getLine()) != "}")
            {
                string[] data = line.Split(' ');
                if(data.Length == 0)
                {
                    continue;
                }

                if(data[0] == "inv_item")
                {
                    skipInventoryItem(reader);
                }
                else if (line.IndexOf('{') >= 0)
                {
                    throw new NotANotecardFormatException(reader.LineNumber);
                }

            }
        }

        private static void skipInventory(NotecardReader reader)
        {
            if (reader.getLine() != "{")
                throw new NotANotecardFormatException(reader.LineNumber);

            string line;
            while((line = reader.getLine()) != "}")
            {
                string[] data = line.Split(' ');
                if(data[0] == "count")
                {
                    int count = Int32.Parse(data[1]);
                    for(int i = 0; i < count; ++i)
                    {
                        skipInventoryItems(reader);
                    }
                }
                else if (line.IndexOf('{') >= 0)
                {
                    throw new NotANotecardFormatException(reader.LineNumber);
                }
            }
        }

        private static string readNotecardText(NotecardReader reader)
        {
            if (reader.getLine() != "{")
                throw new NotANotecardFormatException(reader.LineNumber);

            string notecardString = string.Empty;
            string line;
            while((line = reader.getLine()) != "}")
            {
                string[] data = line.Split(' ');
                if (data.Length == 0)
                {
                    continue;
                }

                if (data[0] == "LLEmbeddedItems")
                {
                    skipInventory(reader);
                }
                else if(data[0] == "Text" && data.Length == 3)
                {
                    int length = Int32.Parse(data[2]);
                    notecardString = reader.getBlock(length);
                }
                else if (line.IndexOf('{') >= 0)
                {
                    throw new NotANotecardFormatException(reader.LineNumber);
                }

            }
            return notecardString;
        }

        private static string readNotecard(byte[] rawInput)
        {
            string rawIntermedInput = string.Empty;

            /* make up a Raw Encoding here */
            foreach(byte c in rawInput)
            {
                char d = (char)c;
                rawIntermedInput += d;
            }

            NotecardReader reader = new NotecardReader(rawIntermedInput);
            string line;
            try
            {
                line = reader.getLine();
            }
            catch(Exception)
            {
                return System.Text.Encoding.UTF8.GetString(rawInput);
            }
            string[] versioninfo = line.Split(' ');
            if(versioninfo.Length < 3)
            {
                return System.Text.Encoding.UTF8.GetString(rawInput);
            }
            else if(versioninfo[0] != "Linden" || versioninfo[1] != "text")
            {
                return System.Text.Encoding.UTF8.GetString(rawInput);
            }
            else
            {
                /* now we actually decode the Encoding, before we needed it in raw */
                string o = readNotecardText(reader);
                byte[] a = new byte[o.Length];
                for(int i = 0; i < o.Length; ++i)
                {
                    a[i] = (byte)o[i];
                }
                return System.Text.Encoding.UTF8.GetString(a);
            }
        }

        /// <summary>
        /// Parse a notecard in Linden format to a string of ordinary text.
        /// </summary>
        /// <param name="rawInput"></param>
        /// <returns></returns>
        public static string ParseNotecardToString(byte[] rawInput)
        {
            return readNotecard(rawInput);
        }

        /// <summary>
        /// Parse a notecard in Linden format to a list of ordinary lines.
        /// </summary>
        /// <param name="rawInput"></param>
        /// <returns></returns>
        public static string[] ParseNotecardToArray(byte[] rawInput)
        {
            return readNotecard(rawInput).Replace("\r", "").Split('\n');
        }
    }
}
