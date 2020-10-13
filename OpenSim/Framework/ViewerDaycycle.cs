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

using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    public class DayCycle
    {
        public struct TrackEntry
        {
            public float time;
            public string frameName;

            public TrackEntry(float t, string f)
            {
                time = t;
                frameName = f;
            }
        }

        public class CompareTrackEntries : IComparer<TrackEntry>
        {
            public int Compare(TrackEntry x, TrackEntry y)
            {
                    return x.time.CompareTo(y.time);
            }
        }

        public bool IsStaticDayCycle = false;
        public List<TrackEntry> waterTrack = new List<TrackEntry>();
        public List<TrackEntry> skyTrack0 = new List<TrackEntry>();
        public List<TrackEntry>[] skyTracks = new List<TrackEntry>[3];

        public Dictionary<string, SkyData> skyframes = new Dictionary<string, SkyData>();
        public Dictionary<string, WaterData> waterframes = new Dictionary<string, WaterData>();

        public string Name;

        public void FromWLOSD(OSDArray array)
        {
            CompareTrackEntries cte = new CompareTrackEntries();
            TrackEntry track;

            OSDArray skytracksArray = null;
            if (array.Count > 1)
                skytracksArray = array[1] as OSDArray;
            if(skytracksArray != null)
            {
                foreach (OSD setting in skytracksArray)
                {
                    OSDArray innerSetting = setting as OSDArray;
                    if(innerSetting != null)
                    {
                        track = new TrackEntry((float)innerSetting[0].AsReal(), innerSetting[1].AsString());
                        skyTrack0.Add(track);
                    }
                }
                skyTrack0.Sort(cte);
            }

            OSDMap skyFramesArray = null;
            if (array.Count > 2)
                skyFramesArray = array[2] as OSDMap;
            if(skyFramesArray != null)
            {
                foreach (KeyValuePair<string, OSD> kvp in skyFramesArray)
                {
                    SkyData sky = new SkyData();
                    sky.FromWLOSD(kvp.Key, kvp.Value);
                    skyframes[kvp.Key] = sky;
                }
            }

            WaterData water = new WaterData();
            OSDMap watermap = null;
            if(array.Count > 3)
                watermap = array[3] as OSDMap;
            if(watermap != null)
                water.FromWLOSD("WLWater", watermap);

            waterframes["WLWater"] = water;
            track = new TrackEntry(-1f, "WLWater");
            waterTrack.Add(track);

            Name = "WLDaycycle";

            if (skyTrack0.Count == 1 && skyTrack0[0].time == -1f)
                IsStaticDayCycle = true;
        }

        public void ToWLOSD(ref OSDArray array)
        {
            OSDArray track = new OSDArray();
            foreach (TrackEntry te in skyTrack0)
                track.Add(new OSDArray { te.time, te.frameName });
            array[1] = track;

            OSDMap frames = new OSDMap();
            foreach (KeyValuePair<string, SkyData> kvp in skyframes)
                frames[kvp.Key] = kvp.Value.ToWLOSD();
            array[2] = frames;

            if(waterTrack.Count > 0)
            {
                TrackEntry te = waterTrack[0];
                if(waterframes.TryGetValue(te.frameName, out WaterData water))
                    array[3] = water.ToWLOSD();
            }
            else
                array[3] = new OSDMap();
        }

        public bool replaceWaterFromOSD(string name, OSDMap map)
        {
            WaterData water = new WaterData();
            if(string.IsNullOrWhiteSpace(name))
                name = "Water";
            try
            {
                water.FromOSD(name, map);
            }
            catch
            {
                return false;
            }
            waterframes.Clear();
            waterframes[name] = water;
            waterTrack.Clear();
            TrackEntry t = new TrackEntry()
            {
                time = -1,
                frameName = name
            };
            waterTrack.Add(t);
            return true;
        }

        public bool replaceSkyFromOSD(string name, OSDMap map)
        {
            SkyData sky = new SkyData();
            if (string.IsNullOrWhiteSpace(name))
                name = "Sky";
            try
            {
                sky.FromOSD(name, map);
            }
            catch
            {
                return false;
            }
            skyframes.Clear();
            skyframes[name] = sky;

            TrackEntry t = new TrackEntry()
            {
                time = -1,
                frameName = name
            };
            skyTrack0.Clear();
            skyTrack0.Add(t);
            skyTracks = new List<TrackEntry>[3];

            return true;
        }

        public void FromOSD(OSDMap map)
        {
            CompareTrackEntries cte = new CompareTrackEntries();
            OSD otmp;

            if(map.TryGetValue("frames", out otmp) && otmp is OSDMap)
            {
                OSDMap mframes = otmp as OSDMap;
                foreach(KeyValuePair<string, OSD> kvp in mframes)
                {
                    OSDMap v = kvp.Value as OSDMap;
                    if(v.TryGetValue("type", out otmp))
                    {
                        string type = otmp;
                        if (type.Equals("water"))
                        {
                            WaterData water = new WaterData();
                            water.FromOSD(kvp.Key, v);
                            waterframes[kvp.Key] = water;
                        }
                        else if (type.Equals("sky"))
                        {
                            SkyData sky = new SkyData();
                            sky.FromOSD(kvp.Key, v);
                            skyframes[kvp.Key] = sky;
                        }
                    }
                }
            }

            if (map.TryGetValue("name", out otmp))
                Name = otmp;
            else
                Name ="DayCycle";

            OSDArray track;
            if (map.TryGetValue("tracks", out otmp) && otmp is OSDArray)
            {
                OSDArray tracks = otmp as OSDArray;
                if(tracks.Count > 0)
                {
                    track = tracks[0] as OSDArray;
                    if (track != null && track.Count > 0)
                    {
                        for (int i = 0; i < track.Count; ++i)
                        {
                            OSDMap d = track[i] as OSDMap;
                            if (d.TryGetValue("key_keyframe", out OSD dtime))
                            {
                                if (d.TryGetValue("key_name", out OSD dname))
                                {
                                    TrackEntry t = new TrackEntry()
                                    {
                                        time = dtime,
                                        frameName = dname
                                    };
                                    waterTrack.Add(t);
                                }
                            }
                        }
                        waterTrack.Sort(cte);
                    }
                }
                if (tracks.Count > 1)
                {
                    track = tracks[1] as OSDArray;
                    if (track != null && track.Count > 0)
                    {
                        for (int i = 0; i < track.Count; ++i)
                        {
                            OSDMap d = track[i] as OSDMap;
                            if (d.TryGetValue("key_keyframe", out OSD dtime))
                            {
                                if (d.TryGetValue("key_name", out OSD dname))
                                {
                                    TrackEntry t = new TrackEntry();
                                    t.time = dtime;
                                    t.frameName = dname;
                                    skyTrack0.Add(t);
                                }
                            }
                        }
                        skyTrack0.Sort(cte);
                    }
                }
                if (tracks.Count > 2)
                {
                    for(int st = 2, dt = 0; st < tracks.Count && dt < 3; ++st, ++dt)
                    {
                        track = tracks[st] as OSDArray;
                        if(track != null && track.Count > 0)
                        {
                            skyTracks[dt] = new List<TrackEntry>();
                            for (int i = 0; i < track.Count; ++i)
                            {
                                OSDMap d = track[i] as OSDMap;
                                if (d.TryGetValue("key_keyframe", out OSD dtime))
                                {
                                    if (d.TryGetValue("key_name", out OSD dname))
                                    {
                                        TrackEntry t = new TrackEntry();
                                        t.time = dtime;
                                        t.frameName = dname;
                                        skyTracks[dt].Add(t);
                                    }
                                }
                            }
                            skyTracks[dt].Sort(cte);
                        }
                    }
                }
            }
        }

        public OSDMap ToOSD()
        {
            OSDMap cycle = new OSDMap();

            OSDMap frames = new OSDMap();
            foreach (KeyValuePair<string, WaterData> kvp in waterframes)
            {
                frames[kvp.Key] = kvp.Value.ToOSD();
            }
            foreach (KeyValuePair<string, SkyData> kvp in skyframes)
            {
                frames[kvp.Key] = kvp.Value.ToOSD();
            }
            cycle["frames"] = frames;

            cycle["name"] = Name;

            OSDArray tracks = new OSDArray();

            OSDArray track = new OSDArray();
            OSDMap tmp;
            foreach (TrackEntry te in waterTrack)
            {
                tmp = new OSDMap();
                if (te.time < 0)
                    tmp["key_keyframe"] = 0f;
                else
                    tmp["key_keyframe"] = te.time;
                tmp["key_name"] = te.frameName;
                track.Add(tmp);
            }
            tracks.Add(track);

            track = new OSDArray();
            foreach (TrackEntry te in skyTrack0)
            {
                tmp = new OSDMap();
                if (te.time < 0)
                    tmp["key_keyframe"] = 0f;
                else
                    tmp["key_keyframe"] = te.time;
                tmp["key_name"] = te.frameName;
                track.Add(tmp);
            }
            tracks.Add(track);

            for(int st = 0; st < 3; ++st)
            {
                track = new OSDArray();
                if(skyTracks[st] != null)
                {
                    foreach (TrackEntry te in skyTracks[st])
                    {
                        tmp = new OSDMap();
                        if (te.time < 0)
                            tmp["key_keyframe"] = 0f;
                        else
                            tmp["key_keyframe"] = te.time;
                        tmp["key_name"] = te.frameName;
                        track.Add(tmp);
                    }
                }
                tracks.Add(track);
            }

            cycle["tracks"] = tracks;
            cycle["type"] = "daycycle";

            return cycle;
        }

        public void GatherAssets(Dictionary<UUID, sbyte> uuids)
        {
            foreach (WaterData wd in waterframes.Values)
            {
                wd.GatherAssets(uuids);
            }
            foreach (SkyData sd in skyframes.Values)
            {
                sd.GatherAssets(uuids);
            }
        }
    }
}
