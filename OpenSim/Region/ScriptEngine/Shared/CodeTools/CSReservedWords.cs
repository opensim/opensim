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

namespace OpenSim.Region.ScriptEngine.Shared.CodeTools
{
    /// <summary>
    /// A container for all of the reserved C# words that are not also reserved words in LSL.
    /// The words must be maintained in alphabetical order.
    /// The words that are key words in lsl are picked up by the lsl compiler as errors.
    /// The LSL reserved words have been left in the list as comments for completeness
    /// </summary>
    internal class CSReservedWords
    {
        private static List<string> reservedWords = new List<string>(new string[] {
                                          "abstract","as",
                                          "base","bool","break","byte",
                                          "case","catch","char","checked","class","const","continue",
                                          "decimal","default","delegate",
                                        //"do",
                                          "double",
                                        //"else",
                                          "enum",
                                        //"event",
                                          "explicit","extern",
                                          "false","finally","fixed",
                                        //"float","for",
                                          "foreach",
                                          "goto",
                                        //"if",
                                          "implicit","in","int","interface","internal","is",
                                          "lock","long",
                                          "namespace","new","null",
                                          "object","operator","out","override",
                                          "params","private","protected","public",
                                          "readonly","ref",
                                        //"return",
                                          "sbyte","sealed","short","sizeof","stackalloc","static",
                                        //"string",
                                          "struct","switch",
                                          "this","throw","true","try","typeof",
                                          "uint","ulong","unchecked","unsafe","ushort","using",
                                          "virtual","void","volatile",
                                        //"while"
                                         });

        /// <summary>
        /// Returns true if the passed string is in the list of reserved words with
        /// a little simple pre-filtering.
        /// </summary>
        internal static bool IsReservedWord(string word)
        {
            // A couple of quick filters to weed out single characters, ll functions and
            // anything that starts with an uppercase letter
            if (String.IsNullOrEmpty(word)) return false;
            if (word.Length < 2) return false;
            if (word.StartsWith("ll")) return false;
            char first = word.ToCharArray(0,1)[0];
            if (first >= 'A' && first <= 'Z') return false;

            return (reservedWords.BinarySearch(word) >= 0);
        }
    }
}
