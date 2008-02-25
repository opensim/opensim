#!/usr/bin/perl
#
# Audit tool for OpenSim class and namespace definitions.
#
# Copyright 2007 IBM
# 
# Authors: Sean Dague
#
#  Redistribution and use in source and binary forms, with or without
#  modification, are permitted provided that the following conditions are met:
#      * Redistributions of source code must retain the above copyright
#        notice, this list of conditions and the following disclaimer.
#      * Redistributions in binary form must reproduce the above copyright
#        notice, this list of conditions and the following disclaimer in the
#        documentation and/or other materials provided with the distribution.
#      * Neither the name of the OpenSim Project nor the
#        names of its contributors may be used to endorse or promote products
#        derived from this software without specific prior written permission.
# 
#  THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
#  EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
#  WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
#  DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
#  DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
#  (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
#  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
#  ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
#  (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
#  SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

use strict;
use File::Find;
use Data::Dumper;
use constant YELLOW => "\033[33m";
use constant RED => "\033[31m";
use constant CLEAR => "\033[0m";
our %totals;


find(\&test, "../../OpenSim");
print Dumper(\%totals);

sub test {
    my $file = $File::Find::name;
    my $dir = $File::Find::dir;
    $file =~ s{^../../}{}; #strip off prefix
    $dir =~ s{^../../}{}; #strip off prefix
    
    return if ($file !~ /\.cs$/);
    return if ($file =~ /AssemblyInfo\.cs$/);

    print "Processing File: $file\n";

    my $namespace = find_namespace($_);
    my $class = find_class($_);

    

    if(cmp_namespace($namespace, $dir) == 1) {
        $totals{goodns}++;
    } else {
        $totals{badns}++;
    }
    

    if(cmp_class($namespace, $class, $file) == 1) {
        $totals{goodclass}++;
    } else {
        $totals{badclass}++;
    }
    print "\n";
}

sub find_class {
    my $file = shift;
    my $content = slurp($file);
    if ($content =~ /\n\s*(public|private|protected)?\s*(class|interface)\s+(\S+)/) {
        return $3;
    }
    return "";
}

sub find_namespace {
    my $file = shift;
    my $content = slurp($file);
    
    if ($content =~ /\bnamespace\s+(\S+)/s) {
        return $1;
    }
    return "";
}

sub slurp {
    my $file = shift;
    local(*IN);
    local $/ = undef;
    
    open(IN, "$file") or die "Can't open '$file': $!";
    my $content = <IN>;
    close(IN);
    
    return $content;
}

sub cmp_class {
    my ($ns, $class, $file) = @_;
    $class = "$ns.$class";
    my $classtrans = $class;
    $classtrans =~ s{\.}{/}g;
    $classtrans .= ".cs";
    
    if($classtrans ne $file) {
        error(YELLOW, "CLASS: $class != $file");
        return -1;
    }
    return 1;
}

sub cmp_namespace {
    my ($ns, $dir) = @_;
    my $nstrans = $ns;
    $nstrans =~ s{\.}{/}g;

    if($nstrans ne $dir) {
        error(RED, "NS: $ns != $dir");
        return -1;
    }
    return 1;
}
    
sub error {
    print @_, CLEAR, "\n";
}
