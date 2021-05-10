<?PHP
# 
#  Copyright (c)Melanie Thielker and Teravus Ovares (http://opensimulator.org/)
# 
#  Redistribution and use in source and binary forms, with or without
#  modification, are permitted provided that the following conditions are met:
#      * Redistributions of source code must retain the above copyright
#        notice, this list of conditions and the following disclaimer.
#      * Redistributions in binary form must reproduce the above copyright
#        notice, this list of conditions and the following disclaimer in the
#        documentation and/or other materials provided with the distribution.
#      * Neither the name of the OpenSim Project nor the
#        names of its contributors may be used to endorse or promote products
#        derived from this software without specific prior written permission.
# 
#  THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
#  EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
#  WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
#  DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
#  DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
#  (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
#  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
#  ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
#  (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
#  SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
# 

########################################################################
# This file enables buying currency in the client.
#
# For this to work, the all clients using currency need to add
#
#                -helperURI <WebpathToThisDirectory>
#
# to the commandline parameters when starting the client!
#
# Example:
#    client.exe -loginuri http://foo.com:8002/ -helperuri http://foo.com/
#
# Don't forget to change the currency conversion value in the wi_economy_money
# table!
#
# This requires PHP curl, XMLRPC, and MySQL extensions.
#
# If placed in the opensimwiredux web directory, it will share the db module
#

#
# These match opensimwiredux
#
include("settings/config.php");
include("settings/mysqli.php");
require("helpers.php");

###################### No user serviceable parts below #####################

#
# The XMLRPC server object
#

$xmlrpc_server = xmlrpc_server_create();

#
# Viewer communications section
#
# Functions in this section are called by the viewer directly. Names and
# parameters are determined by the viewer only.
#

#
# Viewer retrieves currency buy quote
#

xmlrpc_server_register_method($xmlrpc_server, "getCurrencyQuote",
		"get_currency_quote");

function get_currency_quote($method_name, $params, $app_data)
{
	$confirmvalue = "1234567883789";

	$req       = $params[0];

	$agentid   = $req['agentId'];
	$sessionid = $req['secureSessionId'];
	$amount    = $req['currencyBuy'];

	#
	# Validate Requesting user has a session
	#

	$db = new DB;
	$db->query("select UserID from Presence where ".
			"UserID='".           $db->escape($agentid).  "' and ".
			"SecureSessionID='".$db->escape($sessionid)."'");

	list($UUID) = $db->next_record();

	if($UUID)
	{
		$estimatedcost = convert_to_real($amount);
		
		$currency = array('estimatedCost'=>$estimatedcost,
				'currencyBuy'=>$amount);
		
		header("Content-type: text/xml");
		$response_xml = xmlrpc_encode(array(
				'success'  => True,
				'currency' => $currency,
				'confirm'  => $confirmvalue));

		print $response_xml;
	}
	else
	{
		header("Content-type: text/xml");
		$response_xml = xmlrpc_encode(array(
				'success'      => False,
				'errorMessage' => "Unable to Authenticate\n\nClick URL for more info.",
				'errorURI'     => "".SYSURL.""));

		print $response_xml;
	}

	return "";
}

#
# Viewer buys currency
#

xmlrpc_server_register_method($xmlrpc_server, "buyCurrency", "buy_currency");

function buy_currency($method_name, $params, $app_data)
{
	global $economy_source_account;
	global $minimum_real;
	global $low_amount_error;

	$req       = $params[0];

	$agentid   = $req['agentId'];
	$regionid  = $req['regionId'];
	$sessionid = $req['secureSessionId'];
	$amount    = $req['currencyBuy'];
	$ipAddress = $_SERVER['REMOTE_ADDR'];

	#
	# Validate Requesting user has a session
	#

	$db = new DB;
	$db->query("select UserID from Presence where ".
			"UserID='".           $db->escape($agentid).  "' and ".
			"SecureSessionID='".$db->escape($sessionid)."'");

	list($UUID) = $db->next_record();

	if($UUID)
	{
		$cost = convert_to_real($amount);

		if($cost < $minimum_real)
		{
			$error=sprintf($low_amount_error, $minimum_real/100.0);

			header("Content-type: text/xml");
			$response_xml = xmlrpc_encode(array(
					'success'      => False,
					'errorMessage' => $error,
					'errorURI'     => "".SYSURL.""));

			print $response_xml;

			return "";
		}

		$transactionResult = process_transaction($agentid,$cost,$ipAddress);
		
		if ($transactionResult)
		{
			header("Content-type: text/xml");
			$response_xml = xmlrpc_encode(array(
					'success' => True));

			print $response_xml;

			move_money($economy_source_account, $agentid, $amount, 0, 0, 0, 0,
					"Currency purchase",$regionid,$ipAddress);

			update_simulator_balance($agentid);
		}
		else
		{
			header("Content-type: text/xml");
			$response_xml = xmlrpc_encode(array(
					'success'      => False,
					'errorMessage' => "We were unable to process the transaction.  The gateway denied your charge",
					'errorURI'     => "".SYSURL.""));

			print $response_xml;
		}
	}
	else 
	{
		header("Content-type: text/xml");
		$response_xml = xmlrpc_encode(array(
					'success'      => False,
					'errorMessage' => "Unable to Authenticate\n\nClick URL for more info.",
					'errorURI'     => "".SYSURL.""));
		print $response_xml;
	}
	
	return "";
}

