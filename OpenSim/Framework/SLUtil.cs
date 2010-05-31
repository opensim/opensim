using System;
using System.Collections.Generic;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public static class SLUtil
    {
        #region SL / file extension / content-type conversions

        public static string SLAssetTypeToContentType(int assetType)
        {
            switch ((AssetType)assetType)
            {
                case AssetType.Texture:
                    return "image/x-j2c";
                case AssetType.Sound:
                    return "application/ogg";
                case AssetType.CallingCard:
                    return "application/vnd.ll.callingcard";
                case AssetType.Landmark:
                    return "application/vnd.ll.landmark";
                case AssetType.Clothing:
                    return "application/vnd.ll.clothing";
                case AssetType.Object:
                    return "application/vnd.ll.primitive";
                case AssetType.Notecard:
                    return "application/vnd.ll.notecard";
                case AssetType.Folder:
                    return "application/vnd.ll.folder";
                case AssetType.RootFolder:
                    return "application/vnd.ll.rootfolder";
                case AssetType.LSLText:
                    return "application/vnd.ll.lsltext";
                case AssetType.LSLBytecode:
                    return "application/vnd.ll.lslbyte";
                case AssetType.TextureTGA:
                case AssetType.ImageTGA:
                    return "image/tga";
                case AssetType.Bodypart:
                    return "application/vnd.ll.bodypart";
                case AssetType.TrashFolder:
                    return "application/vnd.ll.trashfolder";
                case AssetType.SnapshotFolder:
                    return "application/vnd.ll.snapshotfolder";
                case AssetType.LostAndFoundFolder:
                    return "application/vnd.ll.lostandfoundfolder";
                case AssetType.SoundWAV:
                    return "audio/x-wav";
                case AssetType.ImageJPEG:
                    return "image/jpeg";
                case AssetType.Animation:
                    return "application/vnd.ll.animation";
                case AssetType.Gesture:
                    return "application/vnd.ll.gesture";
                case AssetType.Simstate:
                    return "application/x-metaverse-simstate";
                case AssetType.Unknown:
                default:
                    return "application/octet-stream";
            }
        }

        public static sbyte ContentTypeToSLAssetType(string contentType)
        {
            switch (contentType)
            {
                case "image/x-j2c":
                case "image/jp2":
                    return (sbyte)AssetType.Texture;
                case "application/ogg":
                    return (sbyte)AssetType.Sound;
                case "application/vnd.ll.callingcard":
                case "application/x-metaverse-callingcard":
                    return (sbyte)AssetType.CallingCard;
                case "application/vnd.ll.landmark":
                case "application/x-metaverse-landmark":
                    return (sbyte)AssetType.Landmark;
                case "application/vnd.ll.clothing":
                case "application/x-metaverse-clothing":
                    return (sbyte)AssetType.Clothing;
                case "application/vnd.ll.primitive":
                case "application/x-metaverse-primitive":
                    return (sbyte)AssetType.Object;
                case "application/vnd.ll.notecard":
                case "application/x-metaverse-notecard":
                    return (sbyte)AssetType.Notecard;
                case "application/vnd.ll.folder":
                    return (sbyte)AssetType.Folder;
                case "application/vnd.ll.rootfolder":
                    return (sbyte)AssetType.RootFolder;
                case "application/vnd.ll.lsltext":
                case "application/x-metaverse-lsl":
                    return (sbyte)AssetType.LSLText;
                case "application/vnd.ll.lslbyte":
                case "application/x-metaverse-lso":
                    return (sbyte)AssetType.LSLBytecode;
                case "image/tga":
                    // Note that AssetType.TextureTGA will be converted to AssetType.ImageTGA
                    return (sbyte)AssetType.ImageTGA;
                case "application/vnd.ll.bodypart":
                case "application/x-metaverse-bodypart":
                    return (sbyte)AssetType.Bodypart;
                case "application/vnd.ll.trashfolder":
                    return (sbyte)AssetType.TrashFolder;
                case "application/vnd.ll.snapshotfolder":
                    return (sbyte)AssetType.SnapshotFolder;
                case "application/vnd.ll.lostandfoundfolder":
                    return (sbyte)AssetType.LostAndFoundFolder;
                case "audio/x-wav":
                    return (sbyte)AssetType.SoundWAV;
                case "image/jpeg":
                    return (sbyte)AssetType.ImageJPEG;
                case "application/vnd.ll.animation":
                case "application/x-metaverse-animation":
                    return (sbyte)AssetType.Animation;
                case "application/vnd.ll.gesture":
                case "application/x-metaverse-gesture":
                    return (sbyte)AssetType.Gesture;
                case "application/x-metaverse-simstate":
                    return (sbyte)AssetType.Simstate;
                case "application/octet-stream":
                default:
                    return (sbyte)AssetType.Unknown;
            }
        }

        public static sbyte ContentTypeToSLInvType(string contentType)
        {
            switch (contentType)
            {
                case "image/x-j2c":
                case "image/jp2":
                case "image/tga":
                case "image/jpeg":
                    return (sbyte)InventoryType.Texture;
                case "application/ogg":
                case "audio/x-wav":
                    return (sbyte)InventoryType.Sound;
                case "application/vnd.ll.callingcard":
                case "application/x-metaverse-callingcard":
                    return (sbyte)InventoryType.CallingCard;
                case "application/vnd.ll.landmark":
                case "application/x-metaverse-landmark":
                    return (sbyte)InventoryType.Landmark;
                case "application/vnd.ll.clothing":
                case "application/x-metaverse-clothing":
                case "application/vnd.ll.bodypart":
                case "application/x-metaverse-bodypart":
                    return (sbyte)InventoryType.Wearable;
                case "application/vnd.ll.primitive":
                case "application/x-metaverse-primitive":
                    return (sbyte)InventoryType.Object;
                case "application/vnd.ll.notecard":
                case "application/x-metaverse-notecard":
                    return (sbyte)InventoryType.Notecard;
                case "application/vnd.ll.folder":
                    return (sbyte)InventoryType.Folder;
                case "application/vnd.ll.rootfolder":
                    return (sbyte)InventoryType.RootCategory;
                case "application/vnd.ll.lsltext":
                case "application/x-metaverse-lsl":
                case "application/vnd.ll.lslbyte":
                case "application/x-metaverse-lso":
                    return (sbyte)InventoryType.LSL;
                case "application/vnd.ll.trashfolder":
                case "application/vnd.ll.snapshotfolder":
                case "application/vnd.ll.lostandfoundfolder":
                    return (sbyte)InventoryType.Folder;
                case "application/vnd.ll.animation":
                case "application/x-metaverse-animation":
                    return (sbyte)InventoryType.Animation;
                case "application/vnd.ll.gesture":
                case "application/x-metaverse-gesture":
                    return (sbyte)InventoryType.Gesture;
                case "application/x-metaverse-simstate":
                    return (sbyte)InventoryType.Snapshot;
                case "application/octet-stream":
                default:
                    return (sbyte)InventoryType.Unknown;
            }
        }

        #endregion SL / file extension / content-type conversions

        /// <summary>
        /// Parse a notecard in Linden format to a string of ordinary text.
        /// </summary>
        /// <param name="rawInput"></param>
        /// <returns></returns>        
        public static string ParseNotecardToString(string rawInput)
        {
            return string.Join("\n", ParseNotecardToList(rawInput).ToArray());
        }
                
        /// <summary>
        /// Parse a notecard in Linden format to a list of ordinary lines.
        /// </summary>
        /// <param name="rawInput"></param>
        /// <returns></returns>
        public static List<string> ParseNotecardToList(string rawInput)
        {
            string[] input = rawInput.Replace("\r", "").Split('\n');            
            int idx = 0;
            int level = 0;
            List<string> output = new List<string>();
            string[] words;

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
                        int len = int.Parse(words[2]);
                        idx++;

                        int count = -1;

                        while (count < len)
                        {
                            // int l = input[idx].Length;
                            string ln = input[idx];

                            int need = len-count-1;
                            if (ln.Length > need)
                                ln = ln.Substring(0, need);

                            output.Add(ln);
                            count += ln.Length + 1;
                            idx++;
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