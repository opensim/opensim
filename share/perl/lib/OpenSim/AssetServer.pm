package OpenSim::AssetServer;

use strict;
use MIME::Base64;
use XML::Simple;
use OpenSim::Utility;
use OpenSim::AssetServer::AssetManager;

# !!
# TODO: delete asset
#

sub getAsset {
	my ($asset_id, $param) = @_;
	# get asset
	my $asset_id_string = &OpenSim::Utility::UUID2HEX($asset_id);
	my $asset = &OpenSim::AssetServer::AssetManager::getAssetByUUID($asset_id_string);
	$asset->{assetUUID} = $asset_id;
	# make response
	return &_asset_to_xml($asset);
}

sub saveAsset {
	my $xml = shift;
	my $asset = &_xml_to_asset($xml);
	&OpenSim::AssetServer::AssetManager::saveAsset($asset);
	return ""; # TODO: temporary solution of "success!"
}

# ##################
# private functions
sub _asset_to_xml {
	my $asset = shift;
	my $asset_data = &MIME::Base64::encode_base64($asset->{data});
	return << "ASSET_XML";
<AssetBase>
	<Data>
$asset_data
	</Data>
	<FullID>
		<UUID>$asset->{assetUUID}</UUID>
	</FullID>
	<Type>$asset->{assetType}</Type>
	<InvType>$asset->{invType}</InvType>
	<Name>$asset->{name}</Name>
	<Description>$asset->{description}</Description>
	<Local>$asset->{local}</Local>
	<Temporary>$asset->{temporary}</Temporary>
</AssetBase>
ASSET_XML
}

sub _xml_to_asset {
	my $xml = shift;
    my $xs = new XML::Simple();
    my $obj = $xs->XMLin($xml);
print STDERR $obj->{FullID}->{UUID} . "\n";
	my %asset = (
		"id" => &OpenSim::Utility::UUID2BIN($obj->{FullID}->{UUID}),
		"name" => $obj->{Name},
		"description" => $obj->{Description},
		"assetType" => $obj->{Type},
		"invType" => $obj->{InvType},
		"local" => $obj->{Local},
		"temporary" => $obj->{Temporary},
		"data" => &MIME::Base64::decode_base64($obj->{Data}),
	);
	return \%asset;
}

1;

__END__