#
# Region communications section
#
# Functions in this section are called by the region server
#

#
# Region requests account balance
#

xmlrpc_server_register_method($xmlrpc_server, "simulatorUserBalanceRequest",
		"balance_request");

function balance_request($method_name, $params, $app_data)
{
	$req            = $params[0];

	$agentid        = $req['agentId'];
	$sessionid      = $req['secureSessionId'];
	$regionid       = $req['regionId'];
	$secret         = $req['secret'];
	$currencySecret = $req['currencySecret'];

    #
    # Validate region secret
    #

    $db=new DB;
    $sql="select uuid from regions ".
            "where uuid='".  $db->escape($regionid)."' and ".
            "regionSecret='".$db->escape($secret)  ."'";

    $db->query($sql);

    list($region_id) = $db->next_record();

    if ($region_id)
    {
        # We have a region, check agent session

        $sql = "select UserID from Presence ".
                "where UserID='".     $db->escape($agentid)  ."' and ".
                "SecureSessionID='".$db->escape($sessionid)."' and ".
                "RegionID='".  $db->escape($regionid) ."'";

        $db->query($sql);
        list($user_id) = $db->next_record();

        if($user_id)
        {
            $response_xml = xmlrpc_encode(array(
                    'success' => True,
                    'agentId' => $agentid,
                    'funds'   => (integer)get_balance($agentid)));
        }
        else
        {
            $response_xml = xmlrpc_encode(array(
                    'success'      => False,
                    'errorMessage' => "Could not authenticate your avatar. Money operations may be unavailable",
                    'errorURI'     => " "));
        }
    }
    else
    {
        $response_xml = xmlrpc_encode(array(
                'success'      => False,
                'errorMessage' => "This region is not authorized to check your balance. Money operations may be unavailable",
                'errorURI'     => " "));
    }

    header("Content-type: text/xml");
    print $response_xml;

    return "";
}

#
# Region initiates money transfer
#

xmlrpc_server_register_method($xmlrpc_server, "regionMoveMoney",
		"region_move_money");

