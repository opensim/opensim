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
using System.Text;

namespace OpenSim.Framework.Monitoring
{
    public class Check
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static readonly char[] DisallowedShortNameCharacters = { '.' };

        /// <summary>
        /// Category of this stat (e.g. cache, scene, etc).
        /// </summary>
        public string Category { get; private set; }

        /// <summary>
        /// Containing name for this stat.
        /// FIXME: In the case of a scene, this is currently the scene name (though this leaves
        /// us with a to-be-resolved problem of non-unique region names).
        /// </summary>
        /// <value>
        /// The container.
        /// </value>
        public string Container { get; private set; }

        /// <summary>
        /// Action used to check whether alert should go off.
        /// </summary>
        /// <remarks>
        /// Should return true if check passes.  False otherwise.
        /// </remarks>
        public Func<Check, bool> CheckFunc { get; private set; }

        /// <summary>
        /// Message from the last failure, if any.  If there is no message or no failure then will be null.
        /// </summary>
        /// <remarks>
        /// Should be set by the CheckFunc when applicable.
        /// </remarks>
        public string LastFailureMessage { get; set; }

        public StatVerbosity Verbosity { get; private set; }
        public string ShortName { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }

        public Check(
            string shortName,
            string name,
            string description,
            string category,
            string container,
            Func<Check, bool> checkFunc,
            StatVerbosity verbosity) 
        {
            if (ChecksManager.SubCommands.Contains(category))
                throw new Exception(
                    string.Format("Alert cannot be in category '{0}' since this is reserved for a subcommand", category));

            foreach (char c in DisallowedShortNameCharacters)
            {
                if (shortName.IndexOf(c) != -1)
                    throw new Exception(string.Format("Alert name {0} cannot contain character {1}", shortName, c));
            }

            ShortName = shortName;
            Name = name;
            Description = description;
            Category = category;
            Container = container;
            CheckFunc = checkFunc;
            Verbosity = verbosity;
        }

        public bool CheckIt()
        {
            return CheckFunc(this);
        }

        public virtual string ToConsoleString()
        {
            return string.Format(
                "{0}.{1}.{2} - {3}", 
                Category, 
                Container, 
                ShortName,
                Description);
        }
    }
}