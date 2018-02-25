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

using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Yengine
{
    public partial class Yengine
    {

        private void XmrTestLs(string[] args, int indx)
        {
            bool flagFull = false;
            bool flagQueues = false;
            bool flagTopCPU = false;
            int maxScripts = 0x7FFFFFFF;
            int numScripts = 0;
            string outName = null;
            XMRInstance[] instances;

             // Decode command line options.
            for(int i = indx; i < args.Length; i++)
            {
                if(args[i] == "-full")
                {
                    flagFull = true;
                    continue;
                }
                if(args[i] == "-help")
                {
                    m_log.Info("[YEngine]: yeng ls -full -max=<number> -out=<filename> -queues -topcpu");
                    return;
                }
                if(args[i].StartsWith("-max="))
                {
                    try
                    {
                        maxScripts = Convert.ToInt32(args[i].Substring(5));
                    }
                    catch(Exception e)
                    {
                        m_log.Error("[YEngine]: bad max " + args[i].Substring(5) + ": " + e.Message);
                        return;
                    }
                    continue;
                }
                if(args[i].StartsWith("-out="))
                {
                    outName = args[i].Substring(5);
                    continue;
                }
                if(args[i] == "-queues")
                {
                    flagQueues = true;
                    continue;
                }
                if(args[i] == "-topcpu")
                {
                    flagTopCPU = true;
                    continue;
                }
                if(args[i][0] == '-')
                {
                    m_log.Error("[YEngine]: unknown option " + args[i] + ", try 'yeng ls -help'");
                    return;
                }
            }

            TextWriter outFile = null;
            if(outName != null)
            {
                try
                {
                    outFile = File.CreateText(outName);
                }
                catch(Exception e)
                {
                    m_log.Error("[YEngine]: error creating " + outName + ": " + e.Message);
                    return;
                }
            }
            else
            {
                outFile = new LogInfoTextWriter(m_log);
            }

            try
            {
                 // Scan instance list to find those that match selection criteria.
                if(!Monitor.TryEnter(m_InstancesDict, 100))
                {
                    m_log.Error("[YEngine]: deadlock m_LockedDict=" + m_LockedDict);
                    return;
                }
                try
                {
                    instances = new XMRInstance[m_InstancesDict.Count];
                    foreach(XMRInstance ins in m_InstancesDict.Values)
                    {
                        if(InstanceMatchesArgs(ins, args, indx))
                        {
                            instances[numScripts++] = ins;
                        }
                    }
                }
                finally
                {
                    Monitor.Exit(m_InstancesDict);
                }

                 // Maybe sort by descending CPU time.
                if(flagTopCPU)
                {
                    Array.Sort<XMRInstance>(instances, CompareInstancesByCPUTime);
                }

                 // Print the entries.
                if(!flagFull)
                {
                    outFile.WriteLine("                              ItemID" +
                                      "   CPU(ms)" +
                                      " NumEvents" +
                                      " Status    " +
                                      " World Position                  " +
                                      " <Part>:<Item>");
                }
                for(int i = 0; (i < numScripts) && (i < maxScripts); i++)
                {
                    outFile.WriteLine(instances[i].RunTestLs(flagFull));
                }

                 // Print number of scripts that match selection criteria,
                 // even if we were told to print fewer.
                outFile.WriteLine("total of {0} script(s)", numScripts);

                 // If -queues given, print out queue contents too.
                if(flagQueues)
                {
                    LsQueue(outFile, "start", m_StartQueue, args, indx);
                    LsQueue(outFile, "sleep", m_SleepQueue, args, indx);
                    LsQueue(outFile, "yield", m_YieldQueue, args, indx);
                }
            }
            finally
            {
                outFile.Close();
            }
        }

        private void XmrTestPev(string[] args, int indx)
        {
            bool flagAll = false;
            int numScripts = 0;
            XMRInstance[] instances;

             // Decode command line options.
            int i, j;
            List<string> selargs = new List<string>(args.Length);
            MethodInfo[] eventmethods = typeof(IEventHandlers).GetMethods();
            MethodInfo eventmethod;
            for(i = indx; i < args.Length; i++)
            {
                string arg = args[i];
                if(arg == "-all")
                {
                    flagAll = true;
                    continue;
                }
                if(arg == "-help")
                {
                    m_log.Info("[YEngine]: yeng pev -all | <part-of-script-name> <event-name> <params...>");
                    return;
                }
                if(arg[0] == '-')
                {
                    m_log.Error("[YEngine]: unknown option " + arg + ", try 'yeng pev -help'");
                    return;
                }
                for(j = 0; j < eventmethods.Length; j++)
                {
                    eventmethod = eventmethods[j];
                    if(eventmethod.Name == arg)
                        goto gotevent;
                }
                selargs.Add(arg);
            }
            m_log.Error("[YEngine]: missing <event-name> <params...>, try 'yeng pev -help'");
            return;
            gotevent:
            string eventname = eventmethod.Name;
            StringBuilder sourcesb = new StringBuilder();
            while(++i < args.Length)
            {
                sourcesb.Append(' ');
                sourcesb.Append(args[i]);
            }
            string sourcest = sourcesb.ToString();
            string sourcehash;
            youveanerror = false;
            Token t = TokenBegin.Construct("", null, ErrorMsg, sourcest, out sourcehash);
            if(youveanerror)
                return;
            ParameterInfo[] paraminfos = eventmethod.GetParameters();
            object[] paramvalues = new object[paraminfos.Length];
            i = 0;
            while(!((t = t.nextToken) is TokenEnd))
            {
                if(i >= paramvalues.Length)
                {
                    ErrorMsg(t, "extra parameter(s)");
                    return;
                }
                paramvalues[i] = ParseParamValue(ref t);
                if(paramvalues[i] == null)
                    return;
                i++;
            }
            OpenSim.Region.ScriptEngine.Shared.EventParams eps =
                    new OpenSim.Region.ScriptEngine.Shared.EventParams(eventname, paramvalues, zeroDetectParams);

             // Scan instance list to find those that match selection criteria.
            if(!Monitor.TryEnter(m_InstancesDict, 100))
            {
                m_log.Error("[YEngine]: deadlock m_LockedDict=" + m_LockedDict);
                return;
            }

            try
            {
                instances = new XMRInstance[m_InstancesDict.Count];
                foreach(XMRInstance ins in m_InstancesDict.Values)
                {
                    if(flagAll || InstanceMatchesArgs(ins, selargs.ToArray(), 0))
                    {
                        instances[numScripts++] = ins;
                    }
                }
            }
            finally
            {
                Monitor.Exit(m_InstancesDict);
            }

             // Post event to the matching instances.
            for(i = 0; i < numScripts; i++)
            {
                XMRInstance inst = instances[i];
                m_log.Info("[YEngine]: post " + eventname + " to " + inst.m_DescName);
                inst.PostEvent(eps);
            }
        }

        private object ParseParamValue(ref Token token)
        {
            if(token is TokenFloat)
            {
                return new LSL_Float(((TokenFloat)token).val);
            }
            if(token is TokenInt)
            {
                return new LSL_Integer(((TokenInt)token).val);
            }
            if(token is TokenStr)
            {
                return new LSL_String(((TokenStr)token).val);
            }
            if(token is TokenKwCmpLT)
            {
                List<double> valuelist = new List<double>();
                while(!((token = token.nextToken) is TokenKwCmpGT))
                {
                    if(!(token is TokenKwComma))
                    {
                        object value = ParseParamValue(ref token);
                        if(value == null)
                            return null;
                        if(value is int)
                            value = (double)(int)value;
                        if(!(value is double))
                        {
                            ErrorMsg(token, "must be float or integer constant");
                            return null;
                        }
                        valuelist.Add((double)value);
                    }
                    else if(token.prevToken is TokenKwComma)
                    {
                        ErrorMsg(token, "missing constant");
                        return null;
                    }
                }
                double[] values = valuelist.ToArray();
                switch(values.Length)
                {
                    case 3:
                        {
                            return new LSL_Vector(values[0], values[1], values[2]);
                        }
                    case 4:
                        {
                            return new LSL_Rotation(values[0], values[1], values[2], values[3]);
                        }
                    default:
                        {
                            ErrorMsg(token, "not rotation or vector");
                            return null;
                        }
                }
            }
            if(token is TokenKwBrkOpen)
            {
                List<object> valuelist = new List<object>();
                while(!((token = token.nextToken) is TokenKwBrkClose))
                {
                    if(!(token is TokenKwComma))
                    {
                        object value = ParseParamValue(ref token);
                        if(value == null)
                            return null;
                        valuelist.Add(value);
                    }
                    else if(token.prevToken is TokenKwComma)
                    {
                        ErrorMsg(token, "missing constant");
                        return null;
                    }
                }
                return new LSL_List(valuelist.ToArray());
            }
            if(token is TokenName)
            {
                FieldInfo field = typeof(OpenSim.Region.ScriptEngine.Shared.ScriptBase.ScriptBaseClass).GetField(((TokenName)token).val);
                if((field != null) && field.IsPublic && (field.IsLiteral || (field.IsStatic && field.IsInitOnly)))
                {
                    return field.GetValue(null);
                }
            }
            ErrorMsg(token, "invalid constant");
            return null;
        }

        private bool youveanerror;
        private void ErrorMsg(Token token, string message)
        {
            youveanerror = true;
            m_log.Info("[YEngine]: " + token.posn + " " + message);
        }

        private void XmrTestReset(string[] args, int indx)
        {
            bool flagAll = false;
            int numScripts = 0;
            XMRInstance[] instances;

            if(args.Length <= indx)
            {
                m_log.Error("[YEngine]: must specify part of script name or -all for all scripts");
                return;
            }

             // Decode command line options.
            for(int i = indx; i < args.Length; i++)
            {
                if(args[i] == "-all")
                {
                    flagAll = true;
                    continue;
                }
                if(args[i] == "-help")
                {
                    m_log.Info("[YEngine]: yeng reset -all | <part-of-script-name>");
                    return;
                }
                if(args[i][0] == '-')
                {
                    m_log.Error("[YEngine]: unknown option " + args[i] + ", try 'yeng reset -help'");
                    return;
                }
            }

             // Scan instance list to find those that match selection criteria.
            if(!Monitor.TryEnter(m_InstancesDict, 100))
            {
                m_log.Error("[YEngine]: deadlock m_LockedDict=" + m_LockedDict);
                return;
            }

            try
            {
                instances = new XMRInstance[m_InstancesDict.Count];
                foreach(XMRInstance ins in m_InstancesDict.Values)
                {
                    if(flagAll || InstanceMatchesArgs(ins, args, indx))
                    {
                        instances[numScripts++] = ins;
                    }
                }
            }
            finally
            {
                Monitor.Exit(m_InstancesDict);
            }

             // Reset the instances as if someone clicked their "Reset" button.
            for(int i = 0; i < numScripts; i++)
            {
                XMRInstance inst = instances[i];
                m_log.Info("[YEngine]: resetting " + inst.m_DescName);
                inst.Reset();
            }
        }

        private static int CompareInstancesByCPUTime(XMRInstance a, XMRInstance b)
        {
            if(a == null)
            {
                return (b == null) ? 0 : 1;
            }
            if(b == null)
            {
                return -1;
            }
            if(b.m_CPUTime < a.m_CPUTime)
                return -1;
            if(b.m_CPUTime > a.m_CPUTime)
                return 1;
            return 0;
        }

        private void LsQueue(TextWriter outFile, string name, XMRInstQueue queue, string[] args, int indx)
        {
            outFile.WriteLine("Queue " + name + ":");
            lock(queue)
            {
                for(XMRInstance inst = queue.PeekHead(); inst != null; inst = inst.m_NextInst)
                {
                    try
                    {
                         // Try to print instance name.
                        if(InstanceMatchesArgs(inst, args, indx))
                        {
                            outFile.WriteLine("   " + inst.ItemID.ToString() + " " + inst.m_DescName);
                        }
                    }
                    catch(Exception e)
                    {
                         // Sometimes there are instances in the queue that are disposed.
                        outFile.WriteLine("   " + inst.ItemID.ToString() + " " + inst.m_DescName + ": " + e.Message);
                    }
                }
            }
        }

        private bool InstanceMatchesArgs(XMRInstance ins, string[] args, int indx)
        {
            bool hadSomethingToCompare = false;

            for(int i = indx; i < args.Length; i++)
            {
                if(args[i][0] != '-')
                {
                    hadSomethingToCompare = true;
                    if(ins.m_DescName.Contains(args[i]))
                        return true;
                    if(ins.ItemID.ToString().Contains(args[i]))
                        return true;
                    if(ins.AssetID.ToString().Contains(args[i]))
                        return true;
                }
            }
            return !hadSomethingToCompare;
        }
    }

    /**
     * @brief Make m_log.Info look like a text writer.
     */
    public class LogInfoTextWriter: TextWriter
    {
        private StringBuilder sb = new StringBuilder();
        private ILog m_log;
        public LogInfoTextWriter(ILog m_log)
        {
            this.m_log = m_log;
        }
        public override void Write(char c)
        {
            if(c == '\n')
            {
                m_log.Info("[YEngine]: " + sb.ToString());
                sb.Remove(0, sb.Length);
            }
            else
            {
                sb.Append(c);
            }
        }
        public override void Close()
        {
        }
        public override Encoding Encoding
        {
            get
            {
                return Encoding.UTF8;
            }
        }
    }
}
