/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
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
using System.Collections.Generic;
using System.Text;

using OMV = OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public abstract class BSShape
{
    public int referenceCount { get; set; }
    public DateTime lastReferenced { get; set; }

    public BSShape()
    {
        referenceCount = 0;
        lastReferenced = DateTime.Now;
    }

    // Get a reference to a physical shape. Create if it doesn't exist
    public static BSShape GetShapeReference(BSScene physicsScene, bool forceRebuild, BSPhysObject prim)
    {
        BSShape ret = null;

        if (prim.PreferredPhysicalShape == BSPhysicsShapeType.SHAPE_CAPSULE)
        {
            // an avatar capsule is close to a native shape (it is not shared)
            ret = BSShapeNative.GetReference(physicsScene, prim, BSPhysicsShapeType.SHAPE_CAPSULE,
                                        FixedShapeKey.KEY_CAPSULE);
            physicsScene.DetailLog("{0},BSShape.GetShapeReference,avatarCapsule,shape={1}", prim.LocalID, ret);
        }

        // Compound shapes are handled special as they are rebuilt from scratch.
        // This isn't too great a hardship since most of the child shapes will have already been created.
        if (ret == null  && prim.PreferredPhysicalShape == BSPhysicsShapeType.SHAPE_COMPOUND)
        {
            // Getting a reference to a compound shape gets you the compound shape with the root prim shape added
            ret = BSShapeCompound.GetReference(prim);
            physicsScene.DetailLog("{0},BSShapeCollection.CreateGeom,compoundShape,shape={1}", prim.LocalID, ret);
        }

        // Avatars have their own unique shape
        if (ret == null  && prim.PreferredPhysicalShape == BSPhysicsShapeType.SHAPE_AVATAR)
        {
            // Getting a reference to a compound shape gets you the compound shape with the root prim shape added
            ret = BSShapeAvatar.GetReference(prim);
            physicsScene.DetailLog("{0},BSShapeCollection.CreateGeom,avatarShape,shape={1}", prim.LocalID, ret);
        }

        if (ret == null)
            ret = GetShapeReferenceNonSpecial(physicsScene, forceRebuild, prim);

        return ret;
    }
    public static BSShape GetShapeReferenceNonSpecial(BSScene physicsScene, bool forceRebuild, BSPhysObject prim)
    {
        return null;
    }
    public static BSShape GetShapeReferenceNonNative(BSScene physicsScene, bool forceRebuild, BSPhysObject prim)
    {
        return null;
    }

    // Release the use of a physical shape.
    public abstract void Dereference(BSScene physicsScene);

    // All shapes have a static call to get a reference to the physical shape
    // protected abstract static BSShape GetReference();

    // Returns a string for debugging that uniquily identifies the memory used by this instance
    public virtual string AddrString
    {
        get { return "unknown"; }
    }

    public override string ToString()
    {
        StringBuilder buff = new StringBuilder();
        buff.Append("<p=");
        buff.Append(AddrString);
        buff.Append(",c=");
        buff.Append(referenceCount.ToString());
        buff.Append(">");
        return buff.ToString();
    }
}

public class BSShapeNull : BSShape
{
    public BSShapeNull() : base()
    {
    }
    public static BSShape GetReference() { return new BSShapeNull();  }
    public override void Dereference(BSScene physicsScene) { /* The magic of garbage collection will make this go away */ }
}

public class BSShapeNative : BSShape
{
    private static string LogHeader = "[BULLETSIM SHAPE NATIVE]";
    public BSShapeNative() : base()
    {
    }
    public static BSShape GetReference(BSScene physicsScene, BSPhysObject prim, 
                    BSPhysicsShapeType shapeType, FixedShapeKey shapeKey) 
    {
        // Native shapes are not shared and are always built anew.
        //return new BSShapeNative(physicsScene, prim, shapeType, shapeKey);
        return null;
    }

