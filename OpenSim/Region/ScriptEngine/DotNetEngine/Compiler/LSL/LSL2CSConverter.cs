using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LSL2CS.Converter
{
    public class LSL2CSConverter
    {
        //private Regex rnw = new Regex(@"[a-zA-Z0-9_\-]", RegexOptions.Compiled);
        private Dictionary<string, string> DataTypes = new Dictionary<string, string>();
        private Dictionary<string, string> QUOTES = new Dictionary<string, string>();
        
        public LSL2CSConverter()
        {
            DataTypes.Add("void", "void");
            DataTypes.Add("integer", "int");
            DataTypes.Add("float", "float");
            DataTypes.Add("string", "string");
            DataTypes.Add("key", "string");
            DataTypes.Add("vector", "vector");
            DataTypes.Add("rotation", "rotation");
            DataTypes.Add("list", "list");
            DataTypes.Add("null", "null");
        }



        public string Convert(string Script)
        {
            string Return = "";

            //
            // Prepare script for processing
            //

            // Here we will remove those linebreaks that are allowed in so many languages.
            // One hard-ass regex should do the job.

            // Then we will add some linebreaks.
            Script = Regex.Replace(Script, @"\r\n", "\n");
            Script = Regex.Replace(Script, @"\n", "\r\n");
            //Script = Regex.Replace(Script, @"\n\s*{", @"{");
            //Script = Regex.Replace(Script, @"[\r\n]", @"");

            // Then we remove unwanted linebreaks            


            ////
            //// Split up script to process line by line
            ////
            //int count = 0;
            //string line;
            //Regex r = new Regex("\r?\n", RegexOptions.Multiline | RegexOptions.Compiled);
            //string[] lines = r.Split(Script);

            //for (int i = 0; i < lines.Length; i++)
            //{
            //    // Remove space in front and back
            //    line = lines[i].Trim();
            //    count++;
            //    Return += count + ". " + translate(line) + "\r\n";
            //}

            int level = 0;
            bool Level0Set = false;
            bool Level1Set = false;


            // QUOTE REPLACEMENT
            //char[] SA = Script.ToCharArray();
            string _Script = "";
            string C;
            bool in_quote = false;
            bool quote_replaced = false;
            string quote_replacement_string = "Q_U_O_T_E_REPLACEMENT_";
            string quote = "";
            int quote_replaced_count = 0;
            for (int p = 0; p < Script.Length; p++)
            {

                while (true) {
                    C = Script.Substring(p,1);
                    if (C == "\"") {
                        // Toggle inside/outside quote
                        in_quote = ! in_quote;
                        if(in_quote) {
                            quote_replaced_count++;
                        } else {
                            // We just left a quote
                            QUOTES.Add(quote_replacement_string + quote_replaced_count.ToString().PadLeft(5, "0".ToCharArray()[0]), quote);
                            quote = "";
                        }
                        break;
                    }
                    if (!in_quote) {
                        // We are not inside a quote
                        quote_replaced = false;

                    } else {
                        // We are inside a quote
                        if (!quote_replaced) {
                            // Replace quote
                            _Script += quote_replacement_string + quote_replaced_count.ToString().PadLeft(5, "0".ToCharArray()[0]);
                            quote_replaced = true;
                        }
                        quote += C;
                        break;
                    }
                    _Script += C;
                break;
                }
            }
            //
            // END OF QUOTE REPLACEMENT
            //

            // Replace CAST - (integer) with (int)
            Script = Regex.Replace(_Script, @"\(integer\)", @"(int)", RegexOptions.Compiled | RegexOptions.Multiline);
            // Replace return types and function variables - integer a() and f(integer a, integer a)
            _Script = Regex.Replace(Script, @"(^|[\(,])(\s*int)eger(\s*)", @"$1$2$3", RegexOptions.Compiled | RegexOptions.Multiline);

            // Add void to functions not having that
            Script = Regex.Replace(_Script, @"^(\s*)((?!if|switch|for|foreach)[a-zA-Z0-9_]*\s*\([^\)]*\)[^;]*\{)", @"$1void $2", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);


            // Replace int (TEMPORARY QUICKFIX)
            //_Script = Regex.Replace(Script, "integer", "int", RegexOptions.Compiled);
            
            foreach (string key in QUOTES.Keys)
            {
                string val;
                QUOTES.TryGetValue(key, out val);
                _Script = Script.Replace(key, "\"" + val + "\"");
                Script = _Script;
            }


            Return = Script;

            return Return;
        }
        //private string GetWord(char[] SA, int p)
        //{
        //    string ret = "";
        //    for (int i = p; p < SA.Length; i++)
        //    {
        //        if (!rnw.IsMatch(SA[i].ToString()))
        //            break;
        //        ret += SA[i];
        //    }
        //    return ret;
        //}

    }
}
