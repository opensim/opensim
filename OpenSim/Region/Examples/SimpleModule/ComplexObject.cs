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

using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Examples.SimpleModule
{
    public class ComplexObject : SceneObjectGroup
    {
        private readonly Quaternion m_rotationDirection;

        protected override bool InSceneBackup
        {
            get
            {
                return false;
            }
        }

        private class RotatingWheel : SceneObjectPart
        {
            private readonly Quaternion m_rotationDirection;

            public RotatingWheel()
            {
            }

            public RotatingWheel(
                UUID ownerID, Vector3 groupPosition, Vector3 offsetPosition, Quaternion rotationDirection)
                : base(ownerID, PrimitiveBaseShape.Default, groupPosition, Quaternion.Identity, offsetPosition)
            {
                m_rotationDirection = rotationDirection;

                Flags |= PrimFlags.Touch;
            }

            public override void UpdateMovement()
            {
                UpdateRotation(RotationOffset * m_rotationDirection);
            }
        }

        public override void UpdateMovement()
        {
            UpdateGroupRotation(GroupRotation * m_rotationDirection);

            base.UpdateMovement();
        }

        public ComplexObject(Scene scene, ulong regionHandle, UUID ownerID, uint localID, Vector3 pos)
            : base(ownerID, pos, PrimitiveBaseShape.Default)
        {
            m_rotationDirection = new Quaternion(0.05f, 0.1f, 0.15f);

            AddPart(
                new RotatingWheel(ownerID, pos, new Vector3(0, 0, 0.75f),
                                  new Quaternion(0.05f, 0, 0)));
            AddPart(
                new RotatingWheel(ownerID, pos, new Vector3(0, 0, -0.75f),
                                  new Quaternion(-0.05f, 0, 0)));

            AddPart(
                new RotatingWheel(ownerID, pos, new Vector3(0, 0.75f, 0),
                                  new Quaternion(0.5f, 0, 0.05f)));
            AddPart(
                new RotatingWheel(ownerID, pos, new Vector3(0, -0.75f, 0),
                                  new Quaternion(-0.5f, 0, -0.05f)));

            AddPart(
                new RotatingWheel(ownerID, pos, new Vector3(0.75f, 0, 0),
                                  new Quaternion(0, 0.5f, 0.05f)));
            AddPart(
                new RotatingWheel(ownerID, pos, new Vector3(-0.75f, 0, 0),
                                  new Quaternion(0, -0.5f, -0.05f)));

            RootPart.Flags |= PrimFlags.Touch;
        }

        public override void OnGrabPart(SceneObjectPart part, Vector3 offsetPos, IClientAPI remoteClient)
        {
            m_parts.Remove(part.UUID);

            remoteClient.SendKillObject(m_regionHandle, part.LocalId);
            remoteClient.AddMoney(1);
            remoteClient.SendChatMessage("Poof!", 1, AbsolutePosition, "Party Party", UUID.Zero, (byte)ChatSourceType.Object, (byte)ChatAudibleLevel.Fully);
        }

        public override void OnGrabGroup(Vector3 offsetPos, IClientAPI remoteClient)
        {
            if (m_parts.Count == 1)
            {
                m_parts.Remove(m_rootPart.UUID);
                m_scene.DeleteSceneObject(this, false);
                remoteClient.SendKillObject(m_regionHandle, m_rootPart.LocalId);
                remoteClient.AddMoney(50);
                remoteClient.SendChatMessage("KABLAM!!!", 1, AbsolutePosition, "Groupie Groupie", UUID.Zero, (byte)ChatSourceType.Object, (byte)ChatAudibleLevel.Fully);
            }
        }
    }
}
