package OpenSim::GridServer::GridManager;

use strict;
use Carp;
use OpenSim::Utility;
use OpenSim::GridServer::Config;

sub getRegionByUUID {
	my $uuid = shift;
	my $result = &OpenSim::Utility::getSimpleResult($OpenSim::GridServer::Config::SYS_SQL{select_region_by_uuid}, $uuid);
	my $count = @$result;
	if ($count > 0) {
		return $result->[0];
	}
	Carp::croak("can not find region");
}

sub getRegionByHandle {
	my $handle = shift;
	my $result = &OpenSim::Utility::getSimpleResult($OpenSim::GridServer::Config::SYS_SQL{select_region_by_handle}, $handle);
	my $count = @$result;
	if ($count > 0) {
		return $result->[0];
	}
	Carp::croak("can not find region # $handle");
}

sub getRegionList {
	my ($xmin, $ymin, $xmax, $ymax) = @_;
	my $result = &OpenSim::Utility::getSimpleResult($OpenSim::GridServer::Config::SYS_SQL{select_region_list}, $xmin, $xmax, $ymin, $ymax);
	my $count = @$result;
	if ($count > 0) {
		return $result;
	}
	Carp::croak("can not find region");
}

sub getRegionList2 {
	my ($xmin, $ymin, $xmax, $ymax) = @_;
	my $result = &OpenSim::Utility::getSimpleResult($OpenSim::GridServer::Config::SYS_SQL{select_region_list2}, $xmin, $xmax, $ymin, $ymax);
	my $count = @$result;
	if ($count > 0) {
		return $result;
	}
	Carp::croak("can not find region");
}

sub deleteRegions {
	my $result = &OpenSim::Utility::getSimpleResult($OpenSim::GridServer::Config::SYS_SQL{delete_all_regions});
	my $count = @$result;
	if ($count > 0) {
		return $result;
	}
	Carp::croak("failed to delete regions");
}

1;
