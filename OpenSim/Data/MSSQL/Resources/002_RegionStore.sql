BEGIN TRANSACTION

CREATE TABLE regionban (
	[regionUUID] VARCHAR(36) NOT NULL, 
	[bannedUUID] VARCHAR(36) NOT NULL, 
	[bannedIp] VARCHAR(16) NOT NULL, 
	[bannedIpHostMask] VARCHAR(16) NOT NULL)

create table [dbo].[regionsettings] (
	[regionUUID] [varchar](36) not null,
	[block_terraform] [bit] not null,
	[block_fly] [bit] not null,
	[allow_damage] [bit] not null,
	[restrict_pushing] [bit] not null,
	[allow_land_resell] [bit] not null,
	[allow_land_join_divide] [bit] not null,
	[block_show_in_search] [bit] not null,
	[agent_limit] [int] not null,
	[object_bonus] [float] not null,
	[maturity] [int] not null,
	[disable_scripts] [bit] not null,
	[disable_collisions] [bit] not null,
	[disable_physics] [bit] not null,
	[terrain_texture_1] [varchar](36) not null,
	[terrain_texture_2] [varchar](36) not null,
	[terrain_texture_3] [varchar](36) not null,
	[terrain_texture_4] [varchar](36) not null,
	[elevation_1_nw] [float] not null,
	[elevation_2_nw] [float] not null,
	[elevation_1_ne] [float] not null,
	[elevation_2_ne] [float] not null,
	[elevation_1_se] [float] not null,
	[elevation_2_se] [float] not null,
	[elevation_1_sw] [float] not null,
	[elevation_2_sw] [float] not null,
	[water_height] [float] not null,
	[terrain_raise_limit] [float] not null,
	[terrain_lower_limit] [float] not null,
	[use_estate_sun] [bit] not null,
	[fixed_sun] [bit] not null,
	[sun_position] [float] not null,
	[covenant] [varchar](36) default NULL,
	[Sandbox] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[regionUUID] ASC
)WITH (PAD_INDEX  = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

COMMIT