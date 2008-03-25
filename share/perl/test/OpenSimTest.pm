package OpenSimTest;

use strict;
use PerformanceTest;
use OpenSimTest::Config;
use OpenSimTest::UserTester;
use OpenSimTest::GridTester;
use OpenSimTest::AssetTester;
use OpenSimTest::InventoryTester;

sub init {
	UserTester::init();
	GridTester::init();
	AssetTester::init();
	InventoryTester::init();
}

sub SingleTest {
	my $url = shift;
	my $methodname = shift;
	my @ARGS = @_;

	if (!$OpenSimTest::Config::HANDLER_LIST{$methodname}) {
	    Carp::croak("unknown handler name: [$methodname]");
	} else {
	    my $handler = $OpenSimTest::Config::HANDLER_LIST{$methodname};
	    my $result = $handler->($url, @ARGS);
		return $result;
	}
}

sub PerformanceCompare {
	my $server_name = shift;
	my $count = shift;
	my @args = @_;
	my $test = new PerformanceTest();
	{
		my @params = @args;
		unshift(@params, $OpenSimTest::Config::APACHE_SERVERS{$server_name});
		$test->add_test("APACHE::$args[0]", \&OpenSimTest::SingleTest, \@params);
	}
	{
		my @params = @args;
		unshift(@params, $OpenSimTest::Config::OPENSIM_SERVERS{$server_name});
		$test->add_test("OPENSIM::$args[0]", \&OpenSimTest::SingleTest, \@params);
	}
	$test->set_count($count);
	$test->start();
	print "\n\n";
	#$test->bref_result();
}

1;
