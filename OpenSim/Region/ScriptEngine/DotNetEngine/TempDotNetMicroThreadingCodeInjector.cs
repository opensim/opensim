/*
* Copyright (c) Contributors, http://opensimulator.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.IO;
using Rail.Reflect;
using Rail.Transformation;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    /// <summary>
    /// Tedds Sandbox for RAIL/microtrheading. This class is only for testing purposes!
    /// Its offspring will be the actual implementation.
    /// </summary>
    internal class TempDotNetMicroThreadingCodeInjector
    {
        public static string TestFix(string FileName)
        {
            string ret = Path.GetFileNameWithoutExtension(FileName + "_fixed.dll");

            Console.WriteLine("Loading: \"" + FileName + "\"");
            RAssemblyDef rAssembly = RAssemblyDef.LoadAssembly(FileName);


            //Get the type of the method to copy from assembly Teste2.exe to assembly Teste.exe
            RTypeDef type = (RTypeDef) rAssembly.RModuleDef.GetType("SecondLife.Script");

            //Get the methods in the type
            RMethod[] m = type.GetMethods();

            //Create a MethodPrologueAdder visitor object with the method to add
            //and with the flag that enables local variable creation set to true
            MethodPrologueAdder mpa = new MethodPrologueAdder((RMethodDef) m[0], true);

            //Apply the changes to the assembly
            rAssembly.Accept(mpa);

            //Save the new assembly
            rAssembly.SaveAssembly(ret);

            return ret;
        }
    }
}