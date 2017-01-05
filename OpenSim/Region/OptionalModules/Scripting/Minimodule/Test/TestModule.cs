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

using System.Collections;
using System.Collections.Generic;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;

namespace OpenSim
{
    class MiniModule : MRMBase
    {
        // private microthreaded Function(params...)
        private IEnumerable TestMicrothread(string param)
        {
            Host.Console.Info("Microthreaded " + param);
            // relax;
            yield return null;
            Host.Console.Info("Microthreaded 2" + param);
            yield return null;
            int c = 100;
            while (c-- < 0)
            {
                Host.Console.Info("Microthreaded Looped " + c + " " + param);
                yield return null;
            }
        }

        public void Microthread(IEnumerable thread)
        {

        }

        public void RunMicrothread()
        {
            List<IEnumerator> threads = new List<IEnumerator>();
            threads.Add(TestMicrothread("A").GetEnumerator());
            threads.Add(TestMicrothread("B").GetEnumerator());
            threads.Add(TestMicrothread("C").GetEnumerator());

            Microthread(TestMicrothread("Ohai"));

            int i = 0;
            while (threads.Count > 0)
            {
                i++;
                bool running = threads[i%threads.Count].MoveNext();

                if (!running)
                    threads.Remove(threads[i%threads.Count]);
            }
        }

        public override void Start()
        {
            // Say Hello
            Host.Object.Say("Hello, Avatar!");

            // Register ourselves to listen
            // for touch events.
            Host.Object.OnTouch += OnTouched;
        }

        // This is our touch event handler
        void OnTouched(IObject sender, TouchEventArgs e)
        {
            Host.Object.Say("Touched.");
        }

        public override void Stop()
        {

        }
    }
}
