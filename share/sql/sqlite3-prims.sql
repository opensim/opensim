--
-- Database schema for local prim storage
--

create table prims (
        LocalID integer primary key not null,
        ParentID integer default 0,
        FullID char(36),
        CreationDate integer, 
        -- permissions
        OwnerID char(36),
        OwnerMask integer,
        NextOwnerMask integer,
        GroupMask integer,
        EveryoneMask integer,
        BaseMask integer,
        -- vectors (converted from LLVector3)
        ScaleX integer,
        ScaleY integer,
        ScaleZ integer,
        PositionX integer,
        PositionY integer,
        PositionZ integer,
        -- quaternions (converted from LLQuaternion)
        RotationX integer,
        RotationY integer,
        RotationZ integer,
        RotationW integer,
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

create index prims_parent on prims(ParentID);
create index prims_ownerid on prims(OwnerID);
create index prims_fullid on prims(FullID);