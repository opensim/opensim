ALTER TABLE Prims ADD LinkNum INT null;
ALTER TABLE Prims ADD Material TINYINT null;
ALTER TABLE Prims ADD ScriptAccessPin INT null;
ALTER TABLE Prims ADD TextureAnimation VARBINARY(max) null;
ALTER TABLE Prims ADD ParticleSystem VARBINARY(max) null;
ALTER TABLE Prims ADD ClickAction TINYINT null;
ALTER TABLE Prims ADD Color INT null;

CREATE TABLE RegionSettings
(
	RegionID NVARCHAR(255) NOT NULL,
	BlockTerraform  bit NOT NULL,
	BlockFly  bit NOT NULL,
	AllowDamage  bit NOT NULL,
	RestrictPushing  bit NOT NULL,
	AllowLandResell  bit NOT NULL,
	AllowLandJoinDivide  bit NOT NULL,
	BlockShowInSearch  bit NOT NULL,
	AgentLimit  int NOT NULL,
	ObjectBonus  float(53) NOT NULL,
	Maturity  int NOT NULL,
	DisableScripts  bit NOT NULL,
	DisableCollisions  bit NOT NULL,
	DisablePhysics  bit NOT NULL,
	TerrainTexture1  NVARCHAR(36) NOT NULL,
	TerrainTexture2  NVARCHAR(36) NOT NULL,
	TerrainTexture3  NVARCHAR(36) NOT NULL,
	TerrainTexture4  NVARCHAR(36) NOT NULL,
	Elevation1NW  float(53) NOT NULL,
	Elevation2NW  float(53) NOT NULL,
	Elevation1NE  float(53) NOT NULL,
	Elevation2NE  float(53) NOT NULL,
	Elevation1SE  float(53) NOT NULL,
	Elevation2SE  float(53) NOT NULL,
	Elevation1SW  float(53) NOT NULL,
	Elevation2SW  float(53) NOT NULL,
	WaterHeight  float(53) NOT NULL,
	TerrainRaiseLimit  float(53) NOT NULL,
	TerrainLowerLimit  float(53) NOT NULL,
	UseEstateSun  bit NOT NULL,
	FixedSun  bit NOT NULL,
	SunPosition  float(53) NOT NULL,
	Covenant  NVARCHAR(36) NULL DEFAULT (NULL),
	Sandbox bit NOT NULL,
	SunVectorX  float(53) NOT NULL DEFAULT ((0)),
	SunVectorY  float(53) NOT NULL DEFAULT ((0)),
	SunVectorZ  float(53) NOT NULL DEFAULT ((0)),
	
	primary key (RegionID)
)

