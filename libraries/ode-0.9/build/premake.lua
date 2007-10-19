project.name = "ode"

  if (options["target"] == "vs6") then
    error("Visual Studio 6 is no longer supported; please upgrade to Visual Studio 2005 C++ Express.")
  end
  

-- Define the build configurations. You can also use the flags 
-- `--enable-shared-only` and `--enable-static-only` if you want to 
-- call these packages from within your own Premake-enabled project.

  if (not options["enable-shared-only"] and not options["enable-static-only"]) then
    project.configs = { "DebugDLL", "ReleaseDLL", "DebugLib", "ReleaseLib" }
  end


-- Project options

  addoption("with-doubles",  "Use double instead of float as base numeric type")
  addoption("with-demos",    "Builds the demo applications and DrawStuff library")
  addoption("with-tests",    "Builds the unit test application")
  addoption("with-gimpact",  "Use GIMPACT for trimesh collisions (experimental)")
  addoption("no-dif",        "Exclude DIF (Dynamics Interchange Format) exports")
  addoption("no-trimesh",    "Exclude trimesh collision geometry")
  addoption("no-alloca",     "Use heap memory instead of the stack (experimental)")


-- If the `--usetargetpath` flag is specified, each set of generated files
-- be placed in a directory named for the target toolset. This flag is
-- used by the `--makeall` command (see below).

  if (options["usetargetpath"]) then
    project.path = options["target"]
  end


-- Set the output directories

  if (options["enable-shared-only"] or options["enable-static-only"]) then
    project.config["Debug"].bindir   = "../lib/debug"
    project.config["Debug"].libdir   = "../lib/debug"
    project.config["Release"].bindir = "../lib/release"
    project.config["Release"].bindir = "../lib/release"
  else
    project.config["DebugDLL"].bindir   = "../lib/DebugDLL"
    project.config["DebugDLL"].libdir   = "../lib/DebugDLL"
    project.config["ReleaseDLL"].bindir = "../lib/ReleaseDLL"
    project.config["ReleaseDLL"].libdir = "../lib/ReleaseDLL"
    project.config["DebugLib"].bindir   = "../lib/DebugLib"
    project.config["DebugLib"].libdir   = "../lib/DebugLib"
    project.config["ReleaseLib"].bindir = "../lib/ReleaseLib"
    project.config["ReleaseLib"].libdir = "../lib/ReleaseLib"
  end


-- Build packages

  dopackage("ode.lua")

  if (options["with-demos"]) then
    dopackage("drawstuff.lua")
    dopackage("demos.lua")
  end

  if (options["with-tests"]) then
    dopackage("tests.lua")
  end
  

-- Remove all intermediate files

  function doclean(cmd, arg)
    docommand(cmd, arg)
    if (options["target"] == "") then
      os.remove("../include/ode/config.h")
    end
    os.rmdir("custom")
    os.rmdir("../lib/debug")
    os.rmdir("../lib/release")
    os.rmdir("../lib/DebugDLL")
    os.rmdir("../lib/DebugLib")
    os.rmdir("../lib/ReleaseDLL")
    os.rmdir("../lib/ReleaseLib")
    os.rmdir("gnu/obj")
    os.rmdir("vs2002/obj")
    os.rmdir("vs2003/obj")
    os.rmdir("vs2005/obj")
  end


-- Generate all toolsets in one go

  function domakeall(cmd, arg)
    os.execute("premake --usetargetpath --with-demos --with-tests --clean --target vs2002")
    os.execute("premake --usetargetpath --with-demos --with-tests --clean --target vs2003")
    os.execute("premake --usetargetpath --with-demos --with-tests --clean --target vs2005")
    os.execute("premake --usetargetpath --with-demos --with-tests --clean --target gnu")
  end
