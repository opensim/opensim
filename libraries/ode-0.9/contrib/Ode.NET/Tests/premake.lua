-- This function creates the test packages
function maketest(name)

  package = newpackage()
  package.name = name
  package.kind = "exe"
  package.language = "c#"

  if (options["with-doubles"]) then
    package.defines = { "dDOUBLE" }
  else
    package.defines = { "dSINGLE " }
  end

  package.links = { 
    "System", 
    "Ode.NET", 
    "Drawstuff.NET" 
  }

  package.files = {
    name .. ".cs"
  }

end

maketest("BoxStack")
