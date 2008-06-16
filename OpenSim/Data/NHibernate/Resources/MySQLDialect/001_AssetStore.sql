START TRANSACTION;

create table Assets(
       ID varchar(36) not null primary key,
       Type int default 0,
       InvType int default 0,
       Name varchar(64),
       Description varchar(64),
       Local boolean,
       Temporary boolean,
       Data blob
);

COMMIT;