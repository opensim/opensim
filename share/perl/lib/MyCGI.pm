package MyCGI;

use strict;
use CGI;

sub getParam {
	my $cgi;
	if ($ARGV[0]) {
		$cgi = new CGI($ARGV[0]);
	} else {
		$cgi = new CGI;
	}
	my @param_names = $cgi->param();
	my %param = ();
	foreach (@param_names) {
		$param{$_} = $cgi->param($_);
	}
	return \%param;
}

sub getCookie {
	my $name = shift;
	my $cookie_value = &CGI::cookie($name);
	return &_parse($cookie_value);
}

sub outputHtml {
	my ($charset, $html) = @_;
	print &CGI::header(-charset => $charset);
	print $html;
}

sub outputXml {
	my ($charset, $xml) = @_;
	print &CGI::header( -type => 'text/xml', -charset => $charset );
	print $xml;
}

sub makeCookieValue {
	my $param = shift;
	my @data = ();
	foreach(keys %$param) {
		push(@data, $_ . "=" . $param->{$_});
	}
	return join("&", @data);
}

sub setCookie {
	my $param = shift;
	my $cookie = &CGI::cookie(
		-name => $param->{name} || return,
		-value => $param->{value},
		-domain => $param->{domain},
		-path => $param->{path},
		-expires => $param->{expires},
	);
	return &CGI::header(-cookie => $cookie);
}

sub redirect {
	my $dest = shift;
	&CGI::redirect($dest);
}

sub urlEncode {
	my $str = shift;
	$str =~ s/([^\w ])/'%'.unpack('H2', $1)/eg;
	$str =~ tr/ /+/;
	return $str;
}

sub urlDecode {
  my $str = shift;
  $str =~ tr/+/ /;
  $str =~ s/%([0-9A-Fa-f][0-9A-Fa-f])/pack('H2', $1)/eg;
  return $str;
}

sub _parse {
	my $value = shift;
	my @pair = split(/&/, $value);
	my %data = ();
	foreach(@pair) {
		my ($name, $value) = split(/=/, $_);
		$data{$name} = $value;
	}
	return \%data;
}

1;

