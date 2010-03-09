BEGIN TRANSACTION

INSERT INTO Friends (PrincipalID, Friend, Flags, Offered) SELECT [ownerID], [friendID], [friendPerms], 0 FROM userfriends;


COMMIT