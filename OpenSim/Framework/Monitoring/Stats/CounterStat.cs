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
using System.Text;

using OpenMetaverse.StructuredData;

namespace OpenSim.Framework.Monitoring
{
// A statistic that wraps a counter.
// Built this way mostly so histograms and history can be created.
public class CounterStat : Stat
{
    private SortedDictionary<string, EventHistogram> m_histograms;
    private object counterLock = new object();

    public CounterStat(
                        string shortName,
                        string name,
                        string description,
                        string unitName,
                        string category,
                        string container,
                        StatVerbosity verbosity)
        : base(shortName, name, description, unitName, category, container, StatType.Push, null, verbosity)
    {
        m_histograms = new SortedDictionary<string, EventHistogram>();
    }

    // Histograms are presumably added at intialization time and the list does not change thereafter.
    // Thus no locking of the histogram list.
    public void AddHistogram(string histoName, EventHistogram histo)
    {
        m_histograms.Add(histoName, histo);
    }

    public delegate void ProcessHistogram(string name, EventHistogram histo);
    public void ForEachHistogram(ProcessHistogram process)
    {
        foreach (KeyValuePair<string, EventHistogram> kvp in m_histograms)
        {
            process(kvp.Key, kvp.Value);
        }
    }

    public void Event()
    {
        this.Event(1);
    }

    // Count the underlying counter.
    public void Event(int cnt)
    {
        lock (counterLock)
        {
            base.Value += cnt;

            foreach (EventHistogram histo in m_histograms.Values)
            {
                histo.Event(cnt);
            }
        }
    }

    // CounterStat is a basic stat plus histograms
    public override OSDMap ToOSDMap()
    {
        // Get the foundational instance
        OSDMap map = base.ToOSDMap();

        map["StatType"] = "CounterStat";

        // If there are any histograms, add a new field that is an array of histograms as OSDMaps
        if (m_histograms.Count > 0)
        {
            lock (counterLock)
            {
                if (m_histograms.Count > 0)
                {
                    OSDArray histos = new OSDArray();
                    foreach (EventHistogram histo in m_histograms.Values)
                    {
                        histos.Add(histo.GetHistogramAsOSDMap());
                    }
                    map.Add("Histograms", histos);
                }
            }
        }
        return map;
    }
}
}
