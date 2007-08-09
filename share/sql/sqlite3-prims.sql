--
-- Database schema for local prim storage
--
-- 
-- Some type mappings
-- LLUID => char(36) (in ascii hex format)
-- uint => integer
-- string => varchar(256) until such time as we know we need bigger

create table prims (
        UUID char(36) primary key, -- this.UUID
        ParentID integer default 0, -- this.ParentID
        CreationDate integer, -- this.CreationDate
        Name varchar(256),
        -- various text fields
        Text varchar(256),
        Description varchar(256),
        SitName varchar(256),
        TouchName varchar(256),
        -- permissions
        CreatorID char(36),
        OwnerID char(36),
        GroupID char(36),
        LastOwnerID char(36),
        OwnerMask integer,
        NextOwnerMask integer,
        GroupMask integer,
        EveryoneMask integer,
        BaseMask integer,
        -- vectors (converted from LLVector3)
        PositionX float,
        PositionY float,
        PositionZ float,
        GroupPositionX float,
        GroupPositionY float,
        GroupPositionZ float,
        VelocityX float,
        VelocityY float,
        VelocityZ float,
        AngularVelocityX float,
        AngularVelocityY float,
        AngularVelocityZ float,
        AccelerationX float,
        AccelerationY float,
        AccelerationZ float,
        -- quaternions (converted from LLQuaternion)
        RotationX float,
        RotationY float,
        RotationZ float,
        RotationW float
);

create index prims_parent on prims(ParentID);
create index prims_ownerid on prims(OwnerID);
create index prims_lastownerid on prims(LastOwnerID);

create table primshapes (
        -- The same UUID as prim, just to keep them easily linked
        UUID varchar(36) primary key not null,
        -- Shape is an enum 
        Shape integer, 
        -- vectors (converted from LLVector3)
        ScaleX float,
        ScaleY float,
        ScaleZ float,
        -- paths
        PCode integer,
        PathBegin integer,
        PathEnd integer,
        PathScaleX integer,
        PathScaleY integer,
        PathShearX integer,
        PathShearY integer,
        PathSkew integer,
        PathCurve integer,
        PathRadiusOffset integer,
        PathRevolutions integer,
        PathTaperX integer,
        PathTaperY integer,
        PathTwist integer,
        PathTwistBegin integer,
        -- profile
        ProfileBegin integer,
        ProfileEnd integer,
        ProfileCurve integer,
        ProfileHollow integer,
        -- text
        Texture blob
);

