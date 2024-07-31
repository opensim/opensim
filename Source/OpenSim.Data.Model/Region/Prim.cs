using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Region;

public partial class Prim
{
    public int? CreationDate { get; set; }

    public string Name { get; set; }

    public string Text { get; set; }

    public string Description { get; set; }

    public string SitName { get; set; }

    public string TouchName { get; set; }

    public int? ObjectFlags { get; set; }

    public int? OwnerMask { get; set; }

    public int? NextOwnerMask { get; set; }

    public int? GroupMask { get; set; }

    public int? EveryoneMask { get; set; }

    public int? BaseMask { get; set; }

    public float? PositionX { get; set; }

    public float? PositionY { get; set; }

    public float? PositionZ { get; set; }

    public float? GroupPositionX { get; set; }

    public float? GroupPositionY { get; set; }

    public float? GroupPositionZ { get; set; }

    public float? VelocityX { get; set; }

    public float? VelocityY { get; set; }

    public float? VelocityZ { get; set; }

    public float? AngularVelocityX { get; set; }

    public float? AngularVelocityY { get; set; }

    public float? AngularVelocityZ { get; set; }

    public float? AccelerationX { get; set; }

    public float? AccelerationY { get; set; }

    public float? AccelerationZ { get; set; }

    public float? RotationX { get; set; }

    public float? RotationY { get; set; }

    public float? RotationZ { get; set; }

    public float? RotationW { get; set; }

    public float? SitTargetOffsetX { get; set; }

    public float? SitTargetOffsetY { get; set; }

    public float? SitTargetOffsetZ { get; set; }

    public float? SitTargetOrientW { get; set; }

    public float? SitTargetOrientX { get; set; }

    public float? SitTargetOrientY { get; set; }

    public float? SitTargetOrientZ { get; set; }

    public string Uuid { get; set; }

    public string RegionUuid { get; set; }

    public string CreatorId { get; set; }

    public string OwnerId { get; set; }

    public string GroupId { get; set; }

    public string LastOwnerId { get; set; }

    public string SceneGroupId { get; set; }

    public int PayPrice { get; set; }

    public int PayButton1 { get; set; }

    public int PayButton2 { get; set; }

    public int PayButton3 { get; set; }

    public int PayButton4 { get; set; }

    public string LoopedSound { get; set; }

    public float? LoopedSoundGain { get; set; }

    public byte[] TextureAnimation { get; set; }

    public float? OmegaX { get; set; }

    public float? OmegaY { get; set; }

    public float? OmegaZ { get; set; }

    public float? CameraEyeOffsetX { get; set; }

    public float? CameraEyeOffsetY { get; set; }

    public float? CameraEyeOffsetZ { get; set; }

    public float? CameraAtOffsetX { get; set; }

    public float? CameraAtOffsetY { get; set; }

    public float? CameraAtOffsetZ { get; set; }

    public sbyte ForceMouselook { get; set; }

    public int ScriptAccessPin { get; set; }

    public sbyte AllowedDrop { get; set; }

    public sbyte DieAtEdge { get; set; }

    public int SalePrice { get; set; }

    public sbyte SaleType { get; set; }

    public int ColorR { get; set; }

    public int ColorG { get; set; }

    public int ColorB { get; set; }

    public int ColorA { get; set; }

    public byte[] ParticleSystem { get; set; }

    public sbyte ClickAction { get; set; }

    public sbyte Material { get; set; }

    public string CollisionSound { get; set; }

    public double CollisionSoundVolume { get; set; }

    public int LinkNumber { get; set; }

    public sbyte PassTouches { get; set; }

    public string MediaUrl { get; set; }

    public string DynAttrs { get; set; }

    public sbyte PhysicsShapeType { get; set; }

    public float? Density { get; set; }

    public float? GravityModifier { get; set; }

    public float? Friction { get; set; }

    public float? Restitution { get; set; }

    public byte[] KeyframeMotion { get; set; }

    public float? AttachedPosX { get; set; }

    public float? AttachedPosY { get; set; }

    public float? AttachedPosZ { get; set; }

    public sbyte PassCollisions { get; set; }

    public string Vehicle { get; set; }

    public sbyte RotationAxisLocks { get; set; }

    public string RezzerId { get; set; }

    public string PhysInertia { get; set; }

    public byte[] Sopanims { get; set; }

    public float? Standtargetx { get; set; }

    public float? Standtargety { get; set; }

    public float? Standtargetz { get; set; }

    public float? Sitactrange { get; set; }

    public int? Pseudocrc { get; set; }

    public string Linksetdata { get; set; }
}
