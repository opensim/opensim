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

using System.Collections.Generic;
using libsecondlife;

namespace OpenSim.Region.Environment.Modules.World.Archiver
{
    /// <summary>
    /// Constants for the archiving module
    /// </summary>
    public class ArchiveConstants
    {
        /// <summary>
        /// Path for the assets held in an archive
        /// </summary>
        public static readonly string ASSETS_PATH = "assets/";
        
        /// <summary>
        /// Path for the assets metadata file
        /// </summary>
        //public static readonly string ASSETS_METADATA_PATH = "assets.xml";
        
        /// <summary>
        /// Path for the prims file
        /// </summary>
        public static readonly string OBJECTS_PATH = "objects/";
        
        /// <summary>
        /// Path for terrains.  Technically these may be assets, but I think it's quite nice to split them out.
        /// </summary>
        public static readonly string TERRAINS_PATH = "terrains/";
                
        /// <summary>
        /// Extensions used for asset types in the archive
        /// </summary>
        public static readonly IDictionary<sbyte, string> ASSET_TYPE_TO_EXTENSION = new Dictionary<sbyte, string>();
        public static readonly IDictionary<string, sbyte> EXTENSION_TO_ASSET_TYPE = new Dictionary<string, sbyte>();
        
        static ArchiveConstants()
        {
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Animation]           = "_animation.bvh";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Bodypart]            = "_bodypart.txt";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.CallingCard]         = "_callingcard.txt";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Clothing]            = "_clothing.txt";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Folder]              = "_folder.txt";   // Not sure if we'll ever see this
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Gesture]             = "_gesture.txt";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.ImageJPEG]           = "_image.jpg";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.ImageTGA]            = "_image.tga";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.LostAndFoundFolder]  = "_lostandfoundfolder.txt";   // Not sure if we'll ever see this
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.LSLBytecode]         = "_bytecode.lso";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.LSLText]             = "_script.lsl";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Notecard]            = "_notecard.txt";            
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Object]              = "_object.xml";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.RootFolder]          = "_rootfolder.txt";   // Not sure if we'll ever see this
// disable warning: we know Script is obsolete, but need to support it
// anyhow 
#pragma warning disable 0612
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Script]              = "_script.txt";   // Not sure if we'll ever see this
#pragma warning restore 0612
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Simstate]            = "_simstate.bin";   // Not sure if we'll ever see this
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.SnapshotFolder]      = "_snapshotfolder.txt";   // Not sure if we'll ever see this
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Sound]               = "_sound.ogg";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.SoundWAV]            = "_sound.wav";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Texture]             = "_texture.jp2";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.TextureTGA]          = "_texture.tga";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.TrashFolder]         = "_trashfolder.txt";   // Not sure if we'll ever see this                                                            
            
            EXTENSION_TO_ASSET_TYPE["_animation.bvh"] = (sbyte)AssetType.Animation;
            EXTENSION_TO_ASSET_TYPE["_bodypart.txt"] = (sbyte)AssetType.Bodypart;
            EXTENSION_TO_ASSET_TYPE["_callingcard.txt"] = (sbyte)AssetType.CallingCard;
            EXTENSION_TO_ASSET_TYPE["_clothing.txt"] = (sbyte)AssetType.Clothing;
            EXTENSION_TO_ASSET_TYPE["_folder.txt"] = (sbyte)AssetType.Folder;
            EXTENSION_TO_ASSET_TYPE["_gesture.txt"] = (sbyte)AssetType.Gesture;
            EXTENSION_TO_ASSET_TYPE["_image.jpg"] = (sbyte)AssetType.ImageJPEG;
            EXTENSION_TO_ASSET_TYPE["_image.tga"] = (sbyte)AssetType.ImageTGA;
            EXTENSION_TO_ASSET_TYPE["_lostandfoundfolder.txt"] = (sbyte)AssetType.LostAndFoundFolder;
            EXTENSION_TO_ASSET_TYPE["_bytecode.lso"] = (sbyte)AssetType.LSLBytecode;
            EXTENSION_TO_ASSET_TYPE["_script.lsl"] = (sbyte)AssetType.LSLText;
            EXTENSION_TO_ASSET_TYPE["_notecard.txt"] = (sbyte)AssetType.Notecard;
            EXTENSION_TO_ASSET_TYPE["_object.xml"] = (sbyte)AssetType.Object;
            EXTENSION_TO_ASSET_TYPE["_rootfolder.txt"] = (sbyte)AssetType.RootFolder;
// disable warning: we know Script is obsolete, but need to support it
// anyhow 
#pragma warning disable 0612
            EXTENSION_TO_ASSET_TYPE["_script.txt"] = (sbyte)AssetType.Script;
#pragma warning restore 0612
            EXTENSION_TO_ASSET_TYPE["_simstate.bin"] = (sbyte)AssetType.Simstate;
            EXTENSION_TO_ASSET_TYPE["_snapshotfolder.txt"] = (sbyte)AssetType.SnapshotFolder;
            EXTENSION_TO_ASSET_TYPE["_sound.ogg"] = (sbyte)AssetType.Sound;
            EXTENSION_TO_ASSET_TYPE["_sound.wav"] = (sbyte)AssetType.SoundWAV;
            EXTENSION_TO_ASSET_TYPE["_texture.jp2"] = (sbyte)AssetType.Texture;
            EXTENSION_TO_ASSET_TYPE["_texture.tga"] = (sbyte)AssetType.TextureTGA;
            EXTENSION_TO_ASSET_TYPE["_trashfolder.txt"] = (sbyte)AssetType.TrashFolder;
        }
    }
}
