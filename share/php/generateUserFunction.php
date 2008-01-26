<?php
	// GenerateUser (v1.0)
	// Creates a new user account, and returns it into an associative array.
	// --
	// $firstname - The users firstname
	// $lastname - The users lastname
	// $password - the users password
	// $home - the regionhandle of the users home location
	// --
	function generateUser($firstname,$lastname,$password,$home) {
		$user = array();
		$user['UUID'] = sprintf( '%04x%04x-%04x-%04x-%04x-%04x%04x%04x',
			mt_rand( 0, 0xffff ), mt_rand( 0, 0xffff ), mt_rand( 0, 0xffff ),
			mt_rand( 0, 0x0fff ) | 0x4000,
			mt_rand( 0, 0x3fff ) | 0x8000,
			mt_rand( 0, 0xffff ), mt_rand( 0, 0xffff ), mt_rand( 0, 0xffff ) );
		$user['username'] = $firstname;
		$user['lastname'] = $lastname;
		
		$user['passwordSalt'] = md5(microtime() . mt_rand(0,0xffff));
		$user['passwordHash'] = md5(md5($password) . ":" . $user['passwordSalt']);
		
		$user['homeRegion'] = $home;
		$user['homeLocationX'] = 128;
		$user['homeLocationY'] = 128;
		$user['homeLocationZ'] = 128;
		$user['homeLookAtX'] = 15;
		$user['homeLookAtY'] = 15;
		$user['homeLookAtZ'] = 15;
		
		$user['created'] = time();
		$user['lastLogin'] = 0;
		
		$user['userInventoryURI'] = "http://inventory.server.tld:8004/";
		$user['userAssetURI'] = "http://asset.server.tld:8003/";
		
		$user['profileCanDoMask'] = 0;
		$user['profileWantDoMask'] = 0;
		$user['profileAboutText'] = "I am a user.";
		$user['profileFirstText'] = "Stuff.";
		$user['profileImage'] = sprintf( '%04x%04x-%04x-%04x-%04x-%04x%04x%04x', 0, 0, 0, 0, 0, 0, 0, 0 );
		$user['profileFirstImage'] = sprintf( '%04x%04x-%04x-%04x-%04x-%04x%04x%04x', 0, 0, 0, 0, 0, 0, 0, 0 );
		
		return $user;
	}
?>