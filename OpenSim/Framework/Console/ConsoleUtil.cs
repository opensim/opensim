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
using System.Linq;
using System.Reflection;
using log4net;
using OpenMetaverse;

namespace OpenSim.Framework.Console
{
    public class ConsoleUtil
    {
    //    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const int LocalIdNotFound = 0;
    
        /// <summary>
        /// Used by modules to display stock co-ordinate help, though possibly this should be under some general section
        /// rather than in each help summary.
        /// </summary>
        public const string CoordHelp
    = @"Each component of the coord is comma separated.  There must be no spaces between the commas.
    If you don't care about the z component you can simply omit it.
    If you don't care about the x or y components then you can leave them blank (though a comma is still required)
    If you want to specify the maximum value of a component then you can use ~ instead of a number
    If you want to specify the minimum value of a component then you can use -~ instead of a number
    e.g.
    show object pos 20,20,20 to 40,40,40
    delete object pos 20,20 to 40,40
    show object pos ,20,20 to ,40,40
    delete object pos ,,30 to ,,~
    show object pos ,,-~ to ,,30";
    
        public const string MinRawConsoleVectorValue = "-~";
        public const string MaxRawConsoleVectorValue = "~";
    
        public const string VectorSeparator = ",";
        public static char[] VectorSeparatorChars = VectorSeparator.ToCharArray();

        /// <summary>
        /// Check if the given file path exists.
        /// </summary>
        /// <remarks>If not, warning is printed to the given console.</remarks>
        /// <returns>true if the file does not exist, false otherwise.</returns>
        /// <param name='console'></param>
        /// <param name='path'></param>
        public static bool CheckFileDoesNotExist(ICommandConsole console, string path)
        {
            if (File.Exists(path))
            {
                console.OutputFormat("File {0} already exists.  Please move or remove it.", path);
                return false;
            }

            return true;
        }
    
        /// <summary>
        /// Try to parse a console UUID from the console.
        /// </summary>
        /// <remarks>
        /// Will complain to the console if parsing fails.
        /// </remarks>
        /// <returns></returns>
        /// <param name='console'>If null then no complaint is printed.</param>
        /// <param name='rawUuid'></param>
        /// <param name='uuid'></param>
        public static bool TryParseConsoleUuid(ICommandConsole console, string rawUuid, out UUID uuid)
        {
            if (!UUID.TryParse(rawUuid, out uuid))
            {
                if (console != null)
                    console.OutputFormat("ERROR: {0} is not a valid uuid", rawUuid);

                return false;
            }
    
            return true;
        }

