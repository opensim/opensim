# hey, emacs! this is a -*- makefile -*-
#
# OpenSim makefile
#

RUBY    = $(strip $(shell which ruby 2>/dev/null))
ifeq ($(RUBY),)
NANT    = nant
else
NANT	= $(shell if test "$$EMACS" = "t" ; then echo "nant"; else echo "./nant-color"; fi)
endif

all: prebuild
	# @export PATH=/usr/local/bin:$(PATH)
	${NANT}
	find OpenSim -name \*.mdb -exec cp {} bin \; 

release: prebuild
	${NANT} -D:project.config=Release
	find OpenSim -name \*.mdb -exec cp {} bin \;

prebuild:
	./runprebuild.sh

clean:
	# @export PATH=/usr/local/bin:$(PATH)
	-${NANT} clean

test: prebuild
	${NANT} test

test-xml: prebuild
	${NANT} test-xml

tags:
	find OpenSim -name \*\.cs | xargs etags 

cscope-tags:
	find OpenSim -name \*\.cs -fprint cscope.files
	cscope -b

include $(wildcard Makefile.local)

