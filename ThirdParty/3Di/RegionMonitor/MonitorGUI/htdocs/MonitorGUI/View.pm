package MonitorGUI::View;

use strict;

my @server_list;
my $max_port;
my $regions;

sub screen_header {
    return << "HEADER";
<HTML>
<HEAD>
<STYLE TYPE="text/css">
<!--
a:link    {font-size: 12pt; text-decoration:none; color:#0000ff ;}
a:visited {font-size: 12pt; text-decoration:none; color:#ff0000 ;}
a:active  {font-size: 12pt; text-decoration:none; color:#00ff00 ;}
a:hover   {font-size: 12pt; text-decoration:underline; color:#ff00ff ;}
td        {font-size: 12pt;border:0;}
th        {background-color:#000000; font-size: 12pt;border:0; color:#FFFFFF; }
tr        {background-color:#FFFFFF; }
b         {font-size: 12pt;}
//table     {background-color:#000000; }
-->
</STYLE>    
<META http-equiv="content-type" content="text/html;charset=UTF-8" />
<META name="refresh" content="300" />
<TITLE>Region Monitor GUI, 3Di</TITLE>
</HEAD>
<BODY>
HEADER
}

sub screen_footer {
    return << "FOOTER";
</BODY>
</HTML>
FOOTER
}

sub html {
	my $grid_info = shift;
	my $regions_list = $grid_info->{"sim-profiles"};
	$regions = undef;
	foreach(@$regions_list) {
		my $ip = $_->{sim_ip} || "UNKNOWN";
		my $port = $_->{sim_port} || "UNKNOWN";
		$regions->{$ip}->{$port} = $_;
		if (!$regions->{max_port} || $regions->{max_port} < $port) {
			$regions->{max_port} = $port;
		}
	}
	@server_list = keys %$regions;
	$max_port = $regions->{max_port};
	my $html = "";
	foreach my $machine (@server_list) {
		next if ($machine eq "max_port");
		$html .= &_machine_view($machine, $regions->{$machine});
	}
	return $html;
}

sub _machine_view {
	my ($ip, $info) = @_;
	my $region_html = "";
	foreach my $region (keys %$info) {
		$region_html .= &_region_html($info->{$region});
	}
	my $html =<< "MACHINE_HTML";
<h3>$ip</h3>
$region_html
<hr size=0 noshade /> 
MACHINE_HTML
}

sub _region_html {
	my $region_info = shift;
	my $name = $region_info->{name} || "UNKNOWN";
	my $x = $region_info->{x} || -1;
	my $y = $region_info->{y} || -1;
	my $ip = $region_info->{sim_ip} || "UNKNOWN";
	my $port = $region_info->{sim_port} || "UNKNOWN";
	my $get_scene_presence_filter = $region_info->{get_scene_presence_filter};
	my $get_scene_presence = $region_info->{get_scene_presence};
	my $get_avatar_filter = $region_info->{get_avatar_filter};
	my $get_avatar = $region_info->{get_avatar};
	my $avatar_names = $region_info->{avatar_names};
	my $action_forms = &_action_forms($region_info);
	my $html = <<"REGION_HTML";
<strong>$name</strong><br/>
$ip:$port | X: $x Y: $y<br/>
<table border="0">
<tr>
<td>get_avatar</td>
<td>$get_avatar</td>
<td></td>
</tr>
<tr>
<td>get_avatar_filter</td>
<td>$get_avatar_filter</td>
<td>$avatar_names</td>
</tr>
<tr>
<td>get_scene_presence</td>
<td>$get_scene_presence</td>
<td></td>
</tr>
<tr>
<td>get_scene_presence_filter</td>
<td>$get_scene_presence_filter</td>
<td></td>
</tr>
</table>
$action_forms
REGION_HTML
	return $html;
}

sub _action_forms {
	my $region_info = shift;
	my $ip = $region_info->{sim_ip};
	my $port = $region_info->{sim_port};
	my $default_input_port = $max_port + 1;
	my $move_to_options = "";
	my $split_to_options = "";
	my $merge_ip_options = "";
	foreach(@server_list) {
		next if ($_ eq "max_port");
		$merge_ip_options .= "<option value=\"$_\">$_\n";
		$split_to_options .= "<option value=\"$_\">$_\n";
		#next if ($_ eq $ip);
		$move_to_options .= "<option value=\"$_\">$_\n";
	}
	my $merge_port_options = "";
	my $merge_disabled = "disabled";

	foreach(keys %{$regions->{$ip}}) {
		next if ($_ eq $port);
		$merge_disabled = "";
	}
#	for(9000..$max_port) { # TODO :
#		next if ($_ eq $port);
#		$merge_port_options .= "<option value=\"$_\">$_\n";
#	}
	my %port = ();
	foreach my $ip (keys %$regions) {
		next if ($ip eq "max_port");
		print STDERR "--" . $ip . "\n";
		foreach my $region_port (keys %{$regions->{$ip}}) {
		print STDERR "---" . $region_port . "\n";
			$port{$region_port} = 1;
		}
	}
	foreach (keys %port) {
		$merge_port_options .= "<option value=\"$_\">$_\n";
		$merge_disabled = "";
	}
	return << "ACTION_FORMS";
<table>
<tr>
<form method="POST">
<td>
<input type="hidden" name="A" value="move" />
<input type="hidden" name="from_ip" value="$ip" />
<input type="hidden" name="from_port" value="$port" />
<input type="submit" value="Move to" />
<select name="to_ip">
$move_to_options
</select>:
<input type="text" name="to_port" size="5" value="$default_input_port"/>
</td>
</form>

<td>
&nbsp;&nbsp;|&nbsp;&nbsp;
</td>

<form method="POST">
<td>
<input type="hidden" name="A" value="split" />
<input type="hidden" name="from_ip" value="$ip" />
<input type="hidden" name="from_port" value="$port" />
<input type="submit" value="Split to" />
<select name="to_ip">
$split_to_options
</select>:
<input type="text" name="to_port" size="5" value="$default_input_port"/>
</td>
</form>

<td>
&nbsp;&nbsp;|&nbsp;&nbsp;
</td>

<form method="POST">
<td>
<input type="hidden" name="A" value="merge" />
<input type="hidden" name="from_ip" value="$ip" />
<input type="hidden" name="master_port" value="$port" />
<input type="submit" value="Merge" $merge_disabled />
<select name="slave_ip" $merge_disabled>
$merge_ip_options
</select>
<select name="slave_port" $merge_disabled>
$merge_port_options
</select>
</td>
</form>
</tr>
</table>
ACTION_FORMS
}

1;
