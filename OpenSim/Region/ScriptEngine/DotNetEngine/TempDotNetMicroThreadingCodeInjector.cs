using System;
using System.Collections.Generic;
using System.Text;
using Rail.Transformation;
using Rail.Reflect;
using Rail.Exceptions;
using Rail.MSIL;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    /// <summary>
    /// Tedds Sandbox for RAIL/microtrheading. This class is only for testing purposes!
    /// Its offspring will be the actual implementation.
    /// </summary>
    class TempDotNetMicroThreadingCodeInjector
    {
        public static string TestFix(string FileName)
        {
            string ret = System.IO.Path.GetFileNameWithoutExtension(FileName + "_fixed.dll");

            Console.WriteLine("Loading: \"" + FileName + "\"");
            RAssemblyDef rAssembly = RAssemblyDef.LoadAssembly(FileName);
            

            //Get the type of the method to copy from assembly Teste2.exe to assembly Teste.exe
            RTypeDef type = (RTypeDef)rAssembly.RModuleDef.GetType("SecondLife.Script");

            //Get the methods in the type
            RMethod[] m = type.GetMethods();

            //Create a MethodPrologueAdder visitor object with the method to add
            //and with the flag that enables local variable creation set to true
            MethodPrologueAdder mpa = new MethodPrologueAdder((RMethodDef)m[0], true);

            //Apply the changes to the assembly
            rAssembly.Accept(mpa);

            //Save the new assembly
            rAssembly.SaveAssembly(ret);

            return ret;

        }
    }
}
