begin;

alter table estate_settings add column AbuseEmail varchar(255) not null default '';

alter table estate_settings add column EstateOwner varchar(36) not null default '';

commit;

