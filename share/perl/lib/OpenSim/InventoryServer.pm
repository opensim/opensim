package OpenSim::InventoryServer;

use strict;
use XML::Serializer;
use OpenSim::Utility;
use OpenSim::Config;
use OpenSim::InventoryServer::Config;
use OpenSim::InventoryServer::InventoryManager;

my $METHOD_LIST = undef;

sub getHandlerList {
	if (!$METHOD_LIST) {
		my %list = (
			"GetInventory" => \&_get_inventory,
			"CreateInventory" => \&_create_inventory,
			"NewFolder" => \&_new_folder,
			"MoveFolder" => \&_move_folder,
			"NewItem" => \&_new_item,
			"DeleteItem" => \&_delete_item,
			"RootFolders" => \&_root_folders,
		);
		$METHOD_LIST = \%list;
	}
    return $METHOD_LIST;
}

# #################
# Handlers
sub _get_inventory {
	my $post_data = shift;
	my $uuid = &_get_uuid($post_data);
	my $inventry_folders = &OpenSim::InventoryServer::InventoryManager::getUserInventoryFolders($uuid);
	my @response_folders = ();
	foreach (@$inventry_folders) {
		my $folder = &_convert_to_response_folder($_);
		push @response_folders, $folder;
	}
	my $inventry_items = &OpenSim::InventoryServer::InventoryManager::getUserInventoryItems($uuid);
	my @response_items = ();
	foreach (@$inventry_items) {
		my $item = &_convert_to_response_item($_);
		push @response_items, $item;
	}
	my $response_obj = {
		Folders => { InventoryFolderBase => \@response_folders },
		AllItems => { InventoryItemBase => \@response_items },
		UserID => { UUID => $uuid },
	};
	my $serializer = new XML::Serializer( $response_obj, "InventoryCollection");
	return $serializer->to_formatted(XML::Serializer::WITH_HEADER); # TODO:
}

sub _create_inventory {
	my $post_data = shift;
	my $uuid = &_get_uuid($post_data);
	my $InventoryFolders = &_create_default_inventory($uuid);
	foreach (@$InventoryFolders) {
		&OpenSim::InventoryServer::InventoryManager::saveInventoryFolder($_);
	}
	my $serializer = new XML::Serializer("true", "boolean");
	return $serializer->to_formatted(XML::Serializer::WITH_HEADER); # TODO:
}

sub _new_folder {
	my $post_data = shift;
	my $request_obj = &OpenSim::Utility::XML2Obj($post_data);
	my $folder = &_convert_to_db_folder($request_obj);
	&OpenSim::InventoryServer::InventoryManager::saveInventoryFolder($folder);
	my $serializer = new XML::Serializer("true", "boolean");
	return $serializer->to_formatted(XML::Serializer::WITH_HEADER); # TODO:
}

sub _move_folder {
	my $post_data = shift;
	my $request_info = &OpenSim::Utility::XML2Obj($post_data);
	&OpenSim::InventoryServer::InventoryManager::moveInventoryFolder($request_info);
	my $serializer = new XML::Serializer("true", "boolean");
	return $serializer->to_formatted(XML::Serializer::WITH_HEADER); # TODO:
}

sub _new_item {
	my $post_data = shift;
	my $request_obj = &OpenSim::Utility::XML2Obj($post_data);
	my $item = &_convert_to_db_item($request_obj);
	&OpenSim::InventoryServer::InventoryManager::saveInventoryItem($item);
	my $serializer = new XML::Serializer("true", "boolean");
	return $serializer->to_formatted(XML::Serializer::WITH_HEADER); # TODO:
}

sub _delete_item {
	my $post_data = shift;
	my $request_obj = &OpenSim::Utility::XML2Obj($post_data);
	my $item_id = $request_obj->{inventoryID}->{UUID};
	&OpenSim::InventoryServer::InventoryManager::deleteInventoryItem($item_id);
	my $serializer = new XML::Serializer("true", "boolean");
	return $serializer->to_formatted(XML::Serializer::WITH_HEADER); # TODO:
}

sub _root_folders {
	my $post_data = shift;
	my $uuid = &_get_uuid($post_data);
	my $response = undef;
	my $inventory_root_folder = &OpenSim::InventoryServer::InventoryManager::getRootFolder($uuid);
	if ($inventory_root_folder) {
		my $root_folder_id = $inventory_root_folder->{folderID};
		my $root_folder = &_convert_to_response_folder($inventory_root_folder);
		my $root_folders = &OpenSim::InventoryServer::InventoryManager::getChildrenFolders($root_folder_id);
		my @folders = ($root_folder);
		foreach(@$root_folders) {
			my $folder = &_convert_to_response_folder($_);
			push @folders, $folder;
		}
		$response = { InventoryFolderBase => \@folders };
	} else {
		$response = ""; # TODO: need better failed message
	}
	my $serializer = new XML::Serializer($response, "ArrayOfInventoryFolderBase");
	return $serializer->to_formatted(XML::Serializer::WITH_HEADER); # TODO:
}

