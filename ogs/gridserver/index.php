<?
error_reporting(E_ALL); // yes, we remember this from the login server, don't we boys and girls? don't kill innocent XML-RPC!

// these files are soooo common..... (to the grid)
include("../common/xmlrpc.inc.php");
include("../common/database.inc.php");
include("../common/grid_config.inc.php");
include("../common/util.inc.php");

include("gridserver_config.inc.php"); // grid server specific config stuff

function get_sim_info($args) {
    global $dbhost,$dbuser,$dbpasswd,$dbname;
    global $userserver_sendkey, $userserver_recvkey;

    // First see who's talking to us, if key is invalid then send an invalid one back and nothing more
    if($args['authkey']!=$userserver_recvkey) {
        return Array(
            'authkey' => 'I can play the bad key trick too you know',
	    'login' => 'false'
        );
    }

    // if we get to here, the key is valid, give that login server what it wants!

    $link = mysql_connect($dbhost,$dbuser,$dbpasswd)
     OR die("Unable to connect to database");
     
     mysql_select_db($dbname)
      or die("Unable to select database");

    $region_handle = $args['region_handle'];
    $query = "SELECT * FROM region_profiles WHERE region_handle='$region_handle'";
    $result = mysql_query($query);
    
    return mysql_fetch_assoc($result);
}

function get_session_info($args) {
    global $dbhost,$dbuser,$dbpasswd,$dbname;
    global $sim_sendkey, $sim_recvkey;

    // authkey, session-id, agent-id
    	
    // First see who's talking to us, if key is invalid then send an invalid one back and nothing more
    if($args[0]!=$sim_recvkey) {
	return Array(
	    'authkey' => "I can play the bad key trick too you know" 
	);
    }

    $link = mysql_connect($dbhost,$dbuser,$dbpasswd)
     OR die("Unable to connect to database");
     
     mysql_select_db($dbname)
      or die("Unable to select database");

    $session_id = $args[1];
    $agent_id = $args[2];
    
    $query = "SELECT * FROM sessions WHERE session_id = '$session_id' AND agent_id='$agent_id' AND session_active=1";
    $result = mysql_query($query);
    if(mysql_num_rows($result)>0) {
	$info=mysql_fetch_assoc($result);
	$circuit_code = $info['circuit_code'];
	$secure_session_id=$info['secure_session_id'];

	$query = "SELECT * FROM local_user_profiles WHERE userprofile_LLUUID='$agent_id'";
	$result=mysql_query($query);
	$userinfo=mysql_fetch_assoc($result);
	$firstname=$userinfo['profile_firstname'];
	$lastname=$userinfo['profile_lastname'];
	$agent_id=$userinfo['userprofile_LLUUID'];
	return Array(
	    'authkey' => $sim_sendkey,
	    'circuit_code' => $circuit_code,
	    'agent_id' => $agent_id,
	    'session_id' => $session_id,
	    'secure_session_id' => $secure_session_id,
	    'firstname' => $firstname,
	    'lastname' => $lastname
	);
    }
}

function check_loggedin($args) {
    global $dbhost,$dbuser,$dbpasswd,$dbname;
    global $userserver_sendkey, $userserver_recvkey;
	
    // First see who's talking to us, if key is invalid then send an invalid one back and nothing more
    if($args['authkey']!=$userserver_recvkey) {
	return Array(
	    'authkey' => "I can play the bad key trick too you know" 
	);
    }

    // if we get to here, the key is valid, give that login server what it wants!

    $link = mysql_connect($dbhost,$dbuser,$dbpasswd)
     OR die("Unable to connect to database");
     
     mysql_select_db($dbname)
      or die("Unable to select database");

    $userprofile_LLUUID = $args['userprofile_LLUUID'];
    $query = "SELECT * FROM sessions WHERE agent_id='$userprofile_LLUUID' AND session_active=1";
    $result = mysql_query($query);

    if(mysql_num_rows($result)>1) {
        return Array(
	    'authkey' => $userserver_sendkey,
	    'logged_in' => 1
        );
    } else {
	return Array(
	    'authkey' => $userserver_sendkey,
	    'logged_in' => 0
	);
    }
}

function create_session($args) {
    global $dbhost,$dbuser,$dbpasswd,$dbname;
    global $userserver_sendkey, $userserver_recvkey;

    // First see who's talking to us, if key is invalid then send an invalid one back and nothing more
    if($args['authkey']!=$userserver_recvkey) {
	return Array(
	    'authkey' => "I can play the bad key trick too you know" 
	);
    }

    // if we get to here, the key is valid, give that login server what it wants!

    $link = mysql_connect($dbhost,$dbuser,$dbpasswd)
     OR die("Unable to connect to database");
     
     mysql_select_db($dbname)
      or die("Unable to select database");

    // yes, secure_sessionid should be different, i know...
    $query = "SELECT value FROM Grid_settings WHERE setting='highest_LLUUID'";
    $result = mysql_query($query);
    $row = mysql_fetch_array($result);
    $highest_LLUUID = $row['value'];
    $newsession_id=inc_lluuid($highest_LLUUID);
    $secure_session_id=inc_lluuid($newsession_id);
    
    $query="UPDATE Grid_settings SET value='$secure_session_id' WHERE setting='highest_LLUUID' LIMIT 1";
    $result=mysql_query($query);
    
    $userprofile_LLUUID=$args['userprofile_LLUUID'];
    $current_location=$args['current_location'];
    $remote_ip=$args['remote_ip'];
    $query="INSERT INTO sessions(session_id,secure_session_id,agent_id,session_start,session_active,current_location,remote_ip) VALUES('$newsession_id','$secure_session_id','$userprofile_LLUUID',NOW(),1,'$current_location','$remote_ip')";
    $result=mysql_query($query);
    if(!isset($result)) {
	die();
    }
    return Array(
	'authkey' => $userserver_sendkey,
	'session_id' => $newsession_id,
	'secure_session_id' => $secure_session_id
    );    
}

$server=new IXR_Server(
    Array(
	'check_session_loggedin' => 'check_loggedin',
	'create_session' => 'create_session',
	'get_sim_info' => 'get_sim_info',
	'get_session_info' => 'get_session_info'
    )
);

?>