    private BSShapeNative(BSScene physicsScene, BSPhysObject prim,
                    BSPhysicsShapeType shapeType, FixedShapeKey shapeKey)
    {
        ShapeData nativeShapeData = new ShapeData();
        nativeShapeData.Type = shapeType;
        nativeShapeData.ID = prim.LocalID;
        nativeShapeData.Scale = prim.Scale;
        nativeShapeData.Size = prim.Scale;
        nativeShapeData.MeshKey = (ulong)shapeKey;
        nativeShapeData.HullKey = (ulong)shapeKey;

       
        /*
        if (shapeType == BSPhysicsShapeType.SHAPE_CAPSULE)
        {
            ptr = PhysicsScene.PE.BuildCapsuleShape(physicsScene.World, 1f, 1f, prim.Scale);
            physicsScene.DetailLog("{0},BSShapeCollection.BuiletPhysicalNativeShape,capsule,scale={1}", prim.LocalID, prim.Scale);
        }
        else
        {
            ptr = PhysicsScene.PE.BuildNativeShape(physicsScene.World, nativeShapeData);
        }
        if (ptr == IntPtr.Zero)
        {
            physicsScene.Logger.ErrorFormat("{0} BuildPhysicalNativeShape failed. ID={1}, shape={2}",
                                    LogHeader, prim.LocalID, shapeType);
        }
        type = shapeType;
        key = (UInt64)shapeKey;
         */
    }
    // Make this reference to the physical shape go away since native shapes are not shared.
    public override void Dereference(BSScene physicsScene)
    {
        /*
        // Native shapes are not tracked and are released immediately
        physicsScene.DetailLog("{0},BSShapeCollection.DereferenceShape,deleteNativeShape,shape={1}", BSScene.DetailLogZero, this);
        PhysicsScene.PE.DeleteCollisionShape(physicsScene.World, this);
        ptr = IntPtr.Zero;
        // Garbage collection will free up this instance.
         */
    }
}

public class BSShapeMesh : BSShape
{
    private static string LogHeader = "[BULLETSIM SHAPE MESH]";
    private static Dictionary<System.UInt64, BSShapeMesh> Meshes = new Dictionary<System.UInt64, BSShapeMesh>();

    public BSShapeMesh() : base()
    {
    }
    public static BSShape GetReference() { return new BSShapeNull();  }
    public override void Dereference(BSScene physicsScene) { }
}

public class BSShapeHull : BSShape
{
    private static string LogHeader = "[BULLETSIM SHAPE HULL]";
    private static Dictionary<System.UInt64, BSShapeHull> Hulls = new Dictionary<System.UInt64, BSShapeHull>();

    public BSShapeHull() : base()
    {
    }
    public static BSShape GetReference() { return new BSShapeNull();  }
    public override void Dereference(BSScene physicsScene) { }
}

public class BSShapeCompound : BSShape
{
    private static string LogHeader = "[BULLETSIM SHAPE COMPOUND]";
    public BSShapeCompound() : base()
    {
    }
    public static BSShape GetReference(BSPhysObject prim) 
    { 
        return new BSShapeNull();
    }
    public override void Dereference(BSScene physicsScene) { }
}

public class BSShapeAvatar : BSShape
{
    private static string LogHeader = "[BULLETSIM SHAPE AVATAR]";
    public BSShapeAvatar() : base()
    {
    }
    public static BSShape GetReference(BSPhysObject prim) 
    { 
        return new BSShapeNull();
    }
    public override void Dereference(BSScene physicsScene) { }

    // From the front:
    //     A---A
    //    /     \
    //   B-------B
    //  /         \        +Z
    // C-----------C        |
    // \           /   -Y --+-- +Y
    //  \         /         |
    //   \       /         -Z
    //    D-----D
    //     \   /
    //      E-E

    // From the top A and E are just lines.
    //              B, C and D are hexagons:
    //
    //     C1--C2            +X
    //    /      \            |
    //  C0        C3     -Y --+-- +Y
    //    \      /            |
    //     C5--C4            -X

    // Zero goes directly through the middle so the offsets are from that middle axis
    //     and up and down from a middle horizon (A and E are the same distance from the zero).
    // The height, width and depth is one. All scaling is done by the simulator.

    // Z component -- how far the level is from the middle zero
    private const float Aup = 0.5f;
    private const float Bup = 0.4f;
    private const float Cup = 0.3f;
    private const float Dup = -0.4f;
    private const float Eup = -0.5f;

    // Y component -- distance from center to x0 and x3
    private const float Awid = 0.25f;
    private const float Bwid = 0.3f;
    private const float Cwid = 0.5f;
    private const float Dwid = 0.3f;
    private const float Ewid = 0.2f;

    // Y component -- distance from center to x1, x2, x4 and x5
    private const float Afwid = 0.0f;
    private const float Bfwid = 0.2f;
    private const float Cfwid = 0.4f;
    private const float Dfwid = 0.2f;
    private const float Efwid = 0.0f;

    // X component -- distance from zero to the front or back of a level
    private const float Adep = 0f;
    private const float Bdep = 0.3f;
    private const float Cdep = 0.5f;
    private const float Ddep = 0.2f;
    private const float Edep = 0f;

