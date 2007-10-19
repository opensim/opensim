  package.name = "tests"
  package.kind = "exe"
  package.language = "c++"
  package.path = packagepath
  package.objdir = "obj/tests"

  package.includepaths =
  {
    "../../include",
    "../../tests/CppTestHarness"
  }

  package.defines =
  {
    "_CRT_SECURE_NO_DEPRECATE"
  }

  package.links =
  {
    "ode"
  }

  package.files =
  {
    matchrecursive("../../tests/*.h", "../../tests/*.cpp")
  }
