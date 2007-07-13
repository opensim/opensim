using System;
using System.Collections.Generic;
using System.Text;
using Key = libsecondlife.LLUUID;
using Rotation = libsecondlife.LLQuaternion;
using Vector = libsecondlife.LLVector3;
using LSLList = System.Collections.Generic.List<string>;


using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Scripting
{
    // This class is to be used for engines which may not be able to access the Scene directly.
    // Scene access is preffered, but obviously not possible on some non-.NET languages.
    public class ScriptAPI
    {
        Scene scene;

        public ScriptAPI(Scene world)
        {
            scene = world;
        }

        public Object CallMethod(String method, Object[] args)
        {
            return null;
        }

        // LSL Compatibility below
        // remap LL* functions to OS*
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
            return 0.0;
        }
    }
}
