BEGIN TRANSACTION

INSERT INTO Friends (PrincipalID, FriendID, Flags, Offered) SELECT [ownerID], [friendID], [friendPerms], 0 FROM userfriends;


COMMIT