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
using System.Linq;
//using log4net;

namespace OpenSim.Framework.Console
{
    public class ConsoleLevel
    {
        public string m_string;

        ConsoleLevel(string v)
        {
            m_string = v;
        }

        public static implicit operator ConsoleLevel(string s)
        {
            return new ConsoleLevel(s);
        }

        public static string ToString(ConsoleLevel s)
        {
            return s.m_string;
        }

        public override string ToString()
        {
            return m_string;
        }
    }


    public class ConsoleBase : IConsole
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);

        protected string prompt = "# ";

        public IScene ConsoleScene { get; set; }

        public string DefaultPrompt { get; set; }

        public ConsoleBase(string defaultPrompt)
        {
            DefaultPrompt = defaultPrompt;
        }

        public virtual void LockOutput()
        {
        }

        public virtual void UnlockOutput()
        {
        }

        public virtual void Output(string format)
        {
            System.Console.WriteLine(format);
        }

        public virtual void Output(string format, params object[] components)
        {
            //string level = null;
            if (components is { Length: > 0 })
            {
                var cl = components[0] as ConsoleLevel;
                if (cl != null)
                {
                    //level = cl.ToString();
                    if (components.Length > 1)
                    {
                        var tmp = new object[components.Length - 1];
                        Array.Copy(components, 1, tmp, 0, components.Length - 1);
                        components = tmp;
                    }
                    else
                        components = null;
                }
            }

            var text = (components == null || components.Length == 0) ? format : string.Format(format, components);

            System.Console.WriteLine(text);
        }

        public string Prompt(string p)
        {
            return ReadLine($"{p}: ", false, true);
        }

        public string Prompt(string p, string def)
        {
            var ret = ReadLine($"{p} [{def}]: ", false, true);
            if (ret.Length == 0)
                ret = def;

            return ret;
        }

        public string Prompt(string p, List<char> excludedCharacters)
        {
            var itisdone = false;
            var ret = String.Empty;
            while (!itisdone)
            {
                itisdone = true;
                ret = Prompt(p);

                foreach (var c in excludedCharacters.Where(c => ret.Contains(c.ToString())))
                {
                    System.Console.WriteLine("The character \"" + c.ToString() + "\" is not permitted.");
                    itisdone = false;
                }
            }

            return ret;
        }

        public virtual string Prompt(string p, string def, List<char> excludedCharacters, bool echo = true)
        {
            var itisdone = false;
            var ret = string.Empty;
            while (!itisdone)
            {
                itisdone = true;

                ret = ReadLine(def == null ? $"{p}: " : $"{p} [{def}]: ", false, echo);

                if (ret.Length == 0 && def != null)
                {
                    ret = def;
                }
                else
                {
                    if (excludedCharacters == null) continue;
                    foreach (var c in excludedCharacters.Where(c => ret.Contains(c.ToString())))
                    {
                        System.Console.WriteLine("The character \"" + c.ToString() + "\" is not permitted.");
                        itisdone = false;
                    }
                }
            }

            return ret;
        }

        // Displays a command prompt and returns a default value, user may only enter 1 of 2 options
        public virtual string Prompt(string prompt, string defaultresponse, List<string> options)
        {
            var itisdone = false;
            var optstr = options.Aggregate(string.Empty, (current, s) => current + (" " + s));

            var temp = Prompt(prompt, defaultresponse);
            while (!itisdone)
            {
                if (options.Contains(temp))
                {
                    itisdone = true;
                }
                else
                {
                    System.Console.WriteLine("Valid options are" + optstr);
                    temp = Prompt(prompt, defaultresponse);
                }
            }
            return temp;
        }

        public virtual string ReadLine(string p, bool isCommand, bool e)
        {
            System.Console.Write("{0}", p);
            return System.Console.ReadLine();
        }
    }
}
