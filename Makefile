all:
	export PATH=/usr/local/bin:$(PATH)
	./runprebuild.sh
	./nant-color
	find OpenSim -name \*.mdb -exec cp {} bin \; 

clean:
	export PATH=/usr/local/bin:$(PATH)
	./nant-color clean

tags:
	find OpenSim -name \*\.cs | xargs etags 
