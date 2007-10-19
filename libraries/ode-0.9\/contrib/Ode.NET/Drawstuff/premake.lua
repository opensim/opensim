package.name = "Drawstuff.NET"
package.kind = "dll"
package.language = "c#"

if (options["with-doubles"]) then
  package.defines = { "dDOUBLE" }
else
  package.defines = { "dSINGLE " }
end

package.links = {
  "System",
  "Ode.NET"
}

package.files = {
  "AssemblyInfo.cs",
  "Drawstuff.cs"
}
