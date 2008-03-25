package OpenSim::Config;

# REGION keys
our $SIM_RECV_KEY = "";
our $SIM_SEND_KEY = "";
# ASSET server url
#our $ASSET_SERVER_URL = "http://127.0.0.1:8003/";
our $ASSET_SERVER_URL = "http://opensim.wolfdrawer.net:80/asset.cgi";
our $ASSET_RECV_KEY = "";
our $ASSET_SEND_KEY = "";
# USER server url
#our $USER_SERVER_URL = "http://127.0.0.1:8001/";
our $USER_SERVER_URL = "http://opensim.wolfdrawer.net:80/user.cgi";
our $USER_RECV_KEY = "";
our $USER_SEND_KEY = "";
# GRID server url
#our $GRID_SERVER_URL = "http://127.0.0.1:8001/";
our $GRID_SERVER_URL = "http://opensim.wolfdrawer.net:80/grid.cgi";
our $GRID_RECV_KEY = "";
our $GRID_SEND_KEY = "";
# INVENTORY server url
#our $INVENTORY_SERVER_URL = "http://127.0.0.1:8004";
our $INVENTORY_SERVER_URL = "http://opensim.wolfdrawer.net:80/inventory.cgi";
# DB
our $DSN = "dbi:mysql:database=opensim;host=192.168.0.20";
our $DBUSER = "lulu";
our $DBPASS = "1234";

# DEBUG LOG
our $DEBUG_LOGDIR = "/home/lulu/temp/opensim";

# MSG
our %SYS_MSG = (
    FATAL => "You must have been eaten by a wolf.",
    FAIL  => "Late! There is a wolf behind you",
    LOGIN_WELCOME => "Do you fear the wolf ?",
);


1;

