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

####################################################################

#
# User provided interface routine to interface with payment processor
#

function process_transaction($avatarId, $amount, $ipAddress)
{
	# Do Credit Card Processing here!  Return False if it fails!
	# Remember, $amount is stored without decimal places, however it's assumed
	# that the transaction amount is in Cents and has two decimal places
	# 5 dollars will be 500
	# 15 dollars will be 1500

	return True;
}

###################### No user serviceable parts below #####################

#
# Helper routines
#

function convert_to_real($currency)
{
	return 0;
}

function update_simulator_balance($agentId)
{
	$db = new DB;
	$sql = "select serverIP, serverHttpPort from Presence ".
			"inner join regions on regions.uuid = Presence.RegionID ".
			"where Presence.UserID = '".$db->escape($agentId)."'";

	$db->query($sql);
	$results = $db->next_record();
	if ($results)
	{
		$serverIp = $results["serverIP"];
		$httpport = $results["serverHttpPort"];
	

		$req      = array('agentId'=>$agentId);
		$params   = array($req);

		$request  = xmlrpc_encode_request('balanceUpdateRequest', $params);
		$response = do_call($serverIp, $httpport, $request); 
	}
}

function user_alert($agentId, $soundId, $text)
{
    $db = new DB;
    $sql = "select serverIP, serverHttpPort, regionSecret from Presence ".
			"inner join regions on regions.uuid = Presence.RegionID ".
			"where Presence.UserID = '".$db->escape($agentId)."'";
    
    $db->query($sql);

    $results = $db->next_record();
    if ($results)
    {
        $serverIp = $results["serverIP"];
        $httpport = $results["serverHttpPort"];
		$secret   = $results["regionSecret"];
        
        
        $req = array('agentId'=>$agentId, 'soundID'=>$soundId,
				'text'=>$text, 'secret'=>$secret);

        $params = array($req);

        $request = xmlrpc_encode_request('userAlert', $params);
        $response = do_call($serverIp, $httpport, $request);
    }
}

function move_money($sourceId, $destId, $amount, $aggregatePermInventory,
		$aggregatePermNextOwner, $flags, $transactionType, $description,
		$regionGenerated,$ipGenerated)
{
	$db = new DB;
	
	# select current region
	$sql = "select RegionID from Presence ".
			"where UserID = '".$db->escape($destId)."'";
    
    $db->query($sql);

    $results = $db->next_record();
    if ($results)
    {
        $currentRegion = $results["currentRegion"];
	 }
}

function get_balance($avatarId)
{
    $db=new DB;

    $cash = 0;

    return (integer)$cash;
}

function do_call($host, $port, $request)
{
    $url = "http://$host:$port/";
    $header[] = "Content-type: text/xml";
    $header[] = "Content-length: ".strlen($request);
    
    $ch = curl_init();   
    curl_setopt($ch, CURLOPT_URL, $url);
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
    curl_setopt($ch, CURLOPT_TIMEOUT, 1);
    curl_setopt($ch, CURLOPT_HTTPHEADER, $header);
    curl_setopt($ch, CURLOPT_POSTFIELDS, $request);
    
    $data = curl_exec($ch);       
    if (!curl_errno($ch))
	{
        curl_close($ch);
        return $data;
    }
}

function agent_name($agentId)
{
	$db=new DB;

	$sql="select FirstName, LastName from UserAccounts where PrincipalID='".$agentId."'";
	$db->query($sql);

	$record=$db->next_record();
	if(!$record)
		return "";

	$name=implode(" ", array($record[0], $record[1]));

	return $name;
}
?>
