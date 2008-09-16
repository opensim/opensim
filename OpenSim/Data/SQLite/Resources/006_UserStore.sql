BEGIN TRANSACTION;

-- usersagents table
CREATE TABLE IF NOT EXISTS useragents(
       UUID varchar(255) primary key,
       agentIP varchar(255),
       agentPort integer,
       agentOnline boolean,
       sessionID varchar(255),
       secureSessionID varchar(255),
       regionID varchar(255),
       loginTime integer,
       logoutTime integer,
       currentRegion varchar(255),
       currentHandle varchar(255),
       currentPosX float,
       currentPosY float,
       currentPosZ float);

COMMIT;
