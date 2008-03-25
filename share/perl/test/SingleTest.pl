#!/usr/bin/perl

# usage:
# ./SingleTest.pl $URL $METHOD @PARAMETERS
# example
# ./SingleTest.pl http://127.0.0.1/user.cgi get_user_by_name "Test User"
# ./SingleTest.pl http://127.0.0.1/grid.cgi simulator_login 3507f395-88e5-468c-a45f-d4fd96a1c668 10.8.1.148 9000
# ./SingleTest.pl http://127.0.0.1/grid.cgi map_block 999 999 1001 1001
# ./SingleTest.pl  http://127.0.0.1/asset.cgi get_asset 00000000000022223333000000000001
#

use strict;
use Data::Dump;
use OpenSimTest;

&OpenSimTest::init();
my $url = shift @ARGV;
#my $url = "http://localhost:8002";
my $result = &OpenSimTest::SingleTest($url, @ARGV);
Data::Dump::dump($result);

