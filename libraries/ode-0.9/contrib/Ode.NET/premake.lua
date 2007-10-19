project.name = "Ode.NET"

-- Target checking

  if (target and target ~= "vs2005" and target ~= "gnu") then
    error("Ode.NET requires a .NET 2.0 compiler")
  end
      

-- Project options

  addoption("with-doubles",  "Use double instead of float as base numeric type")
  addoption("with-tests",    "Builds the test applications and DrawStuff library")
  addoption("no-unsafe",     "Exclude functions using unsafe code (dBodyGetPosition, etc.)")


-- Build settings

  project.config["Debug"].bindir = "bin/Debug"
  project.config["Release"].bindir = "bin/Release"
  

-- Packages

  if (options["with-tests"]) then
    dopackage("Tests")
    dopackage("Drawstuff")
  end
  dopackage("Ode")
