LIBTOOL_DEPS = @LIBTOOL_DEPS@
libtool: $(LIBTOOL_DEPS)
	$(SHELL) ./config.status --recheck

if ENABLE_DEMOS
SUBDIRS = include drawstuff ode tests
else
SUBDIRS = include ode tests
endif

bin_SCRIPTS = ode-config

# Utility rule for making a release
release: dist dist-bz2 dist-zip
	@echo Created release packages for ${PACKAGE}-${VERSION}.

# Rules for creating .tar.bz2 and .zip packages
dist-bz2: ${PACKAGE}-${VERSION}.tar.gz
	gzip -dc ${PACKAGE}-${VERSION}.tar.gz | bzip2 > ${PACKAGE}-${VERSION}.tar.bz2

dist-zip: ${PACKAGE}-${VERSION}.tar.gz
	tar -zxf ${PACKAGE}-${VERSION}.tar.gz && \
	zip -r ${PACKAGE}-${VERSION}.zip ${PACKAGE}-${VERSION} && \
	rm -rf ${PACKAGE}-${VERSION}

if USE_SONAME
install-exec-hook:
	ln -s $(libdir)/@ODE_SONAME@.@ODE_REVISION@.@ODE_AGE@ \
	$(libdir)/libode.so
	ln -s $(libdir)/@ODE_SONAME@.@ODE_REVISION@.@ODE_AGE@ \
	$(libdir)/@ODE_SONAME@
	ln -s $(libdir)/@ODE_SONAME@.@ODE_REVISION@.@ODE_AGE@ \
	$(libdir)/@ODE_SONAME@.@ODE_REVISION@
	/sbin/ldconfig
else
install-exec-hook:
endif
