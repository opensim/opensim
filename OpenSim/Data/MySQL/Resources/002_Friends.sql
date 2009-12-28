BEGIN;

INSERT INTO Friends (PrincipalID, FriendID, Flags) SELECT ownerID, friendID, friendPerms FROM userfriends;

COMMIT;
