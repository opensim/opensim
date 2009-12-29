BEGIN;

CREATE TABLE `useraccount` (
    `UserID` CHAR(36) NOT NULL,
    `ScopeID` CHAR(36) NOT NULL,
    `FirstName` VARCHAR(64) NOT NULL,
    `LastName` VARCHAR(64) NOT NULL,
    `Email` VARCHAR(64),
    `ServiceURLs` TEXT,
    `Created` DATETIME
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

COMMIT;
