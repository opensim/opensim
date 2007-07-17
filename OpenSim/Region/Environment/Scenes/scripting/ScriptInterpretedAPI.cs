using System;
using System.Collections.Generic;
using System.Text;
using Key = libsecondlife.LLUUID;
using Rotation = libsecondlife.LLQuaternion;
using Vector = libsecondlife.LLVector3;
using LSLList = System.Collections.Generic.List<string>;

using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.LandManagement;

namespace OpenSim.Region.Scripting
{
    /// <summary>
    /// A class inteded to act as an API for LSL-styled interpreted languages
    /// </summary>
    /// <remarks>Avoid at all costs. This should ONLY be used for LSL.</remarks>
    class ScriptInterpretedAPI
    {
        protected libsecondlife.LLUUID m_object;
        protected Scene m_scene;

        /// <summary>
        /// The scene in which this script is acting
        /// </summary>
        public Scene World
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
        public SceneObject Task
        {
            get { return World.Objects[ObjectID]; }
        }

        /// <summary>
        /// Creates a new ScriptInterpretedAPI for a specified object
        /// </summary>
        /// <param name="world">The scene the object is located in</param>
        /// <param name="member">The specific member being 'occupied' by the script</param>
        public ScriptInterpretedAPI(Scene world, libsecondlife.LLUUID member)
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
            return (float)Math.Acos(val);
        }

        [Obsolete("Unimplemented")]
        public void osAddToLandPassList(Key avatar, float hours)
        {
            int parcelID = 0;

            Vector myPosition = Task.Pos;
            Land myParcel = World.LandManager.getLandObject(myPosition.X, myPosition.Y);

            OpenSim.Framework.Console.MainLog.Instance.Warn("Unimplemented function called by script: osAddToLandPassList(Key avatar, float hours)");
            return;
        }

        [Obsolete("Unimplemented")]
        public void osAdjustSoundVolume(float volume)
        {
            OpenSim.Framework.Console.MainLog.Instance.Warn("Unimplemented function called by script: osAdjustSoundVolume(float volume)");
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
            Axiom.Math.Quaternion axA = new Axiom.Math.Quaternion(a.W, a.X, a.Y, a.Z);
            Axiom.Math.Quaternion axB = new Axiom.Math.Quaternion(b.W, b.X, b.Y, b.Z);

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
            return (float)Math.Asin(val);
        }

        public float osAtan2(float x, float y)
        {
            return (float)Math.Atan2(x, y);
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
            foreach (KeyValuePair<Key, EntityBase> Child in Task.Children)
            {
                if (Child.Value is ScenePresence)
                {
                    return Child.Value.uuid;
                }
            }

            return Key.Zero;
        }

        public Rotation osAxes2Rot(Vector fwd, Vector left, Vector up)
        {
            Axiom.Math.Quaternion axQ = new Axiom.Math.Quaternion();
            Axiom.Math.Vector3 axFwd = new Axiom.Math.Vector3(fwd.X, fwd.Y, fwd.Z);
            Axiom.Math.Vector3 axLeft = new Axiom.Math.Vector3(left.X, left.Y, left.Z);
            Axiom.Math.Vector3 axUp = new Axiom.Math.Vector3(up.X, up.Y, up.Z);

            axQ.FromAxes(axFwd, axLeft, axUp);

            return new Rotation(axQ.x, axQ.y, axQ.z, axQ.w);
        }

        public Rotation osAxisAngle2Rot(Vector axis, float angle)
        {
            Axiom.Math.Quaternion axQ = Axiom.Math.Quaternion.FromAngleAxis(angle, new Axiom.Math.Vector3(axis.X, axis.Y, axis.Z));

            return new Rotation(axQ.x, axQ.y, axQ.z, axQ.w);
        }

        public string osBase64ToString(string str)
        {
            Encoding enc = System.Text.Encoding.UTF8;
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
            return (int)Math.Ceiling(val);
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
            return (float)Math.Cos(theta);
        }

        [Obsolete("Unimplemented")]
        public void osCreateLink(Key target, int parent)
        {
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
