package OpenSim::GridServer;

use strict;
use OpenSim::Utility;
use OpenSim::GridServer::Config;
use OpenSim::GridServer::GridManager;

sub getHandlerList {
    my %list = (
	"simulator_login" => \&_simulator_login,
	"simulator_data_request" => \&_simulator_data_request,
	"map_block" => \&_map_block,
	"map_block2" => \&_map_block2, # this is better for the Region Monitor
	);
    return \%list;
}

# #################
# XMLRPC Handlers
sub _simulator_login {
	my $params = shift;

	my $region_data = undef;
	my %response = ();
	if ($params->{"UUID"}) {
		$region_data = &OpenSim::GridServer::GridManager::getRegionByUUID($params->{"UUID"});
	} elsif ($params->{"region_handle"}) {
		$region_data = &OpenSim::GridServer::GridManager::getRegionByHandle($params->{"region_handle"});
	} else {
		$response{"error"} = "No UUID or region_handle passed to grid server - unable to connect you";
		return \%response;
	}

	if (!$region_data) {
		my %new_region_data = (
			uuid => undef,
			regionHandle => OpenSim::Utility::UIntsToLong($params->{region_locx}*256, $params->{region_locx}*256),
			regionName => $params->{sim_name},
			regionRecvKey => $OpenSim::Config::SIM_RECV_KEY,
			regionSendKey => $OpenSim::Config::SIM_SEND_KEY,
			regionSecret => $OpenSim::Config::SIM_RECV_KEY,
			regionDataURI => "",
			serverIP => $params->{sim_ip},
			serverPort => $params->{sim_port},
			serverURI => "http://" + $params->{sim_ip} + ":" + $params->{sim_port} + "/",
			LocX => $params->{region_locx},
			LocY => $params->{region_locy},
			LocZ => 0,
			regionAssetURI => $OpenSim::Config::ASSET_SERVER_URL,
			regionAssetRecvKey => $OpenSim::Config::ASSET_RECV_KEY,
			regionAssetSendKey => $OpenSim::Config::ASSET_SEND_KEY,
			regionUserURI => $OpenSim::Config::USER_SERVER_URL,
			regionUserRecvKey => $OpenSim::Config::USER_RECV_KEY,
			regionUserSendKey => $OpenSim::Config::USER_SEND_KEY,
			regionMapTextureID => $params->{"map-image-id"},
			serverHttpPort => $params->{http_port},
			serverRemotingPort => $params->{remoting_port},
		);
		eval {
			&OpenSim::GridServer::GridManager::addRegion(\%new_region_data);
		};
		if ($@) {
			$response{"error"} = "unable to add region";
			return \%response;
		}
		$region_data = \%new_region_data;
	}

	my @region_neighbours_data = ();
	my $region_list = &OpenSim::GridServer::GridManager::getRegionList($region_data->{locX}-1, $region_data->{locY}-1, $region_data->{locX}+1, $region_data->{locY}+1);
	foreach my $region (@$region_list) {
		next if ($region->{regionHandle} eq $region_data->{regionHandle});
		my %neighbour_block = (
			"sim_ip" => $region->{serverIP},
			"sim_port" => $region->{serverPort},
			"region_locx" => $region->{locX},
			"region_locy" => $region->{locY},
			"UUID" => $region->{uuid},
			"regionHandle" => $region->{regionHandle},
		);
		push @region_neighbours_data, \%neighbour_block;
 	}

	%response = (
		UUID => $region_data->{uuid},
		region_locx => $region_data->{locX},
		region_locy => $region_data->{locY},
		regionname => $region_data->{regionName},
		estate_id => "1", # TODO ???
		neighbours => \@region_neighbours_data,
		sim_ip => $region_data->{serverIP},
		sim_port => $region_data->{serverPort},
		asset_url => $region_data->{regionAssetURI},
		asset_recvkey => $region_data->{regionAssetRecvKey},
		asset_sendkey => $region_data->{regionAssetSendKey},
		user_url => $region_data->{regionUserURI},
		user_recvkey => $region_data->{regionUserRecvKey},
		user_sendkey => $region_data->{regionUserSendKey},
		authkey => $region_data->{regionSecret},
		data_uri => $region_data->{regionDataURI},
		"allow_forceful_banlines" => "TRUE",
	);

	return \%response;
}