        public static bool TryParseConsoleLocalId(ICommandConsole console, string rawLocalId, out uint localId)
        {
            if (!uint.TryParse(rawLocalId, out localId))
            {
                if (console != null)
                    console.OutputFormat("ERROR: {0} is not a valid local id", localId);

                return false;
            }

            if (localId == 0)
            {
                if (console != null)
                    console.OutputFormat("ERROR: {0} is not a valid local id - it must be greater than 0", localId);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Tries to parse the input as either a UUID or a local ID.
        /// </summary>
        /// <returns>true if parsing succeeded, false otherwise.</returns>
        /// <param name='console'></param>
        /// <param name='rawId'></param>
        /// <param name='uuid'></param>
        /// <param name='localId'>
        /// Will be set to ConsoleUtil.LocalIdNotFound if parsing result was a UUID or no parse succeeded.
        /// </param>
        public static bool TryParseConsoleId(ICommandConsole console, string rawId, out UUID uuid, out uint localId)
        {
            if (TryParseConsoleUuid(null, rawId, out uuid))
            {
                localId = LocalIdNotFound;
                return true;
            }

            if (TryParseConsoleLocalId(null, rawId, out localId))
            {
                return true;
            }

            if (console != null)
                console.OutputFormat("ERROR: {0} is not a valid UUID or local id", rawId);

            return false;
        }

        /// <summary>
        /// Convert a console input to a bool, automatically complaining if a console is given.
        /// </summary>
        /// <param name='console'>Can be null if no console is available.</param>
        /// <param name='rawConsoleVector'>/param>
        /// <param name='vector'></param>
        /// <returns></returns>
        public static bool TryParseConsoleBool(ICommandConsole console, string rawConsoleString, out bool b)
        {
            if (!bool.TryParse(rawConsoleString, out b))
            {
                if (console != null)
                    console.OutputFormat("ERROR: {0} is not a true or false value", rawConsoleString);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Convert a console input to an int, automatically complaining if a console is given.
        /// </summary>
        /// <param name='console'>Can be null if no console is available.</param>
        /// <param name='rawConsoleInt'>/param>
        /// <param name='i'></param>
        /// <returns></returns>
        public static bool TryParseConsoleInt(ICommandConsole console, string rawConsoleInt, out int i)
        {
            if (!int.TryParse(rawConsoleInt, out i))
            {
                if (console != null)
                    console.OutputFormat("ERROR: {0} is not a valid integer", rawConsoleInt);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Convert a console input to a float, automatically complaining if a console is given.
        /// </summary>
        /// <param name='console'>Can be null if no console is available.</param>
        /// <param name='rawConsoleInput'>/param>
        /// <param name='i'></param>
        /// <returns></returns>
        public static bool TryParseConsoleFloat(ICommandConsole console, string rawConsoleInput, out float i)
        {
            if (!float.TryParse(rawConsoleInput, out i))
            {
                if (console != null)
                    console.OutputFormat("ERROR: {0} is not a valid float", rawConsoleInput);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Convert a console input to a double, automatically complaining if a console is given.
        /// </summary>
        /// <param name='console'>Can be null if no console is available.</param>
        /// <param name='rawConsoleInput'>/param>
        /// <param name='i'></param>
        /// <returns></returns>
        public static bool TryParseConsoleDouble(ICommandConsole console, string rawConsoleInput, out double i)
        {
            if (!double.TryParse(rawConsoleInput, out i))
            {
                if (console != null)
                    console.OutputFormat("ERROR: {0} is not a valid double", rawConsoleInput);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Convert a console integer to a natural int, automatically complaining if a console is given.
        /// </summary>
        /// <param name='console'>Can be null if no console is available.</param>
        /// <param name='rawConsoleInt'>/param>
        /// <param name='i'></param>
        /// <returns></returns>
        public static bool TryParseConsoleNaturalInt(ICommandConsole console, string rawConsoleInt, out int i)
        {
            if (TryParseConsoleInt(console, rawConsoleInt, out i))
            {
                if (i < 0)
                {
                    if (console != null)
                        console.OutputFormat("ERROR: {0} is not a positive integer", rawConsoleInt);

                    return false;
                }

                return true;
            }

            return false;
        }
    
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
        /// <param name='blankComponentFunc'>
        /// Behaviour if component is blank.  If null then conversion fails on a blank component.
        /// </param>
        /// <param name='vector'></param>
        /// <returns></returns>
        public static bool TryParseConsoleVector(
            string rawConsoleVector, Func<string, string> blankComponentFunc, out Vector3 vector)
        {
            return Vector3.TryParse(CookVector(rawConsoleVector, 3, blankComponentFunc), out vector);
        }

        /// <summary>
        /// Convert a vector input from the console to an OpenMetaverse.Vector2
        /// </summary>
        /// <param name='rawConsoleVector'>
        /// A string in the form <x>,<y> where there is no space between values.
        /// Any component can be missing (e.g. ,40).  blankComponentFunc is invoked to replace the blank with a suitable value
        /// Also, if the blank component is at the end, then the comma can be missed off entirely (e.g. 40)
        /// The strings "~" and "-~" are valid in components.  The first substitutes float.MaxValue whilst the second is float.MinValue
        /// Other than that, component values must be numeric.
        /// </param>
        /// <param name='blankComponentFunc'>
        /// Behaviour if component is blank.  If null then conversion fails on a blank component.
        /// </param>
        /// <param name='vector'></param>
        /// <returns></returns>
        public static bool TryParseConsole2DVector(
            string rawConsoleVector, Func<string, string> blankComponentFunc, out Vector2 vector)
        {
            // We don't use Vector2.TryParse() for now because for some reason it expects an input with 3 components
            // rather than 2.
            string cookedVector = CookVector(rawConsoleVector, 2, blankComponentFunc);

            if (cookedVector == null)
            {
                vector = Vector2.Zero;

                return false;
            }
            else
            {
                string[] cookedComponents = cookedVector.Split(VectorSeparatorChars);

                vector = new Vector2(float.Parse(cookedComponents[0]), float.Parse(cookedComponents[1]));

                return true;
            }

            //return Vector2.TryParse(CookVector(rawConsoleVector, 2, blankComponentFunc), out vector);
        }

        /// <summary>
        /// Convert a raw console vector into a vector that can be be parsed by the relevant OpenMetaverse.TryParse()
        /// </summary>
        /// <param name='rawConsoleVector'></param>
        /// <param name='dimensions'></param>
        /// <param name='blankComponentFunc'></param>
        /// <returns>null if conversion was not possible</returns>
        private static string CookVector(
            string rawConsoleVector, int dimensions, Func<string, string> blankComponentFunc)
        {
            List<string> components = rawConsoleVector.Split(VectorSeparatorChars).ToList();
    
            if (components.Count < 1 || components.Count > dimensions)
                return null;
    
            if (components.Count < dimensions)
            {
                if (blankComponentFunc == null)
                    return null;
                else
                    for (int i = components.Count; i < dimensions; i++)
                        components.Add("");
            }

            List<string> cookedComponents
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
    
            return string.Join(VectorSeparator, cookedComponents.ToArray());
        }
    }
}