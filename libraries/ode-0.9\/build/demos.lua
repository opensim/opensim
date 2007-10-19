-- Here are the lists of demos to build. Add/remove new
-- demos here and everything else should just work

  local demos =
  {
    "boxstack",
    "buggy",
    "chain1",
    "chain2",
    "collision",
    "crash",
    "feedback",
    "friction",
    "heightfield",
    "hinge",
    "I",
    "jointsPR",
    "joints",
    "motor",
    "ode",
    "plane2d",
    "slider",
    "space",
    "space_stress",
    "step"
  }

  if (not options["no-trimesh"]) then
    table.insert(demos, "basket")
    if (not options["no-cylinder"]) then
      table.insert(demos, "cyl")
    end
    table.insert(demos, "moving_trimesh")
    table.insert(demos, "trimesh")
  end

  if (not options["no-cylinder"]) then
    table.insert(demos, "cylvssphere")
  end


-- Separate distribution files into toolset subdirectories

  if (options["usetargetpath"]) then
    packagepath = options["target"]
  else
    packagepath = "custom"
  end


-- Factory function for demo packages

  function makedemo(index, name)
    package = newpackage()
    package.name = "demo_" .. name
    package.kind = "exe"
    package.language = "c++"
    package.path = packagepath
    package.objdir = "obj/"..name

    package.includepaths = { "../../include" }
    package.defines = { "_CRT_SECURE_NO_DEPRECATE" }

    if (options.target == "vs6" or options.target == "vs2002" or options.target == "vs2003") then
      package.config.DebugLib.buildflags   = { "static-runtime" }
      package.config.ReleaseLib.buildflags = { "static-runtime" }
    end

    package.links = { "ode", "drawstuff" }
    if (windows) then
      table.insert(package.links, { "user32", "winmm", "gdi32", "opengl32", "glu32" })
    else
      table.insert(package.links, { "GL", "GLU" })
    end

    if (name == "chain1") then
      package.files = { "../../ode/demo/demo_" .. name .. ".c" }
    else
      package.files = { "../../ode/demo/demo_" .. name .. ".cpp" }
    end

    if (windows) then
      table.insert(package.defines, "WIN32")
      table.insert(package.files, "../../drawstuff/src/resources.rc")
    end
  end

  table.foreach(demos, makedemo)
