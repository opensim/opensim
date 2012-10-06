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
using OpenMetaverse;

namespace OpenSim.Framework
{
    [Serializable]
    public class GridInstantMessage
    {
        public Guid fromAgentID;
        public string fromAgentName;
        public Guid toAgentID;
        public byte dialog;
        public bool fromGroup;
        public string message;
        public Guid imSessionID;
        public byte offline;
        public Vector3 Position;
        public byte[] binaryBucket;

        public uint ParentEstateID;
        public Guid RegionID;
        public uint timestamp;

        public GridInstantMessage()
        {
            binaryBucket = new byte[0];
        }

        public GridInstantMessage(IScene scene, UUID _fromAgentID,
                string _fromAgentName, UUID _toAgentID,
                byte _dialog, bool _fromGroup, string _message,
                UUID _imSessionID, bool _offline, Vector3 _position,
                byte[] _binaryBucket, bool addTimestamp)
        {
            fromAgentID = _fromAgentID.Guid;
            fromAgentName = _fromAgentName;
            toAgentID = _toAgentID.Guid;
            dialog = _dialog;
            fromGroup = _fromGroup;
            message = _message;
            imSessionID = _imSessionID.Guid;
            if (_offline)
                offline = 1;
            else
                offline = 0;
            Position = _position;
            binaryBucket = _binaryBucket;

            if (scene != null)
            {
                ParentEstateID = scene.RegionInfo.EstateSettings.ParentEstateID;
                RegionID = scene.RegionInfo.RegionSettings.RegionUUID.Guid;
            }

            if (addTimestamp)
                timestamp = (uint)Util.UnixTimeSinceEpoch();
        }

        public GridInstantMessage(IScene scene, UUID _fromAgentID,
                string _fromAgentName, UUID _toAgentID, byte _dialog,
                string _message, bool _offline,
                Vector3 _position) : this(scene, _fromAgentID, _fromAgentName,
                _toAgentID, _dialog, false, _message,
                _fromAgentID ^ _toAgentID, _offline, _position, new byte[0], true)
        {
        }
    }
}
