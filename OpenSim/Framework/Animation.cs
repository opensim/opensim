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
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    /// <summary>
    /// Information about an Animation
    /// </summary>
    [Serializable]
    public class Animation
    {
        private UUID animID;

        /// <summary>
        /// ID of Animation
        /// </summary>
        public UUID AnimID
        {
            get => animID;
            set => animID = value;
        }

        private int sequenceNum;
        public int SequenceNum
        {
            get => sequenceNum;
            set => sequenceNum = value;
        }

        private UUID objectID;

        /// <summary>
        /// Unique ID of object that is being animated
        /// </summary>
        public UUID ObjectID
        {
            get => objectID;
            set => objectID = value;
        }

        public Animation()
        {
        }

        /// <summary>
        /// Creates an Animation based on the data
        /// </summary>
        /// <param name="animID">UUID ID of animation</param>
        /// <param name="sequenceNum"></param>
        /// <param name="objectID">ID of object to be animated</param>
        public Animation(UUID animID, int sequenceNum, UUID objectID)
        {
            this.animID = animID;
            this.sequenceNum = sequenceNum;
            this.objectID = objectID;
        }

        /// <summary>
        /// Animation from OSDMap from LLSD XML or LLSD json
        /// </summary>
        /// <param name="args"></param>
        public Animation(OSDMap args)
        {
            UnpackUpdateMessage(args);
        }


        /// <summary>
        /// Pack this object up as an OSDMap for transferring via LLSD XML or LLSD json
        /// </summary>
        /// <returns></returns>
        public OSDMap PackUpdateMessage()
        {
            var anim = new OSDMap
            {
                ["animation"] = OSD.FromUUID(animID),
                ["object_id"] = OSD.FromUUID(objectID),
                ["seq_num"] = OSD.FromInteger(sequenceNum)
            };
            return anim;
        }

        /// <summary>
        /// Fill object with data from OSDMap
        /// </summary>
        /// <param name="args"></param>
        public void UnpackUpdateMessage(OSDMap args)
        {
            OSD tmp;
            if (args.TryGetValue("animation", out tmp))
                animID = tmp.AsUUID();
            if (args.TryGetValue("object_id", out tmp))
                objectID = tmp.AsUUID();
            if (args.TryGetValue("seq_num", out tmp))
                sequenceNum = tmp.AsInteger();
        }

        public override bool Equals(object obj)
        {
            if (obj is Animation other)
            {
                return (other.AnimID.Equals(this.AnimID)
                        && other.SequenceNum == this.SequenceNum
                        && other.ObjectID.Equals(this.ObjectID) );
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return $"AnimID= {AnimID}/seq={SequenceNum}/objID={ObjectID}";
        }

    }
}
