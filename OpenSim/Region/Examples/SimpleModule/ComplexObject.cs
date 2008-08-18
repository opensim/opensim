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

using libsecondlife;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Examples.SimpleModule
{
    public class ComplexObject : SceneObjectGroup
    {
        private readonly LLQuaternion m_rotationDirection;

        protected override bool InSceneBackup
        {
            get
            {
                return false;
            }
        }

        private class RotatingWheel : SceneObjectPart
        {
            private readonly LLQuaternion m_rotationDirection;

            public RotatingWheel()
            {
            }

            public RotatingWheel(ulong regionHandle, SceneObjectGroup parent, LLUUID ownerID, uint localID,
                                 LLVector3 groupPosition, LLVector3 offsetPosition, LLQuaternion rotationDirection)
                : base(
                    regionHandle, parent, ownerID, localID, PrimitiveBaseShape.Default, groupPosition, offsetPosition
                    )
            {
                m_rotationDirection = rotationDirection;

                Flags |= LLObject.ObjectFlags.Touch;
            }

            public override void UpdateMovement()
            {
                UpdateRotation(RotationOffset*m_rotationDirection);
            }
        }

        public override void UpdateMovement()
        {
            UpdateGroupRotation(GroupRotation*m_rotationDirection);

            base.UpdateMovement();
        }

        public ComplexObject()
        {
        }

        public ComplexObject(Scene scene, ulong regionHandle, LLUUID ownerID, uint localID, LLVector3 pos)
            : base(scene, regionHandle, ownerID, localID, pos, PrimitiveBaseShape.Default)
        {
            m_rotationDirection = new LLQuaternion(0.05f, 0.1f, 0.15f);

            AddPart(
                new RotatingWheel(regionHandle, this, ownerID, scene.PrimIDAllocate(), pos, new LLVector3(0, 0, 0.75f),
                                  new LLQuaternion(0.05f, 0, 0)));
            AddPart(
                new RotatingWheel(regionHandle, this, ownerID, scene.PrimIDAllocate(), pos, new LLVector3(0, 0, -0.75f),
                                  new LLQuaternion(-0.05f, 0, 0)));

            AddPart(
                new RotatingWheel(regionHandle, this, ownerID, scene.PrimIDAllocate(), pos, new LLVector3(0, 0.75f, 0),
                                  new LLQuaternion(0.5f, 0, 0.05f)));
            AddPart(
                new RotatingWheel(regionHandle, this, ownerID, scene.PrimIDAllocate(), pos, new LLVector3(0, -0.75f, 0),
                                  new LLQuaternion(-0.5f, 0, -0.05f)));

            AddPart(
                new RotatingWheel(regionHandle, this, ownerID, scene.PrimIDAllocate(), pos, new LLVector3(0.75f, 0, 0),
                                  new LLQuaternion(0, 0.5f, 0.05f)));
            AddPart(
                new RotatingWheel(regionHandle, this, ownerID, scene.PrimIDAllocate(), pos, new LLVector3(-0.75f, 0, 0),
                                  new LLQuaternion(0, -0.5f, -0.05f)));

            RootPart.Flags |= LLObject.ObjectFlags.Touch;

            UpdateParentIDs();
        }

        public override void OnGrabPart(SceneObjectPart part, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            m_parts.Remove(part.UUID);

            remoteClient.SendKillObject(m_regionHandle, part.LocalId);
            remoteClient.AddMoney(1);
            remoteClient.SendChatMessage("Poof!", 1, AbsolutePosition, "Party Party", LLUUID.Zero, (byte)ChatSourceType.Object, (byte)ChatAudibleLevel.Fully);
        }

        public override void OnGrabGroup(LLVector3 offsetPos, IClientAPI remoteClient)
        {
            if (m_parts.Count == 1)
            {
                m_parts.Remove(m_rootPart.UUID);
                m_scene.DeleteSceneObject(this);
                remoteClient.SendKillObject(m_regionHandle, m_rootPart.LocalId);
                remoteClient.AddMoney(50);
                remoteClient.SendChatMessage("KABLAM!!!", 1, AbsolutePosition, "Groupie Groupie", LLUUID.Zero, (byte)ChatSourceType.Object, (byte)ChatAudibleLevel.Fully);
            }
        }
    }
}
