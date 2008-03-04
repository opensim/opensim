#!/usr/bin/perl -w

use strict;
use Carp;
use MyCGI;
use XML::RPC;
use MonitorGUI::View;

use vars qw ($THIS_URL $GRID_SERVER_URL $DEFAULT_PROXY_PORT);
$THIS_URL = "http://10.8.1.165/monitorgui/monitor.cgi";
$GRID_SERVER_URL = "http://10.8.1.165/opensim/grid.cgi";
$DEFAULT_PROXY_PORT = 9000;

my %ACTIONS = (
    # Region commands
    move  => \&move_command,
    split => \&split_command,
    merge => \&merge_command,
    # display commands
    default => \&main_screen,
    refresh => \&refresh,
);

# ##################
# main
my $param = &MyCGI::getParam;
my $act = $param->{A} || "default";
my $contents = "";
if (!$ACTIONS{$act}) {
    &gui_error("404 NOT FOUND");
} else {
    eval {
		$ACTIONS{$act}->($param);
    };
    if ($@) {
		&gui_error($@);
    }
}

# #################
# Region Commands
sub move_command {
    my $param = shift;
	# from
	my $from_ip = $param->{from_ip} || Carp::croak("not enough params (from_ip)");
	my $from_port = $param->{from_port} || Carp::croak("not enough params (from_port)");
	my $from_url = "http://" . $param->{from_ip} . ":" . $DEFAULT_PROXY_PORT;
	# to
	my $to_ip = $param->{to_ip} || Carp::croak("not enough params (to_ip)");
	my $to_port = $param->{to_port} || Carp::croak("not enough params (to_port)");
	my $to_url = "http://" . $param->{to_ip} . ":" . $DEFAULT_PROXY_PORT;
	# commands
	eval {
		&OpenSim::Utility::XMLRPCCall_array($from_url, "SerializeRegion", [$from_ip, $from_port]);
		&OpenSim::Utility::XMLRPCCall_array($to_url, "DeserializeRegion_Move", [$from_ip, $from_port, $to_ip, $to_port]);
		&OpenSim::Utility::XMLRPCCall_array($from_url, "TerminateRegion", [$from_port]);
	};			
	if ($@) {
	   	print STDERR "Get Status Error: $@\n";
	}

	# client refresh
    &redirect_refresh({wait=>5, force=>"$from_url|$to_url", msg=>"Move region $from_ip:$from_port from $from_url to $to_url"});
}

sub split_command {
    my $param = shift;
	# from
	my $from_ip = $param->{from_ip} || Carp::croak("not enough params (from_ip)");
	my $from_port = $param->{from_port} || Carp::croak("not enough params (from_port)");
	my $from_url = "http://" . $param->{from_ip} . ":" . $DEFAULT_PROXY_PORT;
	# to
	my $to_ip = $param->{to_ip} || Carp::croak("not enough params (to_ip)");
	my $to_port = $param->{to_port} || Carp::croak("not enough params (to_port)");
	my $to_url = "http://" . $param->{to_ip} . ":" . $DEFAULT_PROXY_PORT;
	# commands
	eval {
		&OpenSim::Utility::XMLRPCCall_array($from_url, "SerializeRegion", [$from_ip, $from_port]);
		&OpenSim::Utility::XMLRPCCall_array($to_url, "DeserializeRegion_Clone", [$from_ip, $from_port, $to_ip, $to_port]);
	};			
	if ($@) {
	   	print STDERR "Get Status Error: $@\n";
	}

    &redirect_refresh({wait=>5, force=>"$from_url", msg=>"Split region $from_ip:$from_port"});
}

sub merge_command {
    my $param = shift;
	# from
	my $from_ip = $param->{from_ip} || Carp::croak("not enough params (from_ip)");
	my $url = "http://" . $param->{from_ip} . ":" . $DEFAULT_PROXY_PORT;
	# ports
	my $master_port = $param->{master_port} || Carp::croak("not enough params (master_port)");
	my $slave_ip = $param->{slave_ip} || Carp::croak("not enough params (slave_ip)");
	my $slave_port = $param->{slave_port} || Carp::croak("not enough params (slave_port)");
	my $slave_url = "http://" . $param->{slave_ip} . ":" . $DEFAULT_PROXY_PORT;
	# commands
	eval {
		&XMLRPCCall_array($url, "MergeRegions", [$from_ip, $master_port]);
		&XMLRPCCall_array($slave_url, "TerminateRegion", [$slave_port]);
	};
	if ($@) {
	   	print STDERR "Get Status Error: $@\n";
	}
	&redirect_refresh({wait=>5, force=>"$url", msg=>"Merge region $from_ip:$master_port, $slave_port"});
}

