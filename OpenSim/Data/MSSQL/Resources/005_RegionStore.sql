BEGIN TRANSACTION

CREATE TABLE Tmp_primshapes
	(
	UUID varchar(36) NOT NULL,
	Shape int NULL,
	ScaleX float(53) NULL,
	ScaleY float(53) NULL,
	ScaleZ float(53) NULL,
	PCode int NULL,
	PathBegin int NULL,
	PathEnd int NULL,
	PathScaleX int NULL,
	PathScaleY int NULL,
	PathShearX int NULL,
	PathShearY int NULL,
	PathSkew int NULL,
	PathCurve int NULL,
	PathRadiusOffset int NULL,
	PathRevolutions int NULL,
	PathTaperX int NULL,
	PathTaperY int NULL,
	PathTwist int NULL,
	PathTwistBegin int NULL,
	ProfileBegin int NULL,
	ProfileEnd int NULL,
	ProfileCurve int NULL,
	ProfileHollow int NULL,
	State int NULL,
	Texture image NULL,
	ExtraParams image NULL
	)  ON [PRIMARY]
	 TEXTIMAGE_ON [PRIMARY]

IF EXISTS(SELECT * FROM primshapes)
	 EXEC('INSERT INTO Tmp_primshapes (UUID, Shape, ScaleX, ScaleY, ScaleZ, PCode, PathBegin, PathEnd, PathScaleX, PathScaleY, PathShearX, PathShearY, PathSkew, PathCurve, PathRadiusOffset, PathRevolutions, PathTaperX, PathTaperY, PathTwist, PathTwistBegin, ProfileBegin, ProfileEnd, ProfileCurve, ProfileHollow, State, Texture, ExtraParams)
		SELECT CONVERT(varchar(36), UUID), Shape, ScaleX, ScaleY, ScaleZ, PCode, PathBegin, PathEnd, PathScaleX, PathScaleY, PathShearX, PathShearY, PathSkew, PathCurve, PathRadiusOffset, PathRevolutions, PathTaperX, PathTaperY, PathTwist, PathTwistBegin, ProfileBegin, ProfileEnd, ProfileCurve, ProfileHollow, State, Texture, ExtraParams FROM primshapes WITH (HOLDLOCK TABLOCKX)')

DROP TABLE primshapes

EXECUTE sp_rename N'Tmp_primshapes', N'primshapes', 'OBJECT' 

ALTER TABLE primshapes ADD CONSTRAINT
	PK__primshapes__0880433F PRIMARY KEY CLUSTERED 
	(
	UUID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
