BEGIN TRANSACTION;

CREATE TABLE IF NOT EXISTS avatarappearance(
  Owner varchar(36) NOT NULL primary key,
  BodyItem varchar(36) DEFAULT NULL,
  BodyAsset varchar(36) DEFAULT NULL,
  SkinItem varchar(36) DEFAULT NULL,
  SkinAsset varchar(36) DEFAULT NULL,
  HairItem varchar(36) DEFAULT NULL,
  HairAsset varchar(36) DEFAULT NULL,
  EyesItem varchar(36) DEFAULT NULL,
  EyesAsset varchar(36) DEFAULT NULL,
  ShirtItem varchar(36) DEFAULT NULL,
  ShirtAsset varchar(36) DEFAULT NULL,
  PantsItem varchar(36) DEFAULT NULL,
  PantsAsset varchar(36) DEFAULT NULL,
  ShoesItem varchar(36) DEFAULT NULL,
  ShoesAsset varchar(36) DEFAULT NULL,
  SocksItem varchar(36) DEFAULT NULL,
  SocksAsset varchar(36) DEFAULT NULL,
  JacketItem varchar(36) DEFAULT NULL,
  JacketAsset varchar(36) DEFAULT NULL,
  GlovesItem varchar(36) DEFAULT NULL,
  GlovesAsset varchar(36) DEFAULT NULL,
  UnderShirtItem varchar(36) DEFAULT NULL,
  UnderShirtAsset varchar(36) DEFAULT NULL,
  UnderPantsItem varchar(36) DEFAULT NULL,
  UnderPantsAsset varchar(36) DEFAULT NULL,
  SkirtItem varchar(36) DEFAULT NULL,
  SkirtAsset varchar(36) DEFAULT NULL,
  Texture blob,
  VisualParams blob,
  Serial int DEFAULT NULL,
  AvatarHeight float DEFAULT NULL
);

COMMIT;
