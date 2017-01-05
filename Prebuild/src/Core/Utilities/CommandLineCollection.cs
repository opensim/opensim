#region BSD License
/*
Copyright (c) 2004-2005 Matthew Holmes (matthew@wildfiregames.com), Dan Moorehead (dan05a@gmail.com)

Redistribution and use in source and binary forms, with or without modification, are permitted
provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this list of conditions
  and the following disclaimer.
* Redistributions in binary form must reproduce the above copyright notice, this list of conditions
  and the following disclaimer in the documentation and/or other materials provided with the
  distribution.
* The name of the author may not be used to endorse or promote products derived from this software
  without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING,
BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY
OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING
IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
#endregion

using System.Collections;
using System.Collections.Generic;

namespace Prebuild.Core.Utilities
{
    /// <summary>
    /// The CommandLine class parses and interprets the command-line arguments passed to
    /// prebuild.
    /// </summary>
    public class CommandLineCollection : IEnumerable<KeyValuePair<string, string>>
    {
        #region Fields

        // The raw OS arguments
        private readonly string[] m_RawArgs;

        // Command-line argument storage
        private readonly Dictionary<string, string> m_Arguments = new Dictionary<string, string>();

        #endregion

        #region Constructors

        /// <summary>
        /// Create a new CommandLine instance and set some internal variables.
        /// </summary>
        public CommandLineCollection(string[] args)
        {
            m_RawArgs = args;

            Parse();
        }

        #endregion

        #region Private Methods

        private void Parse()
        {
            if(m_RawArgs.Length < 1)
                return;

            int idx = 0;
            string lastArg = null;

            while(idx <m_RawArgs.Length)
            {
                string arg = m_RawArgs[idx];

                if(arg.Length > 2 && arg[0] == '/')
                {
                    arg = arg.Substring(1);
                    lastArg = arg;
                    m_Arguments[arg] = "";
                }
                else
                {
                    if(lastArg != null)
                    {
                        m_Arguments[lastArg] = arg;
                        lastArg = null;
                    }
                }

                idx++;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Wases the passed.
        /// </summary>
        /// <param name="arg">The arg.</param>
        /// <returns></returns>
        public bool WasPassed(string arg)
        {
            return (m_Arguments.ContainsKey(arg));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the parameter associated with the command line option
        /// </summary>
        /// <remarks>Returns null if option was not specified,
        /// null string if no parameter was specified, and the value if a parameter was specified</remarks>
        public string this[string index]
        {
            get
            {
                if(m_Arguments.ContainsKey(index))
                {
                    return (m_Arguments[index]);
                }
                return null;
            }
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// Returns an enumerator that can iterate through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IDictionaryEnumerator"/>
        /// that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return m_Arguments.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
