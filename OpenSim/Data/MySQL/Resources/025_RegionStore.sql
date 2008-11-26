BEGIN;

alter table prims change column `PositionX` `PositionX` double default NULL;
alter table prims change column `PositionY` `PositionY` double default NULL;
alter table prims change column `PositionZ` `PositionZ` double default NULL;
alter table prims change column `GroupPositionX` `GroupPositionX` double default NULL;
alter table prims change column `GroupPositionY` `GroupPositionY` double default NULL;
alter table prims change column `GroupPositionZ` `GroupPositionZ` double default NULL;
alter table prims change column `VelocityX` `VelocityX` double default NULL;
alter table prims change column `VelocityY` `VelocityY` double default NULL;
alter table prims change column `VelocityZ` `VelocityZ` double default NULL;
alter table prims change column `AngularVelocityX` `AngularVelocityX` double default NULL;
alter table prims change column `AngularVelocityY` `AngularVelocityY` double default NULL;
alter table prims change column `AngularVelocityZ` `AngularVelocityZ` double default NULL;
alter table prims change column `AccelerationX` `AccelerationX` double default NULL;
alter table prims change column `AccelerationY` `AccelerationY` double default NULL;
alter table prims change column `AccelerationZ` `AccelerationZ` double default NULL;
alter table prims change column `RotationX` `RotationX` double default NULL;
alter table prims change column `RotationY` `RotationY` double default NULL;
alter table prims change column `RotationZ` `RotationZ` double default NULL;
alter table prims change column `RotationW` `RotationW` double default NULL;
alter table prims change column `SitTargetOffsetX` `SitTargetOffsetX` double default NULL;
alter table prims change column `SitTargetOffsetY` `SitTargetOffsetY` double default NULL;
alter table prims change column `SitTargetOffsetZ` `SitTargetOffsetZ` double default NULL;
alter table prims change column `SitTargetOrientW` `SitTargetOrientW` double default NULL;
alter table prims change column `SitTargetOrientX` `SitTargetOrientX` double default NULL;
alter table prims change column `SitTargetOrientY` `SitTargetOrientY` double default NULL;
alter table prims change column `SitTargetOrientZ` `SitTargetOrientZ` double default NULL;
alter table prims change column `LoopedSoundGain` `LoopedSoundGain` double NOT NULL default '0';
alter table prims change column `OmegaX` `OmegaX` double NOT NULL default '0';
alter table prims change column `OmegaY` `OmegaY` double NOT NULL default '0';
alter table prims change column `OmegaZ` `OmegaZ` double NOT NULL default '0';
alter table prims change column `CameraEyeOffsetX` `CameraEyeOffsetX` double NOT NULL default '0';
alter table prims change column `CameraEyeOffsetY` `CameraEyeOffsetY` double NOT NULL default '0';
alter table prims change column `CameraEyeOffsetZ` `CameraEyeOffsetZ` double NOT NULL default '0';
alter table prims change column `CameraAtOffsetX` `CameraAtOffsetX` double NOT NULL default '0';
alter table prims change column `CameraAtOffsetY` `CameraAtOffsetY` double NOT NULL default '0';
alter table prims change column `CameraAtOffsetZ` `CameraAtOffsetZ` double NOT NULL default '0';
alter table prims change column `CollisionSoundVolume` `CollisionSoundVolume` double NOT NULL default '0';

alter table primshapes change column `ScaleX` `ScaleX` double NOT NULL default '0';
alter table primshapes change column `ScaleY` `ScaleY` double NOT NULL default '0';
alter table primshapes change column `ScaleZ` `ScaleZ` double NOT NULL default '0';

COMMIT;

