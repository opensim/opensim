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
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using OpenSim.Region.ScriptEngine.Shared.YieldProlog;

namespace OpenSim.Region.ScriptEngine.Shared.CodeTools
{
    public class YP2CSConverter
    {
        public YP2CSConverter()
        {
        }

        public string Convert(string Script)
        {
            string CS_code = GenCode(Script);
            return CS_code;
        }

        static string GenCode(string myCode)
        {
            Variable TermList = new Variable();
            Variable FunctionCode = new Variable();

            string CS_code = "";

            int cs_pointer = myCode.IndexOf("\n//cs");
            if (cs_pointer > 0)
            {
                CS_code = myCode.Substring(cs_pointer); // CS code comes after
                myCode = myCode.Substring(0, cs_pointer);
            }
            myCode.Replace("//yp", "%YPCode");

            StringWriter myCS_SW = new StringWriter();
            StringReader myCode_SR = new StringReader(" yp_nop_header_nop. \n "+myCode + "\n");

            YP.see(myCode_SR);
            YP.tell(myCS_SW);

            //m_log.Debug("Mycode\n ===================================\n" + myCode+"\n");

// disable warning: don't see how we can code this differently short
// of rewriting the whole thing
#pragma warning disable 0168, 0219
            foreach (bool l1 in Parser.parseInput(TermList))
            {
                foreach (bool l2 in YPCompiler.makeFunctionPseudoCode(TermList, FunctionCode))
                {
                    // ListPair VFC = new ListPair(FunctionCode, new Variable());
                    //m_log.Debug("-------------------------")
                    //m_log.Debug(FunctionCode.ToString())
                    //m_log.Debug("-------------------------")
                    YPCompiler.convertFunctionCSharp(FunctionCode);
                    //YPCompiler.convertStringCodesCSharp(VFC);
                }
            }
#pragma warning restore 0168, 0219
            YP.seen();
            myCS_SW.Close();
            YP.told();
            StringBuilder bu = myCS_SW.GetStringBuilder();
            string finalcode = "//YPEncoded\n" + bu.ToString();
            // FIX script events (we're in the same script)
            // 'YP.script_event(Atom.a(@"sayit"),' ==> 'sayit('
            finalcode = Regex.Replace(finalcode,
                                        @"YP.script_event\(Atom.a\(\@\""(.*?)""\)\,",
                                        @"this.$1(",
                                        RegexOptions.Compiled | RegexOptions.Singleline);
            finalcode = Regex.Replace(finalcode,
                                        @"YP.script_event\(Atom.a\(\""(.*?)""\)\,",
                                        @"this.$1(",
                                        RegexOptions.Compiled | RegexOptions.Singleline);
            finalcode = Regex.Replace(finalcode,
                            @" static ",
                            @" ",
                            RegexOptions.Compiled | RegexOptions.Singleline);

            finalcode = CS_code+"\n\r"+ finalcode;
            finalcode = Regex.Replace(finalcode,
                                        @"PrologCallback",
                                        @"public IEnumerable<bool> ",
                                        RegexOptions.Compiled | RegexOptions.Singleline);
            return finalcode;
        }
    }
}
