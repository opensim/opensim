using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Environment.Scenes;
using Axiom.Math;
using libsecondlife;
using OpenSim.Framework.Types;
using OpenSim.Framework.Interfaces;

namespace SimpleApp
{
    public class ComplexObject : SceneObjectGroup
    {
        private LLQuaternion m_rotationDirection;
        
        private class RotatingWheel : SceneObjectPart
        {
            private LLQuaternion m_rotationDirection;

            public RotatingWheel(ulong regionHandle, SceneObjectGroup parent, LLUUID ownerID, uint localID, LLVector3 groupPosition, LLVector3 offsetPosition, LLQuaternion rotationDirection)
                : base(regionHandle, parent, ownerID, localID, new CylinderShape( 0.5f, 0.2f ), groupPosition, offsetPosition )
            {
                m_rotationDirection = rotationDirection;                
            }

            public override void UpdateMovement()
            {
                UpdateRotation(RotationOffset * m_rotationDirection);
            }
        }

        public override void UpdateMovement()
        {
            UpdateGroupRotation(Rotation * m_rotationDirection);
            
            base.UpdateMovement();
        }

        
        
        public ComplexObject(Scene scene, ulong regionHandle, LLUUID ownerID, uint localID, LLVector3 pos )
            : base(scene, regionHandle, ownerID, localID, pos, BoxShape.Default )
        {
            m_rotationDirection = new LLQuaternion(0.05f, 0.1f, 0.15f);

            AddPart(new RotatingWheel(regionHandle, this, ownerID, scene.PrimIDAllocate(), pos, new LLVector3(0, 0, 0.75f), new LLQuaternion(0.05f,0,0)));
            AddPart(new RotatingWheel(regionHandle, this, ownerID, scene.PrimIDAllocate(), pos, new LLVector3(0, 0, -0.75f), new LLQuaternion(-0.05f,0,0)));

            AddPart(new RotatingWheel(regionHandle, this, ownerID, scene.PrimIDAllocate(), pos, new LLVector3(0, 0.75f,0), new LLQuaternion(0.5f, 0, 0.05f)));
            AddPart(new RotatingWheel(regionHandle, this, ownerID, scene.PrimIDAllocate(), pos, new LLVector3(0, -0.75f,0), new LLQuaternion(-0.5f, 0, -0.05f)));

            AddPart(new RotatingWheel(regionHandle, this, ownerID, scene.PrimIDAllocate(), pos, new LLVector3(0.75f, 0, 0), new LLQuaternion(0, 0.5f, 0.05f)));
            AddPart(new RotatingWheel(regionHandle, this, ownerID, scene.PrimIDAllocate(), pos, new LLVector3(-0.75f, 0, 0), new LLQuaternion(0, -0.5f, -0.05f)));
            
            UpdateParentIDs();
        }

        public override void OnGrabPart(SceneObjectPart part, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            m_parts.Remove(part.UUID);
            remoteClient.SendKillObject(m_regionHandle, part.LocalID);
            remoteClient.AddMoney(1);
            remoteClient.SendChatMessage("Poof!", 1, Pos, "Party Party", LLUUID.Zero);
        }

        public override void OnGrabGroup( LLVector3 offsetPos, IClientAPI remoteClient)
        {
            if( m_parts.Count == 1 )
            {
                m_parts.Remove(m_rootPart.UUID);
                m_scene.RemoveEntity(this);
                remoteClient.SendKillObject(m_regionHandle, m_rootPart.LocalID);
                remoteClient.AddMoney(50);
                remoteClient.SendChatMessage("KABLAM!!!", 1, Pos, "Groupie Groupie", LLUUID.Zero);
            }
        }
    }
}
