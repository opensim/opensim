using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Environment.Scenes;
using Axiom.Math;
using libsecondlife;
using OpenSim.Framework.Types;

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
    }
}
