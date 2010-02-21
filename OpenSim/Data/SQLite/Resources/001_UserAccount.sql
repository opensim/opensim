BEGIN TRANSACTION;

-- useraccounts table
CREATE TABLE UserAccounts (
    PrincipalID CHAR(36) NOT NULL,
    ScopeID CHAR(36) NOT NULL,
    FirstName VARCHAR(64) NOT NULL,
    LastName VARCHAR(64) NOT NULL,
    Email VARCHAR(64),
    ServiceURLs TEXT,
    Created INT(11),
    UserLevel integer NOT NULL DEFAULT 0,
    UserFlags integer NOT NULL DEFAULT 0,
    UserTitle varchar(64) NOT NULL DEFAULT ''
);

COMMIT;