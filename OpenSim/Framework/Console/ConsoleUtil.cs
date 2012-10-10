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
using System.Reflection;
using log4net;
using OpenMetaverse;

public class ConsoleUtil
{
//    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    public const string MinRawConsoleVectorValue = "-~";
    public const string MaxRawConsoleVectorValue = "~";

    public const string VectorSeparator = ",";
    public static char[] VectorSeparatorChars = VectorSeparator.ToCharArray();

    /// <summary>
    /// Convert a minimum vector input from the console to an OpenMetaverse.Vector3
    /// </summary>
    /// <param name='rawConsoleVector'>/param>
    /// <param name='vector'></param>
    /// <returns></returns>
    public static bool TryParseConsoleMinVector(string rawConsoleVector, out Vector3 vector)
    {
        return TryParseConsoleVector(rawConsoleVector, c => float.MinValue.ToString(), out vector);
    }

    /// <summary>
    /// Convert a maximum vector input from the console to an OpenMetaverse.Vector3
    /// </summary>
    /// <param name='rawConsoleVector'>/param>
    /// <param name='vector'></param>
    /// <returns></returns>
    public static bool TryParseConsoleMaxVector(string rawConsoleVector, out Vector3 vector)
    {
        return TryParseConsoleVector(rawConsoleVector, c => float.MaxValue.ToString(), out vector);
    }

    /// <summary>
    /// Convert a vector input from the console to an OpenMetaverse.Vector3
    /// </summary>
    /// <param name='rawConsoleVector'>
    /// A string in the form <x>,<y>,<z> where there is no space between values.
    /// Any component can be missing (e.g. ,,40).  blankComponentFunc is invoked to replace the blank with a suitable value
    /// Also, if the blank component is at the end, then the comma can be missed off entirely (e.g. 40,30 or 40)
    /// The strings "~" and "-~" are valid in components.  The first substitutes float.MaxValue whilst the second is float.MinValue
    /// Other than that, component values must be numeric.
    /// </param>
    /// <param name='blankComponentFunc'></param>
    /// <param name='vector'></param>
    /// <returns></returns>
    public static bool TryParseConsoleVector(
        string rawConsoleVector, Func<string, string> blankComponentFunc, out Vector3 vector)
    {
        List<string> components = rawConsoleVector.Split(VectorSeparatorChars).ToList();

        if (components.Count < 1 || components.Count > 3)
        {
            vector = Vector3.Zero;
            return false;
        }

        for (int i = components.Count; i < 3; i++)
            components.Add("");

        List<string> semiDigestedComponents
            = components.ConvertAll<string>(
                c =>
                {
                    if (c == "")
                        return blankComponentFunc.Invoke(c);
                    else if (c == MaxRawConsoleVectorValue)
                        return float.MaxValue.ToString();
                    else if (c == MinRawConsoleVectorValue)
                        return float.MinValue.ToString();
                    else
                        return c;
                });

        string semiDigestedConsoleVector = string.Join(VectorSeparator, semiDigestedComponents.ToArray());

//        m_log.DebugFormat("[CONSOLE UTIL]: Parsing {0} into OpenMetaverse.Vector3", semiDigestedConsoleVector);

        return Vector3.TryParse(semiDigestedConsoleVector, out vector);
    }
}