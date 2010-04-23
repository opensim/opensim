BEGIN TRANSACTION;
CREATE TABLE assets(
       UUID varchar(255) primary key,
       Name varchar(255),
       Description varchar(255),
       Type integer,
       InvType integer,
       Local integer,
       Temporary integer,
       Data blob);

COMMIT;
