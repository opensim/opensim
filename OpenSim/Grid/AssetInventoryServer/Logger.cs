/*
 * Copyright (c) 2008 Intel Corporation
 * All rights reserved.
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *
 * -- Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * -- Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * -- Neither the name of the Intel Corporation nor the names of its
 *    contributors may be used to endorse or promote products derived from
 *    this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
 * PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE INTEL OR ITS
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using log4net;
using log4net.Config;

[assembly: log4net.Config.XmlConfigurator(ConfigFileExtension = "log4net")]

namespace OpenSim.Grid.AssetInventoryServer
{
    /// <summary>
    /// Singleton logging class for the entire library
    /// </summary>
    public static class Logger
    {
        /// <summary>log4net logging engine</summary>
        public static ILog Log;

        static Logger()
        {
            Log = LogManager.GetLogger(System.Reflection.Assembly.GetExecutingAssembly().FullName);

            // If error level reporting isn't enabled we assume no logger is configured and initialize a default
            // ConsoleAppender
            if (!Log.Logger.IsEnabledFor(log4net.Core.Level.Error))
            {
                log4net.Appender.ConsoleAppender appender = new log4net.Appender.ConsoleAppender();
                appender.Layout = new log4net.Layout.PatternLayout("%timestamp [%thread] %-5level - %message%newline");
                BasicConfigurator.Configure(appender);

                Log.Info("No log configuration found, defaulting to console logging");
            }
        }
    }
}
