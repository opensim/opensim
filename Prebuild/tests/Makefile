#
# Executable
#

# Executables
PREBUILD = mono ../src/bin/Release/prebuild.exe /target makefile

# Files
PREBUILDS = $(wildcard *.prebuild)
RESULTS   = $(PREBUILDS:prebuild=results)
TESTS     = $(PREBUILDS:prebuild=test)

all: $(TESTS)

clean:
	rm -f *~ *.log
	rm -f $(RESULTS)

%.test: %.prebuild
	$(PREBUILD) /log $*.log /file $*.prebuild /ppi $*.results >& /dev/null
	if ! cmp $*.expected $*.results; then \
		echo $*.prebuild failed; \
		false; \
	fi
