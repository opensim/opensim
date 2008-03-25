#!/usr/bin/perl -w

use strict;
use Carp;
use XML::RPC;
use MyCGI;
use OpenSim::Utility;
use OpenSim::GridServer;

my $param = &MyCGI::getParam();
my $request = $param->{'POSTDATA'};
#&OpenSim::Utility::Log("grid", "request", $request);
my $xmlrpc = new XML::RPC();
my $response = $xmlrpc->receive($request, \&XMLRPCHandler);
#&OpenSim::Utility::Log("grid", "response", $response);
&MyCGI::outputXml("utf-8", $response);

sub XMLRPCHandler {
    my ($methodname, @param) = @_;
    my $handler_list = &OpenSim::GridServer::getHandlerList();
    if (!$handler_list->{$methodname}) {
	Carp::croak("?");
    } else {
	my $handler = $handler_list->{$methodname};
	$handler->(@param);
    }
}
