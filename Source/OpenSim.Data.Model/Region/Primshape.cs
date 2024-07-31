using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Region;

public partial class Primshape
{
    public int? Shape { get; set; }

    public double ScaleX { get; set; }

    public double ScaleY { get; set; }

    public double ScaleZ { get; set; }

    public int? Pcode { get; set; }

    public int? PathBegin { get; set; }

    public int? PathEnd { get; set; }

    public int? PathScaleX { get; set; }

    public int? PathScaleY { get; set; }

    public int? PathShearX { get; set; }

    public int? PathShearY { get; set; }

    public int? PathSkew { get; set; }

    public int? PathCurve { get; set; }

    public int? PathRadiusOffset { get; set; }

    public int? PathRevolutions { get; set; }

    public int? PathTaperX { get; set; }

    public int? PathTaperY { get; set; }

    public int? PathTwist { get; set; }

    public int? PathTwistBegin { get; set; }

    public int? ProfileBegin { get; set; }

    public int? ProfileEnd { get; set; }

    public int? ProfileCurve { get; set; }

    public int? ProfileHollow { get; set; }

    public int? State { get; set; }

    public byte[] Texture { get; set; }

    public byte[] ExtraParams { get; set; }

    public string Uuid { get; set; }

    public string Media { get; set; }

    public int LastAttachPoint { get; set; }

    public byte[] MatOvrd { get; set; }
}
