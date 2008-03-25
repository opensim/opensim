package OpenSimTest::Config;

use strict;

my $apache_server_host = "localhost";
my $opensim_server_host = "localhost";

# REGION
our $SIM_RECV_KEY = "";
our $SIM_SEND_KEY = "";
# ASSET
#our $ASSET_SERVER_URL = "http://127.0.0.1:8003/";
our $ASSET_SERVER_URL = "http://$apache_server_host/opensim/asset.cgi";
our $ASSET_RECV_KEY = "";
our $ASSET_SEND_KEY = "";
# USER
#our $USER_SERVER_URL = "http://127.0.0.1:8001/";
our $USER_SERVER_URL = "http://$apache_server_host/opensim/user.cgi";
our $USER_RECV_KEY = "";
our $USER_SEND_KEY = "";
# GRID
#our $GRID_SERVER_URL = "http://127.0.0.1:8001/";
our $GRID_SERVER_URL = "http://$apache_server_host/opensim/grid.cgi";
our $GRID_RECV_KEY = "";
our $GRID_SEND_KEY = "";
# INVENTORY
#our $INVENTORY_SERVER_URL = "http://127.0.0.1:8004";
our $INVENTORY_SERVER_URL = "http://$apache_server_host/opensim/inventory.cgi";
# handler list
our %HANDLER_LIST = ();

our %APACHE_SERVERS = (
	user		=> "http://$apache_server_host/opensim/user.cgi",
	grid		=> "http://$apache_server_host/opensim/grid.cgi",
	asset		=> "http://$apache_server_host/opensim/asset.cgi",
	inventory	=> "http://$apache_server_host/opensim/inventory.cgi",
); 

our %OPENSIM_SERVERS = (
	user		=> "http://$opensim_server_host:8002",
	grid		=> "http://$opensim_server_host:8001",
	asset		=> "http://$opensim_server_host:8003",
	inventory	=> "http://$opensim_server_host:8004",
); 

sub registerHandler {
	my ($name, $func) = @_;
	$HANDLER_LIST{$name} = $func;
}


1;

