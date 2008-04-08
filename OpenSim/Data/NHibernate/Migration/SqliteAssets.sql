-- The following converts the UUID from XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
-- to XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX.  This puts it in Guid native format
-- for .NET, and the prefered format for LLUUID.

update assets set UUID = SUBSTR(UUID,1,8) || "-" || SUBSTR(UUID,9,4) || "-" || SUBSTR(UUID,13,4)  || "-" || SUBSTR(UUID,17,4)  || "-" || SUBSTR(UUID,21,12) where UUID not like '%-%';