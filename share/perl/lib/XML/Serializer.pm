package XML::Serializer;

use strict;

my $root_element = "root";
my $indent = "  ";
#my $XML_HEADER = << "XMLHEADER";
#<?xml version="1.0" encoding="__CHARSET__"?>
#<?xml-stylesheet type="text/xsl" href="__XSLT__" ?>
#XMLHEADER
my $XML_HEADER = << "XMLHEADER";
<?xml version="1.0" encoding="__CHARSET__"?>
XMLHEADER

sub WITH_HEADER {
	return 1;
}

sub new {
	my ($this, $data, $root_name, $xslt) = @_;
	my %fields = (
			_charset => "utf-8",
			_data => "",
			_output => "",
			_root_name => $root_name ? $root_name : "root",
			_xslt => $xslt ? $xslt : ""
	);
	if (defined $data) {
		$fields{_data} = $data;
	}
	return bless \%fields, $this;
}

sub set_root_name {
	my ($this, $root_name) = @_;
	$this->{_root_name} = $root_name;
}

sub set_data {
	my ($this, $data) = @_;
	$this->{_data} = $data;
}

sub set_charset {
	my ($this, $charset) = @_;
	$this->{_charset} = $charset;
}

sub set_xslt {
	my ($this, $xslt) = @_;
	$this->{_xslt} = $xslt;
}

sub to_string{
	my ($this, $header) = @_;
	if ($header) {
		$this->{_output} = &_make_xml_header($this->{_charset}, $this->{_xslt});
	}
	$this->{_output} .= &_to_string($this->{_data}, $this->{_root_name});
}

sub to_formatted{
	my ($this, $header) = @_;
	if ($header) {
		$this->{_output} = &_make_xml_header($this->{_charset}, $this->{_xslt});
	}
	$this->{_output} .= &_to_formatted($this->{_root_name}, $this->{_data});
}

sub _make_xml_header {
	my $header = $XML_HEADER;
	$header =~ s/__CHARSET__/$_[0]/;
	$header =~ s/__XSLT__/$_[1]/;
	return $header;
}

sub _to_string {
	my ($obj, $name) = @_;
	my $output = "";

	if (ref($obj) eq "HASH") {
		my $attr_list = "";
		my $tmp_mid = "";
		foreach (sort keys %$obj) {
			if ($_ =~ /^@/) {
				$attr_list = &_to_string($_, $obj->{$_});
			}
			$tmp_mid .= &_to_string($_, $obj->{$_});
		}
		$output = &_start_node($name, $attr_list) . $tmp_mid . &_end_node($name);
	}
	elsif (ref($obj) eq "ARRAY") {
		foreach (@$obj) {
			$output .= &_to_string($_, $name);
		}
	}
	else {
		if ($_ =~ /^@(.+)$/) {
			return "$1=\"$obj\" ";
		} else {
			$output = &_start_node($name) . $obj . &_end_node($name);
		}
	}
	return $output;
}

sub _to_formatted {
	my ($name, $obj, $depth) = @_;
#	if (!$obj) { $obj = ""; }
	if (!defined($depth)) { $depth = 0; }
	my $output = "";
	if (ref($obj) eq "HASH") {
		my $attr_list = "";
		my $tmp_mid = "";
		foreach (sort keys %$obj) {
			if ($_ =~ /^@/) {
				$attr_list = &_to_string($_, $obj->{$_});
			}
			$tmp_mid .= &_to_formatted($_, $obj->{$_}, $depth+1);
		}
		$output = &_start_node($name, $attr_list, $depth) . "\n" . $tmp_mid . &_end_node($name, $depth);
	}
	elsif (ref($obj) eq "ARRAY") {
		foreach (@$obj) {
			$output .= &_to_formatted($name, $_, $depth);
		}
	}
	else {
		if ($_ =~ /^@(.+)$/) {
			#return "$1=\"$obj\" ";
		} else {
			$output .= &_start_node($name, "", $depth);
			$output .= $obj;
			$output .= &_end_node($name);
		}
	}
	return $output;
}

sub _start_node {
	my $ret = "";
	if (defined $_[2]) {
		for(1..$_[2]) { $ret .= $indent; }
	}
	my $tag = $_[0] ? $_[0] : "";
	my $attr = $_[1] ? $_[1] : "";
	$ret .= "<$tag $attr>";
	return $ret;
}

sub _end_node {
  my $ret = "";
  if (defined $_[1]) {
    for(1..$_[1]) { $ret .= $indent; }
  }
  if (defined $_[0]) {
    $ret .= "</$_[0]>\n";
  }
  return $ret;
}

1;

