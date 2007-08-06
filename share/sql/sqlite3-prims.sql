--
-- Database schema for local prim storage
--
-- 
-- Some type mappings
-- LLUID => char(36) (in ascii hex format)
-- uint => integer

create table prims (
        id integer primary key autoincrement, -- this.LocalID
        ParentID integer default 0, -- this.ParentID
        UUID char(36), -- this.UUID
        CreationDate integer, -- this.CreationDate
        Name varchar(256),
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
        id integer primary key autoincrement,
        prim_id integer not null,
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

create index primshapes_parentid on primshapes(prim_id);