sub _simulator_data_request {
    my $params = shift;

	my $region_data = undef;
	my %response = ();
	if ($params->{"region_UUID"}) {
		$region_data = &OpenSim::GridServer::GridManager::getRegionByUUID($params->{"region_UUID"});
	} elsif ($params->{"region_handle"}) {
		$region_data = &OpenSim::GridServer::GridManager::getRegionByHandle($params->{"region_handle"});
	}
	if (!$region_data) {
		$response{"error"} = "Sim does not exist";
		return \%response;
	}

	$response{"sim_ip"} = $region_data->{serverIP};
	$response{"sim_port"} = $region_data->{serverPort};
	$response{"http_port"} = $region_data->{serverHttpPort};
	$response{"remoting_port"} = $region_data->{serverRemotingPort};
	$response{"region_locx"} = $region_data->{locX};
	$response{"region_locy"} = $region_data->{locY};
	$response{"region_UUID"} = $region_data->{uuid};
	$response{"region_name"} = $region_data->{regionName};
	$response{"regionHandle"} = $region_data->{regionHandle};

	return \%response;
}

sub _map_block {
    my $params = shift;

	my $xmin = $params->{xmin} || 980;
	my $ymin = $params->{ymin} || 980;
	my $xmax = $params->{xmax} || 1020;
	my $ymax = $params->{ymax} || 1020;

	my @sim_block_list = ();
	my $region_list = &OpenSim::GridServer::GridManager::getRegionList($xmin, $ymin, $xmax, $ymax);
	foreach my $region (@$region_list) {
		my %sim_block = (
			"x" => $region->{locX},
			"y" => $region->{locY},
			"name" => $region->{regionName},
			"access" => 0, # TODO ? meaning unknown
			"region-flags" => 0, # TODO ? unknown
			"water-height" => 20, # TODO ? get from a XML
			"agents" => 1, # TODO
			"map-image-id" => $region->{regionMapTexture},
			"regionhandle" => $region->{regionHandle},
			"sim_ip" => $region->{serverIP},
			"sim_port" => $region->{serverPort},
			"sim_uri" => $region->{serverURI},
			"uuid" => $region->{uuid},
			"remoting_port" => $region->{serverRemotingPort},
		);
		push @sim_block_list, \%sim_block;
 	}

	my %response = (
		"sim-profiles" => \@sim_block_list,
	);
	return \%response;
}

sub _map_block2 {
    my $params = shift;

	my $xmin = $params->{xmin} || 980;
	my $ymin = $params->{ymin} || 980;
	my $xmax = $params->{xmax} || 1020;
	my $ymax = $params->{ymax} || 1020;

	my @sim_block_list = ();
	my $region_list = &OpenSim::GridServer::GridManager::getRegionList2($xmin, $ymin, $xmax, $ymax);
	foreach my $region (@$region_list) {
		my %sim_block = (
			"x" => $region->{locX},
			"y" => $region->{locY},
			"name" => $region->{regionName},
			"access" => 0, # TODO ? meaning unknown
			"region-flags" => 0, # TODO ? unknown
			"water-height" => 20, # TODO ? get from a XML
			"agents" => 1, # TODO
			"map-image-id" => $region->{regionMapTexture},
			"regionhandle" => $region->{regionHandle},
			"sim_ip" => $region->{serverIP},
			"sim_port" => $region->{serverPort},
			"sim_uri" => $region->{serverURI},
			"uuid" => $region->{uuid},
			"remoting_port" => $region->{serverRemotingPort},
		);
		push @sim_block_list, \%sim_block;
 	}

	my %response = (
		"sim-profiles" => \@sim_block_list,
	);
	return \%response;
}

1;

