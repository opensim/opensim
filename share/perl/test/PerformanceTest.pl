#!/usr/bin/perl

# Usage:
# ./PerformanceTest.pl
# 2 variables should be changed:
# Line 14: $fork_limit
# Line 13: $benchmark_loop_count
#

use strict;
use OpenSimTest;

my $script = "./PerformanceTest.pl";
my $fork_limit = 50; # the number of process
my $benchmark_loop_count = 10000; # the number of requests sent by each process

my @child_pid = ();

for(1..$fork_limit) {
	my $pid = fork;
	if ($pid) {
		&parent_do($pid);
	} elsif ($pid == 0) {
		&child_do;
		exit(0);
	} else {
		die "could not fork";
	}
}

foreach (@child_pid) {
	waitpid($_, 0);
}


sub parent_do {
	my $pid = shift;
	push(@child_pid, $pid);
}

sub child_do {
	#for(1..10000) {
	#	print "$_ ";
	#}
	&OpenSimTest::init();
	# user
	&OpenSimTest::PerformanceCompare("user", $benchmark_loop_count, "get_user_by_name", "Test User");
	&OpenSimTest::PerformanceCompare("user", $benchmark_loop_count, "get_user_by_uuid", "db836502-de98-49c9-9edc-b90a67beb0a8");
	# grid
	&OpenSimTest::PerformanceCompare("grid", $benchmark_loop_count, "simulator_login", "3507f395-88e5-468c-a45f-d4fd96a1c668", "10.8.1.148", 9000);
	&OpenSimTest::PerformanceCompare("grid", $benchmark_loop_count, "simulator_data_request", "1099511628032000");
	&OpenSimTest::PerformanceCompare("grid", $benchmark_loop_count, "map_block", 999, 999, 1001, 1001);
	# asset
	&OpenSimTest::PerformanceCompare("asset", $benchmark_loop_count, "get_asset", "00000000000022223333000000000001");
	# inventory
	&OpenSimTest::PerformanceCompare("inventory", $benchmark_loop_count, "root_folders", "b9cb58e8-f3c9-4af5-be47-029762baa68f");
	&OpenSimTest::PerformanceCompare("inventory", $benchmark_loop_count, "get_inventory", "b9cb58e8-f3c9-4af5-be47-029762baa68f");
}

__END__
my $count = 10000;

&OpenSimTest::init();
# user
#&OpenSimTest::PerformanceCompare("user", $count, "get_user_by_name", "Test User");
#&OpenSimTest::PerformanceCompare("user", $count, "get_user_by_uuid", "db836502-de98-49c9-9edc-b90a67beb0a8");
# grid
#&OpenSimTest::PerformanceCompare("grid", $count, "simulator_login", "3507f395-88e5-468c-a45f-d4fd96a1c668", "10.8.1.148", 9000);
#&OpenSimTest::PerformanceCompare("grid", $count, "simulator_data_request", "1099511628032000");
#&OpenSimTest::PerformanceCompare("grid", $count, "map_block", 999, 999, 1001, 1001);
# asset
&OpenSimTest::PerformanceCompare("asset", $count, "get_asset", "00000000000022223333000000000001");
# inventory
#&OpenSimTest::PerformanceCompare("inventory", $count, "create_inventory", "b9cb58e8-f3c9-4af5-be47-029762baa68f");
#&OpenSimTest::PerformanceCompare("inventory", $count, "root_folders", "b9cb58e8-f3c9-4af5-be47-029762baa68f");
#&OpenSimTest::PerformanceCompare("inventory", $count, "get_inventory", "b9cb58e8-f3c9-4af5-be47-029762baa68f");
#&OpenSimTest::PerformanceCompare("inventory", $count, "new_item");
#&OpenSimTest::PerformanceCompare("inventory", $count, "new_folder");
