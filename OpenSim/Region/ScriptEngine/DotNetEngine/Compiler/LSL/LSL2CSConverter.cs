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


            // QUOTE REPLACEMENT
            //char[] SA = Script.ToCharArray();
            string _Script = "";
            string C;
            bool in_quote = false;
            bool quote_replaced = false;
            string quote_replacement_string = "Q_U_O_T_E_REPLACEMENT_";
            string quote = "";
            bool last_was_escape = false;
            int quote_replaced_count = 0;
            for (int p = 0; p < Script.Length; p++)
            {

                C = Script.Substring(p, 1);
                while (true)
                {
                    if (C == "\"" && last_was_escape == false)
                    {
                        // Toggle inside/outside quote
                        in_quote = !in_quote;
                        if (in_quote)
                        {
                            quote_replaced_count++;
                        }
                        else
                        {
                            // We just left a quote
                            QUOTES.Add(quote_replacement_string + quote_replaced_count.ToString().PadLeft(5, "0".ToCharArray()[0]), quote);
                            quote = "";
                        }
                        break;
                    }
                    if (!in_quote)
                    {
                        // We are not inside a quote
                        quote_replaced = false;

                    }
                    else
                    {
                        // We are inside a quote
                        if (!quote_replaced)
                        {
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
                last_was_escape = false;
                if (C == @"\")
                {
                    last_was_escape = true;
                }
            }
            Script = _Script;
            //
            // END OF QUOTE REPLACEMENT
            //




            // PROCESS STATES
            int ilevel = 0;
            int lastlevel = 0;
            string ret = "";
            string cache = "";
            bool in_state = false;
            string current_statename = "";
            for (int p = 0; p < Script.Length; p++)
            {
                C = Script.Substring(p, 1);
                while (true)
                {
                    // inc / dec level
                    if (C == @"{")
                        ilevel++;
                    if (C == @"}")
                        ilevel--;
                    if (ilevel < 0)
                        ilevel = 0;
                    cache += C;

                    // if level == 0, add to return
                    if (ilevel == 1 && lastlevel == 0)
                    {
                        // 0 => 1: Get last 
                        Match m = Regex.Match(cache, @"(?![a-zA-Z_]+)\s*([a-zA-Z_]+)[^a-zA-Z_\(\)]*{", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

                        in_state = false;
                        if (m.Success)
                        {
                            // Go back to level 0, this is not a state
                            in_state = true;
                            current_statename = m.Groups[1].Captures[0].Value;
                            Console.WriteLine("Current statename: " + current_statename);
                            cache = Regex.Replace(cache, @"(?![a-zA-Z_]+)\s*([a-zA-Z_]+)[^a-zA-Z_\(\)]*{", "", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
                        }
                        ret += cache;
                        cache = "";
                    }
                    if (ilevel == 0 && lastlevel == 1)
                    {
                        // 1 => 0: Remove last }
                        if (in_state == true)
                        {
                            cache = cache.Remove(cache.Length - 1, 1);
                            //cache = Regex.Replace(cache, "}$", "", RegexOptions.Multiline | RegexOptions.Singleline);

                            //Replace function names
                            // void dataserver(key query_id, string data) {
                            //cache = Regex.Replace(cache, @"([^a-zA-Z_]\s*)((?!if|switch|for)[a-zA-Z_]+\s*\([^\)]*\)[^{]*{)", "$1" + "<STATE>" + "$2", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
                            Console.WriteLine("Replacing using statename: " + current_statename);
                            cache = Regex.Replace(cache, @"^(\s*)((?!if|switch|for)[a-zA-Z0-9_]*\s*\([^\)]*\)[^;]*\{)", @"$1" + current_statename + "_$2", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
                        }

                        ret += cache;
                        cache = "";
                        in_state = true;
                        current_statename = "";
                    }

                    // if we moved from level 0 to level 1 don't add last word + {
                    // if we moved from level 1 to level 0 don't add last }
                    // if level > 0 cache all data so we can run regex on it when going to level 0




                    break;
                }
                lastlevel = ilevel;
            }
            ret += cache;
            cache = "";

            Script = ret;
            ret = "";



            // Replace CAST - (integer) with (int)
            Script = Regex.Replace(Script, @"\(integer\)", @"(int)", RegexOptions.Compiled | RegexOptions.Multiline);
            // Replace return types and function variables - integer a() and f(integer a, integer a)
            Script = Regex.Replace(Script, @"(^|;|}|[\(,])(\s*int)eger(\s*)", @"$1$2$3", RegexOptions.Compiled | RegexOptions.Multiline);

            // Add "void" in front of functions that needs it
            Script = Regex.Replace(Script, @"^(\s*)((?!if|switch|for)[a-zA-Z0-9_]*\s*\([^\)]*\)[^;]*\{)", @"$1void $2", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

            // Replace <x,y,z> and <x,y,z,r>
            Script = Regex.Replace(Script, @"<([^,>]*,[^,>]*,[^,>]*,[^,>]*)>", @"Rotation.Parse($1)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
            Script = Regex.Replace(Script, @"<([^,>]*,[^,>]*,[^,>]*)>", @"Vector.Parse($1)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

            // Replace List []'s
            Script = Regex.Replace(Script, @"\[([^\]]*)\]", @"List.Parse($1)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);


            // Replace (string) to .ToString() //
            Script = Regex.Replace(Script, @"\(string\)\s*([a-zA-Z0-9_]+(\s*\([^\)]*\))?)", @"$1.ToString()", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
            Script = Regex.Replace(Script, @"\((float|int)\)\s*([a-zA-Z0-9_]+(\s*\([^\)]*\))?)", @"$1.Parse($2)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);


            // REPLACE BACK QUOTES
            foreach (string key in QUOTES.Keys)
            {
                string val;
                QUOTES.TryGetValue(key, out val);
                Script = Script.Replace(key, "\"" + val + "\"");
            }


            Return = "namespace SecondLife {" + Environment.NewLine;
            Return += "public class Script : LSL_BaseClass {" + Environment.NewLine;
            Return += Script;
            Return += "} }" + Environment.NewLine;

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
