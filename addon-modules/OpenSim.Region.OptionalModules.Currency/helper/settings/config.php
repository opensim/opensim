<?php
/*
 * Copyright (c) 2007, 2008 Contributors, http://opensimulator.org/
 * See CONTRIBUTORS for a full list of copyright holders.
 *
 * See LICENSE for the full licensing terms of this file.
 *
*/

##################### System #########################
define("SYSNAME","Grid Name");
define("SYSURL","http://website.com");
define("SYSMAIL","info@email.com");

$userInventoryURI="http://robusturl.com:8004";
$userAssetURI="http://robusturl.com:8003";

############ Delete Unconfirmed accounts ################
// e.g. 24 for 24 hours  leave empty for no timed delete
$unconfirmed_deltime="24";

###################### Money Settings ####################

// Key of the account that all fees go to:
$economy_sink_account="00000000-0000-0000-0000-000000000000";

// Key of the account that all purchased currency is debited from:
$economy_source_account="00000000-0000-0000-0000-000000000000";

// Minimum amount of real currency (in CENTS!) to allow purchasing:
$minimum_real=0;

// Error message if the amount is not reached:
$low_amount_error="You tried to buy less than the minimum amount of currency. You cannot buy currency for less than US$ %.2f.";

##################### Database ########################
define("C_DB_TYPE","mysql");
//Your Hostname here:
define("C_DB_HOST","localhost");	// Hostname of our MySQL server
//Your Databasename here:
define("C_DB_NAME","database");		// Logical database name on that server
//Your Username from Database here:
define("C_DB_USER","user");			// Database user
//Your Database Password here:
define("C_DB_PASS","password");		// Database user's password

?>
