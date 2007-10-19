noinst_PROGRAMS= dstest
dstest_CXXFLAGS = -I$(top_srcdir)/include -I$(top_builddir)

dstest_SOURCES= dstest.cpp
dstest_DEPENDENCIES=$(top_builddir)/drawstuff/src/libdrawstuff.a
dstest_LDFLAGS = -L$(top_builddir)/drawstuff/src
dstest_LDADD=-ldrawstuff @LDFLAGS@ @GL_LIBS@ @LIBS@
if WIN32
dstest_DEPENDENCIES+=resources.o
resources.o: ../src/resources.rc ../src/resource.h
	@WINDRES@ ../src/resources.rc -o resources.o
dstest_LDADD+= resources.o
endif
