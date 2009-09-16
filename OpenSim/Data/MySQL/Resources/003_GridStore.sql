BEGIN;

ALTER TABLE regions add column ScopeID char(36) not null default '00000000-0000-0000-0000-000000000000';

create index ScopeID on regions(ScopeID);

COMMIT;
