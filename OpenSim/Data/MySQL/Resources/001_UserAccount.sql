BEGIN;

CREATE TABLE `UserAccounts` (
    `PrincipalID` CHAR(36) NOT NULL,
    `ScopeID` CHAR(36) NOT NULL,
    `FirstName` VARCHAR(64) NOT NULL,
    `LastName` VARCHAR(64) NOT NULL,
    `Email` VARCHAR(64),
    `ServiceURLs` TEXT,
    `Created` INT(11)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

COMMIT;
