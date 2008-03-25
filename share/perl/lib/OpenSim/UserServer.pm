package OpenSim::UserServer;

use strict;
use OpenSim::Config;
use OpenSim::UserServer::Config;
use OpenSim::UserServer::UserManager;

sub getHandlerList {
    my %list = (
	"login_to_simulator" => \&_login_to_simulator,
	"get_user_by_name" => \&_get_user_by_name,
	"get_user_by_uuid" => \&_get_user_by_uuid,
	"get_avatar_picker_avatar" => \&_get_avatar_picker_avatar,
	);
    return \%list;
}

# #################
# Handlers
sub _login_to_simulator {
    my $params = shift;
    # check params
    if (!$params->{first} || !$params->{last} || !$params->{passwd}) {
	return &_make_false_response("not enough params", $OpenSim::Config::SYS_MSG{FATAL});
    }
    # select user (check passwd)
    my $user = &OpenSim::UserServer::UserManager::getUserByName($params->{first}, $params->{last});
    if ($user->{passwordHash} ne $params->{passwd}) {
	&_make_false_response("password not match", $OpenSim::Config::SYS_MSG{FAIL});
    }
    
    # contact with Grid server
    my %grid_request_params = (
	region_handle => $user->{homeRegion},
	authkey => undef
	);
    my $grid_response = &OpenSim::Utility::XMLRPCCall($OpenSim::Config::GRID_SERVER_URL, "simulator_data_request", \%grid_request_params);
    my $region_server_url = "http://" . $grid_response->{sim_ip} . ":" . $grid_response->{http_port};

    # contact with Region server
    my $session_id = &OpenSim::Utility::GenerateUUID;
    my $secure_session_id = &OpenSim::Utility::GenerateUUID;
    my $circuit_code = int(rand() * 1000000000); # just a random integer
    my $caps_id = &OpenSim::Utility::GenerateUUID;
    my %region_request_params = (
	session_id => $session_id,
	secure_session_id => $secure_session_id,
	firstname => $user->{username},
	lastname => $user->{lastname},
	agent_id => $user->{UUID},
	circuit_code => $circuit_code,
	startpos_x => $user->{homeLocationX},
	startpos_y => $user->{homeLocationY},
	startpos_z => $user->{homeLocationZ},
	regionhandle => $user->{homeRegion},
	caps_path => $caps_id,
	);
    my $region_response = &OpenSim::Utility::XMLRPCCall($region_server_url, "expect_user", \%region_request_params);

    # contact with Inventory server
    my $inventory_data = &_create_inventory_data($user->{UUID});

    # return to client
    my %response = (
	# login info
	login => "true",
	session_id => $session_id,
	secure_session_id => $secure_session_id,
	# agent
	first_name => $user->{username},
	last_name => $user->{lastname},
	agent_id => $user->{UUID},
	agent_access => "M", # TODO: do not know its meaning, hard coding in opensim
	# grid
	start_location => $params->{start},
	sim_ip => $grid_response->{sim_ip},
	sim_port => $grid_response->{sim_port},
	#sim_port => 9001,
	region_x => $grid_response->{region_locx} * 256,
	region_y => $grid_response->{region_locy} * 256,
	# other
	inventory_host => undef, # inv13-mysql
	circuit_code => $circuit_code,
	message => $OpenSim::Config::SYS_MSG{LOGIN_WELCOME},
	seconds_since_epoch => time,
	seed_capability => $region_server_url . "/CAPS/" . $caps_id . "0000/", # https://sim2734.agni.lindenlab.com:12043/cap/61d6d8a0-2098-7eb4-2989-76265d80e9b6
	look_at => &_make_r_string($user->{homeLookAtX}, $user->{homeLookAtY}, $user->{homeLookAtZ}),
	home => &_make_home_string(
	    [$grid_response->{region_locx} * 256, $grid_response->{region_locy} * 256],
	    [$user->{homeLocationX}, $user->{homeLocationY}, $user->{homeLocationX}],
	    [$user->{homeLookAtX}, $user->{homeLookAtY}, $user->{homeLookAtZ}]),
	"inventory-skeleton" => $inventory_data->{InventoryArray},
	"inventory-root" => [ { folder_id => $inventory_data->{RootFolderID} } ],
	"event_notifications" => \@OpenSim::UserServer::Config::event_notifications,
	"event_categories" => \@OpenSim::UserServer::Config::event_categories,
	"global-textures" => \@OpenSim::UserServer::Config::global_textures,
	"inventory-lib-owner" => \@OpenSim::UserServer::Config::inventory_lib_owner,
	"inventory-skel-lib" => \@OpenSim::UserServer::Config::inventory_skel_lib, # hard coding in OpenSim
	"inventory-lib-root" => \@OpenSim::UserServer::Config::inventory_lib_root,
	"classified_categories" => \@OpenSim::UserServer::Config::classified_categories,
	"login-flags" => \@OpenSim::UserServer::Config::login_flags,
	"initial-outfit" => \@OpenSim::UserServer::Config::initial_outfit,
	"gestures" => \@OpenSim::UserServer::Config::gestures,
	"ui-config" => \@OpenSim::UserServer::Config::ui_config,
	);
    return \%response;
}

