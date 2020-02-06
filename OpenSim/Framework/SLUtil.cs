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
using System.Globalization;

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
        private static Dictionary<string, AssetType> name2Asset = new Dictionary<string, AssetType>()
        {
            {"texture", AssetType.Texture },
            {"sound", AssetType.Sound},
            {"callcard", AssetType.CallingCard},
            {"landmark", AssetType.Landmark},
            {"script", (AssetType)4},
            {"clothing", AssetType.Clothing},
            {"object", AssetType.Object},
            {"notecard", AssetType.Notecard},
            {"category", AssetType.Folder},
            {"lsltext", AssetType.LSLText},
            {"lslbyte", AssetType.LSLBytecode},
            {"txtr_tga", AssetType.TextureTGA},
            {"bodypart", AssetType.Bodypart},
            {"snd_wav", AssetType.SoundWAV},
            {"img_tga", AssetType.ImageTGA},
            {"jpeg", AssetType.ImageJPEG},
            {"animatn", AssetType.Animation},
            {"gesture", AssetType.Gesture},
            {"simstate", AssetType.Simstate},
            {"mesh", AssetType.Mesh}
//            "settings", AssetType.Settings}
        };
        private static Dictionary<string, FolderType> name2Inventory = new Dictionary<string, FolderType>()
        {
            {"texture", FolderType.Texture},
            {"sound", FolderType.Sound},
            {"callcard", FolderType.CallingCard},
            {"landmark", FolderType.Landmark},
            {"script", (FolderType)4},
            {"clothing", FolderType.Clothing},
            {"object", FolderType.Object},
            {"notecard", FolderType.Notecard},
            {"root", FolderType.Root},
            {"lsltext", FolderType.LSLText},
            {"bodypart", FolderType.BodyPart},
            {"trash", FolderType.Trash},
            {"snapshot", FolderType.Snapshot},
            {"lostandfound", FolderType.LostAndFound},
            {"animatn", FolderType.Animation},
            {"gesture", FolderType.Gesture},
            {"favorites", FolderType.Favorites},
            {"currentoutfit", FolderType.CurrentOutfit},
            {"outfit", FolderType.Outfit},
            {"myoutfits", FolderType.MyOutfits},
            {"mesh", FolderType.Mesh},
//            "settings", FolderType.Settings},
            {"suitcase", FolderType.Suitcase}
        };

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

        public static AssetType SLAssetName2Type(string name)
        {
            if (name2Asset.TryGetValue(name, out AssetType type))
                return type;
            return AssetType.Unknown;
        }

        public static FolderType SLInvName2Type(string name)
        {
            if (name2Inventory.TryGetValue(name, out FolderType type))
                return type;
            return FolderType.None;
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

        private static int skipcontrol(string data, int indx)
        {
            while(indx < data.Length)
            {
                char c = data[indx];
                switch(c)
                {
                    case '\n':
                    case '\t':
                    case '{':
                        ++indx;
                        break;
                    default:
                        return indx;
                }
            }
            return -1;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static int checkfield(string note, int start, string name, out int end)
        {
            end = -1;
            int limit = note.Length - start;
            if(limit > 64)
                limit = 64;
            int indx = note.IndexOf(name, start, limit);
            if (indx < 0)
                return -1;
            indx += name.Length;
            indx = skipcontrol(note, indx);
            if (indx < 0)
                return -1;
            end = note.IndexOfAny(seps, indx);
            if (end < 0)
                return -1;
            return indx;
        }

        static char[] seps = new char[]{ '\t','\n'};
        public static InventoryItemBase GetEmbeddedItem(byte[] data, UUID itemID)
        {
            if(data == null || data.Length < 200)
                return null;

            string note = Util.UTF8.GetString(data);
            if (String.IsNullOrWhiteSpace(note))
                return null;

            int start = note.IndexOf(itemID.ToString());
            if (start < 0)
                return null;

            int end;
            start = note.IndexOf("permissions", start, 100);
            if (start < 0)
                return null;
            start = checkfield(note, start, "base_mask", out end);
            if (start < 0)
                return null;

            if (!uint.TryParse(note.Substring(start, end - start), NumberStyles.HexNumber, Culture.NumberFormatInfo, out uint basemask))
                return null;

            start = checkfield(note, end, "owner_mask", out end);
            if (start < 0)
                return null;

            if (!uint.TryParse(note.Substring(start, end - start), NumberStyles.HexNumber, Culture.NumberFormatInfo, out uint ownermask))
                return null;

            start = checkfield(note, end, "group_mask", out end);
            if (start < 0)
                return null;
            if (!uint.TryParse(note.Substring(start, end - start), NumberStyles.HexNumber, Culture.NumberFormatInfo, out uint groupmask))
                return null;

            start = checkfield(note, end, "everyone_mask", out end);
            if (start < 0)
                return null;
            if (!uint.TryParse(note.Substring(start, end - start), NumberStyles.HexNumber, Culture.NumberFormatInfo, out uint everyonemask))
                return null;

            start = checkfield(note, end, "next_owner_mask", out end);
            if (start < 0)
                return null;
            if (!uint.TryParse(note.Substring(start, end - start), NumberStyles.HexNumber, Culture.NumberFormatInfo, out uint nextownermask))
                return null;

            start = checkfield(note, end, "creator_id", out end);
            if (start < 0)
                return null;
            if (!UUID.TryParse(note.Substring(start, end - start), out UUID creatorID))
                return null;

            start = checkfield(note, end, "owner_id", out end);
            if (start < 0)
                return null;
            if (!UUID.TryParse(note.Substring(start, end - start), out UUID ownerID))
                return null;

            /*
            start = checkfield(note, end, "last_owner_id", out end);
            if (start < 0)
                return null;
            if (!UUID.TryParse(note.Substring(start, end - start), out UUID lastownerID))
                return null;
            */

            int limit = note.Length - end;
            if (limit > 120)
                limit = 120;
            end = note.IndexOf('}', end, limit); // last owner
            start = checkfield(note, end, "asset_id", out end);
            if (start < 0)
                return null;
            if (!UUID.TryParse(note.Substring(start, end - start), out UUID assetID))
                return null;

            start = checkfield(note, end, "type", out end);
            if (start < 0)
                return null;
            string typestr = note.Substring(start, end - start);
            AssetType assetType = SLAssetName2Type(typestr);

            start = checkfield(note, end, "inv_type", out end);
            if (start < 0)
                return null;
            string inttypestr = note.Substring(start, end - start);
            FolderType invType = SLInvName2Type(inttypestr);

            start = checkfield(note, end, "flags", out end);
            if (start < 0)
                return null;
            if (!uint.TryParse(note.Substring(start, end - start), NumberStyles.HexNumber, Culture.NumberFormatInfo, out uint flags))
                return null;

            limit = note.Length - end;
            if (limit > 120)
                limit = 120;
            end = note.IndexOf('}', end, limit); // skip sale
            start = checkfield(note, end, "name", out end);
            if (start < 0)
                return null;

            string name = note.Substring(start, end - start - 1);

            start = checkfield(note, end, "desc", out end);
            if (start < 0)
                return null;
            string desc = note.Substring(start, end - start - 1);
            /*
            start = checkfield(note, end, "creation_date", out end);
            if (start < 0)
                return null;
            if (!int.TryParse(note.Substring(start, end - start), out int creationdate))
                return null;
            */

            InventoryItemBase item = new InventoryItemBase();
            item.AssetID = assetID;
            item.AssetType = (sbyte)assetType;
            item.BasePermissions = basemask;
            item.CreationDate = Util.UnixTimeSinceEpoch();
            item.CreatorData = "";
            item.CreatorId = creatorID.ToString();
            item.CurrentPermissions = ownermask;
            item.Description = desc;
            item.Flags = flags;
            item.Folder = UUID.Zero;
            item.GroupID = UUID.Zero;
            item.GroupOwned = false;
            item.GroupPermissions = groupmask;
            item.InvType = (sbyte)invType;
            item.Name = name;
            item.NextPermissions = nextownermask;
            item.Owner = ownerID;
            item.SalePrice = 0;
            item.SaleType = (byte)SaleType.Not;
            item.ID = UUID.Random();
            return item;
        }
    }
}
