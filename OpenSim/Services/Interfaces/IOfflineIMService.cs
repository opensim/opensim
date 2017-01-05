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

using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Services.Interfaces
{
    public interface IOfflineIMService
    {
        List<GridInstantMessage> GetMessages(UUID principalID);

        bool StoreMessage(GridInstantMessage im, out string reason);

        /// <summary>
        /// Delete messages to or from this user (or group).
        /// </summary>
        /// <param name="userID">A user or group ID</param>
        void DeleteMessages(UUID userID);
    }

    public class OfflineIMDataUtils
    {
        public static GridInstantMessage GridInstantMessage(Dictionary<string, object> dict)
        {
            GridInstantMessage im = new GridInstantMessage();

            if (dict.ContainsKey("BinaryBucket") && dict["BinaryBucket"] != null)
                im.binaryBucket = OpenMetaverse.Utils.HexStringToBytes(dict["BinaryBucket"].ToString(), true);

            if (dict.ContainsKey("Dialog") && dict["Dialog"] != null)
                im.dialog = byte.Parse(dict["Dialog"].ToString());

            if (dict.ContainsKey("FromAgentID") && dict["FromAgentID"] != null)
                im.fromAgentID = new Guid(dict["FromAgentID"].ToString());

            if (dict.ContainsKey("FromAgentName") && dict["FromAgentName"] != null)
                im.fromAgentName = dict["FromAgentName"].ToString();
            else
                im.fromAgentName = string.Empty;

            if (dict.ContainsKey("FromGroup") && dict["FromGroup"] != null)
                im.fromGroup = bool.Parse(dict["FromGroup"].ToString());

            if (dict.ContainsKey("SessionID") && dict["SessionID"] != null)
                im.imSessionID = new Guid(dict["SessionID"].ToString());

            if (dict.ContainsKey("Message") && dict["Message"] != null)
                im.message = dict["Message"].ToString();
            else
                im.message = string.Empty;

            if (dict.ContainsKey("Offline") && dict["Offline"] != null)
                im.offline = byte.Parse(dict["Offline"].ToString());

            if (dict.ContainsKey("EstateID") && dict["EstateID"] != null)
                im.ParentEstateID = UInt32.Parse(dict["EstateID"].ToString());

            if (dict.ContainsKey("Position") && dict["Position"] != null)
                im.Position = Vector3.Parse(dict["Position"].ToString());

            if (dict.ContainsKey("RegionID") && dict["RegionID"] != null)
                im.RegionID = new Guid(dict["RegionID"].ToString());

            if (dict.ContainsKey("Timestamp") && dict["Timestamp"] != null)
                im.timestamp = UInt32.Parse(dict["Timestamp"].ToString());

            if (dict.ContainsKey("ToAgentID") && dict["ToAgentID"] != null)
                im.toAgentID = new Guid(dict["ToAgentID"].ToString());

            return im;
        }

        public static Dictionary<string, object> GridInstantMessage(GridInstantMessage im)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();

            dict["BinaryBucket"] = OpenMetaverse.Utils.BytesToHexString(im.binaryBucket, im.binaryBucket.Length, null);
            dict["Dialog"] = im.dialog.ToString();
            dict["FromAgentID"] = im.fromAgentID.ToString();
            dict["FromAgentName"] = im.fromAgentName == null ? string.Empty : im.fromAgentName;
            dict["FromGroup"] = im.fromGroup.ToString();
            dict["SessionID"] = im.imSessionID.ToString();
            dict["Message"] = im.message == null ? string.Empty : im.message;
            dict["Offline"] = im.offline.ToString();
            dict["EstateID"] = im.ParentEstateID.ToString();
            dict["Position"] = im.Position.ToString();
            dict["RegionID"] = im.RegionID.ToString();
            dict["Timestamp"] = im.timestamp.ToString();
            dict["ToAgentID"] = im.toAgentID.ToString();

            return dict;
        }

    }
}
