#!/usr/bin/perl -w

use strict;
use Carp;
use MyCGI;
use OpenSim::Config;
use OpenSim::Utility;
use OpenSim::AssetServer;

# !!
# TODO: ERROR code
#
my $param = &MyCGI::getParam();
my $response = "<ERROR />";
if ($ENV{REQUEST_METHOD} eq "POST") {
	my $request = $param->{'POSTDATA'};
	#&OpenSim::Utility::Log("asset", "request", $ENV{REQUEST_URI}, $request);
	$response = &OpenSim::AssetServer::saveAsset($request);
} else { # get
	eval {
		my $rest_param = &getRestParam();
		#&OpenSim::Utility::Log("asset", "request", $ENV{REQUEST_URI});
		my $rest_param_count = @$rest_param;
		if ($rest_param_count < 2) {
			Carp::croak($OpenSim::Config::SYS_MSG{FATAL});
		}
		$response = &OpenSim::AssetServer::getAsset($rest_param->[$#$rest_param], $param);
	};
	if ($@) {
		$response = "<ERROR>$@</ERROR>"; # TODO: better return message needed.
	}
}
#&OpenSim::Utility::Log("asset", "response", $response);
&MyCGI::outputXml("utf-8", $response);

sub getRestParam {
	my $uri = $ENV{REQUEST_URI} || Carp::croak($OpenSim::Config::SYS_MSG{FATAL});
	my ($request_uri, undef) = split(/\?/, $uri);
	$request_uri =~ s/\/$//;
	my @param = split(/\//, $request_uri);
	return \@param;
}

