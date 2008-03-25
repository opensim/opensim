package OpenSim::Utility;

use strict;
use XML::RPC;
use XML::Simple;
use Data::UUID;
use DBHandler;
use OpenSim::Config;
use Socket;

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

sub UIntsToLong {
	my ($int1, $int2) = @_;
	return $int1 * 4294967296 + $int2;
}

sub getSimpleResult {
	my ($sql, @args) = @_;
	my $dbh = &DBHandler::getConnection($OpenSim::Config::DSN, $OpenSim::Config::DBUSER, $OpenSim::Config::DBPASS);
	my $st = new Statement($dbh, $sql);
	return $st->exec(@args);
}

sub GenerateUUID {
	my $ug = new Data::UUID();
	my $uuid = $ug->create();
	return $ug->to_string($uuid);
}

sub ZeroUUID {
	return "00000000-0000-0000-0000-000000000000";
}

sub HEX2UUID {
	my $hex = shift;
	Carp::croak("$hex is not a uuid") if (length($hex) != 32);
	my @sub_uuids = ($hex =~ /(\w{8})(\w{4})(\w{4})(\w{4})(\w{12})/);
	return join("-", @sub_uuids);
}

sub BIN2UUID {
	# TODO:
}

sub UUID2HEX {
	my $uuid = shift;
	$uuid =~ s/-//g;
	return $uuid;
}

sub UUID2BIN {
	my $uuid = shift;
	return pack("H*", &UUID2HEX($uuid));
}

sub HttpPostRequest {
	my ($url, $postdata) = @_;
	$url =~ /http:\/\/([^:\/]+)(:([0-9]+))?(\/.*)?/;
	my ($host, $port, $path) = ($1, $3, $4);
	$port ||= 80;
	$path ||= "/";
	my $CRLF= "\015\012";
	my $addr = (gethostbyname($host))[4];
	my $name = pack('S n a4 x8', 2, $port, $addr);
	my $data_len = length($postdata);
	socket(SOCK, PF_INET, SOCK_STREAM, 0);
	connect(SOCK, $name) ;
	select(SOCK); $| = 1; select(STDOUT);
	print SOCK "POST $path HTTP/1.0$CRLF";
	print SOCK "Host: $host:$port$CRLF";
	print SOCK "Content-Length: $data_len$CRLF";
	print SOCK "$CRLF";
	print SOCK $postdata;

	my $ret = "";
	unless (<SOCK>) {
		close(SOCK);
		Carp::croak("can not connect to $url");
	}
	my $header = "";
	while (<SOCK>) {
		$header .= $_;
		last if ($_ eq $CRLF);
	}
	if ($header != /200/) {
		return $ret;
	}
	while (<SOCK>) {
		$ret .= $_;
	}
	return $ret;
}
# TODO : merge with POST
sub HttpGetRequest {
	my ($url) = @_;
	$url =~ /http:\/\/([^:\/]+)(:([0-9]+))?(\/.*)?/;
	my ($host, $port, $path) = ($1, $3, $4);
	$port ||= 80;
	$path ||= "/";
	my $CRLF= "\015\012";
	my $addr = (gethostbyname($host))[4];
	my $name = pack('S n a4 x8', 2, $port, $addr);
	socket(SOCK, PF_INET, SOCK_STREAM, 0);
	connect(SOCK, $name) ;
	select(SOCK); $| = 1; select(STDOUT);
	print SOCK "GET $path HTTP/1.0$CRLF";
	print SOCK "Host: $host:$port$CRLF";
	print SOCK "$CRLF";

	unless (<SOCK>) {
		close(SOCK);
		Carp::croak("can not connect to $url");
	}
	while (<SOCK>) {
		last if ($_ eq $CRLF);
	}
	my $ret = "";
	while (<SOCK>) {
		$ret .= $_;
	}
	return $ret;
}

sub XML2Obj {
	my $xml = shift;
	my $xs = new XML::Simple( keyattr=>[] );
	return $xs->XMLin($xml);
}

sub Log {
	my $server_name = shift;
	my @param = @_;
    open(FILE, ">>" . $OpenSim::Config::DEBUG_LOGDIR . "/" . $server_name . ".log");
	foreach(@param) {
    	print FILE $_ . "\n";
	}
    print FILE "<<<<<<<<<<<=====================\n\n";
    close(FILE);
}

1;

