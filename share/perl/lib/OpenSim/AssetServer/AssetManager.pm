package OpenSim::AssetServer::AssetManager;

use strict;
use Carp;
use OpenSim::Utility;
use OpenSim::AssetServer::Config;


sub getAssetByUUID {
	my $uuid = shift;
	my $result = &OpenSim::Utility::getSimpleResult($OpenSim::AssetServer::Config::SYS_SQL{select_asset_by_uuid}, $uuid);
	my $count = @$result;
	if ($count > 0) {
		return $result->[0];
	}
	Carp::croak("can not find asset($uuid)");
}

sub saveAsset {
	my $asset = shift;
	my $result = &OpenSim::Utility::getSimpleResult(
		$OpenSim::AssetServer::Config::SYS_SQL{insert_asset},
		$asset->{id},
		$asset->{name},
		$asset->{description},
		$asset->{assetType},
		$asset->{invType},
		$asset->{"local"},
		$asset->{temporary},
		$asset->{data}
	);
}

1;
