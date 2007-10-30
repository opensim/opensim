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

using System;
using System.Text;
using Axiom.Math;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.LandManagement;
using OpenSim.Region.Environment.Scenes;
using Key = libsecondlife.LLUUID;
using Rotation = libsecondlife.LLQuaternion;
using Vector = libsecondlife.LLVector3;
using LSLList = System.Collections.Generic.List<string>;

namespace OpenSim.Region.ExtensionsScriptModule
{
    /// <summary>
    /// A class inteded to act as an API for LSL-styled interpreted languages
    /// </summary>
    /// <remarks>Avoid at all costs. This should ONLY be used for LSL.</remarks>
    internal class ScriptInterpretedAPI
    {
        protected Key m_object;
        protected Scene m_scene;

        /// <summary>
        /// The scene in which this script is acting
        /// </summary>
        public Scene Scene
        {
            get { return m_scene; }
        }

        /// <summary>
        /// The id of the object our script is supposed to be acting in
        /// </summary>
        public Key ObjectID
        {
            get { return m_object; }
        }

        /// <summary>
        /// The object our script is supposed to be in
        /// </summary>
        public SceneObjectGroup Task
        {
            get { return Scene.Objects[ObjectID]; }
        }

        /// <summary>
        /// Creates a new ScriptInterpretedAPI for a specified object
        /// </summary>
        /// <param name="world">The scene the object is located in</param>
        /// <param name="member">The specific member being 'occupied' by the script</param>
        public ScriptInterpretedAPI(Scene world, Key member)
        {
            m_scene = world;
            m_object = member;
        }

        /// <summary>
        /// Returns the absolute number of a integer value.
        /// </summary>
        /// <param name="val">Input</param>
        /// <returns>Absolute number of input</returns>
        public int osAbs(int val)
        {
            return Math.Abs(val);
        }

        public float osAcos(float val)
        {
            return (float) Math.Acos(val);
        }

        [Obsolete("Unimplemented")]
        public void osAddToLandPassList(Key avatar, float hours)
        {
            Vector myPosition = Task.AbsolutePosition;
            Land myParcel = Scene.LandManager.getLandObject(myPosition.X, myPosition.Y);

            MainLog.Instance.Warn("script",
                                  "Unimplemented function called by script: osAddToLandPassList(Key avatar, float hours)");
            return;
        }

        [Obsolete("Unimplemented")]
        public void osAdjustSoundVolume(float volume)
        {
            MainLog.Instance.Warn("script", "Unimplemented function called by script: osAdjustSoundVolume(float volume)");
            return;
        }

        [Obsolete("Unimplemented")]
        public void osAllowInventoryDrop(int add)
        {
            return;
        }

        [Obsolete("Unimplemented")]
        public float osAngleBetween(Rotation a, Rotation b)
        {
            Quaternion axA = new Quaternion(a.W, a.X, a.Y, a.Z);
            Quaternion axB = new Quaternion(b.W, b.X, b.Y, b.Z);

            return 0;
        }

        [Obsolete("Unimplemented")]
        public void osApplyImpulse(Vector force, int local)
        {
            return;
        }

        [Obsolete("Unimplemented")]
        public void osApplyRotationalImpulse(Vector force, int local)
        {
            return;
        }

        public float osAsin(float val)
        {
            return (float) Math.Asin(val);
        }

        public float osAtan2(float x, float y)
        {
            return (float) Math.Atan2(x, y);
        }

        [Obsolete("Unimplemented")]
        public void osAttachToAvatar(Key avatar, int attachmentPoint)
        {
            return;
        }

        [Obsolete("Unimplemented")]
        public Key osAvatarOnSitTarget()
        {
            //TODO: Follow this as Children is chanced to be of type entity to support ScenePresences
            /*
            foreach (KeyValuePair<Key, EntityBase> Child in Task.Children)
            {
                if (Child.Value is ScenePresence)
                {
                    return Child.Value.uuid;
                }
            }
            */

            return Key.Zero;
        }

        public Rotation osAxes2Rot(Vector fwd, Vector left, Vector up)
        {
            Quaternion axQ = new Quaternion();
            Vector3 axFwd = new Vector3(fwd.X, fwd.Y, fwd.Z);
            Vector3 axLeft = new Vector3(left.X, left.Y, left.Z);
            Vector3 axUp = new Vector3(up.X, up.Y, up.Z);

            axQ.FromAxes(axFwd, axLeft, axUp);

            return new Rotation(axQ.x, axQ.y, axQ.z, axQ.w);
        }

        public Rotation osAxisAngle2Rot(Vector axis, float angle)
        {
            Quaternion axQ = Quaternion.FromAngleAxis(angle, new Vector3(axis.X, axis.Y, axis.Z));

            return new Rotation(axQ.x, axQ.y, axQ.z, axQ.w);
        }

        public string osBase64ToString(string str)
        {
            Encoding enc = Encoding.UTF8;
            return enc.GetString(Convert.FromBase64String(str));
        }

        [Obsolete("Unimplemented")]
        public void osBreakAllLinks()
        {
            return;
        }

        [Obsolete("Unimplemented")]
        public void osBreakLink()
        {
            return;
        }

        public LSLList osCSV2List(string src)
        {
            LSLList retVal = new LSLList();
            retVal.AddRange(src.Split(','));

            return retVal;
        }

        public int osCeil(float val)
        {
            return (int) Math.Ceiling(val);
        }

        [Obsolete("Unimplemented")]
        public void osCloseRemoteDataChannel(Key channel)
        {
            return;
        }

        [Obsolete("Unimplemented")]
        public float osCloud(Vector offset)
        {
            return 0.0f;
        }

        [Obsolete("Unimplemented")]
        public void osCollisionFilter(string name, Key id, int accept)
        {
            return;
        }

        [Obsolete("Unimplemented")]
        public void osCollisionSprite(string impact_sprite)
        {
            return;
        }

        public float osCos(float theta)
        {
            return (float) Math.Cos(theta);
        }

        public void osCreateLink(Key target, int parent)
        {
            if (Scene.Entities[target] is SceneObjectGroup)
                Task.LinkToGroup((SceneObjectGroup) Scene.Entities[target]);

            return;
        }

        [Obsolete("Partially Unimplemented")]
        public LSLList osDeleteSubList(LSLList src, int start, int end)
        {
            if (start < 0 || end < 0)
            {
                throw new Exception("Unsupported at this time.");
            }

            src.RemoveRange(start, start - end + 1);
            return src;
        }

        [Obsolete("Partially Unimplemented")]
        public string osDeleteSubString(string src, int start, int end)
        {
            if (start < 0 || end < 0)
            {
                throw new Exception("Unsupported at this time.");
            }

            return src.Remove(start, start - end + 1);
        }

        [Obsolete("Unimplemented")]
        public void osDetachFromAvatar(Key avatar)
        {
            return;
        }
    }
}