function region_move_money($method_name, $params, $app_data)
{
	global $economy_sink_account;

	$req                    = $params[0];
	$agentid                = $req['agentId'];
	$sessionid              = $req['secureSessionId'];
	$regionid               = $req['regionId'];
	$secret                 = $req['secret'];
	$currencySecret         = $req['currencySecret'];
	$destid                 = $req['destId'];
	$cash                   = $req['cash'];
	$aggregatePermInventory = $req['aggregatePermInventory'];
	$aggregatePermNextOwner = $req['aggregatePermNextOwner'];
	$flags                  = $req['flags'];
	$transactiontype        = $req['transactionType'];
	$description            = $req['description'];
	$ipAddress              = $_SERVER['REMOTE_ADDR'];

    #
    # Validate region secret
    #

    $db=new DB;
    $sql="select uuid from regions ".
            "where uuid='".  $db->escape($regionid)."' and ".
            "regionSecret='".$db->escape($secret)  ."'";

    $db->query($sql);

    list($region_id) = $db->next_record();

    if ($region_id)
    {
        # We have a region, check agent session

        $sql = "select UserID from Presence ".
                "where UserID='".     $db->escape($agentid)  ."' and ".
                "SecureSessionID='".$db->escape($sessionid)."' and ".
                "RegionID='".  $db->escape($regionid) ."'";

        $db->query($sql);
        list($user_id) = $db->next_record();

        if($user_id)
        {
			if(get_balance($agentid) < $cash)
			{
				$response_xml = xmlrpc_encode(array(
						'success'      => False,
						'errorMessage' => "You do not have sufficient funds for this purchase",
						'errorURI'     => " "));
			}
			else
			{
				if($destid == "00000000-0000-0000-0000-000000000000")
					$destid=$economy_sink_account;

				if($transactiontype == 1101)
				{
					user_alert($agentid, "00000000-0000-0000-0000-000000000000", "You paid L$".$cash." to upload.");
					$description="Asset upload fee";
				}
				else if($transactiontype == 5001) 
				{
					$destName=agent_name($destid);
					$sourceName=agent_name($agentid);

					user_alert($agentid, "00000000-0000-0000-0000-000000000000", "You paid ".$destName." L$".$cash);
					user_alert($destid, "00000000-0000-0000-0000-000000000000", $sourceName." paid you L$".$cash);
				
					$description="Gift";
				}
				else if($transactiontype == 5008) 
				{
					$destName=agent_name($destid);
					$sourceName=agent_name($agentid);

					user_alert($agentid, "00000000-0000-0000-0000-000000000000", "You paid ".$destName." L$".$cash);
					user_alert($destid, "00000000-0000-0000-0000-000000000000", $sourceName." paid you L$".$cash);
				}
				else if($transactiontype == 2) 
				{
					$destName=agent_name($destid);
					$sourceName=agent_name($agentid);

					user_alert($agentid, "00000000-0000-0000-0000-000000000000", "You paid ".$destName." L$".$cash);
					user_alert($destid, "00000000-0000-0000-0000-000000000000", $sourceName." paid you L$".$cash);
				}
				else if($transactiontype == 0) 
				{

					if($destid == $economy_sink_account)
					{
						user_alert($agentid, "00000000-0000-0000-0000-000000000000", "You paid L$".$cash." for a parcel of land.");
					}
					else
					{
						$destName=agent_name($destid);
						$sourceName=agent_name($agentid);

						user_alert($agentid, "00000000-0000-0000-0000-000000000000", "You paid ".$destName." L$".$cash." for a parcel of land.");
						user_alert($destid, "00000000-0000-0000-0000-000000000000", $sourceName." paid you L$".$cash." for a parcel of land");
					}
				
					$description="Land purchase";
				}

				move_money($agentid, $destid, $cash,
						$aggregatePermInventory, $aggregatePermNextOwner,
						$flags, $transactiontype, $description,
						$regionid, $ipAddress);
			
				$response_xml = xmlrpc_encode(array(
						'success'        => True,
						'agentId'        => $agentid,
						'funds'          => get_balance($agentid),
						'funds2'         => get_balance($destid),
						'currencySecret' => " "));
			}
		}
		else
		{
			$response_xml = xmlrpc_encode(array(
					'success'      => False,
					'errorMessage' => "Unable to authenticate avatar. Money operations may be unavailable",
					'errorURI'     => " "));
		}
	}
	else
	{
		$response_xml = xmlrpc_encode(array(
				'success'      => False,
				'errorMessage' => "This region is not authorized to manage your money. Money operations may be unavailable",
				'errorURI'     => " "));
	}

	header("Content-type: text/xml");
	print $response_xml;
	
	$stri = update_simulator_balance($agentid);
	$stri = update_simulator_balance($destid);

	return "";
}

#
# Region claims user
#

xmlrpc_server_register_method($xmlrpc_server, "simulatorClaimUserRequest", "claimUser_func");

function claimUser_func($method_name, $params, $app_data)
{
	$req       = $params[0];
	$agentid   = $req['agentId'];
	$sessionid = $req['secureSessionId'];
	$regionid  = $req['regionId'];
	$secret    = $req['secret'];
	
    #
    # Validate region secret
    #

    $db=new DB;
    $sql="select uuid from regions ".
            "where uuid='".  $db->escape($regionid)."' and ".
            "regionSecret='".$db->escape($secret)  ."'";

    $db->query($sql);

    list($region_id) = $db->next_record();

    if ($region_id)
    {
        # We have a region, check agent session

        $sql = "select UserID from Presence ".
                "where UserID='".     $db->escape($agentid)  ."' and ".
                "SecureSessionID='".$db->escape($sessionid);

        $db->query($sql);
        list($user_id) = $db->next_record();

        if($user_id)
        {
			$sql="update Presence set ".
					"RegionID='".$db->escape($regionid)."' ".
					"where UserID='".   $db->escape($agentid) ."'";

			$db->query($sql);

			$db->next_record();
			$response_xml = xmlrpc_encode(array(
					'success'        => True,
					'agentId'        => $agentid,
					'funds'          => (integer)get_balance($agentid),
					'currencySecret' => " "));
		}
		else
		{
			$response_xml = xmlrpc_encode(array(
					'success'      => False,
					'errorMessage' => "Unable to authenticate avatar. Money operations may be unavailable",
					'errorURI'     => " "));
		}
	}
	else
	{
		$response_xml = xmlrpc_encode(array(
				'success'      => False,
				'errorMessage' => "This region is not authorized to manage your money. Money operations may be unavailable",
				'errorURI'     => " "));
	}

	header("Content-type: text/xml");
	print $response_xml;
	
	return "";
}

#
# Process the request
#

$request_xml = $HTTP_RAW_POST_DATA;
xmlrpc_server_call_method($xmlrpc_server, $request_xml, '');
xmlrpc_server_destroy($xmlrpc_server);

?>
