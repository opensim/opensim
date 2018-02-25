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
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace OpenSim.Region.ScriptEngine.Yengine
{

    public class InternalFuncDict: VarDict
    {
        /**
         * @brief build dictionary of internal functions from an interface.
         * @param iface = interface with function definitions
         * @param inclSig = true: catalog by name with arg sig, eg, llSay(integer,string)
         *                 false: catalog by simple name only, eg, state_entry
         * @returns dictionary of function definition tokens
         */
        public InternalFuncDict(Type iface, bool inclSig)
            : base(false)
        {
             // Loop through list of all methods declared in the interface.
            System.Reflection.MethodInfo[] ifaceMethods = iface.GetMethods();
            foreach(System.Reflection.MethodInfo ifaceMethod in ifaceMethods)
            {
                string key = ifaceMethod.Name;

                 // Only do ones that begin with lower-case letters...
                 // as any others can't be referenced by scripts
                if((key[0] < 'a') || (key[0] > 'z'))
                    continue;

                try
                {
                     // Create a corresponding TokenDeclVar struct.
                    System.Reflection.ParameterInfo[] parameters = ifaceMethod.GetParameters();
                    TokenArgDecl argDecl = new TokenArgDecl(null);
                    for(int i = 0; i < parameters.Length; i++)
                    {
                        System.Reflection.ParameterInfo param = parameters[i];
                        TokenType type = TokenType.FromSysType(null, param.ParameterType);
                        TokenName name = new TokenName(null, param.Name);
                        argDecl.AddArg(type, name);
                    }
                    TokenDeclVar declFunc = new TokenDeclVar(null, null, null);
                    declFunc.name = new TokenName(null, key);
                    declFunc.retType = TokenType.FromSysType(null, ifaceMethod.ReturnType);
                    declFunc.argDecl = argDecl;

                     // Add the TokenDeclVar struct to the dictionary.
                    this.AddEntry(declFunc);
                }
                catch(Exception except)
                {

                    string msg = except.ToString();
                    int i = msg.IndexOf("\n");
                    if(i > 0)
                        msg = msg.Substring(0, i);
                    Console.WriteLine("InternalFuncDict*: {0}:     {1}", key, msg);

                    ///??? IGNORE ANY THAT FAIL - LIKE UNRECOGNIZED TYPE ???///
                }
            }
        }
    }
}