sub _get_user_by_name {
    my $param = shift;
    
    if ($param->{avatar_name}) {
	my ($first, $last) = split(/\s+/, $param->{avatar_name});
	my $user = &OpenSim::UserServer::UserManager::getUserByName($first, $last);
	if (!$user) {
	    return &_unknown_user_response;
	}
	return &_convert_to_response($user);
    } else {
	return &_unknown_user_response;
    }
}

sub _get_user_by_uuid {
    my $param = shift;
    
    if ($param->{avatar_uuid}) {
	my $user = &OpenSim::UserServer::UserManager::getUserByUUID($param->{avatar_uuid});
	if (!$user) {
	    return &_unknown_user_response;
	}
	return &_convert_to_response($user);
    } else {
	return &_unknown_user_response;
    }
}

sub _get_avatar_picker_avatar {
}

# #################
# sub functions
sub _create_inventory_data {
    my $user_id = shift;
    # TODO : too bad!! -> URI encoding
    my $postdata =<< "POSTDATA";
POSTDATA=<?xml version="1.0" encoding="utf-8"?><guid>$user_id</guid>
POSTDATA
    my $res = &OpenSim::Utility::HttpPostRequest($OpenSim::Config::INVENTORY_SERVER_URL . "/RootFolders/", $postdata);
    my $res_obj = &OpenSim::Utility::XML2Obj($res);
    if (!$res_obj->{InventoryFolderBase}) {
	&OpenSim::Utility::HttpPostRequest($OpenSim::Config::INVENTORY_SERVER_URL . "/CreateInventory/", $postdata);
	# Sleep(10000); # TODO: need not to do this
	$res = &OpenSim::Utility::HttpPostRequest($OpenSim::Config::INVENTORY_SERVER_URL . "/RootFolders/", $postdata);
	$res_obj = &OpenSim::Utility::XML2Obj($res);
    }
    my $folders = $res_obj->{InventoryFolderBase};
    my $folders_count = @$folders;
    if ($folders_count > 0) {
	my @AgentInventoryFolders = ();
	my $root_uuid = &OpenSim::Utility::ZeroUUID();
	foreach my $folder (@$folders) {
	    if ($folder->{parentID}->{UUID} eq &OpenSim::Utility::ZeroUUID()) {
		$root_uuid = $folder->{folderID}->{UUID};
	    }
	    my %folder_hash = (
		name => $folder->{name},
		parent_id => $folder->{parentID}->{UUID},
		version => $folder->{version},
		type_default => $folder->{type},
		folder_id => $folder->{folderID}->{UUID},
		);
	    push @AgentInventoryFolders, \%folder_hash;
	}
	return { InventoryArray => \@AgentInventoryFolders, RootFolderID => $root_uuid };
    } else {
	# TODO: impossible ???
    }
    return undef;
}

sub _convert_to_response {
    my $user = shift;
    my %response = (
	firstname => $user->{username},
	lastname => $user->{lastname},
	uuid => $user->{UUID},
	server_inventory => $user->{userInventoryURI},
	server_asset => $user->{userAssetURI},
	profile_about => $user->{profileAboutText},
	profile_firstlife_about => $user->{profileFirstText},
	profile_firstlife_image => $user->{profileFirstImage},
	profile_can_do => $user->{profileCanDoMask} || "0",
	profile_want_do => $user->{profileWantDoMask} || "0",
	profile_image => $user->{profileImage},
	profile_created => $user->{created},
	profile_lastlogin => $user->{lastLogin} || "0",
	home_coordinates_x => $user->{homeLocationX},
	home_coordinates_y => $user->{homeLocationY},
	home_coordinates_z => $user->{homeLocationZ},
	home_region => $user->{homeRegion} || 0,
	home_look_x => $user->{homeLookAtX},
	home_look_y => $user->{homeLookAtY},
	home_look_z => $user->{homeLookAtZ},
	);
    return \%response;
}

# #################
# Utility Functions
sub _make_false_response {
    my ($reason, $message) = @_;
    return { reason => $reason, login => "false", message => $message };
}

sub _unknown_user_response {
    return {
	error_type => "unknown_user",
	error_desc => "The user requested is not in the database",
    };
}

sub _make_home_string {
    my ($region_handle, $position, $look_at) = @_;
    my $region_handle_string = "'region_handle':" . &_make_r_string(@$region_handle);
    my $position_string = "'position':" . &_make_r_string(@$position);
    my $look_at_string = "'look_at':" . &_make_r_string(@$look_at);
    return "{" . $region_handle_string . ", " . $position_string . ", " . $look_at_string . "}";
}

sub _make_r_string {
    my @params = @_;
    foreach (@params) {
	$_ = "r" . $_;
    }
    return "[" . join(",", @params) . "]";
}

1;
