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
#  (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS#  SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
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
# Land purchase sections
#
# Functions are called by the viewer directly.
#

#
# Land buying functions
#

xmlrpc_server_register_method($xmlrpc_server, "preflightBuyLandPrep",
		"buy_land_prep");

function buy_land_prep($method_name, $params, $app_data)
{

#	$confirmvalue = "password"; # Use this to request password re-entry
	$confirmvalue = "";

	$req          = $params[0];

	$agentid      = $req['agentId'];
	$sessionid    = $req['secureSessionId'];
	$amount       = $req['currencyBuy'];
	$billableArea = $req['billableArea'];

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
		$membership_levels = array(
				'levels' => array(
						'id'          => "00000000-0000-0000-0000-000000000000",
						'description' => "some level"));
	
		$landUse = array(
				'upgrade' => False,
				'action'  => "".SYSURL."");

		$currency = array(
				'estimatedCost' => convert_to_real($amount));

		$membership = array(
				'upgrade' => False,
				'action'  => "".SYSURL."",
				'levels'  => $membership_levels);

		$response_xml = xmlrpc_encode(array(
				'success'    => True,
				'currency'   => $currency,
				'membership' => $membership,
				'landUse'    => $landUse,
				'currency'   => $currency,
				'confirm'    => $confirmvalue));

		header("Content-type: text/xml");
		print $response_xml;
	}
	else
	{
		header("Content-type: text/xml");
		$response_xml = xmlrpc_encode(array(
				'success'      => False,
				'errorMessage' => "\n\nUnable to Authenticate\n\nClick URL for more info.",
				'errorURI'     => "".SYSURL.""));

		print $response_xml;
	}

	return "";
}

#
# Perform the buy
#

xmlrpc_server_register_method($xmlrpc_server, "buyLandPrep", "buy_land");

function buy_land($method_name, $params, $app_data)
{
	global $economy_source_account;

	$req          = $params[0];

	$agentid      = $req['agentId'];
	$sessionid    = $req['secureSessionId'];
	$amount       = $req['currencyBuy'];
	$real         = $req['estimatedCost'];
	$billableArea = $req['billableArea'];
	$ipAddress    = $_SERVER['REMOTE_ADDR'];

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
		if($amount > 0)
		{
			if(!process_transaction($agentid, $real, $ipAddress))
			{
				header("Content-type: text/xml");
				$response_xml = xmlrpc_encode(array(
						'success'      => False,
						'errorMessage' => "\n\nThe gateway has declined your transaction. Please update your payment method and try again later.",
						'errorURI'     => "".SYSURL.""));

				print $response_xml;

				return "";
			}
			move_money($economy_source_account, $agentid, $amount, 0, 0, 0, 0,
			                    "Currency purchase",0,$ipAddress);
		}

		header("Content-type: text/xml");
		
		$response_xml = xmlrpc_encode(array(
				'success' => True));

		print $response_xml;
	}
	else
	{
		header("Content-type: text/xml");
		$response_xml = xmlrpc_encode(array(
				'success'      => False,
				'errorMessage' => "\n\nUnable to Authenticate\n\nClick URL for more info.",
				'errorURI'     => "".SYSURL.""));

		print $response_xml;
	}
	return "";
}

#
# Process XMLRPC request
#

$request_xml = $HTTP_RAW_POST_DATA;
# error_log($request_xml);
xmlrpc_server_call_method($xmlrpc_server, $request_xml, '');
xmlrpc_server_destroy($xmlrpc_server);

?>
