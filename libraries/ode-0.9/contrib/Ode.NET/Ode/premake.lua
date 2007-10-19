package.name = "Ode.NET"
package.kind = "dll"
package.language = "c#"

-- Build options

  package.defines = { }

  if (options["with-doubles"]) then
    table.insert(package.defines, "dDOUBLE")
  else
    table.insert(package.defines, "dSINGLE")
  end

  if (options["no-unsafe"]) then
    table.insert(package.defines, "dNO_UNSAFE_CODE")
  else
    package.buildflags = { "unsafe" }
  end
  

-- Files & Libraries

  package.files = {
    "AssemblyInfo.cs",
    "Ode.cs"
  }

  package.links = {
    "System"
  }
