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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using log4net;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public static class SLUtil
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region SL / file extension / content-type conversions

        private class TypeMapping
        {
            private sbyte assetType;
            private InventoryType inventoryType;
            private string contentType;
            private string contentType2;
            private string extension;

            public sbyte AssetTypeCode
            {
                get { return assetType; }
            }

            public object AssetType
            {
                get {
                    if (Enum.IsDefined(typeof(OpenMetaverse.AssetType), assetType))
                        return (OpenMetaverse.AssetType)assetType;
                    else
                        return OpenMetaverse.AssetType.Unknown;
                }
            }

            public InventoryType InventoryType
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

            private TypeMapping(sbyte assetType, InventoryType inventoryType, string contentType, string contentType2, string extension)
            {
                this.assetType = assetType;
                this.inventoryType = inventoryType;
                this.contentType = contentType;
                this.contentType2 = contentType2;
                this.extension = extension;
            }

            public TypeMapping(AssetType assetType, InventoryType inventoryType, string contentType, string contentType2, string extension)
                : this((sbyte)assetType, inventoryType, contentType, contentType2, extension)
            {
            }

            public TypeMapping(AssetType assetType, InventoryType inventoryType, string contentType, string extension)
                : this((sbyte)assetType, inventoryType, contentType, null, extension)
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
            new TypeMapping(AssetType.Folder, InventoryType.Folder, "application/vnd.ll.folder", "folder"),
            new TypeMapping(AssetType.RootFolder, InventoryType.RootCategory, "application/vnd.ll.rootfolder", "rootfolder"),
            new TypeMapping(AssetType.LSLText, InventoryType.LSL, "application/vnd.ll.lsltext", "application/x-metaverse-lsl", "lsl"),
            new TypeMapping(AssetType.LSLBytecode, InventoryType.LSL, "application/vnd.ll.lslbyte", "application/x-metaverse-lso", "lso"),
            new TypeMapping(AssetType.Bodypart, InventoryType.Wearable, "application/vnd.ll.bodypart", "application/x-metaverse-bodypart", "bodypart"),
            new TypeMapping(AssetType.TrashFolder, InventoryType.Folder, "application/vnd.ll.trashfolder", "trashfolder"),
            new TypeMapping(AssetType.SnapshotFolder, InventoryType.Folder, "application/vnd.ll.snapshotfolder", "snapshotfolder"),
            new TypeMapping(AssetType.LostAndFoundFolder, InventoryType.Folder, "application/vnd.ll.lostandfoundfolder", "lostandfoundfolder"),
            new TypeMapping(AssetType.Animation, InventoryType.Animation, "application/vnd.ll.animation", "application/x-metaverse-animation", "animation"),
            new TypeMapping(AssetType.Gesture, InventoryType.Gesture, "application/vnd.ll.gesture", "application/x-metaverse-gesture", "gesture"),
            new TypeMapping(AssetType.Simstate, InventoryType.Snapshot, "application/x-metaverse-simstate", "simstate"),
            new TypeMapping(AssetType.FavoriteFolder, InventoryType.Unknown, "application/vnd.ll.favoritefolder", "favoritefolder"),
            new TypeMapping(AssetType.Link, InventoryType.Unknown, "application/vnd.ll.link", "link"),
            new TypeMapping(AssetType.LinkFolder, InventoryType.Unknown, "application/vnd.ll.linkfolder", "linkfolder"),
            new TypeMapping(AssetType.CurrentOutfitFolder, InventoryType.Unknown, "application/vnd.ll.currentoutfitfolder", "currentoutfitfolder"),
            new TypeMapping(AssetType.OutfitFolder, InventoryType.Unknown, "application/vnd.ll.outfitfolder", "outfitfolder"),
            new TypeMapping(AssetType.MyOutfitsFolder, InventoryType.Unknown, "application/vnd.ll.myoutfitsfolder", "myoutfitsfolder"),
            new TypeMapping(AssetType.Mesh, InventoryType.Mesh, "application/vnd.ll.mesh", "llm")
        };

        private static Dictionary<sbyte, string> asset2Content;
        private static Dictionary<sbyte, string> asset2Extension;
        private static Dictionary<InventoryType, string> inventory2Content;
        private static Dictionary<string, sbyte> content2Asset;
        private static Dictionary<string, InventoryType> content2Inventory;

        static SLUtil()
        {
            asset2Content = new Dictionary<sbyte, string>();
            asset2Extension = new Dictionary<sbyte, string>();
            inventory2Content = new Dictionary<InventoryType, string>();
            content2Asset = new Dictionary<string, sbyte>();
            content2Inventory = new Dictionary<string, InventoryType>();
            
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
            if (!inventory2Content.TryGetValue((InventoryType)invType, out contentType))
                contentType = inventory2Content[InventoryType.Unknown];
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
            InventoryType invType;
            if (!content2Inventory.TryGetValue(contentType, out invType))
                invType = InventoryType.Unknown;
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

        /// <summary>
        /// Parse a notecard in Linden format to a string of ordinary text.
        /// </summary>
        /// <param name="rawInput"></param>
        /// <returns></returns>
        public static string ParseNotecardToString(string rawInput)
        {
            string[] output = ParseNotecardToList(rawInput).ToArray();

//            foreach (string line in output)
//                m_log.DebugFormat("[PARSE NOTECARD]: ParseNotecardToString got line {0}", line);
            
            return string.Join("\n", output);
        }
                
        /// <summary>
        /// Parse a notecard in Linden format to a list of ordinary lines.
        /// </summary>
        /// <param name="rawInput"></param>
        /// <returns></returns>
        public static List<string> ParseNotecardToList(string rawInput)
        {
            string[] input;
            int idx = 0;
            int level = 0;
            List<string> output = new List<string>();
            string[] words;

            //The Linden format always ends with a } after the input data.
            //Strip off trailing } so there is nothing after the input data.
            int i = rawInput.LastIndexOf("}");
            rawInput = rawInput.Remove(i, rawInput.Length-i);
            input = rawInput.Replace("\r", "").Split('\n');

            while (idx < input.Length)
            {
                if (input[idx] == "{")
                {
                    level++;
                    idx++;
                    continue;
                }

                if (input[idx]== "}")
                {
                    level--;
                    idx++;
                    continue;
                }

                switch (level)
                {
                case 0:
                    words = input[idx].Split(' '); // Linden text ver
                    // Notecards are created *really* empty. Treat that as "no text" (just like after saving an empty notecard)
                    if (words.Length < 3)
                        return output;

                    int version = int.Parse(words[3]);
                    if (version != 2)
                        return output;
                    break;
                case 1:
                    words = input[idx].Split(' ');
                    if (words[0] == "LLEmbeddedItems")
                        break;
                    if (words[0] == "Text")
                    {
                        idx++;  //Now points to first line of notecard text

                        //Number of lines in notecard.
                        int lines = input.Length - idx;
                        int line = 0;

                        while (line < lines)
                        {
//                            m_log.DebugFormat("[PARSE NOTECARD]: Adding line {0}", input[idx]);
                            output.Add(input[idx]);
                            idx++;
                            line++;
                        }

                        return output;
                    }
                    break;
                case 2:
                    words = input[idx].Split(' '); // count
                    if (words[0] == "count")
                    {
                        int c = int.Parse(words[1]);
                        if (c > 0)
                            return output;
                        break;
                    }
                    break;
                }
                idx++;
            }
            
            return output;
        }
    }
}
