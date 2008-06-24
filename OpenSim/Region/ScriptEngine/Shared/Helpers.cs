using System;
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.ScriptEngine.Shared
{
    public class DetectParams
    {
        public DetectParams()
        {
            Key = LLUUID.Zero;
            OffsetPos = new LSL_Types.Vector3();
            LinkNum = 0;
            Group = LLUUID.Zero;
            Name = String.Empty;
            Owner = LLUUID.Zero;
            Position = new LSL_Types.Vector3();
            Rotation = new LSL_Types.Quaternion();
            Type = 0;
            Velocity = new LSL_Types.Vector3();
        }

        public LLUUID Key;
        public LSL_Types.Vector3 OffsetPos;
        public int LinkNum;
        public LLUUID Group;
        public string Name;
        public LLUUID Owner;
        public LSL_Types.Vector3 Position;
        public LSL_Types.Quaternion Rotation;
        public int Type;
        public LSL_Types.Vector3 Velocity;

        public void Populate(Scene scene)
        {
            SceneObjectPart part = scene.GetSceneObjectPart(Key);
            if (part == null) // Avatar, maybe?
            {
                ScenePresence presence = scene.GetScenePresence(Key);
                if (presence == null)
                    return;

                Name = presence.Firstname + " " + presence.Lastname;
                Owner = Key;
                Position = new LSL_Types.Vector3(
                        presence.AbsolutePosition.X,
                        presence.AbsolutePosition.X,
                        presence.AbsolutePosition.Z);
                Rotation = new LSL_Types.Quaternion(
                        presence.Rotation.x,
                        presence.Rotation.y,
                        presence.Rotation.z,
                        presence.Rotation.w);
                Velocity = new LSL_Types.Vector3(
                        presence.Velocity.X,
                        presence.Velocity.X,
                        presence.Velocity.Z);

                Type = 0x01; // Avatar
                if (presence.Velocity != LLVector3.Zero)
                    Type |= 0x02; // Active

                Group = presence.ControllingClient.ActiveGroupId;

                return;
            }

            part=part.ParentGroup.RootPart; // We detect objects only

            LinkNum = 0; // Not relevant

            Group = part.GroupID;
            Name = part.Name;
            Owner = part.OwnerID;
            if (part.Velocity == LLVector3.Zero)
                Type = 0x04; // Passive
            else
                Type = 0x02; // Passive

            foreach (SceneObjectPart p in part.ParentGroup.Children.Values)
            {
                if (p.ContainsScripts())
                {
                    Type |= 0x08; // Scripted
                    break;
                }
            }

            Position = new LSL_Types.Vector3(part.AbsolutePosition.X,
                                             part.AbsolutePosition.Y,
                                             part.AbsolutePosition.Z);

            LLQuaternion wr = part.GetWorldRotation();
            Rotation = new LSL_Types.Quaternion(wr.X, wr.Y, wr.Z, wr.W);

            Velocity = new LSL_Types.Vector3(part.Velocity.X,
                                             part.Velocity.Y,
                                             part.Velocity.Z);
        }
    }

    public class EventParams
    {
        public EventParams(string eventName, Object[] eventParams, DetectParams[] detectParams)
        {
            EventName=eventName;
            Params=eventParams;
            DetectParams=detectParams;
        }

        public string EventName;
        public Object[] Params;
        public DetectParams[] DetectParams;
    }
}
