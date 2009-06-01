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
using System.Text;

namespace OpenSim.GridLaunch
{
    internal partial class AppExecutor
    {
        

        #region Start / Stop timer thread
        private void timer_Start()
        {
            asyncReadOutput();
            asyncReadError();
        }

        private bool running = true;
        private void timer_Stop()
        {
            running = false;
        }
        #endregion

        private byte[] readBufferOutput = new byte[4096];
        private byte[] readBufferError = new byte[4096];

        private void asyncReadOutput()
        {
            if (running)
            Output.BaseStream.BeginRead(readBufferOutput, 0, readBufferOutput.Length, asyncReadCallBackOutput, null);
        }
        private void asyncReadError()
        {
            if (running)
            Error.BaseStream.BeginRead(readBufferError, 0, readBufferError.Length, asyncReadCallBackError, null);
        }

        private void asyncReadCallBackOutput(IAsyncResult ar)
        {
            int len = Output.BaseStream.EndRead(ar);
            Program.FireAppConsoleOutput(file,
                System.Text.Encoding.ASCII.GetString(readBufferOutput, 0, len)
                );

            asyncReadOutput();
        }
        private void asyncReadCallBackError(IAsyncResult ar)
        {
            int len = Error.BaseStream.EndRead(ar);
            Program.FireAppConsoleError(file,
            System.Text.Encoding.ASCII.GetString(readBufferError, 0, len)
            );

            asyncReadError();
        }
    }
}
