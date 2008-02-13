--
-- Create schema avatar_appearance
--

CREATE DATABASE IF NOT EXISTS avatar_appearance;
USE avatar_appearance;

DROP TABLE IF EXISTS `avatarappearance`;
CREATE TABLE `avatarappearance` (
  `UUID` char(36) NOT NULL,
  `Serial` int(10) unsigned NOT NULL,
  `WearableItem0` char(36) NOT NULL,
  `WearableAsset0` char(36) NOT NULL,
  `WearableItem1` char(36) NOT NULL,
  `WearableAsset1` char(36) NOT NULL,
  `WearableItem2` char(36) NOT NULL,
  `WearableAsset2` char(36) NOT NULL,
  `WearableItem3` char(36) NOT NULL,
  `WearableAsset3` char(36) NOT NULL,
  `WearableItem4` char(36) NOT NULL,
  `WearableAsset4` char(36) NOT NULL,
  `WearableItem5` char(36) NOT NULL,
  `WearableAsset5` char(36) NOT NULL,
  `WearableItem6` char(36) NOT NULL,
  `WearableAsset6` char(36) NOT NULL,
  `WearableItem7` char(36) NOT NULL,
  `WearableAsset7` char(36) NOT NULL,
  `WearableItem8` char(36) NOT NULL,
  `WearableAsset8` char(36) NOT NULL,
  `WearableItem9` char(36) NOT NULL,
  `WearableAsset9` char(36) NOT NULL,
  `WearableItem10` char(36) NOT NULL,
  `WearableAsset10` char(36) NOT NULL,
  `WearableItem11` char(36) NOT NULL,
  `WearableAsset11` char(36) NOT NULL,
  `WearableItem12` char(36) NOT NULL,
  `WearableAsset12` char(36) NOT NULL,


  PRIMARY KEY  (`UUID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

