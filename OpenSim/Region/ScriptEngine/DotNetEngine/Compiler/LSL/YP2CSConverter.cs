using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.YieldProlog;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL
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

            //Console.WriteLine("Mycode\n ===================================\n" + myCode+"\n");
            foreach (bool l1 in Parser.parseInput(TermList))
            {
                foreach (bool l2 in YPCompiler.makeFunctionPseudoCode(TermList, FunctionCode))
                {
                    ListPair VFC = new ListPair(FunctionCode, new Variable());
                    //Console.WriteLine("-------------------------")
                    //Console.WriteLine( FunctionCode.ToString())
                    //Console.WriteLine("-------------------------")
                    YPCompiler.convertFunctionCSharp(FunctionCode);
                    //YPCompiler.convertStringCodesCSharp(VFC);

                }
            }
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
