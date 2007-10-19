SUBDIRS = CppTestHarness

AM_CXXFLAGS = @ARCHFLAGS@ @CXXFLAGS@ -I$(top_srcdir)/include -I$(top_srcdir)/tests/CppTestHarness -I$(top_builddir)/include
AM_CFLAGS = @ARCHFLAGS@ @CXXFLAGS@ -I$(top_srcdir)/include -I$(top_srcdir)/tests/CppTestHarness -I$(top_builddir)/include


noinst_PROGRAMS=ode_unit_test

ode_unit_test_SOURCES = \
  main.cpp \
  colliders/box_sphere.cpp

ode_unit_test_DEPENDENCIES = \
  $(top_builddir)/ode/src/libode.a \
  $(top_builddir)/tests/CppTestHarness/libodetest.a 

ode_unit_test_LDFLAGS = \
  -L$(top_builddir)/ode/src \
  -L$(top_builddir)/tests/CppTestHarness \
  @LDFLAGS@

ode_unit_test_LDADD = \
  $(top_builddir)/ode/src/libode.a \
  $(top_builddir)/tests/CppTestHarness/libodetest.a \
  @GL_LIBS@ @LIBS@

