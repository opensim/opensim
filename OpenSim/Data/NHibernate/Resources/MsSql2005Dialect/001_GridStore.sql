create table Regions (
   Uuid NVARCHAR(36) not null,
   RegionHandle BIGINT null,
   RegionName NVARCHAR(32) null,
   RegionRecvKey NVARCHAR(128) null,
   RegionSendKey NVARCHAR(128) null,
   RegionSecret NVARCHAR(128) null,
   RegionDataURI NVARCHAR(255) null,
   ServerIP NVARCHAR(64) null,
   ServerPort INT null,
   ServerURI NVARCHAR(255) null,
   RegionLocX INT null,
   RegionLocY INT null,
   RegionLocZ INT null,
   EastOverrideHandle BIGINT null,
   WestOverrideHandle BIGINT null,
   SouthOverrideHandle BIGINT null,
   NorthOverrideHandle BIGINT null,
   RegionAssetURI NVARCHAR(255) null,
   RegionAssetRecvKey NVARCHAR(128) null,
   RegionAssetSendKey NVARCHAR(128) null,
   RegionUserURI NVARCHAR(255) null,
   RegionUserRecvKey NVARCHAR(128) null,
   RegionUserSendKey NVARCHAR(128) null,
   ServerHttpPort INT null,
   ServerRemotingPort INT null,
   RegionMapTextureID NVARCHAR(36) null,
   Owner_uuid NVARCHAR(36) null,
   OriginUUID NVARCHAR(36) null,
   primary key (Uuid)
)
create index region_handle on Regions (RegionHandle)
create index region_name on Regions (RegionName)
create index overrideHandles on Regions (EastOverrideHandle, WestOverrideHandle, SouthOverrideHandle, NorthOverrideHandle)

