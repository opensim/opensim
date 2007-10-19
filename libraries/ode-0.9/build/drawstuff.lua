package.name = "drawstuff"
package.language = "c++"
package.objdir = "obj/drawstuff"


-- Separate distribution files into toolset subdirectories

  if (options["usetargetpath"]) then
    package.path = options["target"]
  else
    package.path = "custom"
  end


-- Package Build Settings

  local dll_defines =
  {
    "DS_DLL",
    "USRDLL"
  }
  
  local lib_defines = 
  {
    "DS_LIB"
  }
      
  if (options["enable-shared-only"]) then
    package.kind = "dll"
    table.insert(package.defines, dll_defines)
  elseif (options["enable-static-only"]) then
    package.kind = "lib"
    table.insert(package.defines, lib_defines)
  else
    package.config["DebugDLL"].kind = "dll"
    package.config["DebugLib"].kind = "lib"
    package.config["ReleaseDLL"].kind = "dll"
    package.config["ReleaseLib"].kind = "lib"

    table.insert(package.config["DebugDLL"].defines, dll_defines)
    table.insert(package.config["ReleaseDLL"].defines, dll_defines)
    table.insert(package.config["DebugLib"].defines, lib_defines)
    table.insert(package.config["ReleaseLib"].defines, lib_defines)
  end

  package.includepaths =
  {
    "../../include"
  }

  -- disable VS2005 CRT security warnings
  if (options["target"] == "vs2005") then
    table.insert(package.defines, "_CRT_SECURE_NO_DEPRECATE")
  end


-- Build Flags

	package.config["DebugLib"].buildflags   = { }
	package.config["DebugDLL"].buildflags   = { }

	package.config["ReleaseDLL"].buildflags = { "optimize-speed", "no-symbols", "no-frame-pointer" }
	package.config["ReleaseLib"].buildflags = { "optimize-speed", "no-symbols", "no-frame-pointer" }

	if (options.target == "vs6" or options.target == "vs2002" or options.target == "vs2003") then
		table.insert(package.config.DebugLib.buildflags, "static-runtime")
		table.insert(package.config.ReleaseLib.buildflags, "static-runtime")
	end


-- Libraries

  local windows_libs =
  {
    "user32",
    "opengl32",
    "glu32",
    "winmm",
    "gdi32"
  }

  local x11_libs =
  {
    "X11",
    "GL",
    "GLU"
  }
  
  if (windows) then
    table.insert(package.links, windows_libs)
  else
    table.insert(package.links, x11_libs)
  end


-- Files

  package.files =
  {
    matchfiles("../../include/drawstuff/*.h"),
    "../../drawstuff/src/internal.h",
    "../../drawstuff/src/drawstuff.cpp"
  }

  if (windows) then
    table.insert(package.defines, "WIN32")
    table.insert(package.files, "../../drawstuff/src/resource.h")
    table.insert(package.files, "../../drawstuff/src/resources.rc")
    table.insert(package.files, "../../drawstuff/src/windows.cpp")
  else
    table.insert(package.files, "../../drawstuff/src/x11.cpp")
  end
