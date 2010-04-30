BEGIN TRANSACTION;

INSERT INTO `Friends` SELECT `ownerID`, `friendID`, `friendPerms`, 0 FROM `userfriends`;

COMMIT;
