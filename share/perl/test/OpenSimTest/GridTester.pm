package GridTester;

use strict;
use OpenSim::Utility;

sub init {
	&OpenSimTest::Config::registerHandler("simulator_login", \&_simulator_login);
	&OpenSimTest::Config::registerHandler("simulator_data_request", \&_simulator_data_request);
	&OpenSimTest::Config::registerHandler("simulator_after_region_moved", \&_simulator_after_region_moved);
	&OpenSimTest::Config::registerHandler("map_block", \&_map_block);
}

sub _simulator_login {
	my $url = shift || $OpenSimTest::Config::GRID_SERVER_URL;
    my @param = @_;
    my %xml_rpc_param = (
		"authkey" => "null",
		"UUID" => $param[0],
		"sim_ip" => $param[1],
		"sim_port" => $param[2],
		"region_locx" => 1000,
		"region_locy" => 1000,
		"sim_name" => "OpenTest",
		"http_port" => 9000,
		"remoting_port" => 8895,
		"map-image-id" => "0e5a5e87-08d9-4b37-9b8e-a4c3c4e409ab",
	);
    return &OpenSim::Utility::XMLRPCCall($url, "simulator_login", \%xml_rpc_param);
}

sub _map_block {
	my $url = shift || $OpenSimTest::Config::GRID_SERVER_URL;
    my @param = @_;
    my %xml_rpc_param = (
		xmin => $param[0],
		ymin => $param[1],
		xmax => $param[2],
		ymax => $param[3],
	);
    return &OpenSim::Utility::XMLRPCCall($url, "map_block", \%xml_rpc_param);
}

sub _simulator_data_request {
	my $url = shift || $OpenSimTest::Config::GRID_SERVER_URL;
    my @param = @_;
	my %xml_rpc_param = (
		region_handle => $param[0],
		authkey => undef,
	);
    return &OpenSim::Utility::XMLRPCCall($url, "simulator_data_request", \%xml_rpc_param);
}

sub _simulator_after_region_moved {
	my $url = shift || $OpenSimTest::Config::GRID_SERVER_URL;
    my @param = @_;
	my %xml_rpc_param = (
		UUID => $param[0],
	);
    return &OpenSim::Utility::XMLRPCCall($url, "simulator_after_region_moved", \%xml_rpc_param);
}

1;
