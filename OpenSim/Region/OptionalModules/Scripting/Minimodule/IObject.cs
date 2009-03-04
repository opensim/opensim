using System;
using System.Drawing;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    interface IObject
    {
        bool Exists { get; }
        uint LocalID { get; }
        UUID GlobalID { get; }

        IObject[] Children { get; }

        /// <summary>
        /// Equals 'this' if we have no parent. Ergo, Root.Children.Count will always return the total number of items in the linkset.
        /// </summary>
        IObject Root { get; }

        IObjectFace[] Faces { get; }

        Vector3 Scale { get; set; }
        Quaternion Rotation { get; set; }

        Vector3 SitTarget { get; set; }
        String SitTargetText { get; set; }

        String TouchText { get; set; }

        String Text { get; set; }

        bool IsPhysical { get; set; } // SetStatus(PHYSICS)
        bool IsPhantom { get; set; } // SetStatus(PHANTOM)
        bool IsRotationLockedX { get; set; } // SetStatus(!ROTATE_X)
        bool IsRotationLockedY { get; set; } // SetStatus(!ROTATE_Y)
        bool IsRotationLockedZ { get; set; } // SetStatus(!ROTATE_Z)
        bool IsSandboxed { get; set; } // SetStatus(SANDBOX)
        bool IsImmotile { get; set; } // SetStatus(BLOCK_GRAB)
        bool IsAlwaysReturned { get; set; } // SetStatus(!DIE_AT_EDGE)
        bool IsTemporary { get; set; } // TEMP_ON_REZ

        bool IsFlexible { get; set; }

        PrimType PrimShape { get; set; }
        // TODO:
        // PrimHole
        // Repeats, Offsets, Cut/Dimple/ProfileCut
        // Hollow, Twist, HoleSize,
        // Taper[A+B], Shear[A+B], Revolutions,
        // RadiusOffset, Skew

        Material Material { get; set; }
    }

    public enum Material
    {
        Default,
        Glass,
        Metal,
        Plastic,
        Wood,
        Rubber,
        Stone,
        Flesh
    }

    public enum PrimType
    {
        NotPrimitive,
        Box,
        Cylinder,
        Prism,
        Sphere,
        Torus,
        Tube,
        Ring,
        Sculpt
    }

    public enum TextureMapping
    {
        Default,
        Planar
    }

    interface IObjectFace
    {
        Color Color { get; set; }
        UUID Texture { get; set; }
        TextureMapping Mapping { get; set; } // SetPrimParms(PRIM_TEXGEN)
        bool Bright { get; set; } // SetPrimParms(FULLBRIGHT)
        double Bloom { get; set; } // SetPrimParms(GLOW)
        bool Shiny { get; set; } // SetPrimParms(SHINY)
        bool BumpMap { get; set; } // SetPrimParms(BUMPMAP) [DEPRECIATE IN FAVOUR OF UUID?]
    }
}
