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
        public static readonly string ASSETS_METADATA_PATH = "assets.xml";
        
        /// <summary>
        /// Path for the prims file
        /// </summary>
        public static readonly string OBJECTS_PATH = "objects/";
                
        /// <summary>
        /// Extensions used for asset types in the archive
        /// </summary>
        public static readonly IDictionary<sbyte, string> ASSET_TYPE_TO_EXTENSION = new Dictionary<sbyte, string>();
        public static readonly IDictionary<string, sbyte> EXTENSION_TO_ASSET_TYPE = new Dictionary<string, sbyte>();
        
        static ArchiveConstants()
        {
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Animation]           = ".bvh";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Bodypart]            = ".bpt.txt";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.CallingCard]         = ".ccd.txt";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Clothing]            = ".clo.txt";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Folder]              = ".fld.txt";   // Not sure if we'll ever see this
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Gesture]             = ".gst.txt";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.ImageJPEG]           = ".jpg";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.ImageTGA]            = ".img.tga";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.LostAndFoundFolder]  = ".lff.txt";   // Not sure if we'll ever see this
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.LSLBytecode]         = ".lso";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.LSLText]             = ".lsl";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Notecard]            = ".ncd.txt";            
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Object]              = ".obj.xml";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.RootFolder]          = ".rof.txt";   // Not sure if we'll ever see this
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Script]              = ".spt";       // Not sure if we'll ever see this
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Simstate]            = ".sst";       // Not sure if we'll ever see this
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.SnapshotFolder]      = ".ssf.txt";   // Not sure if we'll ever see this
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Sound]               = ".ogg";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.SoundWAV]            = ".wav";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.Texture]             = ".jp2";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.TextureTGA]          = ".tga";
            ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.TrashFolder]         = ".trf.txt";   // Not sure if we'll ever see this                                                            
            
            EXTENSION_TO_ASSET_TYPE[".bvh"]     = (sbyte)AssetType.Animation;
            EXTENSION_TO_ASSET_TYPE[".bpt.txt"] = (sbyte)AssetType.Bodypart;
            EXTENSION_TO_ASSET_TYPE[".ccd.txt"] = (sbyte)AssetType.CallingCard;
            EXTENSION_TO_ASSET_TYPE[".clo.txt"] = (sbyte)AssetType.Clothing;
            EXTENSION_TO_ASSET_TYPE[".fld.txt"] = (sbyte)AssetType.Folder;
            EXTENSION_TO_ASSET_TYPE[".gst.txt"] = (sbyte)AssetType.Gesture;
            EXTENSION_TO_ASSET_TYPE[".jpg"]     = (sbyte)AssetType.ImageJPEG;
            EXTENSION_TO_ASSET_TYPE[".img.tga"] = (sbyte)AssetType.ImageTGA;
            EXTENSION_TO_ASSET_TYPE[".lff.txt"] = (sbyte)AssetType.LostAndFoundFolder;
            EXTENSION_TO_ASSET_TYPE[".lso"]     = (sbyte)AssetType.LSLBytecode;
            EXTENSION_TO_ASSET_TYPE[".lsl"]     = (sbyte)AssetType.LSLText;
            EXTENSION_TO_ASSET_TYPE[".ncd.txt"] = (sbyte)AssetType.Notecard;
            EXTENSION_TO_ASSET_TYPE[".obj.xml"] = (sbyte)AssetType.Object;
            EXTENSION_TO_ASSET_TYPE[".rof.txt"] = (sbyte)AssetType.RootFolder;
            EXTENSION_TO_ASSET_TYPE[".spt"]     = (sbyte)AssetType.Script;
            EXTENSION_TO_ASSET_TYPE[".sst"]     = (sbyte)AssetType.Simstate;
            EXTENSION_TO_ASSET_TYPE[".ssf.txt"] = (sbyte)AssetType.SnapshotFolder;
            EXTENSION_TO_ASSET_TYPE[".ogg"]     = (sbyte)AssetType.Sound;
            EXTENSION_TO_ASSET_TYPE[".wav"]     = (sbyte)AssetType.SoundWAV;
            EXTENSION_TO_ASSET_TYPE[".jp2"]     = (sbyte)AssetType.Texture;
            EXTENSION_TO_ASSET_TYPE[".tga"]     = (sbyte)AssetType.TextureTGA;
            EXTENSION_TO_ASSET_TYPE[".trf.txt"] = (sbyte)AssetType.TrashFolder;
        }
    }
}
