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
using System.Linq;
using System.Text;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public abstract class BSShape
{
    public IntPtr ptr { get; set; }
    public PhysicsShapeType type { get; set; }
    public System.UInt64 key { get; set; }
    public int referenceCount { get; set; }
    public DateTime lastReferenced { get; set; }

    public BSShape()
    {
        ptr = IntPtr.Zero;
        type = PhysicsShapeType.SHAPE_UNKNOWN;
        key = 0;
        referenceCount = 0;
        lastReferenced = DateTime.Now;
    }

    // Get a reference to a physical shape. Create if it doesn't exist
    public static BSShape GetShapeReference(BSScene physicsScene, bool forceRebuild, BSPhysObject prim)
    {
        BSShape ret = null;

        if (prim.PreferredPhysicalShape == PhysicsShapeType.SHAPE_CAPSULE)
        {
            // an avatar capsule is close to a native shape (it is not shared)
            ret = BSShapeNative.GetReference(physicsScene, prim, PhysicsShapeType.SHAPE_CAPSULE,
                                        FixedShapeKey.KEY_CAPSULE);
            physicsScene.DetailLog("{0},BSShape.GetShapeReference,avatarCapsule,shape={1}", prim.LocalID, ret);
        }

        // Compound shapes are handled special as they are rebuilt from scratch.
        // This isn't too great a hardship since most of the child shapes will already been created.
        if (ret == null  && prim.PreferredPhysicalShape == PhysicsShapeType.SHAPE_COMPOUND)
        {
            // Getting a reference to a compound shape gets you the compound shape with the root prim shape added
            ret = BSShapeCompound.GetReference(prim);
            physicsScene.DetailLog("{0},BSShapeCollection.CreateGeom,compoundShape,shape={1}", prim.LocalID, ret);
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

    public override string ToString()
    {
        StringBuilder buff = new StringBuilder();
        buff.Append("<p=");
        buff.Append(ptr.ToString("X"));
        buff.Append(",s=");
        buff.Append(type.ToString());
        buff.Append(",k=");
        buff.Append(key.ToString("X"));
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
                    PhysicsShapeType shapeType, FixedShapeKey shapeKey) 
    {
        // Native shapes are not shared and are always built anew.
        return new BSShapeNative(physicsScene, prim, shapeType, shapeKey);
    }

    private BSShapeNative(BSScene physicsScene, BSPhysObject prim,
                    PhysicsShapeType shapeType, FixedShapeKey shapeKey)
    {
        ShapeData nativeShapeData = new ShapeData();
        nativeShapeData.Type = shapeType;
        nativeShapeData.ID = prim.LocalID;
        nativeShapeData.Scale = prim.Scale;
        nativeShapeData.Size = prim.Scale;
        nativeShapeData.MeshKey = (ulong)shapeKey;
        nativeShapeData.HullKey = (ulong)shapeKey;

       
        if (shapeType == PhysicsShapeType.SHAPE_CAPSULE)
        {
            ptr = BulletSimAPI.BuildCapsuleShape2(physicsScene.World.ptr, 1f, 1f, prim.Scale);
            physicsScene.DetailLog("{0},BSShapeCollection.BuiletPhysicalNativeShape,capsule,scale={1}", prim.LocalID, prim.Scale);
        }
        else
        {
            ptr = BulletSimAPI.BuildNativeShape2(physicsScene.World.ptr, nativeShapeData);
        }
        if (ptr == IntPtr.Zero)
        {
            physicsScene.Logger.ErrorFormat("{0} BuildPhysicalNativeShape failed. ID={1}, shape={2}",
                                    LogHeader, prim.LocalID, shapeType);
        }
        type = shapeType;
        key = (UInt64)shapeKey;
    }
    // Make this reference to the physical shape go away since native shapes are not shared.
    public override void Dereference(BSScene physicsScene)
    {
        // Native shapes are not tracked and are released immediately
        physicsScene.DetailLog("{0},BSShapeCollection.DereferenceShape,deleteNativeShape,shape={1}", BSScene.DetailLogZero, this);
        BulletSimAPI.DeleteCollisionShape2(physicsScene.World.ptr, ptr);
        ptr = IntPtr.Zero;
        // Garbage collection will free up this instance.
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
}
