BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_regionsettings
	(
	regionUUID uniqueidentifier NOT NULL,
	block_terraform bit NOT NULL,
	block_fly bit NOT NULL,
	allow_damage bit NOT NULL,
	restrict_pushing bit NOT NULL,
	allow_land_resell bit NOT NULL,
	allow_land_join_divide bit NOT NULL,
	block_show_in_search bit NOT NULL,
	agent_limit int NOT NULL,
	object_bonus float(53) NOT NULL,
	maturity int NOT NULL,
	disable_scripts bit NOT NULL,
	disable_collisions bit NOT NULL,
	disable_physics bit NOT NULL,
	terrain_texture_1 uniqueidentifier NOT NULL,
	terrain_texture_2 uniqueidentifier NOT NULL,
	terrain_texture_3 uniqueidentifier NOT NULL,
	terrain_texture_4 uniqueidentifier NOT NULL,
	elevation_1_nw float(53) NOT NULL,
	elevation_2_nw float(53) NOT NULL,
	elevation_1_ne float(53) NOT NULL,
	elevation_2_ne float(53) NOT NULL,
	elevation_1_se float(53) NOT NULL,
	elevation_2_se float(53) NOT NULL,
	elevation_1_sw float(53) NOT NULL,
	elevation_2_sw float(53) NOT NULL,
	water_height float(53) NOT NULL,
	terrain_raise_limit float(53) NOT NULL,
	terrain_lower_limit float(53) NOT NULL,
	use_estate_sun bit NOT NULL,
	fixed_sun bit NOT NULL,
	sun_position float(53) NOT NULL,
	covenant uniqueidentifier NULL DEFAULT (NULL),
	Sandbox bit NOT NULL,
	sunvectorx float(53) NOT NULL DEFAULT ((0)),
	sunvectory float(53) NOT NULL DEFAULT ((0)),
	sunvectorz float(53) NOT NULL DEFAULT ((0))
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.regionsettings)
	 EXEC('INSERT INTO dbo.Tmp_regionsettings (regionUUID, block_terraform, block_fly, allow_damage, restrict_pushing, allow_land_resell, allow_land_join_divide, block_show_in_search, agent_limit, object_bonus, maturity, disable_scripts, disable_collisions, disable_physics, terrain_texture_1, terrain_texture_2, terrain_texture_3, terrain_texture_4, elevation_1_nw, elevation_2_nw, elevation_1_ne, elevation_2_ne, elevation_1_se, elevation_2_se, elevation_1_sw, elevation_2_sw, water_height, terrain_raise_limit, terrain_lower_limit, use_estate_sun, fixed_sun, sun_position, covenant, Sandbox, sunvectorx, sunvectory, sunvectorz)
		SELECT CONVERT(uniqueidentifier, regionUUID), block_terraform, block_fly, allow_damage, restrict_pushing, allow_land_resell, allow_land_join_divide, block_show_in_search, agent_limit, object_bonus, maturity, disable_scripts, disable_collisions, disable_physics, CONVERT(uniqueidentifier, terrain_texture_1), CONVERT(uniqueidentifier, terrain_texture_2), CONVERT(uniqueidentifier, terrain_texture_3), CONVERT(uniqueidentifier, terrain_texture_4), elevation_1_nw, elevation_2_nw, elevation_1_ne, elevation_2_ne, elevation_1_se, elevation_2_se, elevation_1_sw, elevation_2_sw, water_height, terrain_raise_limit, terrain_lower_limit, use_estate_sun, fixed_sun, sun_position, CONVERT(uniqueidentifier, covenant), Sandbox, sunvectorx, sunvectory, sunvectorz FROM dbo.regionsettings WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.regionsettings

EXECUTE sp_rename N'dbo.Tmp_regionsettings', N'regionsettings', 'OBJECT' 

ALTER TABLE dbo.regionsettings ADD CONSTRAINT
	PK__regionse__5B35159D21B6055D PRIMARY KEY CLUSTERED 
	(
	regionUUID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
