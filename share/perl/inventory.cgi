#!/usr/bin/perl -w

use strict;
use MyCGI;
use OpenSim::Config;
use OpenSim::InventoryServer;
use Carp;

my $request_uri = $ENV{REQUEST_URI} || Carp::croak($OpenSim::Config::SYS_MSG{FATAL});
my $request_method = "";
if ($request_uri =~ /([^\/]+)\/$/) {
	$request_method = $1;
} else {
	&MyCGI::outputXml("utf-8", $OpenSim::Config::SYS_MSG{FATAL});
}
my $param = &MyCGI::getParam();
my $post_data = $param->{'POSTDATA'};
&OpenSim::Utility::Log("inv", "request", $request_uri, $post_data);
my $response = "";
eval {
	$response = &handleRequest($request_method, $post_data);
};
if ($@) {
	$response = "<ERROR>$@</ERROR>";
}
&OpenSim::Utility::Log("inv", "response", $response);
&MyCGI::outputXml("utf-8", $response);

sub handleRequest {
    my ($methodname, $post_data) = @_;
    my $handler_list = &OpenSim::InventoryServer::getHandlerList();
    if (!$handler_list->{$methodname}) {
		Carp::croak("unknown method name");
    } else {
		my $handler = $handler_list->{$methodname};
		return $handler->($post_data);
    }
}

