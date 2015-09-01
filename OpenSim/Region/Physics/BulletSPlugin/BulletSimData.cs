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
// Classes to allow some type checking for the API
// These hold pointers to allocated objects in the unmanaged space.
// These classes are subclassed by the various physical implementations of
// objects. In particular, there is a version for physical instances in
// unmanaged memory ("unman") and one for in managed memory ("XNA").

// Currently, the instances of these classes are a reference to a
// physical representation and this has no releationship to other
// instances. Someday, refarb the usage of these classes so each instance
// refers to a particular physical instance and this class controls reference
// counts and such. This should be done along with adding BSShapes.

public class BulletWorld
{
    public BulletWorld(uint worldId, BSScene bss)
    {
        worldID = worldId;
        physicsScene = bss;
    }
    public uint worldID;
    // The scene is only in here so very low level routines have a handle to print debug/error messages
    public BSScene physicsScene;
}

// An allocated Bullet btRigidBody
public class BulletBody
{
    public BulletBody(uint id)
    {
        ID = id;
        collisionType = CollisionType.Static;
    }
    public uint ID;
    public CollisionType collisionType;

    public virtual void Clear() { }
    public virtual bool HasPhysicalBody { get { return false; } }

    // Apply the specificed collision mask into the physical world
    public virtual bool ApplyCollisionMask(BSScene physicsScene)
    {
        // Should assert the body has been added to the physical world.
        // (The collision masks are stored in the collision proxy cache which only exists for
        //    a collision body that is in the world.)
        return physicsScene.PE.SetCollisionGroupMask(this,
                                BulletSimData.CollisionTypeMasks[collisionType].group,
                                BulletSimData.CollisionTypeMasks[collisionType].mask);
    }

    // Used for log messages for a unique display of the memory/object allocated to this instance
    public virtual string AddrString
    {
        get { return "unknown"; }
    }

    public override string ToString()
    {
        StringBuilder buff = new StringBuilder();
        buff.Append("<id=");
        buff.Append(ID.ToString());
        buff.Append(",p=");
        buff.Append(AddrString);
        buff.Append(",c=");
        buff.Append(collisionType);
        buff.Append(">");
        return buff.ToString();
    }
}

public class BulletShape
{
    public BulletShape()
    {
        shapeType = BSPhysicsShapeType.SHAPE_UNKNOWN;
        shapeKey = (System.UInt64)FixedShapeKey.KEY_NONE;
        isNativeShape = false;
    }
    public BSPhysicsShapeType shapeType;
    public System.UInt64 shapeKey;
    public bool isNativeShape;

    public virtual void Clear() { }
    public virtual bool HasPhysicalShape { get { return false; } }

    // Make another reference to this physical object.
    public virtual BulletShape Clone() { return new BulletShape(); }

    // Return 'true' if this and other refer to the same physical object
    public virtual bool ReferenceSame(BulletShape xx) { return false; }

    // Used for log messages for a unique display of the memory/object allocated to this instance
    public virtual string AddrString
    {
        get { return "unknown"; }
    }

    public override string ToString()
    {
        StringBuilder buff = new StringBuilder();
        buff.Append("<p=");
        buff.Append(AddrString);
        buff.Append(",s=");
        buff.Append(shapeType.ToString());
        buff.Append(",k=");
        buff.Append(shapeKey.ToString("X"));
        buff.Append(",n=");
        buff.Append(isNativeShape.ToString());
        buff.Append(">");
        return buff.ToString();
    }
}

// An allocated Bullet btConstraint
public class BulletConstraint
{
    public BulletConstraint()
    {
    }
    public virtual void Clear() { }
    public virtual bool HasPhysicalConstraint { get { return false; } }

    // Used for log messages for a unique display of the memory/object allocated to this instance
    public virtual string AddrString
    {
        get { return "unknown"; }
    }
}

// An allocated HeightMapThing which holds various heightmap info.
// Made a class rather than a struct so there would be only one
//      instance of this and C# will pass around pointers rather
//      than making copies.
public class BulletHMapInfo
{
    public BulletHMapInfo(uint id, float[] hm, float pSizeX, float pSizeY) {
        ID = id;
        heightMap = hm;
        terrainRegionBase = OMV.Vector3.Zero;
        minCoords = new OMV.Vector3(100f, 100f, 25f);
        maxCoords = new OMV.Vector3(101f, 101f, 26f);
        minZ = maxZ = 0f;
        sizeX = pSizeX;
        sizeY = pSizeY;
    }
    public uint ID;
    public float[] heightMap;
    public OMV.Vector3 terrainRegionBase;
    public OMV.Vector3 minCoords;
    public OMV.Vector3 maxCoords;
    public float sizeX, sizeY;
    public float minZ, maxZ;
    public BulletShape terrainShape;
    public BulletBody terrainBody;
}

