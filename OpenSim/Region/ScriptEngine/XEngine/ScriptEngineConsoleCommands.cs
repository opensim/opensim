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
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.Api.Plugins;

namespace OpenSim.Region.ScriptEngine.XEngine
{
    public class ScriptEngineConsoleCommands
    {
        IScriptEngine m_engine;

        public ScriptEngineConsoleCommands(IScriptEngine engine)
        {
            m_engine = engine;
        }

        public void RegisterCommands()
        {
            MainConsole.Instance.Commands.AddCommand(
                "Scripts", false, "show script sensors", "show script sensors", "Show script sensors information",
                HandleShowSensors);

            MainConsole.Instance.Commands.AddCommand(
                "Scripts", false, "show script timers", "show script timers", "Show script sensors information",
                HandleShowTimers);
        }

        private bool IsSceneSelected()
        {
            return MainConsole.Instance.ConsoleScene == null || MainConsole.Instance.ConsoleScene == m_engine.World;
        }

        private void HandleShowSensors(string module, string[] cmdparams)
        {
            if (!IsSceneSelected())
                return;

            SensorRepeat sr = AsyncCommandManager.GetSensorRepeatPlugin(m_engine);

            if (sr == null)
            {
                MainConsole.Instance.Output("Plugin not yet initialized");
                return;
            }

            List<SensorRepeat.SensorInfo> sensorInfo = sr.GetSensorInfo();

            ConsoleDisplayTable cdt = new ConsoleDisplayTable();
            cdt.AddColumn("Part name", 40);
            cdt.AddColumn("Script item ID", 36);
            cdt.AddColumn("Type", 4);
            cdt.AddColumn("Interval", 8);
            cdt.AddColumn("Range", 8);
            cdt.AddColumn("Arc", 8);

            foreach (SensorRepeat.SensorInfo s in sensorInfo)
            {
                cdt.AddRow(s.host.Name, s.itemID, s.type, s.interval, s.range, s.arc);
            }

            MainConsole.Instance.Output(cdt.ToString());
            MainConsole.Instance.Output("Total: {0}", sensorInfo.Count);
        }

        private void HandleShowTimers(string module, string[] cmdparams)
        {
            if (!IsSceneSelected())
                return;

            Timer timerPlugin = AsyncCommandManager.GetTimerPlugin(m_engine);

            if (timerPlugin == null)
            {
                MainConsole.Instance.Output("Plugin not yet initialized");
                return;
            }

            List<Timer.TimerInfo> timersInfo = timerPlugin.GetTimersInfo();

            ConsoleDisplayTable cdt = new ConsoleDisplayTable();
            cdt.AddColumn("Part local ID", 13);
            cdt.AddColumn("Script item ID", 36);
            cdt.AddColumn("Interval", 10);
            cdt.AddColumn("Next", 8);

            foreach (Timer.TimerInfo t in timersInfo)
            {
                // Convert from 100 ns ticks back to seconds
                cdt.AddRow(t.localID, t.itemID, (double)t.interval / 10000000, t.next);
            }

            MainConsole.Instance.Output(cdt.ToString());
            MainConsole.Instance.Output("Total: {0}", timersInfo.Count);
        }
    }
}