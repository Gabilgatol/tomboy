# handle Mono secondary dependencies
export MONO_PATH := $(MONO_PATH)

CSC = mcs

TARGET = bin/TomboyWebService.dll
WSDL = TomboyWebService.wsdl

CSFLAGS =			\
	-debug			\
	-define:DEBUG	\
	-unsafe			\
	-target:library

CSFILES =			\
	$(srcdir)/*.cs

ASSEMBLIES =				\
	$(TOMBOY_LIBS)			\
	-r:System.Web.Services.dll \
	-r:System.Web.dll		\
	-r:$(srcdir)/../Tomboy.exe


$(TARGET): $(CSFILES)
	if ! test -d "$(srcdir)/bin"; then \
		mkdir $(srcdir)/bin; \
	fi
	$(CSC) -out:$@ $(CSFLAGS) $^ $(ASSEMBLIES)

tomboylibdir = $(pkglibdir)/web/bin
tomboylib_DATA = $(TARGET)

#tomboylib_DATA = $(TARGET) $(TARGET).config

#$(TARGET).config: $(srcdir)/$(TARGET).config.in Makefile
#	sed -e "s|\@pkglibdir\@|$(pkglibdir)|" 
#	    < $< > $@

install-exec-local: $(TARGET)
	$(mkinstalldirs) $(pkglibdir)/web/bin
	$(INSTALL_PROGRAM) $(WSDL) $(pkglibdir)/web
	$(INSTALL_PROGRAM) Tomboy.asmx $(pkglibdir)/web

uninstall-local:
	cd $(pkglibdir)/web; rm -f $(WSDL) Tomboy.asmx

EXTRA_DIST =		\
	$(CSFILES)		\
	$(srcdir)/$(WSDL) \
	$(srcdir)/Tomboy.asmx

#	$(TARGET).config.in	

CLEANFILES =			\
	$(TARGET)			\
	$(TARGET).mdb

#	$(TARGET).config	
