# We build drawstuff as a non libtool static library manually
# so it doesn't get installed when 'make install' is called,
# drawstuff is meant as an aid for testing and not as a full
# rendering library.

noinst_LIBRARIES = libdrawstuff.a
libdrawstuff_a_SOURCES = drawstuff.cpp internal.h
libdrawstuff_a_CXXFLAGS =  @ARCHFLAGS@ -I$(top_srcdir)/include -I$(top_builddir)/include

# libdrawstuff_a_LIBADD = @GL_LIBS@

if WIN32
libdrawstuff_a_SOURCES+= windows.cpp
endif
if X11
libdrawstuff_a_SOURCES+= x11.cpp
endif
if OSX
libdrawstuff_a_SOURCES+= osx.cpp
endif

