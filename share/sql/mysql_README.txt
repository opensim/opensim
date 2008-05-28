mysql_README

This directory used to contain sql files for mysql that needed to be manually sourced in order to
set up the database tables required by OpenSim.  This is no longer necessary - OpenSim now sets
up these tables automatically.  All you need to do is 
create the database that OpenSim is to use and set the configuration in bin/mysql_connection.ini
(using bin/mysql_connection.ini.example as a reference).

If you do need to source the mysql files manually, they can be found in OpenSim/Data/MySQL/Resources

Please note that if you are setting up a MSSQL database, the appropriate mssql files do still need to be 
executed manually.
