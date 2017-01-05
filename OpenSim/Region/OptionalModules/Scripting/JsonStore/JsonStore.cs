/*
 * Copyright (c) Contributors
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
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
using Mono.Addins;

using System;
using System.Reflection;
using System.Threading;
using System.Text;
using System.Net;
using System.Net.Sockets;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OpenSim.Region.OptionalModules.Scripting.JsonStore
{
    public class JsonStore
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected virtual OSD ValueStore { get; set; }

        protected class TakeValueCallbackClass
        {
            public string Path { get; set; }
            public bool UseJson { get; set; }
            public TakeValueCallback Callback { get; set; }

            public TakeValueCallbackClass(string spath, bool usejson, TakeValueCallback cback)
            {
                Path = spath;
                UseJson = usejson;
                Callback = cback;
            }
        }

        protected List<TakeValueCallbackClass> m_TakeStore;
        protected List<TakeValueCallbackClass> m_ReadStore;

        // add separators for quoted paths and array references
        protected static Regex m_ParsePassOne = new Regex("({[^}]+}|\\[[0-9]+\\]|\\[\\+\\])");

        // add quotes to bare identifiers which are limited to alphabetic characters
        protected static Regex m_ParsePassThree = new Regex("(?<!{[^}]*)\\.([a-zA-Z]+)(?=\\.)");

        // remove extra separator characters
        protected static Regex m_ParsePassFour = new Regex("\\.+");

        // expression used to validate the full path, this is canonical representation
        protected static Regex m_ValidatePath = new Regex("^\\.(({[^}]+}|\\[[0-9]+\\]|\\[\\+\\])\\.)*$");

        // expression used to match path components
        protected static Regex m_PathComponent = new Regex("\\.({[^}]+}|\\[[0-9]+\\]|\\[\\+\\])");

        // extract the internals of an array reference
        protected static Regex m_SimpleArrayPattern = new Regex("^\\[([0-9]+)\\]$");
        protected static Regex m_ArrayPattern = new Regex("^\\[([0-9]+|\\+)\\]$");

        // extract the internals of a has reference
        protected static Regex m_HashPattern = new Regex("^{([^}]+)}$");

        // -----------------------------------------------------------------
        /// <summary>
        /// This is a simple estimator for the size of the stored data, it
        /// is not precise, but should be close enough to implement reasonable
        /// limits on the storage space used
        /// </summary>
        // -----------------------------------------------------------------
        public int StringSpace { get; set; }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        public static bool CanonicalPathExpression(string ipath, out string opath)
        {
            Stack<string> path;
            if (! ParsePathExpression(ipath,out path))
            {
                opath = "";
                return false;
            }

            opath = PathExpressionToKey(path);
            return true;
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        public JsonStore()
        {
            StringSpace = 0;
            m_TakeStore = new List<TakeValueCallbackClass>();
            m_ReadStore = new List<TakeValueCallbackClass>();
        }

        public JsonStore(string value) : this()
        {
            // This is going to throw an exception if the value is not
            // a valid JSON chunk. Calling routines should catch the
            // exception and handle it appropriately
            if (String.IsNullOrEmpty(value))
                ValueStore = new OSDMap();
            else
                ValueStore = OSDParser.DeserializeJson(value);
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        public JsonStoreNodeType GetNodeType(string expr)
        {
            Stack<string> path;
            if (! ParsePathExpression(expr,out path))
                return JsonStoreNodeType.Undefined;

            OSD result = ProcessPathExpression(ValueStore,path);

            if (result == null)
                return JsonStoreNodeType.Undefined;

            if (result is OSDMap)
                return JsonStoreNodeType.Object;

            if (result is OSDArray)
                return JsonStoreNodeType.Array;

            if (OSDBaseType(result.Type))
                return JsonStoreNodeType.Value;

            return JsonStoreNodeType.Undefined;
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        public JsonStoreValueType GetValueType(string expr)
        {
            Stack<string> path;
            if (! ParsePathExpression(expr,out path))
                return JsonStoreValueType.Undefined;

            OSD result = ProcessPathExpression(ValueStore,path);

            if (result == null)
                return JsonStoreValueType.Undefined;

            if (result is OSDMap)
                return JsonStoreValueType.Undefined;

            if (result is OSDArray)
                return JsonStoreValueType.Undefined;

            if (result is OSDBoolean)
                return JsonStoreValueType.Boolean;

            if (result is OSDInteger)
                return JsonStoreValueType.Integer;

            if (result is OSDReal)
                return JsonStoreValueType.Float;

            if (result is OSDString)
                return JsonStoreValueType.String;

            return JsonStoreValueType.Undefined;
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        public int ArrayLength(string expr)
        {
            Stack<string> path;
            if (! ParsePathExpression(expr,out path))
                return -1;

            OSD result = ProcessPathExpression(ValueStore,path);
            if (result != null && result.Type == OSDType.Array)
            {
                OSDArray arr = result as OSDArray;
                return arr.Count;
            }

            return -1;
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        public bool GetValue(string expr, out string value, bool useJson)
        {
            Stack<string> path;
            if (! ParsePathExpression(expr,out path))
            {
                value = "";
                return false;
            }

            OSD result = ProcessPathExpression(ValueStore,path);
            return ConvertOutputValue(result,out value,useJson);
        }


        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        public bool RemoveValue(string expr)
        {
            return SetValueFromExpression(expr,null);
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        public bool SetValue(string expr, string value, bool useJson)
        {
            OSD ovalue;

            // One note of caution... if you use an empty string in the
            // structure it will be assumed to be a default value and will
            // not be seialized in the json

            if (useJson)
            {
                // There doesn't appear to be a good way to determine if the
                // value is valid Json other than to let the parser crash
                try
                {
                    ovalue = OSDParser.DeserializeJson(value);
                }
                catch (Exception)
                {
                    if (value.StartsWith("'") && value.EndsWith("'"))
                    {
                        ovalue = new OSDString(value.Substring(1,value.Length - 2));
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                ovalue = new OSDString(value);
            }

            return SetValueFromExpression(expr,ovalue);
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        public bool TakeValue(string expr, bool useJson, TakeValueCallback cback)
        {
            Stack<string> path;
            if (! ParsePathExpression(expr,out path))
                return false;

            string pexpr = PathExpressionToKey(path);

            OSD result = ProcessPathExpression(ValueStore,path);
            if (result == null)
            {
                m_TakeStore.Add(new TakeValueCallbackClass(pexpr,useJson,cback));
                return false;
            }

            string value = String.Empty;
            if (! ConvertOutputValue(result,out value,useJson))
            {
                // the structure does not match the request so i guess we'll wait
                m_TakeStore.Add(new TakeValueCallbackClass(pexpr,useJson,cback));
                return false;
            }

            SetValueFromExpression(expr,null);
            cback(value);

            return true;
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        public bool ReadValue(string expr, bool useJson, TakeValueCallback cback)
        {
            Stack<string> path;
            if (! ParsePathExpression(expr,out path))
                return false;

            string pexpr = PathExpressionToKey(path);

            OSD result = ProcessPathExpression(ValueStore,path);
            if (result == null)
            {
                m_ReadStore.Add(new TakeValueCallbackClass(pexpr,useJson,cback));
                return false;
            }

            string value = String.Empty;
            if (! ConvertOutputValue(result,out value,useJson))
            {
                // the structure does not match the request so i guess we'll wait
                m_ReadStore.Add(new TakeValueCallbackClass(pexpr,useJson,cback));
                return false;
            }

            cback(value);

            return true;
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        protected bool SetValueFromExpression(string expr, OSD ovalue)
        {
            Stack<string> path;
            if (! ParsePathExpression(expr,out path))
                return false;

            if (path.Count == 0)
            {
                ValueStore = ovalue;
                StringSpace = 0;
                return true;
            }

            // pkey will be the final element in the path, we pull it out here to make sure
            // that the assignment works correctly
            string pkey = path.Pop();
            string pexpr = PathExpressionToKey(path);
            if (pexpr != "")
                pexpr += ".";

            OSD result = ProcessPathExpression(ValueStore,path);
            if (result == null)
                return false;

            // Check pkey, the last element in the path, for and extract array references
            MatchCollection amatches = m_ArrayPattern.Matches(pkey,0);
            if (amatches.Count > 0)
            {
                if (result.Type != OSDType.Array)
                    return false;

                OSDArray amap = result as OSDArray;

                Match match = amatches[0];
                GroupCollection groups = match.Groups;
                string akey = groups[1].Value;

                if (akey == "+")
                {
                    string npkey = String.Format("[{0}]",amap.Count);

                    if (ovalue != null)
                    {
                        StringSpace += ComputeSizeOf(ovalue);

                        amap.Add(ovalue);
                        InvokeNextCallback(pexpr + npkey);
                    }
                    return true;
                }

                int aval = Convert.ToInt32(akey);
                if (0 <= aval && aval < amap.Count)
                {
                    if (ovalue == null)
                    {
                        StringSpace -= ComputeSizeOf(amap[aval]);
                        amap.RemoveAt(aval);
                    }
                    else
                    {
                        StringSpace -= ComputeSizeOf(amap[aval]);
                        StringSpace += ComputeSizeOf(ovalue);
                        amap[aval] = ovalue;
                        InvokeNextCallback(pexpr + pkey);
                    }
                    return true;
                }

                return false;
            }

            // Check for and extract hash references
            MatchCollection hmatches = m_HashPattern.Matches(pkey,0);
            if (hmatches.Count > 0)
            {
                Match match = hmatches[0];
                GroupCollection groups = match.Groups;
                string hkey = groups[1].Value;

                if (result is OSDMap)
                {
                    // this is the assignment case
                    OSDMap hmap = result as OSDMap;
                    if (ovalue != null)
                    {
                        StringSpace -= ComputeSizeOf(hmap[hkey]);
                        StringSpace += ComputeSizeOf(ovalue);

                        hmap[hkey] = ovalue;
                        InvokeNextCallback(pexpr + pkey);
                        return true;
                    }

                    // this is the remove case
                    if (hmap.ContainsKey(hkey))
                    {
                        StringSpace -= ComputeSizeOf(hmap[hkey]);
                        hmap.Remove(hkey);
                        return true;
                    }

                    return false;
                }

                return false;
            }

            // Shouldn't get here if the path was checked correctly
            m_log.WarnFormat("[JsonStore] invalid path expression");
            return false;
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        protected bool InvokeNextCallback(string pexpr)
        {
            // Process all of the reads that match the expression first
            List<TakeValueCallbackClass> reads =
                m_ReadStore.FindAll(delegate(TakeValueCallbackClass tb) { return pexpr.StartsWith(tb.Path); });

            foreach (TakeValueCallbackClass readcb in reads)
            {
                m_ReadStore.Remove(readcb);
                ReadValue(readcb.Path,readcb.UseJson,readcb.Callback);
            }

            // Process one take next
            TakeValueCallbackClass takecb =
                m_TakeStore.Find(delegate(TakeValueCallbackClass tb) { return pexpr.StartsWith(tb.Path); });

            if (takecb != null)
            {
                m_TakeStore.Remove(takecb);
                TakeValue(takecb.Path,takecb.UseJson,takecb.Callback);

                return true;
            }

            return false;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Parse the path expression and put the components into a stack. We
        /// use a stack because we process the path in inverse order later
        /// </summary>
        // -----------------------------------------------------------------
        protected static bool ParsePathExpression(string expr, out Stack<string> path)
        {
            path = new Stack<string>();

            // add front and rear separators
            expr = "." + expr + ".";

            // add separators for quoted exprs and array references
            expr = m_ParsePassOne.Replace(expr,".$1.",-1,0);

            // add quotes to bare identifier
            expr = m_ParsePassThree.Replace(expr,".{$1}",-1,0);

            // remove extra separators
            expr = m_ParsePassFour.Replace(expr,".",-1,0);

            // validate the results (catches extra quote characters for example)
            if (m_ValidatePath.IsMatch(expr))
            {
                MatchCollection matches = m_PathComponent.Matches(expr,0);
                foreach (Match match in matches)
                    path.Push(match.Groups[1].Value);

                return true;
            }

            return false;
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        /// <param>path is a stack where the top level of the path is at the bottom of the stack</param>
        // -----------------------------------------------------------------
        protected static OSD ProcessPathExpression(OSD map, Stack<string> path)
        {
            if (path.Count == 0)
                return map;

            string pkey = path.Pop();

            OSD rmap = ProcessPathExpression(map,path);
            if (rmap == null)
                return null;

            // ---------- Check for an array index ----------
            MatchCollection amatches = m_SimpleArrayPattern.Matches(pkey,0);

            if (amatches.Count > 0)
            {
                if (rmap.Type != OSDType.Array)
                {
                    m_log.WarnFormat("[JsonStore] wrong type for key {2}, expecting {0}, got {1}",OSDType.Array,rmap.Type,pkey);
                    return null;
                }

                OSDArray amap = rmap as OSDArray;

                Match match = amatches[0];
                GroupCollection groups = match.Groups;
                string akey = groups[1].Value;
                int aval = Convert.ToInt32(akey);

                if (aval < amap.Count)
                    return (OSD) amap[aval];

                return null;
            }

            // ---------- Check for a hash index ----------
            MatchCollection hmatches = m_HashPattern.Matches(pkey,0);

            if (hmatches.Count > 0)
            {
                if (rmap.Type != OSDType.Map)
                {
                    m_log.WarnFormat("[JsonStore] wrong type for key {2}, expecting {0}, got {1}",OSDType.Map,rmap.Type,pkey);
                    return null;
                }

                OSDMap hmap = rmap as OSDMap;

                Match match = hmatches[0];
                GroupCollection groups = match.Groups;
                string hkey = groups[1].Value;

                if (hmap.ContainsKey(hkey))
                    return (OSD) hmap[hkey];

                return null;
            }

            // Shouldn't get here if the path was checked correctly
            m_log.WarnFormat("[JsonStore] Path type (unknown) does not match the structure");
            return null;
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        protected static bool ConvertOutputValue(OSD result, out string value, bool useJson)
        {
            value = String.Empty;

            // If we couldn't process the path
            if (result == null)
                return false;

            if (useJson)
            {
                // The path pointed to an intermediate hash structure
                if (result.Type == OSDType.Map)
                {
                    value = OSDParser.SerializeJsonString(result as OSDMap,true);
                    return true;
                }

                // The path pointed to an intermediate hash structure
                if (result.Type == OSDType.Array)
                {
                    value = OSDParser.SerializeJsonString(result as OSDArray,true);
                    return true;
                }

                value = "'" + result.AsString() + "'";
                return true;
            }

            if (OSDBaseType(result.Type))
            {
                value = result.AsString();
                return true;
            }

            return false;
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        protected static string PathExpressionToKey(Stack<string> path)
        {
            if (path.Count == 0)
                return "";

            string pkey = "";
            foreach (string k in path)
                pkey = (pkey == "") ? k : (k + "." + pkey);

            return pkey;
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        protected static bool OSDBaseType(OSDType type)
        {
            // Should be the list of base types for which AsString() returns
            // something useful
            if (type == OSDType.Boolean)
                return true;
            if (type == OSDType.Integer)
                return true;
            if (type == OSDType.Real)
                return true;
            if (type == OSDType.String)
                return true;
            if (type == OSDType.UUID)
                return true;
            if (type == OSDType.Date)
                return true;
            if (type == OSDType.URI)
                return true;

            return false;
        }

        // -----------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // -----------------------------------------------------------------
        protected static int ComputeSizeOf(OSD value)
        {
            string sval;

            if (ConvertOutputValue(value,out sval,true))
                return sval.Length;

            return 0;
        }
    }

    // -----------------------------------------------------------------
    /// <summary>
    /// </summary>
    // -----------------------------------------------------------------
    public class JsonObjectStore : JsonStore
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private UUID m_objectID;

        protected override OSD ValueStore
        {
            get
            {
                SceneObjectPart sop = m_scene.GetSceneObjectPart(m_objectID);
                if (sop == null)
                {
                    // This is bad
                    return null;
                }

                return sop.DynAttrs.TopLevelMap;
            }

            // cannot set the top level
            set
            {
                m_log.InfoFormat("[JsonStore] cannot set top level value in object store");
            }
        }

        public JsonObjectStore(Scene scene, UUID oid) : base()
        {
            m_scene = scene;
            m_objectID = oid;

            // the size limit is imposed on whatever is already in the store
            StringSpace = ComputeSizeOf(ValueStore);
        }
    }

}
