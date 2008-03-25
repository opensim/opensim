package XML::RPC;

use strict;
use XML::TreePP;
use Data::Dumper;
use vars qw($VERSION $faultCode);
no strict 'refs';

$VERSION = 0.5;

sub new {
    my $package = shift;
    my $self    = { };
    bless $self, $package;
    $self->{url} = shift;
    $self->{tpp} = XML::TreePP->new(@_);
    return $self;
}

sub call {
    my $self = shift;
    my ( $methodname, @params ) = @_;

    die 'no url' if ( !$self->{url} );

    $faultCode = 0;
    my $xml = $self->create_call_xml( $methodname, @params );
#print STDERR $xml;
    my $result = $self->{tpp}->parsehttp(
        POST => $self->{url},
        $xml,
        {
            'Content-Type'   => 'text/xml',
            'User-Agent'     => 'XML-RPC/' . $VERSION,
            'Content-Length' => length($xml)
        }
    );

    my @data = $self->unparse_response($result);
    return @data == 1 ? $data[0] : @data;
}

sub receive {
    my $self   = shift;
    my $result = eval {
        my $xml     = shift || die 'no xml';
        my $handler = shift || die 'no handler';
        my $hash = $self->{tpp}->parse($xml);
        my ( $methodname, @params ) = $self->unparse_call($hash);
        $self->create_response_xml( $handler->( $methodname, @params ) );
    };
    return $self->create_fault_xml($@) if ($@);
    return $result;

}

sub create_fault_xml {
    my $self  = shift;
    my $error = shift;
    chomp($error);
    return $self->{tpp}
      ->write( { methodResponse => { fault => $self->parse( { faultString => $error, faultCode => $faultCode } ) } } );
}

sub create_call_xml {
    my $self = shift;
    my ( $methodname, @params ) = @_;

    return $self->{tpp}->write(
        {
            methodCall => {
                methodName => $methodname,
                params     => { param => [ map { $self->parse($_) } @params ] }
            }
        }
    );
}

sub create_response_xml {
    my $self   = shift;
    my @params = @_;

    return $self->{tpp}->write( { methodResponse => { params => { param => [ map { $self->parse($_) } @params ] } } } );
}

sub parse {
    my $self = shift;
    my $p    = shift;
    my $result;

    if ( ref($p) eq 'HASH' ) {
        $result = $self->parse_struct($p);
    }
    elsif ( ref($p) eq 'ARRAY' ) {
        $result = $self->parse_array($p);
    }
    else {
        $result = $self->parse_scalar($p);
    }

    return { value => $result };
}

sub parse_scalar {
    my $self   = shift;
    my $scalar = shift;
    local $^W = undef;

    if (   ( $scalar =~ m/^[\-+]?\d+$/ )
        && ( abs($scalar) <= ( 0xffffffff >> 1 ) ) )
    {
        return { i4 => $scalar };
    }
    elsif ( $scalar =~ m/^[\-+]?\d+\.\d+$/ ) {
        return { double => $scalar };
    }
    else {
        return { string => \$scalar };
    }
}

sub parse_struct {
    my $self = shift;
    my $hash = shift;
    my @members;
    while ( my ( $k, $v ) = each(%$hash) ) {
        push @members, { name => $k, %{ $self->parse($v) } };
    }
    return { struct => { member => \@members } };
}

sub parse_array {
    my $self  = shift;
    my $array = shift;

    return { array => { data => { value => [ map { $self->parse($_)->{value} } $self->list($array) ] } } };
}

sub unparse_response {
    my $self = shift;
    my $hash = shift;

    my $response = $hash->{methodResponse} || die 'no data';

    if ( $response->{fault} ) {
        return $self->unparse_value( $response->{fault}->{value} );
    }
    else {
        return map { $self->unparse_value( $_->{value} ) } $self->list( $response->{params}->{param} );
    }
}

sub unparse_call {
    my $self = shift;
    my $hash = shift;

    my $response = $hash->{methodCall} || die 'no data';

    my $methodname = $response->{methodName};
    my @args =
      map { $self->unparse_value( $_->{value} ) } $self->list( $response->{params}->{param} );
    return ( $methodname, @args );
}

sub unparse_value {
    my $self  = shift;
    my $value = shift;
    my $result;

    return $value if ( ref($value) ne 'HASH' );    # for unspecified params
    if ( $value->{struct} ) {
        $result = $self->unparse_struct( $value->{struct} );
        return !%$result
          ? undef
          : $result;                               # fix for empty hashrefs from XML::TreePP
    }
    elsif ( $value->{array} ) {
        return $self->unparse_array( $value->{array} );
    }
    else {
        return $self->unparse_scalar($value);
    }
}

sub unparse_scalar {
    my $self     = shift;
    my $scalar   = shift;
    my ($result) = values(%$scalar);
    return ( ref($result) eq 'HASH' && !%$result )
      ? undef
      : $result;    # fix for empty hashrefs from XML::TreePP
}

sub unparse_struct {
    my $self   = shift;
    my $struct = shift;

    return { map { $_->{name} => $self->unparse_value( $_->{value} ) } $self->list( $struct->{member} ) };
}

sub unparse_array {
    my $self  = shift;
    my $array = shift;
    my $data  = $array->{data};

    return [ map { $self->unparse_value($_) } $self->list( $data->{value} ) ];
}

sub list {
    my $self  = shift;
    my $param = shift;
    return () if ( !$param );
    return @$param if ( ref($param) eq 'ARRAY' );
    return ($param);
}

1;
