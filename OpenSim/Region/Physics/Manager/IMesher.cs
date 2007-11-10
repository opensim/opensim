using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework;

namespace OpenSim.Region.Physics.Manager
{
    public interface IMesher
    {
        IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, PhysicsVector size);
    }

    public interface IVertex {
    }

    public interface IMesh
    {
        List<PhysicsVector> getVertexList();
        int[] getIndexListAsInt();
        int[] getIndexListAsIntLocked();
        float[] getVertexListAsFloatLocked();


    }
}
