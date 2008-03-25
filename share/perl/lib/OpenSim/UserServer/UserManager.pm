package OpenSim::UserServer::UserManager;

use strict;
use Carp;
use OpenSim::Utility;
use OpenSim::UserServer::Config;

sub getUserByName {
	my ($first, $last) = @_;
	my $res = &OpenSim::Utility::getSimpleResult($OpenSim::UserServer::Config::SYS_SQL{select_user_by_name}, $first, $last);
	my $count = @$res;
	my %user = ();
	if ($count == 1) {
		my $user_row = $res->[0];
		foreach (@OpenSim::UserServer::Config::USERS_COLUMNS) {
			$user{$_} = $user_row->{$_} || "";
		}
	} else {
		Carp::croak("user not found");
	}
	return \%user;
}

sub getUserByUUID {
	my ($uuid) = @_;
	my $res = &OpenSim::Utility::getSimpleResult($OpenSim::UserServer::Config::SYS_SQL{select_user_by_uuid}, $uuid);
	my $count = @$res;
	my %user = ();
	if ($count == 1) {
		my $user_row = $res->[0];
		foreach (@OpenSim::UserServer::Config::USERS_COLUMNS) {
			$user{$_} = $user_row->{$_} || "";
		}
	} else {
		Carp::croak("user not found");
	}
	return \%user;
}

sub createUser {
	my $user = shift;
	my @params = ();
	foreach (@OpenSim::UserServer::Config::USERS_COLUMNS) {
		push @params, $user->{$_};
	}
	my $res = &OpenSim::Utility::getSimpleResult($OpenSim::UserServer::Config::SYS_SQL{create_user}, @params);
}

1;
