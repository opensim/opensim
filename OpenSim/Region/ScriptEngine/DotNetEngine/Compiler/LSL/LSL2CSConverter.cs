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

using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL
{
    
    public class LSL2CSConverter
    {


        // Uses regex to convert LSL code to C# code.

        //private Regex rnw = new Regex(@"[a-zA-Z0-9_\-]", RegexOptions.Compiled);
        private Dictionary<string, string> dataTypes = new Dictionary<string, string>();
        private Dictionary<string, string> quotes = new Dictionary<string, string>();
        private Dictionary<string, scriptEvents> state_events = new Dictionary<string, scriptEvents>();

        public LSL2CSConverter()
        {
            // Only the types we need to convert
            dataTypes.Add("void", "void");
            dataTypes.Add("integer", "int");
            dataTypes.Add("float", "double");
            dataTypes.Add("string", "string");
            dataTypes.Add("key", "string");
            dataTypes.Add("vector", "LSL_Types.Vector3");
            dataTypes.Add("rotation", "LSL_Types.Quaternion");
            dataTypes.Add("list", "LSL_Types.list");
            dataTypes.Add("null", "null");
        }

        public string Convert(string Script)
        {
            quotes.Clear();
            string Return = System.String.Empty;
            Script = " \r\n" + Script;

            //
            // Prepare script for processing
            //

            // Clean up linebreaks
            Script = Regex.Replace(Script, @"\r\n", "\n");
            Script = Regex.Replace(Script, @"\n", "\r\n");


            // QUOTE REPLACEMENT
            // temporarily replace quotes so we can work our magic on the script without
            //  always considering if we are inside our outside quotes's
            // TODO: Does this work on half-quotes in strings? ;)
            string _Script = System.String.Empty;
            string C;
            bool in_quote = false;
            bool quote_replaced = false;
            string quote_replacement_string = "Q_U_O_T_E_REPLACEMENT_";
            string quote = System.String.Empty;
            bool last_was_escape = false;
            int quote_replaced_count = 0;
            for (int p = 0; p < Script.Length; p++)
            {
                C = Script.Substring(p, 1);
                while (true)
                {
                    // found " and last was not \ so this is not an escaped \"
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
                            if (quote == System.String.Empty)
                            {
                                // We didn't replace quote, probably because of empty string?
                                _Script += quote_replacement_string +
                                           quote_replaced_count.ToString().PadLeft(5, "0".ToCharArray()[0]);
                            }
                            // We just left a quote
                            quotes.Add(
                                quote_replacement_string +
                                quote_replaced_count.ToString().PadLeft(5, "0".ToCharArray()[0]), quote);
                            quote = System.String.Empty;
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
                            _Script += quote_replacement_string +
                                       quote_replaced_count.ToString().PadLeft(5, "0".ToCharArray()[0]);
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


            //
            // PROCESS STATES
            // Remove state definitions and add state names to start of each event within state
            //
            int ilevel = 0;
            int lastlevel = 0;
            string ret = System.String.Empty;
            string cache = System.String.Empty;
            bool in_state = false;
            string current_statename = System.String.Empty;
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
                        Match m =
                          //Regex.Match(cache, @"(?![a-zA-Z_]+)\s*([a-zA-Z_]+)[^a-zA-Z_\(\)]*{",
                            Regex.Match(cache, @"(?![a-zA-Z_]+)\s*(state\s+)?(?<statename>[a-zA-Z_]+)[^a-zA-Z_\(\)]*{",

                                        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

                        in_state = false;
                        if (m.Success)
                        {
                            // Go back to level 0, this is not a state
                            in_state = true;
                            current_statename = m.Groups["statename"].Captures[0].Value;
                            //Console.WriteLine("Current statename: " + current_statename);
                            cache =
                                              //@"(?<s1>(?![a-zA-Z_]+)\s*)" + @"([a-zA-Z_]+)(?<s2>[^a-zA-Z_\(\)]*){",
                                Regex.Replace(cache,
                                              @"(?<s1>(?![a-zA-Z_]+)\s*)" + @"(state\s+)?([a-zA-Z_]+)(?<s2>[^a-zA-Z_\(\)]*){",
                                              "${s1}${s2}",
                                              RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        }
                        ret += cache;
                        cache = String.Empty;
                    }
                    if (ilevel == 0 && lastlevel == 1)
                    {
                        // 1 => 0: Remove last }
                        if (in_state == true)
                        {
                            cache = cache.Remove(cache.Length - 1, 1);
                            //cache = Regex.Replace(cache, "}$", String.Empty, RegexOptions.Multiline | RegexOptions.Singleline);

                            //Replace function names
                            // void dataserver(key query_id, string data) {
                            //cache = Regex.Replace(cache, @"([^a-zA-Z_]\s*)((?!if|switch|for)[a-zA-Z_]+\s*\([^\)]*\)[^{]*{)", "$1" + "<STATE>" + "$2", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
                            //Console.WriteLine("Replacing using statename: " + current_statename);
                            cache =
                                Regex.Replace(cache,
                                              @"^(\s*)((?!(if|switch|for|while)[^a-zA-Z0-9_])[a-zA-Z0-9_]*\s*\([^\)]*\)[^;]*\{)",
                                              @"$1public " + current_statename + "_event_$2",
                                              RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        }

                        ret += cache;
                        cache = String.Empty;
                        in_state = true;
                        current_statename = String.Empty;
                    }

                    break;
                }
                lastlevel = ilevel;
            }
            ret += cache;
            cache = String.Empty;

            Script = ret;
            ret = String.Empty;


            foreach (string key in dataTypes.Keys)
            {
                string val;
                dataTypes.TryGetValue(key, out val);

                // Replace CAST - (integer) with (int)
                Script =
                    Regex.Replace(Script, @"\(" + key + @"\)", @"(" + val + ")",
                                  RegexOptions.Compiled | RegexOptions.Multiline);
                // Replace return types and function variables - integer a() and f(integer a, integer a)
                Script =
                    Regex.Replace(Script, @"(^|;|}|[\(,])(\s*)" + key + @"(\s+)", @"$1$2" + val + "$3",
                                  RegexOptions.Compiled | RegexOptions.Multiline);
                Script =
                    Regex.Replace(Script, @"(^|;|}|[\(,])(\s*)" + key + @"(\s*)[,]", @"$1$2" + val + "$3,",
                                  RegexOptions.Compiled | RegexOptions.Multiline);
            }

            // Add "void" in front of functions that needs it
            Script =
                Regex.Replace(Script,
                              @"^(\s*public\s+)?((?!(if|switch|for)[^a-zA-Z0-9_])[a-zA-Z0-9_]*\s*\([^\)]*\)[^;]*\{)",
                              @"$1void $2", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

            // Replace <x,y,z> and <x,y,z,r>
            Script =
                Regex.Replace(Script, @"<([^,>;]*,[^,>;]*,[^,>;]*,[^,>;]*)>", @"new LSL_Types.Quaternion($1)",
                              RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
            Script =
                Regex.Replace(Script, @"<([^,>;)]*,[^,>;]*,[^,>;]*)>", @"new LSL_Types.Vector3($1)",
                              RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

            // Replace List []'s
            Script =
                Regex.Replace(Script, @"\[([^\]]*)\]", @"new LSL_Types.list($1)",
                              RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);


            // Replace (string) to .ToString() //
            Script =
                Regex.Replace(Script, @"\(string\)\s*([a-zA-Z0-9_.]+(\s*\([^\)]*\))?)", @"$1.ToString()",
                              RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
            Script =
                Regex.Replace(Script, @"\((float|int)\)\s*([a-zA-Z0-9_.]+(\s*\([^\)]*\))?)", @"$1.Parse($2)",
                              RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

            // Replace "state STATENAME" with "state("statename")"
            Script =
                    Regex.Replace(Script, @"(state)\s+([^;\n\r]+)(;[\r\n\s])", "$1(\"$2\")$3",
                  RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // REPLACE BACK QUOTES
            foreach (string key in quotes.Keys)
            {
                string val;
                quotes.TryGetValue(key, out val);
                Script = Script.Replace(key, "\"" + val + "\"");
            }


            // Finds out which events are in the script and writes a method call with the events in each state_entry event

            // Note the (?:)? block optional, and not returning a group.   Less greedy then .*

            string[] eventmatches = new string[0];
            //Regex stateevents = new Regex(@"(public void )([^_]+)(_event_)([^\(]+)[\(\)]+\s+[^\{]\{");
            eventmatches = Regex.Split(Script, @"public void\s([^_]+)_event_([^\(]+)\((?:[a-zA-Z0-9\s_,\.\-]+)?\)+\s+[^\{]\{", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
            for (int pos = 0; pos<eventmatches.GetUpperBound(0);pos++)
            {
                pos++; // garbage

                string statea = eventmatches[pos]; pos++;
                string eventa = eventmatches[pos];
                scriptEvents storedEventsForState = scriptEvents.None;
                if (state_events.ContainsKey(statea))
                {
                    storedEventsForState = state_events[statea];
                    state_events[statea] |= convertnametoFlag(eventa);
                }
                else
                {
                    state_events.Add(statea, convertnametoFlag(eventa));
                }
                System.Console.WriteLine("State:" + statea + ", event: " + eventa);
            }
            System.Console.WriteLine("Matches:" + eventmatches.GetUpperBound(0));
            // Add namespace, class name and inheritance

            // Looking *ONLY* for state entry events
            string scriptCopy = "";
            
            //Only match State_Entry events now
            // Note the whole regex is a group, then we have the state this entry belongs to.
            eventmatches = Regex.Split(Script, @"(public void\s([^_]+)_event_state_entry[\(\)]+\s+[^\{]\{)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
            int endloop = eventmatches.GetUpperBound(0);
            for (int pos = 0; pos < endloop; pos++)
            {
                // Returns text before state entry match,
                scriptCopy += eventmatches[pos]; pos++;

                // Returns text of state entry match, 
                scriptCopy += eventmatches[pos]; pos++;
                
                // Returns which state we're matching and writes a method call to the end of the above state_entry
                scriptCopy += "\r\n\t\tosSetStateEvents((int)" + (int)state_events[eventmatches[pos]] + ");"; //pos++;
                
                // adds the remainder of the script.
                if ((pos + 1) == endloop)
                {
                    pos++;
                    scriptCopy += eventmatches[pos++];
                }
                
            }
            // save modified script.
            Script = scriptCopy;
            //System.Console.WriteLine(Script);
            Return = String.Empty;// +
                     //"using OpenSim.Region.ScriptEngine.Common; using System.Collections.Generic;";
  

            //Return += String.Empty +
            //          "namespace SecondLife { ";
            //Return += String.Empty +
            //          //"[Serializable] " +
            //          "public class Script : OpenSim.Region.ScriptEngine.Common.LSL_BaseClass { ";
            //Return += @"public Script() { } ";
            Return += Script;
            //Return += "} }\r\n";

            state_events.Clear();
            quotes.Clear();

            return Return;
        }
        public scriptEvents convertnametoFlag(string eventname)
        {
            switch (eventname)
            {
                case "attach":
                    return scriptEvents.attach;
                    //break;
               // case "at_rot_target":
                    //return (long)scriptEvents.at_rot_target;
                    //break;
                //case "at_target":
                    //return (long)scriptEvents.at_target;
                    //break;
                //case "changed":
                    //return (long)scriptEvents.changed;
                    //break;
                case "collision":
                    return scriptEvents.collision;
                   // break;
                case "collision_end":
                    return scriptEvents.collision_end;
                    //break;
                case "collision_start":
                    return scriptEvents.collision_start;
                   // break;
                case "control":
                    return scriptEvents.control;
                    //break;
                case "dataserver":
                    return scriptEvents.dataserver;
                   // break;
                case "email":
                    return scriptEvents.email;
                   // break;
                case "http_response":
                    return scriptEvents.http_response;
                   // break;
                case "land_collision":
                    return scriptEvents.land_collision;
                   // break;
                case "land_collision_end":
                    return scriptEvents.land_collision_end;
                   // break;
                case "land_collision_start":
                    return scriptEvents.land_collision_start;
                   // break;
                case "link_message":
                    return scriptEvents.link_message;
                  //  break;
                case "listen":
                    return scriptEvents.listen;
                  //  break;
                case "money":
                    return scriptEvents.money;
                   // break;
                case "moving_end":
                    return scriptEvents.moving_end;
                   // break;
                case "moving_start":
                    return scriptEvents.moving_start;
                   // break;
                case "not_at_rot_target":
                    return scriptEvents.not_at_rot_target;
                   // break;
                case "not_at_target":
                    return scriptEvents.not_at_target;
                  //  break;
               // case "no_sensor":
                    //return (long)scriptEvents.no_sensor;
                    //break;
                //case "on_rez":
                    //return (long)scriptEvents.on_rez;
                  //  break;
                case "remote_data":
                    return scriptEvents.remote_data;
                   // break;
                case "run_time_permissions":
                    return scriptEvents.run_time_permissions;
                   // break;
                //case "sensor":
                    //return (long)scriptEvents.sensor;
                  //  break;
                case "state_entry":
                    return scriptEvents.state_entry;
                   // break;
                case "state_exit":
                    return scriptEvents.state_exit;
                   // break;
                case "timer":
                    return scriptEvents.timer;
                   // break;
                case "touch":
                    return scriptEvents.touch;
                   // break;
                case "touch_end":
                    return scriptEvents.touch_end;
                   // break;
                case "touch_start":
                    return scriptEvents.touch_start;
                   // break;
                case "object_rez":
                    return scriptEvents.object_rez;
                default:
                    return 0;
                    //break;
            }
            //return 0;
        }
    }

    [Flags]
    public enum scriptEvents : int
    {
        None = 0,
        attach = 1,
        collision = 15,
        collision_end = 32,
        collision_start = 64,
        control = 128,
        dataserver = 256,
        email = 512,
        http_response = 1024,
        land_collision = 2048,
        land_collision_end = 4096,
        land_collision_start = 8192,
        link_message = 16384,
        listen = 32768,
        money = 65536,
        moving_end = 131072,
        moving_start = 262144,
        not_at_rot_target = 524288,
        not_at_target = 1048576,
        remote_data = 8388608,
        run_time_permissions = 268435456,
        state_entry = 1073741824,
        state_exit = 2,
        timer = 4,
        touch = 8,
        touch_end = 536870912,
        touch_start = 2097152,
        object_rez = 4194304
    }
}