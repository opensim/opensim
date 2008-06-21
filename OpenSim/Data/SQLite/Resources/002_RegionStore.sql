BEGIN TRANSACTION;

CREATE TABLE regionban(
		regionUUID varchar (255),
		bannedUUID varchar (255),
		bannedIp varchar (255),
		bannedIpHostMask varchar (255)
		);
       
COMMIT;