// The general class of collsion object.
public enum CollisionType
{
    Avatar,
    PhantomToOthersAvatar, // An avatar that it phantom to other avatars but not to anything else
    Groundplane,
    Terrain,
    Static,
    Dynamic,
    VolumeDetect,
    // Linkset, // A linkset should be either Static or Dynamic
    LinksetChild,
    Unknown
};

// Hold specification of group and mask collision flags for a CollisionType
public struct CollisionTypeFilterGroup
{
    public CollisionTypeFilterGroup(CollisionType t, uint g, uint m)
    {
        type = t;
        group = g;
        mask = m;
    }
    public CollisionType type;
    public uint group;
    public uint mask;
};

public static class BulletSimData
{

// Map of collisionTypes to flags for collision groups and masks.
// An object's 'group' is the collison groups this object belongs to
// An object's 'filter' is the groups another object has to belong to in order to collide with me
// A collision happens if ((obj1.group & obj2.filter) != 0) || ((obj2.group & obj1.filter) != 0)
//
// As mentioned above, don't use the CollisionFilterGroups definitions directly in the code
//     but, instead, use references to this dictionary. Finding and debugging
//     collision flag problems will be made easier.
public static Dictionary<CollisionType, CollisionTypeFilterGroup> CollisionTypeMasks
            = new Dictionary<CollisionType, CollisionTypeFilterGroup>()
{
    { CollisionType.Avatar,
                new CollisionTypeFilterGroup(CollisionType.Avatar,
                                (uint)CollisionFilterGroups.BCharacterGroup,
                                (uint)(CollisionFilterGroups.BAllGroup))
    },
    { CollisionType.PhantomToOthersAvatar,
        new CollisionTypeFilterGroup(CollisionType.PhantomToOthersAvatar,
                                     (uint)CollisionFilterGroups.BCharacterGroup,
                                     (uint)(CollisionFilterGroups.BAllGroup & ~CollisionFilterGroups.BCharacterGroup))
    },
    { CollisionType.Groundplane,
                new CollisionTypeFilterGroup(CollisionType.Groundplane,
                                (uint)CollisionFilterGroups.BGroundPlaneGroup,
                                // (uint)CollisionFilterGroups.BAllGroup)
                                (uint)(CollisionFilterGroups.BCharacterGroup | CollisionFilterGroups.BSolidGroup))
    },
    { CollisionType.Terrain,
                new CollisionTypeFilterGroup(CollisionType.Terrain,
                                (uint)CollisionFilterGroups.BTerrainGroup,
                                (uint)(CollisionFilterGroups.BAllGroup & ~CollisionFilterGroups.BStaticGroup))
    },
    { CollisionType.Static,
                new CollisionTypeFilterGroup(CollisionType.Static,
                                (uint)CollisionFilterGroups.BStaticGroup,
                                (uint)(CollisionFilterGroups.BCharacterGroup | CollisionFilterGroups.BSolidGroup))
    },
    { CollisionType.Dynamic,
                new CollisionTypeFilterGroup(CollisionType.Dynamic,
                                (uint)CollisionFilterGroups.BSolidGroup,
                                (uint)(CollisionFilterGroups.BAllGroup))
    },
    { CollisionType.VolumeDetect,
                new CollisionTypeFilterGroup(CollisionType.VolumeDetect,
                                (uint)CollisionFilterGroups.BSensorTrigger,
                                (uint)(~CollisionFilterGroups.BSensorTrigger))
    },
    { CollisionType.LinksetChild,
                new CollisionTypeFilterGroup(CollisionType.LinksetChild,
                                (uint)CollisionFilterGroups.BLinksetChildGroup,
                                (uint)(CollisionFilterGroups.BNoneGroup))
                                // (uint)(CollisionFilterGroups.BCharacterGroup | CollisionFilterGroups.BSolidGroup))
    },
};

}
}
