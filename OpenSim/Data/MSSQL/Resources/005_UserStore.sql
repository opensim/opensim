BEGIN TRANSACTION

 ALTER TABLE users add email varchar(250);
 
COMMIT
