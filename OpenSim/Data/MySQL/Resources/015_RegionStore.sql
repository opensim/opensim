begin;

alter table estate_settings add column DenyMinors tinyint not null;

commit;

