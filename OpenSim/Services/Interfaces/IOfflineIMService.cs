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
            object otmp;

            if (dict.TryGetValue("BinaryBucket", out otmp) && otmp is string bbs)
                im.binaryBucket = OpenMetaverse.Utils.HexStringToBytes(bbs, true);

            if (dict.TryGetValue("Dialog", out otmp) && otmp is string ds)
                im.dialog = byte.Parse(ds);

            if (dict.TryGetValue("FromAgentID", out otmp) && otmp is string faid)
                im.fromAgentID = new Guid(faid);

            if (dict.TryGetValue("FromAgentName", out otmp) && otmp is string fan)
                im.fromAgentName = fan;
            else
                im.fromAgentName = string.Empty;

            if (dict.TryGetValue("FromGroup", out otmp) && otmp is string fg)
                im.fromGroup = bool.Parse(fg);

            if (dict.TryGetValue("SessionID", out otmp) && otmp is string sid)
                im.imSessionID = new Guid(sid);

            if (dict.TryGetValue("Message", out otmp) && otmp is string msg)
                im.message = msg;
            else
                im.message = string.Empty;

            if (dict.TryGetValue("Offline", out otmp) && otmp is string off)
                im.offline = byte.Parse(off);

            if (dict.TryGetValue("EstateID", out otmp) && otmp is string eid)
                im.ParentEstateID = UInt32.Parse(eid);

            if (dict.TryGetValue("Position", out otmp) && otmp is string vpos)
                im.Position = Vector3.Parse(vpos);

            if (dict.TryGetValue("RegionID", out otmp) && otmp is string rid)
                im.RegionID = new Guid(rid);

            if (dict.TryGetValue("Timestamp", out otmp) && otmp is string ts)
                im.timestamp = UInt32.Parse(ts);

            if (dict.TryGetValue("ToAgentID", out otmp) && otmp is string tid)
                im.toAgentID = new Guid(tid);

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
