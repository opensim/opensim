#!/usr/bin/perl

# -- CREATE TABLE inventoryitems(UUID varchar(255) primary key,
# -- assetID varchar(255),
# -- assetType integer,
# -- invType integer,
# -- parentFolderID varchar(255),
# -- avatarID varchar(255),
# -- creatorsID varchar(255),
# -- inventoryName varchar(255),
# -- inventoryDescription varchar(255),
# -- inventoryNextPermissions integer,
# -- inventoryCurrentPermissions integer,
# -- inventoryBasePermissions integer,
# -- inventoryEveryOnePermissions integer);

# -- CREATE TABLE inventoryfolders(UUID varchar(255) primary key,
# -- name varchar(255),
# -- agentID varchar(255),
# -- parentID varchar(255),
# -- type integer,
# -- version integer);

my $items = "INSERT INTO InventoryItems(ID, AssetID, AssetType, InvType, Folder, Owner, Creator, Name, Description, NextPermissions, CurrentPermissions, BasePermissions, EveryOnePermissions) ";
my $folders = "INSERT INTO InventoryFolders(ID, Name, Owner, ParentID, Type, Version) ";

open(SQLITE, "sqlite3 inventoryStore.db .dump |") or die "can't open the database for migration";
open(WRITE,"| sqlite3 Inventory.db");

while(my $line = <SQLITE>) {
    $line =~ s/([0-9a-f]{8})([0-9a-f]{4})([0-9a-f]{4})([0-9a-f]{4})([0-9a-f]{12})/$1-$2-$3-$4-$5/g;
    if($line =~ s/(INSERT INTO "inventoryitems")/$items/) {
        print $line;
        print WRITE $line;
    }
    if($line =~ s/(INSERT INTO "inventoryfolders")/$folders/) {
        print $line;
        print WRITE $line;
    }    
}

close(WRITE);
close(SQLITE);
