NANT	= $(shell if test "$$EMACS" = "t" ; then echo "nant"; else echo "./nant-color"; fi)

all: prebuild
	# @export PATH=/usr/local/bin:$(PATH)
	${NANT}
	find OpenSim -name \*.mdb -exec cp {} bin \; 

prebuild:
	./runprebuild.sh

clean:
	# @export PATH=/usr/local/bin:$(PATH)
	${NANT} clean

test: prebuild
	${NANT} test

test-xml: prebuild
	${NANT} test-xml

tags:
	find OpenSim -name \*\.cs | xargs etags 

