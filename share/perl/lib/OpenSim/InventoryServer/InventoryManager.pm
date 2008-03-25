package OpenSim::InventoryServer::InventoryManager;

use strict;
use Carp;
use OpenSim::Utility;
use OpenSim::InventoryServer::Config;

sub saveInventoryFolder {
	my $folder = shift;
	my $result = &OpenSim::Utility::getSimpleResult($OpenSim::InventoryServer::Config::SYS_SQL{save_inventory_folder},
		$folder->{"folderID"},
		$folder->{"agentID"},
		$folder->{"parentFolderID"},
		$folder->{"folderName"},
		$folder->{"type"},
		$folder->{"version"});
}

sub saveInventoryItem {
	my $item = shift;
	my $result = &OpenSim::Utility::getSimpleResult($OpenSim::InventoryServer::Config::SYS_SQL{save_inventory_item},
		$item->{"inventoryID"},
		$item->{"assetID"},
		$item->{"type"},
		$item->{"parentFolderID"},
		$item->{"avatarID"},
		$item->{"inventoryName"},
		$item->{"inventoryDescription"},
		$item->{"inventoryNextPermissions"},
		$item->{"inventoryCurrentPermissions"},
		$item->{"assetType"},
		$item->{"invType"},
		$item->{"creatorID"},
		$item->{"inventoryBasePermissions"},
		$item->{"inventoryEveryOnePermissions"});
}

sub getRootFolder {
	my $agent_id = shift;
	my $result = &OpenSim::Utility::getSimpleResult($OpenSim::InventoryServer::Config::SYS_SQL{get_root_folder},
		&OpenSim::Utility::ZeroUUID(),
		$agent_id);
	my $count = @$result;
	if ($count > 0) {
		return $result->[0];
	} else {
		return undef;
	}
}

sub getChildrenFolders {
	my $parent_id = shift;
	my $result = &OpenSim::Utility::getSimpleResult($OpenSim::InventoryServer::Config::SYS_SQL{get_children_folders}, $parent_id);
	return $result;
}

sub getUserInventoryFolders {
	my $agent_id = shift;
	my $result = &OpenSim::Utility::getSimpleResult($OpenSim::InventoryServer::Config::SYS_SQL{get_user_inventory_folders},
		$agent_id);
	return $result;
}

sub getUserInventoryItems {
	my $agent_id = shift;
	my $result = &OpenSim::Utility::getSimpleResult($OpenSim::InventoryServer::Config::SYS_SQL{get_user_inventory_items},
		$agent_id);
	return $result;
}

sub deleteInventoryItem {
	my $item_id = shift;
	&OpenSim::Utility::getSimpleResult($OpenSim::InventoryServer::Config::SYS_SQL{delete_inventory_item},
		$item_id);
}

sub moveInventoryFolder {
	my $info = shift;
	&OpenSim::Utility::getSimpleResult($OpenSim::InventoryServer::Config::SYS_SQL{move_inventory_folder},
		$info->{parentID}->{UUID}, # TODO: not good
		$info->{folderID}->{UUID}, # TODO: not good UUID should be extracted in the higher level
		);
}

1;

