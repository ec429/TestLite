LIBDIR := $(HOME)/ksp/KSP_Data/Managed
KSPDTA := $(HOME)/ksp/GameData/
BINDIR := bin
OUTDIR := ../GameData/TestLite/Plugins

SOURCES := Core.cs Util.cs ModuleTestLite.cs Properties/AssemblyInfo.cs

all: $(OUTDIR)/TestLite.dll

$(BINDIR)/TestLite.dll: $(SOURCES)
	mcs -t:library -nostdlib -r:$(LIBDIR)/Assembly-CSharp.dll,$(LIBDIR)/UnityEngine.dll,$(LIBDIR)/UnityEngine.UI.dll,$(LIBDIR)/System.dll,$(LIBDIR)/System.Core.dll,$(LIBDIR)/mscorlib.dll,$(KSPDTA)/RealFuels/Plugins/RealFuels.dll,$(KSPDTA)/SolverEngines/Plugins/SolverEngines.dll -out:$@ $(SOURCES)

$(OUTDIR)/TestLite.dll: $(BINDIR)/TestLite.dll
	cp $< $@

clean:
	-rm $(OUTDIR)/TestLite.dll $(BINDIR)/TestLite.dll
