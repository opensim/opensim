<?php
	// Inventory repair
	// Balpien Hammerer, 2024
	// Creative Commons: CC-BY-SA
	// 2024/08/15 1.0 - Backend grid manager inventory check/fix


error_reporting(E_ALL & ~E_NOTICE & ~E_STRICT & ~E_DEPRECATED);

define('DEBUG', false);

$sqllink = null;
// require '/var/www/html/common.php';

// Set grid specific table names
define('INVENTORYFOLDERS', 'inventoryfolders');
define('INVENTORYITEMS',   'inventoryitems');
define('USERACCOUNTS',     'UserAccounts');

if (!function_exists('ConnectDB'))
{
	function ConnectDB($db)
	{
		// write a function to open a database via mysqli based on its friendly name (e.g. 'robust')
		// The database must contain tables UserAccount, inventoryfolders, inventoryitems
		// Return the link reference or false;
		
		return false;
	}
}

if (!function_exists('CloseDB'))
{
	function CloseDB()
	{
		global $sqllink;
		mysqli_close($sqllink);
		$sqllink = null;
	}
}

if (!function_exists('mkuuid'))
{
	function mkuuid()
	{
		// Compute a uuid
		return  sprintf('%04x%04x-%04x-%04x-%04x-%04x%04x%04x',
				// 32 bits for "time_low"
				random_int(0, 0xffff), random_int(0, 0xffff),

				// 16 bits for "time_mid"
				random_int(0, 0xffff),

				// 16 bits for "time_hi_and_version",
				// four most significant bits holds version number 4
				random_int(0, 0x0fff) | 0x4000,

				// 16 bits, 8 bits for "clk_seq_hi_res",
				// 8 bits for "clk_seq_low",
				// two most significant bits holds zero and one for variant DCE1.1
				random_int(0, 0x3fff) | 0x8000,

				// 48 bits for "node"
				random_int(0, 0xffff), random_int(0, 0xffff), random_int(0, 0xffff)
				);
	}
}


function OpenRobustDB()
{
	global $sqllink;
	$sqllink = ConnectDB('robust');
	if  ($sqllink === false) return false;
	return true;
}

function GetAvatarUUID($aname)
{
	global $sqllink;
	
	$aname = trim($aname);
	
	// Check for UUID (very basic) 00000000-0000-0000-0000-000000000000
	if (strlen($aname) == 36 && substr($aname, 8,1) == '-' && substr($aname, 13,1) == '-' && substr($aname, 18,1) == '-' && substr($aname, 23,1) == '-')
	{
		return [$aname];
	}
	
	if (trim($aname) != '')
	{
		// Single avatar
		$aa = explode(' ', $aname);
		$fn = $aa[0];
		$ln = @$aa[1];
		if ($ln == '') $ln = 'Resident';
	
		$query = "SELECT PrincipalID FROM " .USERACCOUNTS ." WHERE FirstName='$fn' AND LastName='$ln' ";
		$result = mysqli_query($sqllink, $query);
		if (mysqli_num_rows($result) == 0) return false;
		$vars = mysqli_fetch_assoc($result);
		return [$vars['PrincipalID']];
	}
	else
	{
		// All active avatars
		$query = "SELECT PrincipalID FROM " .USERACCOUNTS ." WHERE UserLevel >= 0 ORDER BY Firstname, LastName ";
		$result = mysqli_query($sqllink, $query);
		if (mysqli_num_rows($result) == 0) return false;
		
		$auuids = [];
		while($vars = mysqli_fetch_assoc($result))
		{
			$auuids[] = $vars['PrincipalID'];
		}
		return $auuids;
	}
}

function CheckDuplicateSystemFolders($rootid, $fix)
{
	global $sqllink;
	global $verbose;
	
	$msgs = [];
	
	// Look for duplicate system folders (type >= 0) that point to the root.
	// Change the rest to ordinary folders (type -1) and rename them to duplicate-{foldername}
	$query = "SELECT * FROM " .INVENTORYFOLDERS ." WHERE type >= 0 AND parentFolderID='$rootid' ORDER BY type, folderName, version DESC";
	$result = mysqli_query($sqllink, $query);
	
	// The list is ordered by type, folder name, version (count of next level folders).
	$currftype = '';
	$currfname = '';
	$numfolders = 0;
	while ($vars = mysqli_fetch_assoc($result))
	{
		$ftype = $vars['type'];
		$fname = $vars['folderName'];
		$fid   = $vars['folderID'];
		$fver  = $vars['version'];
		$fparent = $vars['parentFolderID'];
		if ($currftype == '') $currftype = $ftype;
		if ($currfname == '') $currfname = $fname;
		
		if ($currftype != $ftype || $currfname != $fname)
		{
			$currftype = $ftype;
			$currfname = $fname;
			$numfolders = 0;
		}
		
		if ($currfname == $fname)
		{
			$numfolders++;
			if ($numfolders > 1)
			{
				if ($fix)
				{
					$fname = mysqli_real_escape_string($sqllink, $fname);
					$query = "UPDATE " .INVENTORYFOLDERS ." SET type=-1, folderName='Duplicate-$fname' WHERE folderID='$fid' ";
					mysqli_query($sqllink, $query);
					$msgs[] = "Duplicate ($numfolders) type $ftype folderID=$fid fixedname=Duplicate-$fname";
				}
				else
				{
					$msgs[] = "Duplicate ($numfolders) type $ftype folderID=$fid parentID=$fparent subfolders=$fver name=$fname";
				}
			}
			else
			{
				if ($verbose) $msgs[] = "System folder type $ftype folderID=$fid parentID=$fparent subfolders=$fver name=$fname";
			}
		}		
	}
	
	$msgs[] = "";
	return $msgs;
}

