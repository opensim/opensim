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
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Framework.Monitoring.Monitors
{
    class GenericMonitor : IMonitor
    {
        public Scene Scene { get; private set; }
        public string Name { get; private set; }
        public string FriendlyName { get; private set; }

        private readonly Func<GenericMonitor, double> m_getValueAction;
        private readonly Func<GenericMonitor, string> m_getFriendlyValueAction;

        public GenericMonitor(
            Scene scene,
            string name,
            string friendlyName,
            Func<GenericMonitor, double> getValueAction,
            Func<GenericMonitor, string> getFriendlyValueAction)
        {
            Scene = scene;
            Name = name;
            FriendlyName = name;
            m_getFriendlyValueAction = getFriendlyValueAction;
            m_getValueAction = getValueAction;
        }

        public double GetValue()
        {
            return m_getValueAction(this);
        }

        public string GetName()
        {
            return Name;
        }

        public string GetFriendlyName()
        {
            return FriendlyName;
        }

        public string GetFriendlyValue()
        {
            return m_getFriendlyValueAction(this);
        }
    }
}




