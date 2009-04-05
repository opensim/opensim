using System;

public enum StatusIndicators : int
{
    Generic = 0,
    Start = 1,
    End = 2
}

public struct sCollisionData
{
    public uint ColliderLocalId;
    public uint CollidedWithLocalId;
    public int NumberOfCollisions;
    public int CollisionType;
    public int StatusIndicator;
    public int lastframe;
}

[Flags]
public enum CollisionCategories : int
{
    Disabled = 0,
    Geom = 0x00000001,
    Body = 0x00000002,
    Space = 0x00000004,
    Character = 0x00000008,
    Land = 0x00000010,
    Water = 0x00000020,
    Wind = 0x00000040,
    Sensor = 0x00000080,
    Selected = 0x00000100
}