BEGIN;

update users
  set UUID = substr(UUID, 1, 8) || "-" || substr(UUID, 9, 4) || "-" || substr(UUID, 13, 4) || "-" || substr(UUID, 17, 4) || "-" || substr(UUID, 21, 12)
  where UUID not like '%-%';

update useragents
  set UUID = substr(UUID, 1, 8) || "-" || substr(UUID, 9, 4) || "-" || substr(UUID, 13, 4) || "-" || substr(UUID, 17, 4) || "-" || substr(UUID, 21, 12)
  where UUID not like '%-%';

COMMIT;