{
  Data => "PFNjZW5lT2JqZWN0R3JvdXA+PFJvb3RQYXJ0PjxTY2VuZU9iamVjdFBhcnQgeG1sbnM6eHNpPSJodHRwOi8vd3d3LnczLm9yZy8yMDAxL1hNTFNjaGVtYS1pbnN0YW5jZSIgeG1sbnM6eHNkPSJodHRwOi8vd3d3LnczLm9yZy8yMDAxL1hNTFNjaGVtYSI+PExhc3RPd25lcklEPjxVVUlEPmI5Y2I1OGU4LWYzYzktNGFmNS1iZTQ3LTAyOTc2MmJhYTY4ZjwvVVVJRD48L0xhc3RPd25lcklEPjxPd25lcklEPjxVVUlEPmI5Y2I1OGU4LWYzYzktNGFmNS1iZTQ3LTAyOTc2MmJhYTY4ZjwvVVVJRD48L093bmVySUQ+PEdyb3VwSUQ+PFVVSUQ+MDAwMDAwMDAtMDAwMC0wMDAwLTAwMDAtMDAwMDAwMDAwMDAwPC9VVUlEPjwvR3JvdXBJRD48T3duZXJzaGlwQ29zdD4wPC9Pd25lcnNoaXBDb3N0PjxPYmplY3RTYWxlVHlwZT4wPC9PYmplY3RTYWxlVHlwZT48U2FsZVByaWNlPjA8L1NhbGVQcmljZT48Q2F0ZWdvcnk+MDwvQ2F0ZWdvcnk+PENyZWF0aW9uRGF0ZT4xMTk4NjQ5MjA5PC9DcmVhdGlvbkRhdGU+PFBhcmVudElEPjA8L1BhcmVudElEPjxPd25lck1hc2s+NTI2MDUzNjkyPC9Pd25lck1hc2s+PE5leHRPd25lck1hc2s+MjU3NDg3MTMyPC9OZXh0T3duZXJNYXNrPjxHcm91cE1hc2s+MDwvR3JvdXBNYXNrPjxFdmVyeW9uZU1hc2s+MDwvRXZlcnlvbmVNYXNrPjxCYXNlTWFzaz4yMTQ3NDgzNjQ3PC9CYXNlTWFzaz48Q3JlYXRvcklEPjxVVUlEPmI5Y2I1OGU4LWYzYzktNGFmNS1iZTQ3LTAyOTc2MmJhYTY4ZjwvVVVJRD48L0NyZWF0b3JJRD48VVVJRD48VVVJRD5hMGY3NmQzYi02MTlkLTRjNjktODVmOS0zNzhjMDExZDg2NzI8L1VVSUQ+PC9VVUlEPjxMb2NhbElEPjcwMjAwMTwvTG9jYWxJRD48TmFtZT5QcmltaXRpdmU8L05hbWU+PE9iamVjdEZsYWdzPjY1NjY2PC9PYmplY3RGbGFncz48TWF0ZXJpYWw+MDwvTWF0ZXJpYWw+PFJlZ2lvbkhhbmRsZT4xMDk5NTExNjI4MDMyMDAwPC9SZWdpb25IYW5kbGU+PEdyb3VwUG9zaXRpb24+PFg+MTMwLjA5OTQ8L1g+PFk+MTI4LjcxNTQ8L1k+PFo+MjEuMzM1NTI8L1o+PC9Hcm91cFBvc2l0aW9uPjxPZmZzZXRQb3NpdGlvbj48WD4wPC9YPjxZPjA8L1k+PFo+MDwvWj48L09mZnNldFBvc2l0aW9uPjxSb3RhdGlvbk9mZnNldD48WD4wPC9YPjxZPjA8L1k+PFo+MDwvWj48Vz4xPC9XPjwvUm90YXRpb25PZmZzZXQ+PFZlbG9jaXR5PjxYPjA8L1g+PFk+MDwvWT48Wj4wPC9aPjwvVmVsb2NpdHk+PFJvdGF0aW9uYWxWZWxvY2l0eT48WD4wPC9YPjxZPjA8L1k+PFo+MDwvWj48L1JvdGF0aW9uYWxWZWxvY2l0eT48QW5ndWxhclZlbG9jaXR5PjxYPjA8L1g+PFk+MDwvWT48Wj4wPC9aPjwvQW5ndWxhclZlbG9jaXR5PjxBY2NlbGVyYXRpb24+PFg+MDwvWD48WT4wPC9ZPjxaPjA8L1o+PC9BY2NlbGVyYXRpb24+PERlc2NyaXB0aW9uIC8+PENvbG9yIC8+PFRleHQgLz48U2l0TmFtZSAvPjxUb3VjaE5hbWUgLz48TGlua051bT4wPC9MaW5rTnVtPjxDbGlja0FjdGlvbj4wPC9DbGlja0FjdGlvbj48U2hhcGU+PFN0YXRlPjA8L1N0YXRlPjxQQ29kZT45PC9QQ29kZT48UGF0aEJlZ2luPjA8L1BhdGhCZWdpbj48UGF0aEVuZD4wPC9QYXRoRW5kPjxQYXRoU2NhbGVYPjIwMDwvUGF0aFNjYWxlWD48UGF0aFNjYWxlWT4yMDA8L1BhdGhTY2FsZVk+PFBhdGhTaGVhclg+MDwvUGF0aFNoZWFyWD48UGF0aFNoZWFyWT4wPC9QYXRoU2hlYXJZPjxQYXRoU2tldz4wPC9QYXRoU2tldz48UHJvZmlsZUJlZ2luPjA8L1Byb2ZpbGVCZWdpbj48UHJvZmlsZUVuZD4wPC9Qcm9maWxlRW5kPjxTY2FsZT48WD4wLjU8L1g+PFk+MC41PC9ZPjxaPjAuNTwvWj48L1NjYWxlPjxQYXRoQ3VydmU+MTY8L1BhdGhDdXJ2ZT48UHJvZmlsZUN1cnZlPjA8L1Byb2ZpbGVDdXJ2ZT48UHJvZmlsZUhvbGxvdz4wPC9Qcm9maWxlSG9sbG93PjxQYXRoUmFkaXVzT2Zmc2V0PjA8L1BhdGhSYWRpdXNPZmZzZXQ+PFBhdGhSZXZvbHV0aW9ucz4wPC9QYXRoUmV2b2x1dGlvbnM+PFBhdGhUYXBlclg+MDwvUGF0aFRhcGVyWD48UGF0aFRhcGVyWT4wPC9QYXRoVGFwZXJZPjxQYXRoVHdpc3Q+MDwvUGF0aFR3aXN0PjxQYXRoVHdpc3RCZWdpbj4wPC9QYXRoVHdpc3RCZWdpbj48VGV4dHVyZUVudHJ5PkFBQUFBQUFBQUFDWm1RQUFBQUFBQlFBQUFBQUFBQUFBZ0Q4QUFBQ0FQd0FBQUFBQUFBQUFBQUFBQUFBPTwvVGV4dHVyZUVudHJ5PjxFeHRyYVBhcmFtcz5BQT09PC9FeHRyYVBhcmFtcz48UHJvZmlsZVNoYXBlPkNpcmNsZTwvUHJvZmlsZVNoYXBlPjwvU2hhcGU+PFNjYWxlPjxYPjAuNTwvWD48WT4wLjU8L1k+PFo+MC41PC9aPjwvU2NhbGU+PFVwZGF0ZUZsYWc+MDwvVXBkYXRlRmxhZz48L1NjZW5lT2JqZWN0UGFydD48L1Jvb3RQYXJ0PjxPdGhlclBhcnRzIC8+PC9TY2VuZU9iamVjdEdyb3VwPgA=",
  Description => {},
  FullID => { UUID => "feb7e249-e462-499f-a881-553b9829539a" },
  InvType => 6,
  Local => "false",
  Name => "Primitive",
  Temporary => "false",
  Type => 6,
  "xmlns:xsd" => "http://www.w3.org/2001/XMLSchema",
  "xmlns:xsi" => "http://www.w3.org/2001/XMLSchema-instance",
}