    private OMV.Vector3[] avatarVertices = {
           new OMV.Vector3( 0.0f, -Awid,  Aup),   // A0
           new OMV.Vector3( 0.0f, +Awid,  Aup),   // A3

           new OMV.Vector3( 0.0f, -Bwid,  Bup),   // B0
           new OMV.Vector3(+Bdep, -Bfwid, Bup),   // B1
           new OMV.Vector3(+Bdep, +Bfwid, Bup),   // B2
           new OMV.Vector3( 0.0f, +Bwid,  Bup),   // B3
           new OMV.Vector3(-Bdep, +Bfwid, Bup),   // B4
           new OMV.Vector3(-Bdep, -Bfwid, Bup),   // B5

           new OMV.Vector3( 0.0f, -Cwid,  Cup),   // C0
           new OMV.Vector3(+Cdep, -Cfwid, Cup),   // C1
           new OMV.Vector3(+Cdep, +Cfwid, Cup),   // C2
           new OMV.Vector3( 0.0f, +Cwid,  Cup),   // C3
           new OMV.Vector3(-Cdep, +Cfwid, Cup),   // C4
           new OMV.Vector3(-Cdep, -Cfwid, Cup),   // C5

           new OMV.Vector3( 0.0f, -Dwid,  Dup),   // D0
           new OMV.Vector3(+Ddep, -Dfwid, Dup),   // D1
           new OMV.Vector3(+Ddep, +Dfwid, Dup),   // D2
           new OMV.Vector3( 0.0f, +Dwid,  Dup),   // D3
           new OMV.Vector3(-Ddep, +Dfwid, Dup),   // D4
           new OMV.Vector3(-Ddep, -Dfwid, Dup),   // D5

           new OMV.Vector3( 0.0f, -Ewid,  Eup),   // E0
           new OMV.Vector3( 0.0f, +Ewid,  Eup),   // E3
    };

    // Offsets of the vertices in the vertices array
    private enum Ind : int
    {
        A0, A3,
        B0, B1, B2, B3, B4, B5,
        C0, C1, C2, C3, C4, C5,
        D0, D1, D2, D3, D4, D5,
        E0, E3
    }

    // Comments specify trianges and quads in clockwise direction
    private Ind[] avatarIndices = {
        Ind.A0, Ind.B0, Ind.B1,                         // A0,B0,B1
        Ind.A0, Ind.B1, Ind.B2, Ind.B2, Ind.A3, Ind.A0, // A0,B1,B2,A3
        Ind.A3, Ind.B2, Ind.B3,                         // A3,B2,B3
        Ind.A3, Ind.B3, Ind.B4,                         // A3,B3,B4
        Ind.A3, Ind.B4, Ind.B5, Ind.B5, Ind.A0, Ind.A3, // A3,B4,B5,A0
        Ind.A0, Ind.B5, Ind.B0,                         // A0,B5,B0

        Ind.B0, Ind.C0, Ind.C1, Ind.C1, Ind.B1, Ind.B0, // B0,C0,C1,B1
        Ind.B1, Ind.C1, Ind.C2, Ind.C2, Ind.B2, Ind.B1, // B1,C1,C2,B2
        Ind.B2, Ind.C2, Ind.C3, Ind.C3, Ind.B3, Ind.B2, // B2,C2,C3,B3
        Ind.B3, Ind.C3, Ind.C4, Ind.C4, Ind.B4, Ind.B3, // B3,C3,C4,B4
        Ind.B4, Ind.C4, Ind.C5, Ind.C5, Ind.B5, Ind.B4, // B4,C4,C5,B5
        Ind.B5, Ind.C5, Ind.C0, Ind.C0, Ind.B0, Ind.B5, // B5,C5,C0,B0

        Ind.C0, Ind.D0, Ind.D1, Ind.D1, Ind.C1, Ind.C0, // C0,D0,D1,C1
        Ind.C1, Ind.D1, Ind.D2, Ind.D2, Ind.C2, Ind.C1, // C1,D1,D2,C2
        Ind.C2, Ind.D2, Ind.D3, Ind.D3, Ind.C3, Ind.C2, // C2,D2,D3,C3
        Ind.C3, Ind.D3, Ind.D4, Ind.D4, Ind.C4, Ind.C3, // C3,D3,D4,C4
        Ind.C4, Ind.D4, Ind.D5, Ind.D5, Ind.C5, Ind.C4, // C4,D4,D5,C5
        Ind.C5, Ind.D5, Ind.D0, Ind.D0, Ind.C0, Ind.C5, // C5,D5,D0,C0

        Ind.E0, Ind.D0, Ind.D1,                         // E0,D0,D1
        Ind.E0, Ind.D1, Ind.D2, Ind.D2, Ind.E3, Ind.E0, // E0,D1,D2,E3
        Ind.E3, Ind.D2, Ind.D3,                         // E3,D2,D3
        Ind.E3, Ind.D3, Ind.D4,                         // E3,D3,D4
        Ind.E3, Ind.D4, Ind.D5, Ind.D5, Ind.E0, Ind.E3, // E3,D4,D5,E0
        Ind.E0, Ind.D5, Ind.D0,                         // E0,D5,D0

    };

}
}
