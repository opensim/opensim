NANT	= $(shell if test "$$EMACS" = "t" ; then echo "nant"; else echo "./nant-color"; fi)

all:
	@export PATH=/usr/local/bin:$(PATH)
	./runprebuild.sh
	${NANT}
	find OpenSim -name \*.mdb -exec cp {} bin \; 

clean:
	@export PATH=/usr/local/bin:$(PATH)
	${NANT} clean

tags:
	find OpenSim -name \*\.cs | xargs etags 

