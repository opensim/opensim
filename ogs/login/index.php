<?
error_reporting(0); // Remember kids, PHP errors kill XML-RPC responses!

// include all the common stuff
include("../common/xmlrpc.inc.php");
include("../common/database.inc.php");
include("../common/grid_config.inc.php");
include("../common/util.inc.php");

include("login_config.inc.php"); // include login/user specific config stuff (authentication keys etc)

function login($args) {
    global $dbhost,$dbuser,$dbpasswd,$dbname;
    global $grid_owner, $gridserver_sendkey, $gridserver_recvkey, $gridserver_url;
    

    if(get_magic_quotes_gpc()) {
	    $firstname=addslashes($args['first']);
	    $lastname=addslashes($args['last']);
	    $passwd=addslashes($args['passwd']);
    } else {
	    $firstname=$args['first'];
	    $lastname=$args['last'];
	    $passwd=$args['passwd'];
    }

    $link = mysql_connect($dbhost,$dbuser,$dbpasswd)
     OR die("Unable to connect to database");
     
    mysql_select_db($dbname)
     or die("Unable to select database");
     
    $query = "SELECT userprofile_LLUUID, profile_firstname, profile_lastname, profile_passwdmd5, homesim_ip, homesim_port, homeasset_url, look_at, region_handle, position FROM local_user_profiles WHERE profile_firstname='".$firstname."' AND profile_lastname='".$lastname."' AND profile_passwdmd5='" .$passwd."'";

    $profile_lookup_result=mysql_query($query);

    if(mysql_num_rows($profile_lookup_result) >0) {
	$profiledata = mysql_fetch_assoc($profile_lookup_result);
	
	// if we get here, the username/password is valid, but still need to check there's not an already existing session
	$client = new IXR_Client($gridserver_url);
	if (!$client->query('check_session_loggedin', Array('userprofile_LLUUID' => $profiledata['userprofile_LLUUID'], 'authkey' => $gridserver_sendkey, 'server_type' => 'login'))) { // if this doesn't work, grid server is down - that's bad
	    return Array (
		'reason' => 'key',
		'message' => "Could not connect to grid server. Please try again later or contact the grid owner ". $grid_owner,
		'login' => "false"
	    );
	}
	
	$response=$client->getResponse();
	if($response['authkey'] != $gridserver_recvkey) { // if this doesn't match up, it's a fake grid server
	    return Array (
		'reason' => 'key',
		'message' => "Could not connect to grid server due to possible security issues. It is possible that the grid has been compromised. Please contact the grid owner " . $grid_owner . " and report this issue",
		'login' => "false"
	    );
	}
	
	
	if($response['logged_in'] == 1) { // if the user is already logged in, tell them
	    return Array (
		'reason' => 'presence',
		'message' => "You appear to already be logged into this grid, if your client has recently crashed then please try again later",
		'login' => "false"
	    );
	}
	    
	// now we start a new session on the grid
	$remote_ip=$_SERVER['REMOTE_ADDR'];
	$region_handle=$profiledata['region_handle'];
	$client->query('create_session',Array('userprofile_LLUUID' => $profiledata['userprofile_LLUUID'], 'authkey' => $gridserver_sendkey, 'remote_ip' => $remote_ip, 'current_location' => $region_handle));
	$response = $client->getResponse();
	$session_id = $response['session_id'];
	$secure_session_id = $response['secure_session_id'];
	
	// ask the grid server what the IP address and port of the sim we want to connect to is
	$client->query('get_sim_info', Array('region_handle' => $region_handle, 'authkey' => $gridserver_sendkey) );
	$siminfo = $client->getResponse();
	
	// send the final response!
	$position=$profiledata['position'];
	$look_at=$profiledata['look_at'];
	
	$LocX=intval($siminfo['GridLocX'])*256;
	$LocY=intval($siminfo['GridLocY'])*256;
	$home="{'region_handle':'$region_handle', 'position':'$position', 'look_at':'$look_at'}";

	$globaltextures = new LLBlock(
		Array(
	            'sun_texture_id' => "cce0f112-878f-4586-a2e2-a8f104bba271",
        	    'cloud_texture_id' => "fc4b9f0b-d008-45c6-96a4-01dd947ac621",
		    'moon_texture_id' => "d07f6eed-b96a-47cd-b51d-400ad4a1c428"
		));

	$login_flags = new LLBlock(
		Array(
		    'stipend_since_login' => "N",
	            'ever_logged_in' => "Y",
                    'gendered' => "Y",
                    'daylight_savings' => "N"
		));
	$ui_config = new LLBlock(
		Array(
		    'allow_first_life' => "Y"
		));
	$inventory_skeleton = new LLBlock(Array(
         	Array(
                   'name' => 'My inventory',
                   'parent_id' => '00000000-0000-0000-0000-000000000000',
                   'version' => 4,
                   'type_default' => 8,
                   'folder_id' => 'f798e114-c10f-409b-a90d-a11577ff1de8'
                ),
         	Array(
                   'name' => 'Textures',
                   'parent_id' => 'f798e114-c10f-409b-a90d-a11577ff1de8',
                   'version' => 1,
                   'type_default' => 0,
                   'folder_id' => 'fc8b4059-30bb-43a8-a042-46f5b431ad82'
                )));
	$inventory_root = new LLBlock(
	    Array(
		'folder_id' => "f798e114-c10f-409b-a90d-a11577ff1de8"
	    ));
	$initial_outfit = new LLBlock(
	    Array(
		'folder_name' => "Nightclub Female",
		'gender' => "female"
	    ));	
	return Array (
         'message' => "Welcome to OGS!",
         'session_id' => format_lluuid($session_id),
         'sim_port' => intval($siminfo['port']),
         'agent_access' => "M",
         'start_location' => "last",
         'global-textures' => $globaltextures,
	 'seconds_since_epoch' => time(),
         'first_name' => $profiledata['profile_firstname'],
         'circuit_code' => 50633318,
         'login_flags' => $login_flags,
         'seed_capability' => '',
         'home' => $home,
         'secure_session_id' => format_lluuid($secure_session_id),
         'last_name' => $profiledata['profile_lastname'],
         'ui-config' => $ui_config,
         'region_x' => $LocX,
         'inventory_skeleton' => $inventory_skeleton,
         'sim_ip' => $siminfo['ip_addr'],
         'region_y' => $LocY,
         'inventory-root' => $inventory_root,
         'login' => "true",
         'look_at' => $look_at,
         'agent_id' => format_lluuid($profiledata['userprofile_LLUUID']),
         'initial-outfit' => $initial_outfit
        );

	
    } else {
	// this is the default invalid username/password error
	return Array (
	    'reason' => 'key',
	    'message' => "You have entered an invalid name/password combination or are using an incompatible client. Please check with the grid owner " .$grid_owner . " if you are sure your login details are accurate.",
	    'login' => "false",
	);
    }
    
}

$server=new IXR_Server(array('login_to_simulator' => 'login'));
?>
