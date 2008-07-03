BEGIN;

alter table landaccesslist ENGINE = InnoDB;
alter table migrations ENGINE = InnoDB;
alter table primitems ENGINE = InnoDB;
alter table prims ENGINE = InnoDB;
alter table primshapes ENGINE = InnoDB;
alter table regionsettings ENGINE = InnoDB;
alter table terrain ENGINE = InnoDB;

COMMIT;

