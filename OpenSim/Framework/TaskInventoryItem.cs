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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using libsecondlife;

namespace OpenSim.Framework
{
    public class TaskInventoryItem
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
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "lsl_text",
            ""
        };
        
        /// <summary>
        /// Asset types
        /// </summary>
        public static string[] Types = new string[]
        {
            "texture",
            "sound",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "lsltext",
            ""
        };

        public LLUUID item_id = LLUUID.Zero;
        public LLUUID parent_id = LLUUID.Zero; //parent folder id 

        public uint base_mask = FULL_MASK_PERMISSIONS_GENERAL;
        public uint owner_mask = FULL_MASK_PERMISSIONS_GENERAL;
        public uint group_mask = FULL_MASK_PERMISSIONS_GENERAL;
        public uint everyone_mask = FULL_MASK_PERMISSIONS_GENERAL;
        public uint next_owner_mask = FULL_MASK_PERMISSIONS_GENERAL;
        public LLUUID creator_id = LLUUID.Zero;
        public LLUUID owner_id = LLUUID.Zero;
        public LLUUID last_owner_id = LLUUID.Zero;
        public LLUUID group_id = LLUUID.Zero;

        public LLUUID asset_id = LLUUID.Zero;
        public string type = "";
        public string inv_type = "";
        public uint flags = 0;
        public string name = "";
        public string desc = "";
        public uint creation_date = 0;

        public LLUUID ParentPartID = LLUUID.Zero;
    }
}