function CheckInventory($auuid, $fix=false)
{
	global $sqllink;
	global $verbose;
	
	//  This performs basic integrity tests
	//	Returns [false, msgs...] if failures or [true, msgs...] if inventory is OK.
	//
	$msgs = [];
	
	// Verify uuid is listed in UserAccounts. Issue warning but continue.
	$query = "SELECT FirstName, LastName, UserLevel FROM " .USERACCOUNTS ." WHERE PrincipalID='$auuid' ";
	$result = mysqli_query($sqllink, $query);
	if ($vars = mysqli_fetch_assoc($result))
	{
		$msgs[] = "Checked UUID=$auuid Name={$vars['FirstName']} {$vars['LastName']} ";
	}
	else
	{
		$msgs[] = "Warning: UUID=$auuid does not exist in table " .USERACCOUNTS;
	}
	
	// Find root folder(s)
	$query = "SELECT * FROM " .INVENTORYFOLDERS ." WHERE agentID='$auuid' AND (type=8 OR type=9) ORDER BY type ";
	$result = mysqli_query($sqllink, $query);
	$vars = mysqli_fetch_assoc($result);
	$num = mysqli_num_rows($result);
	if ($num == 0)
	{
		if ($fix)
		{
			$rootid = mkuuid();
			$query = "INSERT INTO " .INVENTORYFOLDERS ." (folderName,type,version,folderID,agentID,parentFolderID) 
						VALUES('My Inventory', 8, 1, '$rootid', '$auuid','00000000-0000-0000-0000-000000000000')";
			mysqli_query($sqllink, $query);
			
			// Rerun select
			$query = "SELECT * FROM " .INVENTORYFOLDERS ." WHERE agentID='$auuid' AND type=8 ";
			$result = mysqli_query($sqllink, $query);
			$vars = mysqli_fetch_assoc($result);
			$num = mysqli_num_rows($result);
			$msgs[] = "Warning: Missing root folder added. Consieder orphan check afterward: --chkorphan='uuid' ";
		}
		else
		{
			$msgs[] = "Warning: Root folder missing - needs adding root then orphan check required: --chkorphan='uuid' ";
			return array_merge([false], $msgs);
		}
	}
	
	if ($num > 1)
	{
		$msgs[] = "Multiple ($num) roots found - manual inspection required";
		
		return array_merge([false], $msgs);
	}
	
	$rtype  = $vars['type'];
	$rootid = $vars['folderID'];
	$rname  = $vars['folderName'];
	
	// One root, could be type 9 (olde version of IAR). Fix it if found.	
	if ($rtype != 8)
	{
		if ($fix)
		{
			$query = "UPDATE " .INVENTORYFOLDERS ." SET type=8 WHERE agentID='$auuid' AND type=9";
			mysqli_query($sqllink, $query);
			$msgs[] = "Warning: Old type 9 root found, converted to type=8";
		}
		else
		{
			$msgs[] = "Warning: Old type 9 root found, needs conversion to type 8";
		}
	}
	if ($verbose) $msgs[] = "Root Folder type=$rtype folderID=$rootid name='$rname' ";
	
	// Cleanup parentFolder of root type 8/9
	$pfolder = $vars['parentFolderID'];
	if ($pfolder != '00000000-0000-0000-0000-000000000000')
	{
		if ($fix)
		{
			$query = "UPDATE " .INVENTORYFOLDERS ." SET parentFolderID='00000000-0000-0000-0000-000000000000' WHERE agentID='$auuid' AND type=$rtype";
			mysqli_query($sqllink, $query);
			$msgs[] = "Warning: type $rtype root parent folder ID was $pfolder, set to null key";
		}
		else
		{
			$msgs[] = "Warning: type $rtype root parent folder ID was $pfolder, should be null key";
		}
	}
	
	// Check the root folder.
	$rc = CheckDuplicateSystemFolders($rootid, $fix);
	$msgs = array_merge($msgs, $rc);
	
	// Check for the My Suitcase folder.
	$query = "SELECT * FROM " .INVENTORYFOLDERS ." WHERE agentID='$auuid' AND type=100 ";
	$result = mysqli_query($sqllink, $query);
	$vars = mysqli_fetch_assoc($result);
	$num = mysqli_num_rows($result);
	if ($num == 0)
	{
		if ($fix)
		{
			$suitid = mkuuid();
			$query = "INSERT INTO " .INVENTORYFOLDERS ." (folderName,type,version,folderID,agentID,parentFolderID) 
						VALUES('My Suitcase', 100, 1, '$suitid', '$auuid','$rootid')";
			mysqli_query($sqllink, $query);
			
			// Rerun select
			$query = "SELECT * FROM " .INVENTORYFOLDERS ." WHERE agentID='$auuid' AND type=100 ";
			$result = mysqli_query($sqllink, $query);
			$vars = mysqli_fetch_assoc($result);
			$num = mysqli_num_rows($result);
			$msgs[] = "Warning: Missing Suitcase folder added. Consieder orphan check afterward: --chkorphan='uuid' ";
		}
		else
		{
			$msgs[] = "My Suitcase folder is missing ";
		}
	}
	
	if ($num > 1)
	{
		if ($fix)
		{
			$msgs[] = "Multiple ($num) roots found - converting one to ordinary folder";
		}
		else
		{
			$msgs[] = "Multiple ($num) roots found";
		}
	}
	
	$rtype  = $vars['type'];
	$suitid = $vars['folderID'];
	$rname  = $vars['folderName'];
	
	if ($verbose) $msgs[] = "My Suitcase Folder type=$rtype folderID=$suitid name='$rname' ";
	
	// Fix parentFolder of My Suitcase type=100
	$pfolder = $vars['parentFolderID'];
	if ($pfolder != $rootid)
	{
		if ($fix)
		{
			$query = "UPDATE " .INVENTORYFOLDERS ." SET parentFolderID='$rootid' WHERE agentID='$auuid' AND type=$rtype";
			mysqli_query($sqllink, $query);
			$msgs[] = "Warning: type $rtype root parent folder ID was $pfolder, set to $rootid";
		}
		else
		{
			$msgs[] = "Warning: type $rtype root parent folder ID was $pfolder, should be $rootid";
		}
	}
	
	// Check the suitcase folder.
	$rc = CheckDuplicateSystemFolders($suitid, $fix);
	$msgs = array_merge($msgs, $rc);
	
	$msgs[] = "Scan Completed";
	if ($verbose) $msgs[] = "";
	return array_merge([true], $msgs);
}

