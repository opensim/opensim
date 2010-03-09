BEGIN TRANSACTION

CREATE UNIQUE INDEX PrincipalID ON UserAccounts(PrincipalID);
CREATE INDEX Email ON UserAccounts(Email);
CREATE INDEX FirstName ON UserAccounts(FirstName);
CREATE INDEX LastName ON UserAccounts(LastName);
CREATE INDEX Name ON UserAccounts(FirstName,LastName);

COMMIT