# #################
# subfunctions
sub _convert_to_db_item {
	my $item = shift;
	my $ret = {
		inventoryID => $item->{inventoryID}->{UUID},
		assetID => $item->{assetID}->{UUID},
		assetType => $item->{assetType},
		invType => $item->{invType},
		parentFolderID => $item->{parentFolderID}->{UUID},
		avatarID => $item->{avatarID}->{UUID},
		creatorID => $item->{creatorsID}->{UUID}, # TODO: human error ???
		inventoryName => $item->{inventoryName},
		inventoryDescription => $item->{inventoryDescription} || "",
		inventoryNextPermissions => $item->{inventoryNextPermissions},
		inventoryCurrentPermissions => $item->{inventoryCurrentPermissions},
		inventoryBasePermissions => $item->{inventoryBasePermissions},
		inventoryEveryOnePermissions => $item->{inventoryEveryOnePermissions},
	};
	return $ret;
}

sub _convert_to_response_item {
	my $item = shift;
	my $ret = {
		inventoryID => { UUID => $item->{inventoryID} },
		assetID => { UUID => $item->{assetID} },
		assetType => $item->{assetType},
		invType => $item->{invType},
		parentFolderID => { UUID => $item->{parentFolderID} },
		avatarID => { UUID => $item->{avatarID} },
		creatorsID => { UUID => $item->{creatorID} }, # TODO: human error ???
		inventoryName => $item->{inventoryName},
		inventoryDescription => $item->{inventoryDescription} || "",
		inventoryNextPermissions => $item->{inventoryNextPermissions},
		inventoryCurrentPermissions => $item->{inventoryCurrentPermissions},
		inventoryBasePermissions => $item->{inventoryBasePermissions},
		inventoryEveryOnePermissions => $item->{inventoryEveryOnePermissions},
	};
	return $ret;
}

sub _convert_to_db_folder {
	my $folder = shift;
	my $ret = {
		folderName => $folder->{name},
		agentID => $folder->{agentID}->{UUID},
		parentFolderID => $folder->{parentID}->{UUID},
		folderID => $folder->{folderID}->{UUID},
		type => $folder->{type},
		version => $folder->{version},
	};
	return $ret;
}

sub _convert_to_response_folder {
	my $folder = shift;
	my $ret = {
		name => $folder->{folderName},
		agentID => { UUID => $folder->{agentID} },
		parentID => { UUID => $folder->{parentFolderID} },
		folderID => { UUID => $folder->{folderID} },
		type => $folder->{type},
		version => $folder->{version},
	};
	return $ret;
}

sub _create_default_inventory {
	my $uuid = shift;

	my @InventoryFolders = ();
	my $root_folder_id = &OpenSim::Utility::GenerateUUID();

	push @InventoryFolders, {
		"folderID" => $root_folder_id,
		"agentID" => $uuid,
		"parentFolderID" => &OpenSim::Utility::ZeroUUID(),
		"folderName" => "My Inventory",
		"type" => 8,
		"version" => 1,
	};

	push @InventoryFolders, {
		"folderID" => &OpenSim::Utility::GenerateUUID(),
		"agentID" => $uuid,
		"parentFolderID" => $root_folder_id,
		"folderName" => "Textures",
		"type" => 0,
		"version" => 1,
	};

	push @InventoryFolders, {
		"folderID" => &OpenSim::Utility::GenerateUUID(),
		"agentID" => $uuid,
		"parentFolderID" => $root_folder_id,
		"folderName" => "Objects",
		"type" => 6,
		"version" => 1,
	};

	push @InventoryFolders, {
		"folderID" => &OpenSim::Utility::GenerateUUID(),
		"agentID" => $uuid,
		"parentFolderID" => $root_folder_id,
		"folderName" => "Clothes",
		"type" => 5,
		"version" => 1,
	};

	return \@InventoryFolders;
}


# #################
# Utilities
sub _get_uuid {
	my $data = shift;
	if ($data =~ /<guid\s*>([^<]+)<\/guid>/) {
		return $1;
	} else {
		Carp::croak("can not find uuid: $data");
	}
}


1;