// ===============================================================================================================
	$sqllink = null;
	
	$opts = getopt('v', ['chkinv::','fixinv:'] );

	if (!OpenRobustDB())
	{
		echo "Unable to open grid database";
		exit;
	}
	
	$cmd = '';
	
	if (isset($opts['v']))		$verbose = true;
	else						$verbose = false;
	
	/**/ if (isset($opts['chkinv']))		$cmd = 'chkinv';
	else if (isset($opts['fixinv']))		$cmd = 'fixinv';
	
	if ($cmd == '')
	{
		echo 'missing parameters:  php xinv.php {--chkinv="firstname lastname" | --fixinv="firstname lastname"}' ."\n";
		CloseDB();
		exit;
	}
	
	// php xchkinv.php --chkinv="firstname lastname" or --chkinv="uuid"
	if ($cmd == 'chkinv')
	{
		$auuids = false;
		$aname = $opts['chkinv'];
		// If avatar name is blank, get all active avatars
		$auuids = GetAvatarUUID($aname);
		if (!$auuids)
		{
			echo "Avatar $aname does not exist.\n";
		}
		else
		{
			foreach($auuids AS $auuid)
			{
				//echo "$aname uuid=$auuid\n";
				$rc = CheckInventory($auuid, false);
				
				foreach ($rc as $msg)
				{
					if ($msg === false) echo "Errors\n";
					else if ($msg === true)	echo "Success\n";
					else echo $msg ."\n";
				}
			}
		}
	}
	
	// php xchkinv.php --fixinv="firstname lastname" or --fixinv="uuid"
	else if ($cmd == 'fixinv')
	{
		$aname = $opts['fixinv'];
		$auuid = GetAvatarUUID($aname);
		if (!$auuid)
		{
			error_log("$aname does not exist.");
		}
		else
		{
			$auuid = $auuid[0];
			error_log("Avatar $aname uuid=$auuid");
			$rc = CheckInventory($auuid, true);
			foreach ($rc as $msg)
			{
				if ($msg === false) echo "Errors\n";
				else if ($msg === true)	echo "Success\n";
				else echo $msg ."\n";
			}
		}
	}
	
	CloseDB();
	exit;
?>