# #################
# Display
sub main_screen {
    my %xml_rpc_param = (
		# TODO: should be 0 - 65535 ?
		xmin => 999, ymin => 999, xmax => 1010, ymax => 1010,
	);
	my $res_obj = undef; 
    eval {
		$res_obj = &XMLRPCCall($GRID_SERVER_URL, "map_block", \%xml_rpc_param);
    };
    if ($@) {
		&gui_error("map_block Error: " . $@);
    }
	my %copy_obj = %$res_obj;
	my $getstatus_failed = "<font color=\"red\">GetStatus Failed</font>";
	my $regions_list = $res_obj->{"sim-profiles"};
	foreach(@$regions_list) {
		if ($_->{sim_ip} && $_->{sim_port}) {
			my $url = "http://" . $_->{sim_ip} . ":" . $DEFAULT_PROXY_PORT;
			my $port = $_->{sim_port};
			my $res = undef;
		    eval {
				$res = &XMLRPCCall_array($url, "GetStatus", [$port]);
		    };			
		    if ($@) {
		    	print STDERR "Get Status Error: $@\n";
		    }
			$_->{get_scene_presence_filter} = $res ? $res->{get_scene_presence_filter} : $getstatus_failed;
			$_->{get_scene_presence} = $res ? $res->{get_scene_presence} : $getstatus_failed;
			$_->{get_avatar_filter} = $res ? $res->{get_avatar_filter} : $getstatus_failed;
			$_->{get_avatar} = $res ? $res->{get_avatar} : $getstatus_failed;
			$_->{avatar_names} = $res ? $res->{avatar_names} : "NO USER";
		}
	}
	my $html = &MonitorGUI::View::html(\%copy_obj);
    &MyCGI::outputHtml("UTF-8", &MonitorGUI::View::screen_header . $html . &MonitorGUI::View::screen_footer);
}

sub gui_error {
    my $msg = shift;
    &MyCGI::outputHtml("UTF-8", "<h1>ERROR</h1><hr />$msg");
}

sub redirect_refresh {
    my $args = shift;
    my $wait = $args->{wait};
    my $force = $args->{force} || "";
    my $msg = $args->{msg} || "";
    my $param = "A=refresh&wait=$wait&ip=$force&msg=$msg";
    my $dist_url = $THIS_URL . "?" . $param;
    &MyCGI::redirect($dist_url);
}

sub refresh {
    my $param = shift;
    my $msg = $param->{msg} || "";
    my $wait = $param->{wait} || 0;
    my $force = $param->{ip} || "";
    #my $jump_url = $force ? "$THIS_URL?A=force&ip=$force" : $THIS_URL;
    my $jump_url = $THIS_URL;
    my $html =<< "HTML";
<html>
<head>
<meta http-equiv="Refresh" content="$wait;URL=$jump_url" />
<title>Region Monitor GUI REFRESH</title>
</head>
<body>
<h3>$msg</h3>
<br>
wait <font color="red"><b>$wait</b></font> sec for server to take effect ... <br>
(* The page will jump to "Monitor Screen" automatically)
</body>
</html>
HTML
    &MyCGI::outputHtml("UTF-8", $html);
}

# ##################
# Utility
sub XMLRPCCall {
    my ($url, $methodname, $param) = @_;
    my $xmlrpc = new XML::RPC($url);
    my $result = $xmlrpc->call($methodname, $param);
    return $result;
}

sub XMLRPCCall_array {
    my ($url, $methodname, $param) = @_;
    my $xmlrpc = new XML::RPC($url);
    my $result = $xmlrpc->call($methodname, @$param);
    return $result;
}

