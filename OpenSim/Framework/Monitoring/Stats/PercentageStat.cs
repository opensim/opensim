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
using System.Text;

namespace OpenSim.Framework.Monitoring
{
    public class PercentageStat : Stat
    {
        public long Antecedent { get; set; }
        public long Consequent { get; set; }

        public override double Value
        {
            get
            {
                // Asking for an update here means that the updater cannot access this value without infinite recursion.
                // XXX: A slightly messy but simple solution may be to flick a flag so we can tell if this is being
                // called by the pull action and just return the value.
                if (StatType == StatType.Pull)
                    PullAction(this);

                long c = Consequent;

                // Avoid any chance of a multi-threaded divide-by-zero
                if (c == 0)
                    return 0;

                return (double)Antecedent / c * 100;
            }

            set
            {
                throw new InvalidOperationException("Cannot set value on a PercentageStat");
            }
        }

        public PercentageStat(
            string shortName,
            string name,
            string description,
            string category,
            string container,
            StatType type,
            Action<Stat> pullAction,
            StatVerbosity verbosity)
            : base(shortName, name, description, "%", category, container, type, pullAction, verbosity) {}

        public override string ToConsoleString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat(
                "{0}.{1}.{2} : {3:0.##}{4} ({5}/{6})",
                Category, Container, ShortName, Value, UnitName, Antecedent, Consequent);

            AppendMeasuresOfInterest(sb);

            return sb.ToString();
        }
    }
}