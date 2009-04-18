using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule.Object
{
    public enum SculptType
    {
        Default = 1,
        Sphere = 1,
        Torus = 2,
        Plane = 3,
        Cylinder = 4
    }

    public enum HoleShape
    {
        Default = 0x00,
        Circle = 0x10,
        Square = 0x20,
        Triangle = 0x30
    }

    public enum PrimType
    {
        NotPrimitive = 255,
        Box = 0,
        Cylinder = 1,
        Prism = 2,
        Sphere = 3,
        Torus = 4,
        Tube = 5,
        Ring = 6,
        Sculpt = 7
    }

    public interface IObjectShape
    {
        UUID SculptMap { get; set; }
        SculptType SculptType { get; set; }

        HoleShape HoleType { get; set; }
        Double HoleSize { get; set; }
        PrimType PrimType { get; set; }

    }
}