BEGIN TRANSACTION

ALTER TABLE regionsettings ADD loaded_creation_date varchar(20) 
ALTER TABLE regionsettings ADD loaded_creation_time varchar(20) 
ALTER TABLE regionsettings ADD loaded_creation_id varchar(64) 

COMMIT
