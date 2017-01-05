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
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using OpenMetaverse;

namespace OpenSim.Tests.Common
{
    public class TestHelpers
    {
        private static Stream EnableLoggingConfigStream
            = new MemoryStream(
                Encoding.UTF8.GetBytes(
@"<log4net>
  <!-- A1 is set to be a ConsoleAppender -->
  <appender name=""A1"" type=""log4net.Appender.ConsoleAppender"">

    <!-- A1 uses PatternLayout -->
    <layout type=""log4net.Layout.PatternLayout"">
    <!-- Print the date in ISO 8601 format -->
      <!-- <conversionPattern value=""%date [%thread] %-5level %logger %ndc - %message%newline"" /> -->
      <conversionPattern value=""%date %message%newline"" />
      </layout>
  </appender>

  <!-- Set root logger level to DEBUG and its only appender to A1 -->
  <root>
    <level value=""DEBUG"" />
    <appender-ref ref=""A1"" />
  </root>
</log4net>"));

        private static MemoryStream DisableLoggingConfigStream
            = new MemoryStream(
                Encoding.UTF8.GetBytes(
//                        "<?xml version=\"1.0\" encoding=\"utf-8\" ?><configuration><log4net><root><level value=\"OFF\"/><appender-ref ref=\"A1\"/></root></log4net></configuration>"));
                    //"<?xml version=\"1.0\" encoding=\"utf-8\" ?><configuration><log4net><root><level value=\"OFF\"/></root></log4net></configuration>")));
//                    "<configuration><log4net><root><level value=\"OFF\"/></root></log4net></configuration>"));
//                    "<configuration><log4net><root></root></log4net></configuration>")));
//                    "<configuration><log4net><root/></log4net></configuration>"));
                    "<log4net><root/></log4net>"));

        public static bool AssertThisDelegateCausesArgumentException(TestDelegate d)
        {
            try
            {
                d();
            }
            catch(ArgumentException)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// A debugging method that can be used to print out which test method you are in
        /// </summary>
        public static void InMethod()
        {
            StackTrace stackTrace = new StackTrace();
            Console.WriteLine();
            Console.WriteLine("===> In Test Method : {0} <===", stackTrace.GetFrame(1).GetMethod().Name);
        }

        public static void EnableLogging()
        {
            log4net.Config.XmlConfigurator.Configure(EnableLoggingConfigStream);
            EnableLoggingConfigStream.Position = 0;
        }

        /// <summary>
        /// Disable logging whilst running the tests.
        /// </summary>
        /// <remarks>
        /// Remember, if a regression test throws an exception before completing this will not be invoked if it's at
        /// the end of the test.
        /// TODO: Always invoke this after every test - probably need to make all test cases inherit from a common
        /// TestCase class where this can be done.
        /// </remarks>
        public static void DisableLogging()
        {
            log4net.Config.XmlConfigurator.Configure(DisableLoggingConfigStream);
            DisableLoggingConfigStream.Position = 0;
        }

        /// <summary>
        /// Parse a UUID stem into a full UUID.
        /// </summary>
        /// <remarks>
        /// The fragment will come at the start of the UUID.  The rest will be 0s
        /// </remarks>
        /// <returns></returns>
        /// <param name='frag'>
        /// A UUID fragment that will be parsed into a full UUID.  Therefore, it can only contain
        /// cahracters which are valid in a UUID, except for "-" which is currently only allowed if a full UUID is
        /// given as the 'fragment'.
        /// </param>
        public static UUID ParseStem(string stem)
        {
            string rawUuid = stem.PadRight(32, '0');

            return UUID.Parse(rawUuid);
        }

        /// <summary>
        /// Parse tail section into full UUID.
        /// </summary>
        /// <param name="tail"></param>
        /// <returns></returns>
        public static UUID ParseTail(int tail)
        {
            return new UUID(string.Format("00000000-0000-0000-0000-{0:X12}", tail));
        }

        /// <summary>
        /// Parse a UUID tail section into a full UUID.
        /// </summary>
        /// <remarks>
        /// The fragment will come at the end of the UUID.  The rest will be 0s
        /// </remarks>
        /// <returns></returns>
        /// <param name='frag'>
        /// A UUID fragment that will be parsed into a full UUID.  Therefore, it can only contain
        /// cahracters which are valid in a UUID, except for "-" which is currently only allowed if a full UUID is
        /// given as the 'fragment'.
        /// </param>
        public static UUID ParseTail(string stem)
        {
            string rawUuid = stem.PadLeft(32, '0');

            return UUID.Parse(rawUuid);
        }
    }
}
