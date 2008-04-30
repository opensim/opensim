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
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL
{
    public static class EventReaderRewriter
    {
        

        public static string ReWriteScriptWithPublishedEventsCS(string Script)
        {
            Dictionary<string, scriptEvents> state_events = new Dictionary<string, scriptEvents>();
            // Finds out which events are in the script and writes a method call with the events in each state_entry event

            // Note the (?:)? block optional, and not returning a group.   Less greedy then .*

            string[] eventmatches = new string[0];
            //Regex stateevents = new Regex(@"(public void )([^_]+)(_event_)([^\(]+)[\(\)]+\s+[^\{]\{");
            eventmatches = Regex.Split(Script, @"public void\s([^_]+)_event_([^\(]+)\((?:[a-zA-Z0-9\s_,\.\-]+)?\)(?:[^\{]+)?\{", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
            for (int pos = 0; pos < eventmatches.GetUpperBound(0); pos++)
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
                Console.WriteLine("State:" + statea + ", event: " + eventa);
            }
            Console.WriteLine("Matches:" + eventmatches.GetUpperBound(0));
            // Add namespace, class name and inheritance

            // Looking *ONLY* for state entry events
            string scriptCopy = "";

            //Only match State_Entry events now
            // Note the whole regex is a group, then we have the state this entry belongs to.
            eventmatches = Regex.Split(Script, @"(public void\s([^_]+)_event_state_entry[\(\)](?:[^\{]+)?\{)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
            int endloop = eventmatches.GetUpperBound(0);

            // Add all the states to a list of 
            List<string> unUsedStates = new List<string>();

            foreach (string state in state_events.Keys)
            {
                unUsedStates.Add(state);
            }

            // If endloop is 0, then there are no state entry events in the entire script.
            // Stick a default state entry in there.
            if (endloop == 0)
            {
                if (state_events.ContainsKey("default"))
                {
                    scriptCopy = "public void default_event_state_entry() {osSetStateEvents((int)" + (int)state_events["default"] + "); } " + Script;
                    unUsedStates.Remove("default");
                }
                else
                {
                    throw new Exception("You must define a default state. Compile failed.  See LSL documentation for more details.");
                }
            }

            // Loop over state entry events and rewrite the first line to define the events the state listens for.
            for (int pos = 0; pos < endloop; pos++)
            {
                // Returns text before state entry match,
                scriptCopy += eventmatches[pos]; pos++;

                // Returns text of state entry match, 
                scriptCopy += eventmatches[pos]; pos++;

                // Returns which state we're matching and writes a method call to the end of the above state_entry
                scriptCopy += "osSetStateEvents((int)" + (int)state_events[eventmatches[pos]] + ");"; //pos++;

                // Remove the state from the unused list.   There might be more states matched then defined, so we 
                // check if the state was defined first
                if (unUsedStates.Contains(eventmatches[pos]))
                    unUsedStates.Remove(eventmatches[pos]);

                // adds the remainder of the script.
                if ((pos + 1) == endloop)
                {
                    pos++;
                    scriptCopy += eventmatches[pos++];
                }

            }

            // states with missing state_entry blocks won't publish their events, 
            // so, to fix that we write a state entry with only the event publishing method for states missing a state_entry event
            foreach (string state in unUsedStates)
            {
                // Write the remainder states out into a blank state entry with the event setting routine
                scriptCopy = "public void " + state + "_event_state_entry() {tosSetStateEvents((int)" + (int)state_events[state] + ");} " + scriptCopy;
            }

            // save modified script.
            unUsedStates.Clear();
            state_events.Clear();
            return scriptCopy;
        }

        public static string ReWriteScriptWithPublishedEventsJS(string Script)
        {
            Dictionary<string, scriptEvents> state_events = new Dictionary<string, scriptEvents>();
            // Finds out which events are in the script and writes a method call with the events in each state_entry event

            // Note the (?:)? block optional, and not returning a group.   Less greedy then .*

            string[] eventmatches = new string[0];
            //Regex stateevents = new Regex(@"(public void )([^_]+)(_event_)([^\(]+)[\(\)]+\s+[^\{]\{");
            eventmatches = Regex.Split(Script, @"function \s([^_]+)_event_([^\(]+)\((?:[a-zA-Z0-9\s_,\.\-]+)?\)(?:[^\{]+)?\{", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
            for (int pos = 0; pos < eventmatches.GetUpperBound(0); pos++)
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
                Console.WriteLine("State:" + statea + ", event: " + eventa);
            }
            Console.WriteLine("Matches:" + eventmatches.GetUpperBound(0));
            // Add namespace, class name and inheritance

            // Looking *ONLY* for state entry events
            string scriptCopy = "";

            //Only match State_Entry events now
            // Note the whole regex is a group, then we have the state this entry belongs to.
            eventmatches = Regex.Split(Script, @"(function \s([^_]+)_event_state_entry[\(\)](?:[^\{]+)?\{)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
            int endloop = eventmatches.GetUpperBound(0);

            // Add all the states to a list of 
            List<string> unUsedStates = new List<string>();

            foreach (string state in state_events.Keys)
            {
                unUsedStates.Add(state);
            }

            // If endloop is 0, then there are no state entry events in the entire script.
            // Stick a default state entry in there.
            if (endloop == 0)
            {
                if (state_events.ContainsKey("default"))
                {
                    scriptCopy = "function default_event_state_entry() {osSetStateEvents(" + (int)state_events["default"] + "); } " + Script;
                    unUsedStates.Remove("default");
                }
                else
                {
                    throw new Exception("You must define a default state. Compile failed.  See LSL documentation for more details.");
                }
            }

            // Loop over state entry events and rewrite the first line to define the events the state listens for.
            for (int pos = 0; pos < endloop; pos++)
            {
                // Returns text before state entry match,
                scriptCopy += eventmatches[pos]; pos++;

                // Returns text of state entry match, 
                scriptCopy += eventmatches[pos]; pos++;

                // Returns which state we're matching and writes a method call to the end of the above state_entry
                scriptCopy += "osSetStateEvents(" + (int)state_events[eventmatches[pos]] + ");"; //pos++;

                // Remove the state from the unused list.   There might be more states matched then defined, so we 
                // check if the state was defined first
                if (unUsedStates.Contains(eventmatches[pos]))
                    unUsedStates.Remove(eventmatches[pos]);

                // adds the remainder of the script.
                if ((pos + 1) == endloop)
                {
                    pos++;
                    scriptCopy += eventmatches[pos++];
                }

            }

            // states with missing state_entry blocks won't publish their events, 
            // so, to fix that we write a state entry with only the event publishing method for states missing a state_entry event
            foreach (string state in unUsedStates)
            {
                // Write the remainder states out into a blank state entry with the event setting routine
                scriptCopy = "function " + state + "_event_state_entry() {tosSetStateEvents(" + (int)state_events[state] + ");} " + scriptCopy;
            }

            // save modified script.
            unUsedStates.Clear();
            state_events.Clear();
            return scriptCopy;
        }


        public static string ReWriteScriptWithPublishedEventsVB(string Script)
        {
            Dictionary<string, scriptEvents> state_events = new Dictionary<string, scriptEvents>();
            // Finds out which events are in the script and writes a method call with the events in each state_entry event

            // Note the (?:)? block optional, and not returning a group.   Less greedy then .*

            string[] eventmatches = new string[0];
            //Regex stateevents = new Regex(@"(public void )([^_]+)(_event_)([^\(]+)[\(\)]+\s+[^\{]\{");
            eventmatches = Regex.Split(Script, @"Public Sub\s([^_]+)_event_([^\(]+)\((?:[a-zA-Z0-9\s_,\.\-]+)?\)(?:[^()])", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
            for (int pos = 0; pos < eventmatches.GetUpperBound(0); pos++)
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
                Console.WriteLine("State:" + statea + ", event: " + eventa);
            }
            Console.WriteLine("Matches:" + eventmatches.GetUpperBound(0));
            // Add namespace, class name and inheritance

            // Looking *ONLY* for state entry events
            string scriptCopy = "";

            //Only match State_Entry events now
            // Note the whole regex is a group, then we have the state this entry belongs to.
            eventmatches = Regex.Split(Script, @"(Public Sub\s([^_]+)_event_state_entry(?:\s+)?\(\))", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
            int endloop = eventmatches.GetUpperBound(0);

            // Add all the states to a list of 
            List<string> unUsedStates = new List<string>();

            foreach (string state in state_events.Keys)
            {
                unUsedStates.Add(state);
            }

            // If endloop is 0, then there are no state entry events in the entire script.
            // Stick a default state entry in there.
            if (endloop == 0)
            {
                if (state_events.ContainsKey("default"))
                {
                    scriptCopy = "function default_event_state_entry() {osSetStateEvents(" + (int)state_events["default"] + "); } " + Script;
                    unUsedStates.Remove("default");
                }
                else
                {
                    throw new Exception("You must define a default state. Compile failed.  See LSL documentation for more details.");
                }
            }

            // Loop over state entry events and rewrite the first line to define the events the state listens for.
            for (int pos = 0; pos < endloop; pos++)
            {
                // Returns text before state entry match,
                scriptCopy += eventmatches[pos]; pos++;

                // Returns text of state entry match, 
                scriptCopy += eventmatches[pos]; pos++;

                // Returns which state we're matching and writes a method call to the end of the above state_entry
                scriptCopy += "osSetStateEvents(" + (int)state_events[eventmatches[pos]] + ");"; //pos++;

                // Remove the state from the unused list.   There might be more states matched then defined, so we 
                // check if the state was defined first
                if (unUsedStates.Contains(eventmatches[pos]))
                    unUsedStates.Remove(eventmatches[pos]);

                // adds the remainder of the script.
                if ((pos + 1) == endloop)
                {
                    pos++;
                    scriptCopy += eventmatches[pos++];
                }

            }

            // states with missing state_entry blocks won't publish their events, 
            // so, to fix that we write a state entry with only the event publishing method for states missing a state_entry event
            foreach (string state in unUsedStates)
            {
                // Write the remainder states out into a blank state entry with the event setting routine
                scriptCopy = "function " + state + "_event_state_entry() {tosSetStateEvents(" + (int)state_events[state] + ");} " + scriptCopy;
            }

            // save modified script.
            unUsedStates.Clear();
            state_events.Clear();
            return scriptCopy;
        }


        private static scriptEvents convertnametoFlag(string eventname)
        {
            switch (eventname)
            {
                case "attach":
                    return scriptEvents.attach;
                //break;
                // case "at_rot_target":
                //return (long)scriptEvents.at_rot_target;
                //break;
                case "at_target":
                    return scriptEvents.at_target;
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
                //case "link_message":
                //return scriptEvents.link_message;
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
        at_target = 16384,
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
