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

using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

/**
 * Contains classes that disassemble or decompile an yobj file.
 * See xmrengcomp.cx utility program.
 */

namespace OpenSim.Region.ScriptEngine.Yengine
{
    /*
     * Encapsulate object code for a method.
     */
    public abstract class ObjectTokens
    {
        public ScriptObjCode scriptObjCode;

        public ObjectTokens(ScriptObjCode scriptObjCode)
        {
            this.scriptObjCode = scriptObjCode;
        }

        public abstract void Close();
        public abstract void BegMethod(DynamicMethod method);
        public abstract void EndMethod();
        public abstract void DefineLabel(int number, string name);
        public abstract void DefineLocal(int number, string name, string type, Type syType);
        public abstract void DefineMethod(string methName, Type retType, Type[] argTypes, string[] argNames);
        public abstract void MarkLabel(int offset, int number);
        public abstract void BegExcBlk(int offset);
        public abstract void BegCatBlk(int offset, Type excType);
        public abstract void BegFinBlk(int offset);
        public abstract void EndExcBlk(int offset);
        public abstract void EmitNull(int offset, OpCode opCode);
        public abstract void EmitField(int offset, OpCode opCode, FieldInfo field);
        public abstract void EmitLocal(int offset, OpCode opCode, int number);
        public abstract void EmitType(int offset, OpCode opCode, Type type);
        public abstract void EmitLabel(int offset, OpCode opCode, int number);
        public abstract void EmitLabels(int offset, OpCode opCode, int[] numbers);
        public abstract void EmitMethod(int offset, OpCode opCode, MethodInfo method);
        public abstract void EmitCtor(int offset, OpCode opCode, ConstructorInfo ctor);
        public abstract void EmitDouble(int offset, OpCode opCode, double value);
        public abstract void EmitFloat(int offset, OpCode opCode, float value);
        public abstract void EmitInteger(int offset, OpCode opCode, int value);
        public abstract void EmitString(int offset, OpCode opCode, string value);
    }

    /******************\
     *  DISASSEMBLER  *
    \******************/

    public class OTDisassemble: ObjectTokens
    {
        private static readonly int OPCSTRWIDTH = 12;

        private Dictionary<int, string> labelNames;
        private Dictionary<int, string> localNames;
        private StringBuilder lbuf = new StringBuilder();
        private TextWriter twout;

        public OTDisassemble(ScriptObjCode scriptObjCode, TextWriter twout) : base(scriptObjCode)
        {
            this.twout = twout;
        }

        public override void Close()
        {
            twout.WriteLine("TheEnd.");
        }

        /**
         * About to generate object code for this method.
         */
        public override void BegMethod(DynamicMethod method)
        {
            labelNames = new Dictionary<int, string>();
            localNames = new Dictionary<int, string>();

            twout.WriteLine("");

            lbuf.Append(method.ReturnType.Name);
            lbuf.Append(' ');
            lbuf.Append(method.Name);

            ParameterInfo[] parms = method.GetParameters();
            int nArgs = parms.Length;
            lbuf.Append(" (");
            for(int i = 0; i < nArgs; i++)
            {
                if(i > 0)
                    lbuf.Append(", ");
                lbuf.Append(parms[i].ParameterType.Name);
            }
            lbuf.Append(')');
            FlushLine();

            lbuf.Append('{');
            FlushLine();
        }

        /**
         * Dump out reconstructed source for this method.
         */
        public override void EndMethod()
        {
            lbuf.Append('}');
            FlushLine();
        }

        /**
         * Add instructions to stream.
         */
        public override void DefineLabel(int number, string name)
        {
            labelNames[number] = name + "$" + number;
        }

        public override void DefineLocal(int number, string name, string type, Type syType)
        {
            localNames[number] = name + "$" + number;

            lbuf.Append("          ");
            lbuf.Append(type.PadRight(OPCSTRWIDTH - 1));
            lbuf.Append(' ');
            lbuf.Append(localNames[number]);
            FlushLine();
        }

        public override void DefineMethod(string methName, Type retType, Type[] argTypes, string[] argNames)
        {
        }

        public override void MarkLabel(int offset, int number)
        {
            LinePrefix(offset);
            lbuf.Append(labelNames[number]);
            lbuf.Append(":");
            FlushLine();
        }

        public override void BegExcBlk(int offset)
        {
            LinePrefix(offset);
            lbuf.Append(" BeginExceptionBlock");
            FlushLine();
        }

        public override void BegCatBlk(int offset, Type excType)
        {
            LinePrefix(offset);
            lbuf.Append(" BeginCatchBlock ");
            lbuf.Append(excType.Name);
            FlushLine();
        }

        public override void BegFinBlk(int offset)
        {
            LinePrefix(offset);
            lbuf.Append(" BeginFinallyBlock");
            FlushLine();
        }

        public override void EndExcBlk(int offset)
        {
            LinePrefix(offset);
            lbuf.Append(" EndExceptionBlock");
            FlushLine();
        }

        public override void EmitNull(int offset, OpCode opCode)
        {
            LinePrefix(offset, opCode);
            FlushLine();
        }

        public override void EmitField(int offset, OpCode opCode, FieldInfo field)
        {
            LinePrefix(offset, opCode);
            lbuf.Append(field.DeclaringType.Name);
            lbuf.Append(':');
            lbuf.Append(field.Name);
            lbuf.Append(" -> ");
            lbuf.Append(field.FieldType.Name);
            lbuf.Append("   (field)");
            FlushLine();
        }

        public override void EmitLocal(int offset, OpCode opCode, int number)
        {
            LinePrefix(offset, opCode);
            lbuf.Append(localNames[number]);
            lbuf.Append("   (local)");
            FlushLine();
        }

        public override void EmitType(int offset, OpCode opCode, Type type)
        {
            LinePrefix(offset, opCode);
            lbuf.Append(type.Name);
            lbuf.Append("   (type)");
            FlushLine();
        }

        public override void EmitLabel(int offset, OpCode opCode, int number)
        {
            LinePrefix(offset, opCode);
            lbuf.Append(labelNames[number]);
            lbuf.Append("   (label)");
            FlushLine();
        }

        public override void EmitLabels(int offset, OpCode opCode, int[] numbers)
        {
            LinePrefix(offset, opCode);

            int lineLen = lbuf.Length;
            int nLabels = numbers.Length;
            for(int i = 0; i < nLabels; i++)
            {
                if(i > 0)
                {
                    lbuf.AppendLine();
                    lbuf.Append(",".PadLeft(lineLen));
                }
                lbuf.Append(labelNames[numbers[i]]);
            }

            FlushLine();
        }

        public override void EmitMethod(int offset, OpCode opCode, MethodInfo method)
        {
            LinePrefix(offset, opCode);

            ParameterInfo[] parms = method.GetParameters();
            int nArgs = parms.Length;
            if(method.DeclaringType != null)
            {
                lbuf.Append(method.DeclaringType.Name);
                lbuf.Append(':');
            }
            lbuf.Append(method.Name);
            lbuf.Append('(');
            for(int i = 0; i < nArgs; i++)
            {
                if(i > 0)
                    lbuf.Append(",");
                lbuf.Append(parms[i].ParameterType.Name);
            }
            lbuf.Append(") -> ");
            lbuf.Append(method.ReturnType.Name);

            FlushLine();
        }

        public override void EmitCtor(int offset, OpCode opCode, ConstructorInfo ctor)
        {
            LinePrefix(offset, opCode);

            ParameterInfo[] parms = ctor.GetParameters();
            int nArgs = parms.Length;
            lbuf.Append(ctor.DeclaringType.Name);
            lbuf.Append(":(");
            for(int i = 0; i < nArgs; i++)
            {
                if(i > 0)
                    lbuf.Append(",");
                lbuf.Append(parms[i].ParameterType.Name);
            }
            lbuf.Append(")");

            FlushLine();
        }

        public override void EmitDouble(int offset, OpCode opCode, double value)
        {
            LinePrefix(offset, opCode);
            lbuf.Append(value.ToString());
            lbuf.Append("   (double)");
            FlushLine();
        }

        public override void EmitFloat(int offset, OpCode opCode, float value)
        {
            LinePrefix(offset, opCode);
            lbuf.Append(value.ToString());
            lbuf.Append("   (float)");
            FlushLine();
        }

        public override void EmitInteger(int offset, OpCode opCode, int value)
        {
            LinePrefix(offset, opCode);
            lbuf.Append(value.ToString());
            lbuf.Append("   (int)");
            FlushLine();
        }

        public override void EmitString(int offset, OpCode opCode, string value)
        {
            LinePrefix(offset, opCode);
            lbuf.Append("\"");
            lbuf.Append(value);
            lbuf.Append("\"   (string)");
            FlushLine();
        }

        /**
         * Put offset and opcode at beginning of line.
         */
        private void LinePrefix(int offset, OpCode opCode)
        {
            LinePrefix(offset);
            lbuf.Append("  ");
            lbuf.Append(opCode.ToString().PadRight(OPCSTRWIDTH - 1));
            lbuf.Append(' ');
        }

        private void LinePrefix(int offset)
        {
            lbuf.Append("  ");
            lbuf.Append(offset.ToString("X4"));
            lbuf.Append("  ");
        }

        /**
         * Flush line buffer to output file.
         */
        private void FlushLine()
        {
            if(lbuf.Length > 0)
            {
                twout.WriteLine(lbuf.ToString());
                lbuf.Remove(0, lbuf.Length);
            }
        }
    }

    /****************\
     *  DECOMPILER  *
    \****************/

    /**
     * Note:  The decompiler does not handle any xmroption extensions
     *        such as &&&, |||, ? operators and switch statements, as
     *        they do branches with a non-empty stack, which is way
     *        beyond this code's ability to analyze.
     */

    public class OTDecompile: ObjectTokens
    {
        public const string _mainCallNo = "__mainCallNo$";
        public const string _callLabel = "__call_";
        public const string _callMode = "callMode";
        public const string _checkRunQuick = "CheckRunQuick";
        public const string _checkRunStack = "CheckRunStack";
        public const string _cmRestore = "__cmRestore";
        public const string _doBreak = "dobreak_";
        public const string _doCont = "docont_";
        public const string _doGblInit = "doGblInit";
        public const string _doLoop = "doloop_";
        public const string _ehArgs = "ehArgs";
        public const string _forBreak = "forbreak_";
        public const string _forCont = "forcont_";
        public const string _forLoop = "forloop_";
        public const string _globalvarinit = "$globalvarinit()";
        public const string _heapTrackerPop = "Pop";
        public const string _heapTrackerPush = "Push";
        public const string _ifDone = "ifdone_";
        public const string _ifElse = "ifelse_";
        public const string _llAbstemp = "llAbstemp";
        public const string _retlbl = "__retlbl";
        public const string _retval = "__retval$";
        public const string _whileBreak = "whilebreak_";
        public const string _whileCont = "whilecont_";
        public const string _whileLoop = "whileloop_";
        public const string _xmrinst = "__xmrinst";
        public const string _xmrinstlocal = "__xmrinst$";

        private const string INDENT = "    ";
        private const string LABELINDENT = "  ";

        private static Dictionary<string, string> typeTranslator = InitTypeTranslator();
        private static Dictionary<string, string> InitTypeTranslator()
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            d["Boolean"] = "integer";
            d["bool"] = "integer";
            d["Double"] = "float";
            d["double"] = "float";
            d["Int32"] = "integer";
            d["int"] = "integer";
            d["htlist"] = "list";
            d["htobject"] = "object";
            d["htstring"] = "string";
            d["lslfloat"] = "float";
            d["lslint"] = "integer";
            d["lsllist"] = "list";
            d["lslrot"] = "rotation";
            d["lslstr"] = "string";
            d["lslvec"] = "vector";
            d["Quaternion"] = "rotation";
            d["String"] = "string";
            d["Vector3"] = "vector";
            return d;
        }

        private Dictionary<int, OTLocal> eharglist;
        private Dictionary<int, OTLabel> labels;
        private Dictionary<int, OTLocal> locals;
        private Dictionary<string, string[]> methargnames;
        private LinkedList<OTCilInstr> cilinstrs;
        private OTStmtBlock topBlock;
        private Stack<OTOpnd> opstack;
        private Stack<OTStmtBegExcBlk> trystack;
        private Stack<OTStmtBlock> blockstack;

        private int dupNo;
        private DynamicMethod method;
        private string laststate;
        private TextWriter twout;

        public OTDecompile(ScriptObjCode scriptObjCode, TextWriter twout) : base(scriptObjCode)
        {
            this.twout = twout;
            twout.Write("xmroption dollarsigns;");
            methargnames = new Dictionary<string, string[]>();
        }

        public override void Close()
        {
            if(laststate != null)
            {
                twout.Write("\n}");
                laststate = null;
            }
            twout.Write('\n');
        }

        /**
         * About to generate object code for this method.
         */
        public override void BegMethod(DynamicMethod method)
        {
            this.method = method;

            eharglist = new Dictionary<int, OTLocal>();
            labels = new Dictionary<int, OTLabel>();
            locals = new Dictionary<int, OTLocal>();
            cilinstrs = new LinkedList<OTCilInstr>();
            opstack = new Stack<OTOpnd>();
            trystack = new Stack<OTStmtBegExcBlk>();
            blockstack = new Stack<OTStmtBlock>();

            dupNo = 0;
        }

        /**
         * Dump out reconstructed source for this method.
         */
        public override void EndMethod()
        {
             // Convert CIL code to primitive statements.
             // There are a bunch of labels and internal code such as call stack save restore.
            topBlock = new OTStmtBlock();
            blockstack.Push(topBlock);
            for(LinkedListNode<OTCilInstr> link = cilinstrs.First; link != null; link = link.Next)
            {
                link.Value.BuildStatements(this, link);
            }

             // Strip out stuff we don't want, such as references to callMode.
             // This strips out stack frame capture and restore code.
            topBlock.StripStuff(null);

            // including a possible final return statement
            // - delete if void return value
            // - delete if returning __retval cuz we converted all __retval assignments to return statements
            if((topBlock.blkstmts.Last != null) && (topBlock.blkstmts.Last.Value is OTStmtRet))
            {
                OTStmtRet finalret = (OTStmtRet)topBlock.blkstmts.Last.Value;
                if((finalret.value == null) ||
                        ((finalret.value is OTOpndLocal) &&
                                ((OTOpndLocal)finalret.value).local.name.StartsWith(_retval)))
                {
                    topBlock.blkstmts.RemoveLast();
                }
            }

             // At this point, all behind-the-scenes references are removed except
             // that the do/for/if/while blocks are represented by OTStmtCont-style
             // if/jumps.  So try to convert them to the higher-level structures.
            topBlock.DetectDoForIfWhile(null);

             // Final strip to get rid of unneeded @forbreak_<suffix>; labels and the like.
            topBlock.StripStuff(null);

             // Build reference counts so we don't output unneeded declarations,
             // especially temps and internal variables.
            foreach(OTLocal local in locals.Values)
            {
                local.nlclreads = 0;
                local.nlclwrites = 0;
            }
            topBlock.CountRefs();
            for(IEnumerator<int> localenum = locals.Keys.GetEnumerator(); localenum.MoveNext();)
            {
                OTLocal local = locals[localenum.Current];
                if(((local.nlclreads | local.nlclwrites) == 0) || local.name.StartsWith(_xmrinstlocal))
                {
                    locals.Remove(localenum.Current);
                    localenum = locals.Keys.GetEnumerator();
                }
            }

             // Strip the $n off of local vars that are not ambiguous.
             // Make sure they don't mask globals and arguments as well.
            Dictionary<string, int> namecounts = new Dictionary<string, int>();
            foreach(Dictionary<int, string> varnames in scriptObjCode.globalVarNames.Values)
            {
                foreach(string varname in varnames.Values)
                {
                    int count;
                    if(!namecounts.TryGetValue(varname, out count))
                        count = 0;
                    namecounts[varname] = count + 1;
                }
            }
            if(methargnames.ContainsKey(method.Name))
            {
                foreach(string argname in methargnames[method.Name])
                {
                    int count;
                    if(!namecounts.TryGetValue(argname, out count))
                        count = 0;
                    namecounts[argname] = count + 1;
                }
            }
            foreach(OTLocal local in locals.Values)
            {
                int i = local.name.LastIndexOf('$');
                string name = local.name.Substring(0, i);
                int count;
                if(!namecounts.TryGetValue(name, out count))
                    count = 0;
                namecounts[name] = count + 1;
            }
            foreach(OTLocal local in locals.Values)
            {
                int i = local.name.LastIndexOf('$');
                string name = local.name.Substring(0, i);
                int count = namecounts[name];
                if(count == 1)
                    local.name = name;
            }

             // Print out result.
            if(method.Name == _globalvarinit)
            {
                GlobalsDump();
            }
            else
            {
                MethodDump();
            }
        }

        /**
         * Add instructions to stream.
         */
        public override void DefineLabel(int number, string name)
        {
            labels.Add(number, new OTLabel(number, name));
        }
        public override void DefineLocal(int number, string name, string type, Type syType)
        {
            locals.Add(number, new OTLocal(number, name, type));
        }
        public override void DefineMethod(string methName, Type retType, Type[] argTypes, string[] argNames)
        {
            methargnames[methName] = argNames;
        }
        public override void MarkLabel(int offset, int number)
        {
            OTCilInstr label = labels[number];
            label.offset = offset;
            cilinstrs.AddLast(label);
        }
        public override void BegExcBlk(int offset)
        {
            cilinstrs.AddLast(new OTCilBegExcBlk(offset));
        }
        public override void BegCatBlk(int offset, Type excType)
        {
            cilinstrs.AddLast(new OTCilBegCatBlk(offset, excType));
        }
        public override void BegFinBlk(int offset)
        {
            cilinstrs.AddLast(new OTCilBegFinBlk(offset));
        }
        public override void EndExcBlk(int offset)
        {
            cilinstrs.AddLast(new OTCilEndExcBlk(offset));
        }
        public override void EmitNull(int offset, OpCode opCode)
        {
            cilinstrs.AddLast(new OTCilNull(offset, opCode));
        }
        public override void EmitField(int offset, OpCode opCode, FieldInfo field)
        {
            cilinstrs.AddLast(new OTCilField(offset, opCode, field));
        }
        public override void EmitLocal(int offset, OpCode opCode, int number)
        {
            cilinstrs.AddLast(new OTCilLocal(offset, opCode, locals[number]));
        }
        public override void EmitType(int offset, OpCode opCode, Type type)
        {
            cilinstrs.AddLast(new OTCilType(offset, opCode, type));
        }
        public override void EmitLabel(int offset, OpCode opCode, int number)
        {
            cilinstrs.AddLast(new OTCilLabel(offset, opCode, labels[number]));
        }
        public override void EmitLabels(int offset, OpCode opCode, int[] numbers)
        {
            OTLabel[] labelarray = new OTLabel[numbers.Length];
            for(int i = 0; i < numbers.Length; i++)
            {
                labelarray[i] = labels[numbers[i]];
            }
            cilinstrs.AddLast(new OTCilLabels(offset, opCode, labelarray));
        }
        public override void EmitMethod(int offset, OpCode opCode, MethodInfo method)
        {
            cilinstrs.AddLast(new OTCilMethod(offset, opCode, method));
        }
        public override void EmitCtor(int offset, OpCode opCode, ConstructorInfo ctor)
        {
            cilinstrs.AddLast(new OTCilCtor(offset, opCode, ctor));
        }
        public override void EmitDouble(int offset, OpCode opCode, double value)
        {
            cilinstrs.AddLast(new OTCilDouble(offset, opCode, value));
        }
        public override void EmitFloat(int offset, OpCode opCode, float value)
        {
            cilinstrs.AddLast(new OTCilFloat(offset, opCode, value));
        }
        public override void EmitInteger(int offset, OpCode opCode, int value)
        {
            cilinstrs.AddLast(new OTCilInteger(offset, opCode, value));
        }
        public override void EmitString(int offset, OpCode opCode, string value)
        {
            cilinstrs.AddLast(new OTCilString(offset, opCode, value));
        }

        /**
         * Add the given statement to the end of the currently open block.
         */
        public void AddLastStmt(OTStmt stmt)
        {
            blockstack.Peek().blkstmts.AddLast(stmt);
        }

        /**
         * Generate output for $globalvarinit() function.
         * Also outputs declarations for global variables.
         */
        private void GlobalsDump()
        {
             // Scan $globalvarinit().  It should only have global var assignments in it.
             // Also gather up list of variables it initializes.
            bool badinit = false;
            Dictionary<string, string> inittypes = new Dictionary<string, string>();
            foreach(OTStmt stmt in topBlock.blkstmts)
            {
                if(!(stmt is OTStmtStore))
                {
                    badinit = true;
                    break;
                }
                OTStmtStore store = (OTStmtStore)stmt;
                if(!(store.varwr is OTOpndGlobal))
                {
                    badinit = true;
                    break;
                }
                OTOpndGlobal globalop = (OTOpndGlobal)store.varwr;
                inittypes[globalop.PrintableString] = "";
            }

             // Scan through list of all global variables in the script.
             // Output declarations for those what don't have any init statement for them.
             // Save the type for those that do have init statements.
            bool first = true;
            foreach(string iartypename in scriptObjCode.globalVarNames.Keys)
            {
                Dictionary<int, string> varnames = scriptObjCode.globalVarNames[iartypename];
                string typename = iartypename.ToLowerInvariant();
                if(typename.StartsWith("iar"))
                    typename = typename.Substring(3);
                if(typename.EndsWith("s"))
                    typename = typename.Substring(0, typename.Length - 1);
                foreach(string varname in varnames.Values)
                {
                    if(!badinit && inittypes.ContainsKey(varname))
                    {
                        inittypes[varname] = typename;
                    }
                    else
                    {
                        if(first)
                            twout.Write('\n');
                        twout.Write('\n' + typename + ' ' + varname + ';');
                        first = false;
                    }
                }
            }

             // If $globalvarinit() has anything bad in it, output it as a function.
             // Otherwise, output it as a series of global declarations with init values.
            if(badinit)
            {
                MethodDump();
            }
            else
            {
                foreach(OTStmt stmt in topBlock.blkstmts)
                {
                    OTStmtStore store = (OTStmtStore)stmt;
                    OTOpndGlobal globalop = (OTOpndGlobal)store.varwr;
                    string name = globalop.PrintableString;
                    if(first)
                        twout.Write('\n');
                    twout.Write('\n' + inittypes[name] + ' ');
                    store.PrintStmt(twout, "");
                    first = false;
                }
            }
        }

        /**
         * Generate output for other functions.
         */
        private void MethodDump()
        {
            string indent;

             // Event handlers don't have an argument list as such in the original
             // code.  Instead they have a series of assignments from ehargs[] to
             // local variables.  So make those local variables look like they are
             // an argument list.
            int i = method.Name.IndexOf(' ');
            if(i >= 0)
            {
                 // Maybe we have to output the state name.
                string statename = method.Name.Substring(0, i);
                string eventname = method.Name.Substring(++i);

                if(laststate != statename)
                {
                    if(laststate != null)
                        twout.Write("\n}");
                    if(statename == "default")
                    {
                        twout.Write("\n\ndefault {");
                    }
                    else
                    {
                        twout.Write("\n\nstate " + statename + " {");
                    }
                    laststate = statename;
                }
                else
                {
                    twout.Write('\n');
                }

                 // Output event name and argument list.
                 // Remove from locals list so they don't print below.
                twout.Write('\n' + INDENT + eventname + " (");
                MethodInfo meth = typeof(IEventHandlers).GetMethod(eventname);
                i = 0;
                foreach(ParameterInfo pi in meth.GetParameters())
                {
                    // skip the first param cuz it's the XMRInstance arg
                    if(i > 0)
                        twout.Write(", ");
                    OTLocal local;
                    if(eharglist.TryGetValue(i, out local) && locals.ContainsKey(local.number))
                    {
                        twout.Write(local.DumpString());
                        locals.Remove(local.number);
                    }
                    else
                    {
                        // maybe the assignment was removed
                        // eg, because the local was write-only (not referenced)
                        // so substitute in placeholder that won't be referenced
                        twout.Write(AbbrType(pi.ParameterType) + " arg$" + (i + 1));
                    }
                    i++;
                }
                twout.Write(')');

                 // Indent method body by 4 spaces.
                indent = INDENT;
            }
            else
            {
                 // Maybe need to close out previous state.
                if(laststate != null)
                {
                    twout.Write("\n}");
                    laststate = null;
                }

                 // Output blank line and return type (if any).
                twout.Write("\n\n");
                if(method.ReturnType != typeof(void))
                {
                    twout.Write(AbbrType(method.ReturnType) + ' ');
                }

                 // Output method name and argument list.
                int j = method.Name.IndexOf('(');
                if(j < 0)
                {
                    twout.Write(method.Name);
                }
                else
                {
                    twout.Write(method.Name.Substring(0, j) + " (");
                    bool first = true;
                    j = 0;
                    foreach(ParameterInfo pi in method.GetParameters())
                    {
                        if(j > 0)
                        {  // skip the XMRInstance arg$0 parameter
                            if(!first)
                                twout.Write(", ");
                            twout.Write(AbbrType(pi.ParameterType) + ' ' + MethArgName(j));
                            first = false;
                        }
                        j++;
                    }
                    twout.Write(')');
                }

                 // Don't indent method body at all.
                indent = "";
            }

             // Output local variable declarations.
            twout.Write('\n' + indent + '{');
            bool didOne = false;
            foreach(OTLocal local in locals.Values)
            {
                twout.Write('\n' + indent + INDENT + local.DumpString() + ";  // r:" + local.nlclreads + " w:" + local.nlclwrites);
                didOne = true;
            }
            if(didOne)
                twout.Write('\n');

             // Output statements.
            if(topBlock.blkstmts.Count == 0)
            {
                twout.Write(" }");
            }
            else
            {
                topBlock.PrintBodyAndEnd(twout, indent);
            }
        }

        /**
         * Get abbreviated type string.
         */
        public static string AbbrType(Type type)
        {
            if(type == null)
                return "null";
            return AbbrType(type.Name);
        }
        public static string AbbrType(string type)
        {
            if(type.StartsWith("OpenSim.Region.ScriptEngine.YEngine."))
            {
                type = type.Substring(38);
                int i = type.IndexOf(',');
                if(i > 0)
                    type = type.Substring(0, i);
            }
            if(typeTranslator.ContainsKey(type))
            {
                type = typeTranslator[type];
            }
            return type;
        }

        /**
         * Get current method's argument name.
         */
        public string MethArgName(int index)
        {
            string[] argnames;
            if(methargnames.TryGetValue(method.Name, out argnames) && (index < argnames.Length))
            {
                return argnames[index];
            }
            return "arg$" + index;
        }

        /**
         * Strip svperflvovs (float) cast from rotation/vector values.
         */
        public static OTOpnd StripFloatCast(OTOpnd op)
        {
            if(op is OTOpndCast)
            {
                OTOpndCast opcast = (OTOpndCast)op;
                if((opcast.type == typeof(double)) && (opcast.value is OTOpndInt))
                {
                    return opcast.value;
                }
            }
            return op;
        }

        /**
         * Strip svperflvovs Brtrues so we don't end up with stuff like 'if (!! someint) ...'.
         */
        public static OTOpnd StripBrtrue(OTOpnd op)
        {
            if(op is OTOpndUnOp)
            {
                OTOpndUnOp opunop = (OTOpndUnOp)op;
                if(opunop.opCode == MyOp.Brtrue)
                    return opunop.value;
            }
            return op;
        }

        /*
         * Local variable declaration.
         */
        private class OTLocal
        {
            public int number;
            public string name;
            public string type;

            public int nlclreads;
            public int nlclwrites;

            public OTLocal(int number, string name, string type)
            {
                this.number = number;
                this.name = name.StartsWith("tmp$") ? name : name + "$" + number;
                this.type = type;
            }

            public string DumpString()
            {
                return AbbrType(type) + ' ' + name;
            }
        }

        /***********************************************\
         *  Tokens that are one-for-one with CIL code  *
        \***********************************************/

        /*
         * Part of instruction stream.
         */
        public abstract class OTCilInstr
        {
            public int offset;     // cil offset

            public OTCilInstr(int offset)
            {
                this.offset = offset;
            }

            public abstract string DumpString();
            public abstract void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link);

            protected void CheckEmptyStack(OTDecompile decompile, string opMnemonic)
            {
                if(decompile.opstack.Count > 0)
                {
                    Console.Error.WriteLine("CheckEmptyStack: " + decompile.method.Name + " 0x" + offset.ToString("X") + ": " +
                            opMnemonic + " stack depth " + decompile.opstack.Count);
                }
            }
        }

        /*
         * Label mark point.
         */
        private class OTLabel: OTCilInstr
        {
            public int number;
            public string name;

            public int lbljumps;

            public OTLabel(int number, string name) : base(-1)
            {
                this.number = number;
                this.name = name;
            }

            public string PrintableName
            {
                get
                {
                    if(name.StartsWith(_doBreak))
                        return _doBreak + "$" + number;
                    if(name.StartsWith(_doCont))
                        return _doCont + "$" + number;
                    if(name.StartsWith(_forBreak))
                        return _forBreak + "$" + number;
                    if(name.StartsWith(_forCont))
                        return _forCont + "$" + number;
                    if(name.StartsWith(_whileBreak))
                        return _whileBreak + "$" + number;
                    if(name.StartsWith(_whileCont))
                        return _whileCont + "$" + number;
                    return name;
                }
            }

            public override string DumpString()
            {
                return name + ":";
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                OTStmtLabel.AddLast(decompile, this);
            }
        }

        /*
         * 'try {'
         */
        private class OTCilBegExcBlk: OTCilInstr
        {
            public LinkedList<OTCilBegCatBlk> catches = new LinkedList<OTCilBegCatBlk>();

            public OTCilBegExcBlk(int offset) : base(offset)
            {
            }

            public override string DumpString()
            {
                return "try {";
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                CheckEmptyStack(decompile, "try");

                // link the try itself onto outer block
                OTStmtBegExcBlk trystmt = new OTStmtBegExcBlk();
                decompile.AddLastStmt(trystmt);

                // subsequent statements go to the try block
                trystmt.tryblock = new OTStmtBlock();
                decompile.trystack.Push(trystmt);
                decompile.blockstack.Push(trystmt.tryblock);
            }
        }

        /*
         * '} catch (...) {'
         */
        private class OTCilBegCatBlk: OTCilInstr
        {
            public Type excType;

            public OTCilBegCatBlk(int offset, Type excType) : base(offset)
            {
                this.excType = excType;
            }

            public override string DumpString()
            {
                return "} catch (" + AbbrType(excType) + ") {";
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                CheckEmptyStack(decompile, "catch");

                // link the catch itself onto the try statement
                OTStmtBegExcBlk trystmt = decompile.trystack.Peek();
                OTStmtBegCatBlk catstmt = new OTStmtBegCatBlk(excType);
                trystmt.catches.AddLast(catstmt);

                // start capturing statements into the catch block
                catstmt.tryblock = trystmt;
                catstmt.catchblock = new OTStmtBlock();
                decompile.blockstack.Pop();
                decompile.blockstack.Push(catstmt.catchblock);

                // fill the stack slot with something for the exception argument
                OTOpndDup dup = new OTOpndDup(++decompile.dupNo);
                decompile.opstack.Push(dup);
            }
        }

        /*
         * '} finally {'
         */
        private class OTCilBegFinBlk: OTCilInstr
        {
            public OTCilBegFinBlk(int offset) : base(offset)
            {
            }

            public override string DumpString()
            {
                return "} finally {";
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                CheckEmptyStack(decompile, "finally");

                // link the finally itself to the try statement
                OTStmtBegExcBlk trystmt = decompile.trystack.Peek();
                OTStmtBegFinBlk finstmt = new OTStmtBegFinBlk();
                trystmt.finblock = finstmt;

                // start capturing statements into the finally block
                finstmt.tryblock = trystmt;
                finstmt.finblock = new OTStmtBlock();
                decompile.blockstack.Pop();
                decompile.blockstack.Push(finstmt.finblock);
            }
        }

        /*
         * '}' end of try
         */
        private class OTCilEndExcBlk: OTCilInstr
        {
            public OTCilEndExcBlk(int offset) : base(offset)
            {
            }

            public override string DumpString()
            {
                return "} // end try";
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                CheckEmptyStack(decompile, "endtry");

                // pop the try/catch/finally blocks from stacks
                decompile.blockstack.Pop();
                decompile.trystack.Pop();

                // subsequent statements collect following the try
            }
        }

        /*
         * Actual opcodes (instructions).
         */
        private class OTCilNull: OTCilInstr
        {
            public MyOp opCode;

            public OTCilNull(int offset, OpCode opCode) : base(offset)
            {
                this.opCode = MyOp.GetByName(opCode.Name);
            }

            public override string DumpString()
            {
                return opCode.ToString();
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                switch(opCode.ToString())
                {
                    case "conv.i1":
                    case "conv.i2":
                    case "conv.i4":
                    case "conv.i8":
                        {
                            OTOpnd value = decompile.opstack.Pop();
                            decompile.opstack.Push(new OTOpndCast(typeof(int), value));
                            break;
                        }
                    case "conv.r4":
                    case "conv.r8":
                        {
                            OTOpnd value = decompile.opstack.Pop();
                            decompile.opstack.Push(new OTOpndCast(typeof(double), value));
                            break;
                        }
                    case "dup":
                        {
                            OTOpnd value = decompile.opstack.Pop();
                            if(!(value is OTOpndDup))
                            {
                                OTOpndDup dup = new OTOpndDup(++decompile.dupNo);
                                OTStmtStore.AddLast(decompile, dup, value);
                                value = dup;
                            }
                            decompile.opstack.Push(value);
                            decompile.opstack.Push(value);
                            break;
                        }
                    case "endfinally":
                        break;
                    case "ldarg.0":
                        {
                            decompile.opstack.Push(new OTOpndArg(0, false, decompile));
                            break;
                        }
                    case "ldarg.1":
                        {
                            decompile.opstack.Push(new OTOpndArg(1, false, decompile));
                            break;
                        }
                    case "ldarg.2":
                        {
                            decompile.opstack.Push(new OTOpndArg(2, false, decompile));
                            break;
                        }
                    case "ldarg.3":
                        {
                            decompile.opstack.Push(new OTOpndArg(3, false, decompile));
                            break;
                        }
                    case "ldc.i4.0":
                        {
                            decompile.opstack.Push(new OTOpndInt(0));
                            break;
                        }
                    case "ldc.i4.1":
                        {
                            decompile.opstack.Push(new OTOpndInt(1));
                            break;
                        }
                    case "ldc.i4.2":
                        {
                            decompile.opstack.Push(new OTOpndInt(2));
                            break;
                        }
                    case "ldc.i4.3":
                        {
                            decompile.opstack.Push(new OTOpndInt(3));
                            break;
                        }
                    case "ldc.i4.4":
                        {
                            decompile.opstack.Push(new OTOpndInt(4));
                            break;
                        }
                    case "ldc.i4.5":
                        {
                            decompile.opstack.Push(new OTOpndInt(5));
                            break;
                        }
                    case "ldc.i4.6":
                        {
                            decompile.opstack.Push(new OTOpndInt(6));
                            break;
                        }
                    case "ldc.i4.7":
                        {
                            decompile.opstack.Push(new OTOpndInt(7));
                            break;
                        }
                    case "ldc.i4.8":
                        {
                            decompile.opstack.Push(new OTOpndInt(8));
                            break;
                        }
                    case "ldc.i4.m1":
                        {
                            decompile.opstack.Push(new OTOpndInt(-1));
                            break;
                        }
                    case "ldelem.i4":
                    case "ldelem.r4":
                    case "ldelem.r8":
                    case "ldelem.ref":
                        {
                            OTOpnd index = decompile.opstack.Pop();
                            OTOpnd array = decompile.opstack.Pop();
                            decompile.opstack.Push(OTOpndArrayElem.Make(array, index, false, decompile));
                            break;
                        }
                    case "ldnull":
                        {
                            decompile.opstack.Push(new OTOpndNull());
                            break;
                        }
                    case "neg":
                    case "not":
                        {
                            OTOpnd value = decompile.opstack.Pop();
                            decompile.opstack.Push(OTOpndUnOp.Make(opCode, value));
                            break;
                        }
                    case "pop":
                        {
                            OTStmtVoid.AddLast(decompile, decompile.opstack.Pop());
                            break;
                        }
                    case "ret":
                        {
                            OTOpnd value = null;
                            if(decompile.method.ReturnType != typeof(void))
                            {
                                value = decompile.opstack.Pop();
                            }
                            CheckEmptyStack(decompile);
                            decompile.AddLastStmt(new OTStmtRet(value));
                            break;
                        }
                    case "stelem.i4":
                    case "stelem.r8":
                    case "stelem.ref":
                        {
                            OTOpnd value = decompile.opstack.Pop();
                            OTOpnd index = decompile.opstack.Pop();
                            OTOpnd array = decompile.opstack.Pop();
                            OTStmtStore.AddLast(decompile, OTOpndArrayElem.Make(array, index, false, decompile), value);
                            break;
                        }
                    case "throw":
                        {
                            OTOpnd value = decompile.opstack.Pop();
                            CheckEmptyStack(decompile);
                            decompile.AddLastStmt(new OTStmtThrow(value, decompile));
                            break;
                        }
                    case "add":
                    case "and":
                    case "ceq":
                    case "cgt":
                    case "cgt.un":
                    case "clt":
                    case "clt.un":
                    case "div":
                    case "div.un":
                    case "mul":
                    case "or":
                    case "rem":
                    case "rem.un":
                    case "shl":
                    case "shr":
                    case "shr.un":
                    case "sub":
                    case "xor":
                        {
                            OTOpnd rite = decompile.opstack.Pop();
                            OTOpnd left = decompile.opstack.Pop();
                            decompile.opstack.Push(OTOpndBinOp.Make(left, opCode, rite));
                            break;
                        }
                    default:
                        throw new Exception("unknown opcode " + opCode.ToString());
                }
            }

            protected void CheckEmptyStack(OTDecompile decompile)
            {
                CheckEmptyStack(decompile, opCode.ToString());
            }
        }

        private class OTCilField: OTCilNull
        {
            public FieldInfo field;

            public OTCilField(int offset, OpCode opCode, FieldInfo field) : base(offset, opCode)
            {
                this.field = field;
            }

            public override string DumpString()
            {
                return opCode.ToString() + ' ' + field.Name;
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                switch(opCode.ToString())
                {
                    case "ldfld":
                        {
                            OTOpnd obj = decompile.opstack.Pop();
                            decompile.opstack.Push(OTOpndField.Make(obj, field));
                            break;
                        }
                    case "ldsfld":
                        {
                            decompile.opstack.Push(new OTOpndSField(field));
                            break;
                        }
                    case "stfld":
                        {
                            OTOpnd val = decompile.opstack.Pop();
                            OTOpnd obj = decompile.opstack.Pop();
                            OTStmtStore.AddLast(decompile, OTOpndField.Make(obj, field), val);
                            break;
                        }
                    case "stsfld":
                        {
                            OTOpnd val = decompile.opstack.Pop();
                            OTStmtStore.AddLast(decompile, new OTOpndSField(field), val);
                            break;
                        }
                    default:
                        throw new Exception("unknown opcode " + opCode.ToString());
                }
            }
        }

        private class OTCilLocal: OTCilNull
        {
            public OTLocal local;

            public OTCilLocal(int offset, OpCode opCode, OTLocal local) : base(offset, opCode)
            {
                this.local = local;
            }

            public override string DumpString()
            {
                return opCode.ToString() + ' ' + local.name;
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                switch(opCode.ToString())
                {
                    case "ldloc":
                        {
                            decompile.opstack.Push(new OTOpndLocal(local));
                            break;
                        }
                    case "ldloca":
                        {
                            decompile.opstack.Push(new OTOpndLocalRef(local));
                            break;
                        }
                    case "stloc":
                        {
                            OTOpnd val = decompile.opstack.Pop();
                            OTStmtStore.AddLast(decompile, new OTOpndLocal(local), val);
                            break;
                        }
                    default:
                        throw new Exception("unknown opcode " + opCode.ToString());
                }
            }
        }

        private class OTCilType: OTCilNull
        {
            public Type type;

            public OTCilType(int offset, OpCode opCode, Type type) : base(offset, opCode)
            {
                this.type = type;
            }

            public override string DumpString()
            {
                return opCode.ToString() + ' ' + AbbrType(type);
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                switch(opCode.ToString())
                {
                    case "box":
                        {
                            break;
                        }
                    case "castclass":
                    case "unbox.any":
                        {
                            OTOpnd value = decompile.opstack.Pop();
                            decompile.opstack.Push(new OTOpndCast(type, value));
                            break;
                        }
                    case "ldelem":
                        {
                            OTOpnd index = decompile.opstack.Pop();
                            OTOpnd array = decompile.opstack.Pop();
                            decompile.opstack.Push(OTOpndArrayElem.Make(array, index, false, decompile));
                            break;
                        }
                    case "ldelema":
                        {
                            OTOpnd index = decompile.opstack.Pop();
                            OTOpnd array = decompile.opstack.Pop();
                            decompile.opstack.Push(OTOpndArrayElem.Make(array, index, true, decompile));
                            break;
                        }
                    case "newarr":
                        {
                            OTOpnd index = decompile.opstack.Pop();
                            decompile.opstack.Push(new OTOpndNewarr(type, index));
                            break;
                        }
                    case "stelem":
                        {
                            OTOpnd value = decompile.opstack.Pop();
                            OTOpnd index = decompile.opstack.Pop();
                            OTOpnd array = decompile.opstack.Pop();
                            OTStmtStore.AddLast(decompile, OTOpndArrayElem.Make(array, index, false, decompile), value);
                            break;
                        }
                    default:
                        throw new Exception("unknown opcode " + opCode.ToString());
                }
            }
        }

        private class OTCilLabel: OTCilNull
        {
            public OTLabel label;

            public OTCilLabel(int offset, OpCode opCode, OTLabel label) : base(offset, opCode)
            {
                this.label = label;
            }

            public override string DumpString()
            {
                return opCode.ToString() + ' ' + label.name;
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                switch(opCode.ToString())
                {
                     // We don't handle non-empty stack at branch points.
                     //
                     // So handle this case specially:
                     //
                     //    dup
                     //    ldc.i4.0
                     //    bge.s  llAbstemp  << we are here
                     //    neg
                     //  llAbstemp:
                     //
                     // becomes:
                     //
                     //    call llAbs
                    case "bge.s":
                        {
                            OTOpnd rite = decompile.opstack.Pop();  // alleged zero
                            OTOpnd left = decompile.opstack.Pop();  // alleged dup

                            if((label.name == _llAbstemp) && (decompile.opstack.Count > 0))
                            {
                                LinkedListNode<OTCilInstr> linkneg = link.Next;
                                if((left is OTOpndDup) && (rite is OTOpndInt) &&
                                        (linkneg != null) && (linkneg.Value is OTCilNull) &&
                                        (((OTCilNull)linkneg.Value).opCode == MyOp.Neg))
                                {
                                    OTOpndInt riteint = (OTOpndInt)rite;
                                    LinkedListNode<OTCilInstr> linklbl = linkneg.Next;
                                    if((riteint.value == 0) && (linklbl != null) && (linklbl.Value is OTLabel) &&
                                            (((OTLabel)linklbl.Value) == label))
                                    {
                                        linkneg.List.Remove(linkneg);
                                        linklbl.List.Remove(linklbl);
                                        MethodInfo method = typeof(ScriptBaseClass).GetMethod("llAbs");
                                        OTOpnd[] args = new OTOpnd[] { new OTOpndNull(), decompile.opstack.Pop() };
                                        OTOpndCall.AddLast(decompile, method, args);
                                        break;
                                    }
                                }
                            }

                            CheckEmptyStack(decompile);
                            OTOpnd valu = OTOpndBinOp.Make(left, opCode, rite);
                            OTStmt jump = OTStmtJump.Make(label);
                            decompile.AddLastStmt(new OTStmtCond(valu, jump));
                            break;
                        }

                    case "beq":
                    case "bge":
                    case "bgt":
                    case "ble":
                    case "blt":
                    case "bne.un":
                    case "beq.s":
                    case "bgt.s":
                    case "ble.s":
                    case "blt.s":
                    case "bne.un.s":
                        {
                            OTOpnd rite = decompile.opstack.Pop();
                            OTOpnd left = decompile.opstack.Pop();
                            CheckEmptyStack(decompile);
                            OTOpnd valu = OTOpndBinOp.Make(left, opCode, rite);
                            OTStmt jump = OTStmtJump.Make(label);
                            decompile.AddLastStmt(new OTStmtCond(valu, jump));
                            break;
                        }
                    case "brfalse":
                    case "brfalse.s":
                    case "brtrue":
                    case "brtrue.s":
                        {
                            OTOpnd value = decompile.opstack.Pop();
                            CheckEmptyStack(decompile);
                            OTOpnd valu = OTOpndUnOp.Make(opCode, value);
                            OTStmt jump = OTStmtJump.Make(label);
                            decompile.AddLastStmt(new OTStmtCond(valu, jump));
                            break;
                        }
                    case "br":
                    case "br.s":
                    case "leave":
                        {
                            CheckEmptyStack(decompile);
                            OTStmt jump = OTStmtJump.Make(label);
                            decompile.AddLastStmt(jump);
                            break;
                        }
                    default:
                        throw new Exception("unknown opcode " + opCode.ToString());
                }
            }
        }

        private class OTCilLabels: OTCilNull
        {
            public OTLabel[] labels;

            public OTCilLabels(int offset, OpCode opCode, OTLabel[] labels) : base(offset, opCode)
            {
                this.labels = labels;
            }

            public override string DumpString()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(opCode.ToString());
                foreach(OTLabel label in labels)
                {
                    sb.Append(' ');
                    sb.Append(label.name);
                }
                return sb.ToString();
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                switch(opCode.ToString())
                {
                    case "switch":
                        {
                            OTOpnd value = decompile.opstack.Pop();
                            CheckEmptyStack(decompile);
                            decompile.AddLastStmt(new OTStmtSwitch(value, labels));
                            break;
                        }
                    default:
                        throw new Exception("unknown opcode " + opCode.ToString());
                }
            }
        }

        private class OTCilMethod: OTCilNull
        {
            public MethodInfo method;

            public OTCilMethod(int offset, OpCode opCode, MethodInfo method) : base(offset, opCode)
            {
                this.method = method;
            }

            public override string DumpString()
            {
                return opCode.ToString() + ' ' + method.Name;
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                switch(opCode.ToString())
                {
                    case "call":
                    case "callvirt":
                        {
                            int nargs = method.GetParameters().Length;
                            if(!method.IsStatic)
                                nargs++;
                            OTOpnd[] args = new OTOpnd[nargs];
                            for(int i = nargs; --i >= 0;)
                            {
                                args[i] = decompile.opstack.Pop();
                            }
                            OTOpndCall.AddLast(decompile, method, args);
                            break;
                        }
                    default:
                        throw new Exception("unknown opcode " + opCode.ToString());
                }
            }
        }

        private class OTCilCtor: OTCilNull
        {
            public ConstructorInfo ctor;

            public OTCilCtor(int offset, OpCode opCode, ConstructorInfo ctor) : base(offset, opCode)
            {
                this.ctor = ctor;
            }

            public override string DumpString()
            {
                return opCode.ToString() + ' ' + AbbrType(ctor.DeclaringType);
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                switch(opCode.ToString())
                {
                    case "newobj":
                        {
                            int nargs = ctor.GetParameters().Length;
                            OTOpnd[] args = new OTOpnd[nargs];
                            for(int i = nargs; --i >= 0;)
                            {
                                args[i] = decompile.opstack.Pop();
                            }
                            decompile.opstack.Push(OTOpndNewobj.Make(ctor, args));
                            break;
                        }
                    default:
                        throw new Exception("unknown opcode " + opCode.ToString());
                }
            }
        }

        private class OTCilDouble: OTCilNull
        {
            public double value;

            public OTCilDouble(int offset, OpCode opCode, double value) : base(offset, opCode)
            {
                this.value = value;
            }

            public override string DumpString()
            {
                return opCode.ToString() + ' ' + value;
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                switch(opCode.ToString())
                {
                    case "ldc.r8":
                        {
                            decompile.opstack.Push(new OTOpndDouble(value));
                            break;
                        }
                    default:
                        throw new Exception("unknown opcode " + opCode.ToString());
                }
            }
        }

        private class OTCilFloat: OTCilNull
        {
            public float value;

            public OTCilFloat(int offset, OpCode opCode, float value) : base(offset, opCode)
            {
                this.value = value;
            }

            public override string DumpString()
            {
                return opCode.ToString() + ' ' + value;
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                switch(opCode.ToString())
                {
                    case "ldc.r4":
                        {
                            decompile.opstack.Push(new OTOpndFloat(value));
                            break;
                        }
                    default:
                        throw new Exception("unknown opcode " + opCode.ToString());
                }
            }
        }

        private class OTCilInteger: OTCilNull
        {
            public int value;

            public OTCilInteger(int offset, OpCode opCode, int value) : base(offset, opCode)
            {
                this.value = value;
            }

            public override string DumpString()
            {
                return opCode.ToString() + ' ' + value;
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                switch(opCode.ToString())
                {
                    case "ldarg":
                    case "ldarg.s":
                        {
                            decompile.opstack.Push(new OTOpndArg(value, false, decompile));
                            break;
                        }
                    case "ldarga":
                    case "ldarga.s":
                        {
                            decompile.opstack.Push(new OTOpndArg(value, true, decompile));
                            break;
                        }
                    case "ldc.i4":
                    case "ldc.i4.s":
                        {
                            decompile.opstack.Push(new OTOpndInt(value));
                            break;
                        }
                    case "starg":
                        {
                            OTOpnd val = decompile.opstack.Pop();
                            OTStmtStore.AddLast(decompile, new OTOpndArg(value, false, decompile), val);
                            break;
                        }
                    default:
                        throw new Exception("unknown opcode " + opCode.ToString());
                }
            }
        }

        private class OTCilString: OTCilNull
        {
            public string value;

            public OTCilString(int offset, OpCode opCode, string value) : base(offset, opCode)
            {
                this.value = value;
            }

            public override string DumpString()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(opCode.ToString());
                sb.Append(' ');
                TokenDeclInline.PrintParamString(sb, value);
                return sb.ToString();
            }

            public override void BuildStatements(OTDecompile decompile, LinkedListNode<OTCilInstr> link)
            {
                switch(opCode.ToString())
                {
                    case "ldstr":
                        {
                            decompile.opstack.Push(new OTOpndString(value));
                            break;
                        }
                    default:
                        throw new Exception("unknown opcode " + opCode.ToString());
                }
            }
        }

        /***************************************\
         *  Tokens what are on operand stack.  *
        \***************************************/

        public abstract class OTOpnd
        {

            /**
             * See if it possibly has any side effects.
             */
            public abstract bool HasSideEffects
            {
                get;
            }

            /**
             * Increment reference counts.
             */
            public virtual void CountRefs(bool writing)
            {
            }

            /**
             * If this operand is a 'by reference' operand,
             * return the corresponding 'by value' operand.
             */
            public virtual OTOpnd GetNonByRefOpnd()
            {
                return this;
            }

            /**
             * If this operand is same as oldopnd, replace it with newopnd.
             *
             * This default just does a shallow search which is ok if this operand does not have any sub-operands.
             * But it must be overridden for a deep search if this operand has any sub-operands.
             */
            public virtual OTOpnd ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd, ref bool rc)
            {
                if(SameAs(oldopnd))
                {
                    rc = true;
                    return newopnd;
                }
                return this;
            }

            /**
             * See if the two operands are the same value.
             * Note that calls might have side-effects so are never the same.
             */
            public abstract bool SameAs(OTOpnd other);

            /**
             * Get a printable string representation of the operand.
             */
            public abstract string PrintableString
            {
                get;
            }
        }

        /**
         * Argument variable.
         */
        private class OTOpndArg: OTOpnd
        {
            public int index;
            public bool byref;

            private OTDecompile decompile;

            public OTOpndArg(int index, bool byref, OTDecompile decompile)
            {
                this.index = index;
                this.byref = byref;
                this.decompile = decompile;
            }

            public override bool HasSideEffects
            {
                get
                {
                    return false;
                }
            }

            public override OTOpnd GetNonByRefOpnd()
            {
                if(!byref)
                    return this;
                return new OTOpndArg(index, false, decompile);
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndArg))
                    return false;
                return (((OTOpndArg)other).byref == byref) && (((OTOpndArg)other).index == index);
            }

            public override string PrintableString
            {
                get
                {
                    string argname = decompile.MethArgName(index);
                    return byref ? ("ref " + argname) : argname;
                }
            }
        }

        /**
         * Element of an array.
         */
        private class OTOpndArrayElem: OTOpnd
        {
            public bool byref;
            public OTOpnd array;
            public OTOpnd index;

            public static OTOpnd Make(OTOpnd array, OTOpnd index, bool byref, OTDecompile decompile)
            {
                 // arg$0.glblVars.iar<type>[<intconst>] is a reference to a global variable
                 // likewise so is __xmrinst.glblVars.iar<type>[<intconst>]
                if((array is OTOpndField) && (index is OTOpndInt))
                {
                     // arrayfield = (arg$0.glblVars).iar<type>
                     // arrayfieldobj = arg$0.glblVars
                     // iartypename = iar<type>
                    OTOpndField arrayfield = (OTOpndField)array;
                    OTOpnd arrayfieldobj = arrayfield.obj;
                    string iartypename = arrayfield.field.Name;

                     // See if they are what they are supposed to be.
                    if((arrayfieldobj is OTOpndField) && iartypename.StartsWith("iar"))
                    {
                         // arrayfieldobjfield = arg$0.glblVars
                        OTOpndField arrayfieldobjfield = (OTOpndField)arrayfieldobj;

                         // See if the parts are what they are supposed to be.
                        if(IsArg0OrXMRInst(arrayfieldobjfield.obj) && (arrayfieldobjfield.field.Name == "glblVars"))
                        {
                             // Everything matches up, make a global variable instead of an array reference.
                            return new OTOpndGlobal(iartypename, ((OTOpndInt)index).value, byref, decompile.scriptObjCode);
                        }
                    }
                }

                 // Other array reference.
                OTOpndArrayElem it = new OTOpndArrayElem();
                it.array = array;
                it.index = index;
                it.byref = byref;
                return it;
            }

            private OTOpndArrayElem()
            {
            }

            public override bool HasSideEffects
            {
                get
                {
                    return array.HasSideEffects || index.HasSideEffects;
                }
            }

            public override void CountRefs(bool writing)
            {
                array.CountRefs(false);
                index.CountRefs(false);
            }

            public override OTOpnd GetNonByRefOpnd()
            {
                if(!byref)
                    return this;
                OTOpndArrayElem it = new OTOpndArrayElem();
                it.array = array;
                it.index = index;
                return it;
            }

            public override OTOpnd ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd, ref bool rc)
            {
                if(SameAs(oldopnd))
                {
                    rc = true;
                    return newopnd;
                }
                array = array.ReplaceOperand(oldopnd, newopnd, ref rc);
                index = index.ReplaceOperand(oldopnd, newopnd, ref rc);
                return this;
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndArrayElem))
                    return false;
                OTOpndArrayElem otherae = (OTOpndArrayElem)other;
                return array.SameAs(otherae.array) && index.SameAs(otherae.index);
            }

            public override string PrintableString
            {
                get
                {
                    return (byref ? "ref " : "") + array.PrintableString + "[" + index.PrintableString + "]";
                }
            }

            /**
             * See if the argument is a reference to arg$0 or __xmrinst
             */
            public static bool IsArg0OrXMRInst(OTOpnd obj)
            {
                if(obj is OTOpndArg)
                {
                    OTOpndArg objarg = (OTOpndArg)obj;
                    return objarg.index == 0;
                }
                if(obj is OTOpndLocal)
                {
                    OTOpndLocal objlcl = (OTOpndLocal)obj;
                    return objlcl.local.name.StartsWith(_xmrinstlocal);
                }
                return false;
            }
        }

        /**
         * Binary operator.
         */
        private class OTOpndBinOp: OTOpnd
        {
            public OTOpnd left;
            public MyOp opCode;
            public OTOpnd rite;

            private static Dictionary<string, string> xor1ops = InitXor1Ops();

            private static Dictionary<string, string> InitXor1Ops()
            {
                Dictionary<string, string> d = new Dictionary<string, string>();
                d["ceq"] = "cne";
                d["cge"] = "clt";
                d["cgt"] = "cle";
                d["cle"] = "cgt";
                d["clt"] = "cge";
                d["cne"] = "ceq";
                return d;
            }

            public static OTOpnd Make(OTOpnd left, MyOp opCode, OTOpnd rite)
            {
                // ((x clt y) xor 1)  =>  (x cge y)  etc
                string xor1op;
                if((left is OTOpndBinOp) && xor1ops.TryGetValue(((OTOpndBinOp)left).opCode.name, out xor1op) &&
                    (opCode == MyOp.Xor) &&
                    (rite is OTOpndInt) && (((OTOpndInt)rite).value == 1))
                {
                    opCode = MyOp.GetByName(xor1op);
                }

                // handle strcmp() cases (see OTOpndStrCmp)
                if(left is OTOpndStrCmp)
                {
                    OTOpnd strcmp = ((OTOpndStrCmp)left).MakeBinOp(opCode, rite);
                    if(strcmp != null)
                        return strcmp;
                }

                // nothing special, make as is
                OTOpndBinOp it = new OTOpndBinOp();
                it.left = left;
                it.opCode = opCode;
                it.rite = rite;
                return it;
            }

            private OTOpndBinOp()
            {
            }

            public override bool HasSideEffects
            {
                get
                {
                    return left.HasSideEffects || rite.HasSideEffects;
                }
            }

            public override void CountRefs(bool writing)
            {
                left.CountRefs(false);
                rite.CountRefs(false);
            }

            public override OTOpnd ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd, ref bool rc)
            {
                if(SameAs(oldopnd))
                {
                    rc = true;
                    return newopnd;
                }
                left = left.ReplaceOperand(oldopnd, newopnd, ref rc);
                rite = rite.ReplaceOperand(oldopnd, newopnd, ref rc);
                return this;
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndBinOp))
                    return false;
                OTOpndBinOp otherbo = (OTOpndBinOp)other;
                return left.SameAs(otherbo.left) && (opCode.ToString() == otherbo.opCode.ToString()) && rite.SameAs(otherbo.rite);
            }

            public override string PrintableString
            {
                get
                {
                    StringBuilder sb = new StringBuilder();

                    bool leftneedsparen = ItNeedsParentheses(left, true);
                    if(leftneedsparen)
                        sb.Append('(');
                    sb.Append(left.PrintableString);
                    if(leftneedsparen)
                        sb.Append(')');

                    sb.Append(' ');
                    sb.Append(opCode.source);
                    sb.Append(' ');

                    bool riteneedsparen = ItNeedsParentheses(rite, false);
                    if(riteneedsparen)
                        sb.Append('(');
                    sb.Append(rite.PrintableString);
                    if(riteneedsparen)
                        sb.Append(')');

                    return sb.ToString();
                }
            }

            /**
             * See if source code representation requires parentheses around the given operand.
             * @param it = the other operand to decide about
             * @param itleft = true: 'it' is on the left of this operand   (A $ B) # C
             *                false: 'it' is on the right of this operand  A $ (B # C)
             */
            private bool ItNeedsParentheses(OTOpnd it, bool itleft)
            {
                if(!(it is OTOpndBinOp))
                    return false;
                string itop = ((OTOpndBinOp)it).opCode.source;
                string myop = opCode.source;

                // find them in table.  higher number is for *, lower is for +.
                int itpi, mypi;
                if(!precedence.TryGetValue(itop, out itpi))
                    return true;
                if(!precedence.TryGetValue(myop, out mypi))
                    return true;
                int itpiabs = Math.Abs(itpi);
                int mypiabs = Math.Abs(mypi);

                // if its precedence is lower (eg +) than my precedence (eg *), it needs parentheses
                if(itpiabs < mypiabs)
                    return true;

                // if its precedence is higher (eg *) than my precedence (eg +), it doesn't needs parentheses
                if(itpiabs > mypiabs)
                    return false;

                // if (A $ B) # C, we can safely go without the parentheses
                if(itleft)
                    return false;

                //  my   it
                // A $ (B # C) only works without parentheses for commutative $
                // A - (B + C) and A - (B - C) require parentheses
                // A + (B - C) does not
                return mypi < 0;  // neg: things like -, /, etc require parentheses
                                  // pos: things like +, *, etc do not need parens
            }

            // see MMRScriptReduce.PrecedenceInit()
            private static Dictionary<string, int> precedence = InitPrecedence();
            private static Dictionary<string, int> InitPrecedence()
            {
                Dictionary<string, int> d = new Dictionary<string, int>();
                d["|"] = 140;
                d["^"] = 160;
                d["&"] = 180;
                d["<<"] = -260;
                d[">>"] = -260;
                d["+"] = 280;
                d["-"] = -280;
                d["*"] = 320;
                d["/"] = -320;
                d["%"] = -320;
                return d;
            }
        }

        /**
         * Call with or without return value.
         */
        private class OTOpndCall: OTOpnd
        {
            private static Dictionary<string, MethodInfo> mathmeths = InitMathMeths();
            private static Dictionary<string, MethodInfo> InitMathMeths()
            {
                Dictionary<string, MethodInfo> d = new Dictionary<string, MethodInfo>();
                d["Acos"] = typeof(ScriptBaseClass).GetMethod("llAcos");
                d["Asin"] = typeof(ScriptBaseClass).GetMethod("llAsin");
                d["Atan"] = typeof(ScriptBaseClass).GetMethod("llAtan");
                d["Cos"] = typeof(ScriptBaseClass).GetMethod("llCos");
                d["Abs"] = typeof(ScriptBaseClass).GetMethod("llFabs");
                d["Log"] = typeof(ScriptBaseClass).GetMethod("llLog");
                d["Log10"] = typeof(ScriptBaseClass).GetMethod("llLog10");
                d["Round"] = typeof(ScriptBaseClass).GetMethod("llRound");
                d["Sin"] = typeof(ScriptBaseClass).GetMethod("llSin");
                d["Sqrt"] = typeof(ScriptBaseClass).GetMethod("llSqrt");
                d["Tan"] = typeof(ScriptBaseClass).GetMethod("llTan");
                return d;
            }

            public MethodInfo method;
            public OTOpnd[] args;

            // pushes on stack for return-value functions
            // pushes to end of instruction stream for return-void functions
            public static void AddLast(OTDecompile decompile, MethodInfo method, OTOpnd[] args)
            {
                int nargs = args.Length;

                // heap tracker push is just the single arg value as far as we're concerned
                if((nargs == 1) && (method.Name == _heapTrackerPush) && method.DeclaringType.Name.StartsWith("HeapTracker"))
                {
                    decompile.opstack.Push(args[0]);
                    return;
                }

                // heap tracker pop is just a store as far as we're concerned
                if((nargs == 2) && (method.Name == _heapTrackerPop) && method.DeclaringType.Name.StartsWith("HeapTracker"))
                {
                    OTStmtStore.AddLast(decompile, args[0], args[1]);
                    return;
                }

                // string.Compare() is its own thing cuz it has to decompile many ways
                if((nargs == 2) && (method.DeclaringType == typeof(string)) && (method.Name == "Compare"))
                {
                    decompile.opstack.Push(new OTOpndStrCmp(args[0], args[1]));
                    return;
                }

                // ObjectToString, etc, should appear as casts
                if((nargs == 1) && (method.DeclaringType == typeof(TypeCast)) && method.Name.EndsWith("ToBool"))
                {
                    MethodInfo meth = typeof(XMRInstAbstract).GetMethod("xmr" + method.Name);
                    AddLast(decompile, meth, new OTOpnd[] { new OTOpndNull(), args[0] });
                    return;
                }
                if((nargs == 1) && (method.DeclaringType == typeof(TypeCast)) && method.Name.EndsWith("ToFloat"))
                {
                    decompile.opstack.Push(new OTOpndCast(typeof(double), args[0]));
                    return;
                }
                if((nargs == 1) && (method.DeclaringType == typeof(TypeCast)) && method.Name.EndsWith("ToInteger"))
                {
                    decompile.opstack.Push(new OTOpndCast(typeof(int), args[0]));
                    return;
                }
                if((nargs == 1) && (method.DeclaringType == typeof(TypeCast)) && method.Name.EndsWith("ToList"))
                {
                    decompile.opstack.Push(new OTOpndCast(typeof(LSL_List), args[0]));
                    return;
                }
                if((nargs == 1) && (method.DeclaringType == typeof(TypeCast)) && method.Name.EndsWith("ToRotation"))
                {
                    decompile.opstack.Push(new OTOpndCast(typeof(LSL_Rotation), args[0]));
                    return;
                }
                if((nargs == 1) && (method.DeclaringType == typeof(TypeCast)) && method.Name.EndsWith("ToString"))
                {
                    decompile.opstack.Push(new OTOpndCast(typeof(string), args[0]));
                    return;
                }
                if((nargs == 1) && (method.DeclaringType == typeof(TypeCast)) && method.Name.EndsWith("ToVector"))
                {
                    decompile.opstack.Push(new OTOpndCast(typeof(LSL_Vector), args[0]));
                    return;
                }

                if((method.DeclaringType == typeof(XMRInstAbstract)) && (method.Name == "xmrHeapLeft"))
                {
                    AddLast(decompile, typeof(ScriptBaseClass).GetMethod("llGetFreeMemory"), new OTOpnd[] { new OTOpndNull() });
                    return;
                }

                // pop to entry in the list/object/string array
                if(PopToGlobalArray(decompile, method, args))
                    return;

                // strip off event handler argument unwrapper calls
                if((nargs == 1) && (method.DeclaringType == typeof(TypeCast)) && method.Name.StartsWith("EHArgUnwrap"))
                {
                    decompile.opstack.Push(args[0]);
                    return;
                }

                // translate Math method to ll method
                MethodInfo mathmeth;
                if((method.DeclaringType == typeof(Math)) && mathmeths.TryGetValue(method.Name, out mathmeth))
                {
                    AddLast(decompile, mathmeth, new OTOpnd[] { new OTOpndNull(), args[0] });
                    return;
                }
                if((method.DeclaringType == typeof(Math)) && (method.Name == "Atan2"))
                {
                    AddLast(decompile, typeof(ScriptBaseClass).GetMethod("llAtan2"), new OTOpnd[] { new OTOpndNull(), args[0], args[1] });
                    return;
                }
                if((method.DeclaringType == typeof(Math)) && (method.Name == "Pow"))
                {
                    AddLast(decompile, typeof(ScriptBaseClass).GetMethod("llPow"), new OTOpnd[] { new OTOpndNull(), args[0], args[1] });
                    return;
                }

                // string concat should be a bunch of adds
                if((method.Name == "Concat") && (method.DeclaringType == typeof(string)))
                {
                    int k = args.Length;
                    while(k > 1)
                    {
                        int j = 0;
                        int i;
                        for(i = 0; i + 2 <= k; i += 2)
                        {
                            args[j++] = OTOpndBinOp.Make(args[i + 0], MyOp.Add, args[i + 1]);
                        }
                        while(i < k)
                            args[j++] = args[i++];
                        k = j;
                    }
                    if(k > 0)
                        decompile.opstack.Push(args[0]);
                    return;
                }

                // bunch of calls for rotation and vector arithmetic
                if((method.DeclaringType == typeof(BinOpStr)) && BinOpStrCall(decompile, method, args))
                    return;
                if((method.DeclaringType == typeof(ScriptCodeGen)) && (method.Name == "LSLRotationNegate"))
                {
                    decompile.opstack.Push(OTOpndUnOp.Make(MyOp.Neg, args[0]));
                    return;
                }
                if((method.DeclaringType == typeof(ScriptCodeGen)) && (method.Name == "LSLVectorNegate"))
                {
                    decompile.opstack.Push(OTOpndUnOp.Make(MyOp.Neg, args[0]));
                    return;
                }

                // otherwise process it as a call
                OTOpndCall call = new OTOpndCall();
                call.method = method;
                call.args = args;
                if(method.ReturnType == typeof(void))
                {
                    OTStmtVoid.AddLast(decompile, call);
                }
                else
                {
                    decompile.opstack.Push(call);
                }
            }

            public override bool HasSideEffects
            {
                get
                {
                    return true;
                }
            }

            /**
             * Handle a call to XMRInstArrays.Pop<List,Object,String>
             * by converting it to a store directly into the array.
             */
            private static bool PopToGlobalArray(OTDecompile decompile, MethodInfo method, OTOpnd[] args)
            {
                if(method.DeclaringType != typeof(XMRInstArrays))
                    return false;
                if(args.Length != 3)
                    return false;

                string array = null;
                if(method.Name == "PopList")
                    array = "iarLists";
                if(method.Name == "PopObject")
                    array = "iarObjects";
                if(method.Name == "PopString")
                    array = "iarStrings";
                if(array == null)
                    return false;

                // make token that points to the iar<whatever> array
                FieldInfo field = typeof(XMRInstArrays).GetField(array);
                OTOpnd arrayfield = OTOpndField.Make(args[0], field);

                // make token that points to the element to be popped to
                OTOpnd element = OTOpndArrayElem.Make(arrayfield, args[1], false, decompile);

                // make a statement to store value in that element
                OTStmtStore.AddLast(decompile, element, args[2]);

                return true;
            }

            /**
             * BinOpStr has a bunch of calls to do funky arithmetic.
             * Instead of generating a call, put back the original source.
             */
            private static bool BinOpStrCall(OTDecompile decompile, MethodInfo method, OTOpnd[] args)
            {
                switch(method.Name)
                {
                    case "MethFloatAddList":
                    case "MethIntAddList":
                    case "MethKeyAddList":
                    case "MethListAddFloat":
                    case "MethListAddInt":
                    case "MethListAddKey":
                    case "MethListAddList":
                    case "MethListAddObj":
                    case "MethListAddRot":
                    case "MethListAddStr":
                    case "MethListAddVec":
                    case "MethObjAddList":
                    case "MethRotAddList":
                    case "MethRotAddRot":
                    case "MethStrAddList":
                    case "MethVecAddList":
                    case "MethVecAddVec":
                        {
                            decompile.opstack.Push(OTOpndBinOp.Make(args[0], MyOp.Add, args[1]));
                            return true;
                        }

                    case "MethListEqList":
                    case "MethRotEqRot":
                    case "MethVecEqVec":
                        {
                            decompile.opstack.Push(OTOpndBinOp.Make(args[0], MyOp.Ceq, args[1]));
                            return true;
                        }

                    case "MethListNeList":
                    case "MethRotNeRot":
                    case "MethVecNeVec":
                        {
                            decompile.opstack.Push(OTOpndBinOp.Make(args[0], MyOp.Cne, args[1]));
                            return true;
                        }

                    case "MethRotSubRot":
                    case "MethVecSubVec":
                        {
                            decompile.opstack.Push(OTOpndBinOp.Make(args[0], MyOp.Sub, args[1]));
                            return true;
                        }

                    case "MethFloatMulVec":
                    case "MethIntMulVec":
                    case "MethRotMulRot":
                    case "MethVecMulFloat":
                    case "MethVecMulInt":
                    case "MethVecMulRot":
                    case "MethVecMulVec":
                        {
                            decompile.opstack.Push(OTOpndBinOp.Make(args[0], MyOp.Mul, args[1]));
                            return true;
                        }

                    case "MethRotDivRot":
                    case "MethVecDivFloat":
                    case "MethVecDivInt":
                    case "MethVecDivRot":
                        {
                            decompile.opstack.Push(OTOpndBinOp.Make(args[0], MyOp.Div, args[1]));
                            return true;
                        }

                    default:
                        return false;
                }
            }

            private OTOpndCall()
            {
            }

            public override void CountRefs(bool writing)
            {
                foreach(OTOpnd arg in args)
                {
                    arg.CountRefs(false);
                }
            }

            public override OTOpnd ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd, ref bool rc)
            {
                for(int i = 0; i < args.Length; i++)
                {
                    args[i] = args[i].ReplaceOperand(oldopnd, newopnd, ref rc);
                }
                return this;
            }

            public override bool SameAs(OTOpnd other)
            {
                return false;
            }

            public override string PrintableString
            {
                get
                {
                    StringBuilder sb = new StringBuilder();

                    // GetByKey(a,i) => a[i]
                    if((method.DeclaringType == typeof(XMR_Array)) && (method.Name == "GetByKey") && (args.Length == 2))
                    {
                        sb.Append(args[0].PrintableString);
                        sb.Append('[');
                        sb.Append(args[1].PrintableString);
                        sb.Append(']');
                        return sb.ToString();
                    }

                    // SetByKey(a,i,v) => a[i] = v
                    if((method.DeclaringType == typeof(XMR_Array)) && (method.Name == "SetByKey") && (args.Length == 3))
                    {
                        sb.Append(args[0].PrintableString);
                        sb.Append('[');
                        sb.Append(args[1].PrintableString);
                        sb.Append("] = ");
                        sb.Append(args[2].PrintableString);
                        return sb.ToString();
                    }

                    // CompValuListEl.GetElementFromList accesses list elements like an array.
                    if((method.DeclaringType == typeof(CompValuListEl)) && (method.Name == "GetElementFromList"))
                    {
                        sb.Append(args[0].PrintableString);
                        sb.Append('[');
                        sb.Append(args[1].PrintableString);
                        sb.Append(']');
                        return sb.ToString();
                    }

                    // methods that are part of ScriptBaseClass are LSL functions such as llSay()
                    // so we want to skip outputting "arg$0," as it is the hidden "this" argument.
                    // and there are also XMRInstAbstract functions such as xmrEventDequeue().
                    int starti = 0;
                    if((method.DeclaringType == typeof(ScriptBaseClass)) && !method.IsStatic)
                        starti = 1;
                    if((method.DeclaringType == typeof(XMRInstAbstract)) && !method.IsStatic)
                        starti = 1;

                    // likewise, method that have null as the declaring type are script-defined
                    // dynamic methods which have a hidden "this" argument passed as "arg$0".
                    if(method.DeclaringType == null)
                        starti = 1;

                    // all others we want to show the type name (such as Math.Abs, String.Compare, etc)
                    if(starti == 0)
                    {
                        sb.Append(AbbrType(method.DeclaringType));
                        sb.Append('.');
                    }

                    // script-defined functions have the param types as part of their name
                    // so strip them off here so they don't clutter things up
                    int i = method.Name.IndexOf('(');
                    if(i < 0)
                        sb.Append(method.Name);
                    else
                        sb.Append(method.Name.Substring(0, i));

                    // now add the call arguments
                    sb.Append(" (");
                    bool first = true;
                    foreach(OTOpnd arg in args)
                    {
                        if(--starti < 0)
                        {
                            if(!first)
                                sb.Append(", ");
                            sb.Append(arg.PrintableString);
                            first = false;
                        }
                    }
                    sb.Append(')');
                    return sb.ToString();
                }
            }
        }

        /**
         * Cast value to the given type.
         */
        private class OTOpndCast: OTOpnd
        {
            public Type type;
            public OTOpnd value;

            public OTOpndCast(Type type, OTOpnd value)
            {
                this.type = type;
                this.value = value;
            }

            public override bool HasSideEffects
            {
                get
                {
                    return value.HasSideEffects;
                }
            }

            public override void CountRefs(bool writing)
            {
                value.CountRefs(false);
            }

            public override OTOpnd ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd, ref bool rc)
            {
                if(SameAs(oldopnd))
                {
                    rc = true;
                    return newopnd;
                }
                value = value.ReplaceOperand(oldopnd, newopnd, ref rc);
                return this;
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndCast))
                    return false;
                OTOpndCast othercast = (OTOpndCast)other;
                return (type == othercast.type) && value.SameAs(othercast.value);
            }

            public override string PrintableString
            {
                get
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append('(');
                    sb.Append(AbbrType(type));
                    sb.Append(") ");
                    if(value is OTOpndBinOp)
                        sb.Append('(');
                    sb.Append(value.PrintableString);
                    if(value is OTOpndBinOp)
                        sb.Append(')');
                    return sb.ToString();
                }
            }
        }

        /**
         * Duplicate stack value without re-performing computation.
         * Semantics just like local var except it doesn't have a declaration.
         */
        private class OTOpndDup: OTOpnd
        {
            public int index;
            public int ndupreads;

            public OTOpndDup(int index)
            {
                this.index = index;
            }

            public override bool HasSideEffects
            {
                get
                {
                    return false;
                }
            }

            public override void CountRefs(bool writing)
            {
                if(!writing)
                    ndupreads++;
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndDup))
                    return false;
                return ((OTOpndDup)other).index == index;
            }

            public override string PrintableString
            {
                get
                {
                    return "dup$" + index;
                }
            }
        }

        /**
         * Field of an object.
         */
        private class OTOpndField: OTOpnd
        {
            public OTOpnd obj;
            public FieldInfo field;

            public static OTOpnd Make(OTOpnd obj, FieldInfo field)
            {
                //  LSL_Float.value => the object itself
                if((field.DeclaringType == typeof(LSL_Float)) && (field.Name == "value"))
                {
                    return obj;
                }

                //  LSL_Integer.value => the object itself
                if((field.DeclaringType == typeof(LSL_Integer)) && (field.Name == "value"))
                {
                    return obj;
                }

                // LSL_String.m_string => the object itself
                if((field.DeclaringType == typeof(LSL_String)) && (field.Name == "m_string"))
                {
                    return obj;
                }

                // some other field, output code to access it
                // sometimes the object comes as by reference (value types), so we might need to deref it first
                OTOpndField it = new OTOpndField();
                it.obj = obj.GetNonByRefOpnd();
                it.field = field;
                return it;
            }

            private OTOpndField()
            {
            }

            public override bool HasSideEffects
            {
                get
                {
                    return obj.HasSideEffects;
                }
            }

            public override void CountRefs(bool writing)
            {
                // the field may be getting written to, but the object is being read
                obj.CountRefs(false);
            }

            public override OTOpnd ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd, ref bool rc)
            {
                if(SameAs(oldopnd))
                {
                    rc = true;
                    return newopnd;
                }
                obj = obj.ReplaceOperand(oldopnd, newopnd, ref rc);
                return this;
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndField))
                    return false;
                OTOpndField otherfield = (OTOpndField)other;
                return (field.Name == otherfield.field.Name) && obj.SameAs(otherfield.obj);
            }

            public override string PrintableString
            {
                get
                {
                    StringBuilder sb = new StringBuilder();
                    if(obj is OTOpndBinOp)
                        sb.Append('(');
                    sb.Append(obj.PrintableString);
                    if(obj is OTOpndBinOp)
                        sb.Append(')');
                    sb.Append('.');
                    sb.Append(field.Name);
                    return sb.ToString();
                }
            }
        }

        /**
         * Script-level global variable.
         */
        private class OTOpndGlobal: OTOpnd
        {
            public string iartypename;
            public int iararrayidx;
            public bool byref;
            public ScriptObjCode scriptObjCode;

            public OTOpndGlobal(string iartypename, int iararrayidx, bool byref, ScriptObjCode scriptObjCode)
            {
                this.iartypename = iartypename;
                this.iararrayidx = iararrayidx;
                this.byref = byref;
                this.scriptObjCode = scriptObjCode;
            }

            public override bool HasSideEffects
            {
                get
                {
                    return false;
                }
            }

            public override OTOpnd GetNonByRefOpnd()
            {
                if(!byref)
                    return this;
                return new OTOpndGlobal(iartypename, iararrayidx, false, scriptObjCode);
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndGlobal))
                    return false;
                OTOpndGlobal otherglobal = (OTOpndGlobal)other;
                return (iartypename == otherglobal.iartypename) && (iararrayidx == otherglobal.iararrayidx);
            }

            public override string PrintableString
            {
                get
                {
                    return (byref ? "ref " : "") + scriptObjCode.globalVarNames[iartypename][iararrayidx];
                }
            }
        }

        /**
         * List initialization.
         */
        private class OTOpndListIni: OTOpnd
        {
            public OTOpnd[] values;

            /**
             * Try to detect list initialization building idiom:
             *    dup$<n> = newarr object[<m>]      << link points here
             *    dup$<n>[0] = bla
             *    dup$<n>[1] = bla
             *        ...
             *    ... newobj list (dup$<n>) ...
             */
            public static bool Detect(LinkedListNode<OTStmt> link)
            {
                if(link == null)
                    return false;

                /*
                 * Check for 'dup$<n> = newarr object[<m>]' and get listsize from <m>.
                 */
                OTStmtStore store = (OTStmtStore)link.Value;
                if(!(store.varwr is OTOpndDup))
                    return false;
                if(!(store.value is OTOpndNewarr))
                    return false;
                OTOpndDup storevar = (OTOpndDup)store.varwr;
                OTOpndNewarr storeval = (OTOpndNewarr)store.value;
                if(storeval.type != typeof(object))
                    return false;
                if(!(storeval.index is OTOpndInt))
                    return false;
                int listsize = ((OTOpndInt)storeval.index).value;

                 // Good chance of having list initializer, malloc an object to hold it.
                OTOpndListIni it = new OTOpndListIni();
                it.values = new OTOpnd[listsize];

                 // There should be exactly listsize statements following that of the form:
                 //    dup$<n>[<i>] = bla
                 // If so, save the bla values in the values[] array.
                LinkedListNode<OTStmt> vallink = link;
                for(int i = 0; i < listsize; i++)
                {
                    vallink = vallink.Next;
                    if(vallink == null)
                        return false;
                    if(!(vallink.Value is OTStmtStore))
                        return false;
                    OTStmtStore valstore = (OTStmtStore)vallink.Value;
                    if(!(valstore.varwr is OTOpndArrayElem))
                        return false;
                    OTOpndArrayElem varelem = (OTOpndArrayElem)valstore.varwr;
                    if(varelem.array != storevar)
                        return false;
                    if(!(varelem.index is OTOpndInt))
                        return false;
                    if(((OTOpndInt)varelem.index).value != i)
                        return false;
                    it.values[i] = valstore.value;
                }

                 // The next statement should have a 'newobj list (dup$<n>)' in it somewhere
                 // that we want to replace with 'it'.
                ConstructorInfo protoctor = typeof(LSL_List).GetConstructor(new Type[] { typeof(object[]) });
                OTOpnd[] protoargs = new OTOpnd[] { storevar };
                OTOpnd proto = OTOpndNewobj.Make(protoctor, protoargs);

                vallink = vallink.Next;
                bool rc = vallink.Value.ReplaceOperand(proto, it);

                 // If successful, delete 'dup$n =' and all 'dup$n[i] =' statements.
                if(rc)
                {
                    do
                    {
                        LinkedListNode<OTStmt> nextlink = link.Next;
                        link.List.Remove(link);
                        link = nextlink;
                    } while(link != vallink);
                }

                return rc;
            }

            public override bool HasSideEffects
            {
                get
                {
                    foreach(OTOpnd value in values)
                    {
                        if(value.HasSideEffects)
                            return true;
                    }
                    return false;
                }
            }

            public override void CountRefs(bool writing)
            {
                foreach(OTOpnd value in values)
                {
                    value.CountRefs(false);
                }
            }

            public override OTOpnd ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd, ref bool rc)
            {
                if(SameAs(oldopnd))
                {
                    rc = true;
                    return newopnd;
                }
                for(int i = 0; i < values.Length; i++)
                {
                    values[i] = values[i].ReplaceOperand(oldopnd, newopnd, ref rc);
                }
                return this;
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndListIni))
                    return false;
                OTOpndListIni otherli = (OTOpndListIni)other;
                if(otherli.values.Length != values.Length)
                    return false;
                for(int i = 0; i < values.Length; i++)
                {
                    if(!values[i].SameAs(otherli.values[i]))
                        return false;
                }
                return true;
            }

            public override string PrintableString
            {
                get
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append('[');
                    for(int i = 0; i < values.Length; i++)
                    {
                        if(i > 0)
                            sb.Append(',');
                        sb.Append(' ');
                        sb.Append(values[i].PrintableString);
                    }
                    sb.Append(" ]");
                    return sb.ToString();
                }
            }
        }

        /**
         * Local variable.
         */
        private class OTOpndLocal: OTOpnd
        {
            public OTLocal local;

            public OTOpndLocal(OTLocal local)
            {
                this.local = local;
            }

            public override bool HasSideEffects
            {
                get
                {
                    return false;
                }
            }

            public override void CountRefs(bool writing)
            {
                if(writing)
                    local.nlclwrites++;
                else
                    local.nlclreads++;
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndLocal))
                    return false;
                OTOpndLocal otherlocal = (OTOpndLocal)other;
                return local == otherlocal.local;
            }

            public override string PrintableString
            {
                get
                {
                    return local.name;
                }
            }
        }
        private class OTOpndLocalRef: OTOpnd
        {
            public OTLocal local;

            public OTOpndLocalRef(OTLocal local)
            {
                this.local = local;
            }

            public override bool HasSideEffects
            {
                get
                {
                    return true;
                }
            }

            public override void CountRefs(bool writing)
            {
                local.nlclreads++;
                local.nlclwrites++;
            }

            public override OTOpnd GetNonByRefOpnd()
            {
                return new OTOpndLocal(local);
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndLocal))
                    return false;
                OTOpndLocal otherlocal = (OTOpndLocal)other;
                return local == otherlocal.local;
            }

            public override string PrintableString
            {
                get
                {
                    return "ref " + local.name;
                }
            }
        }

        /**
         * New C#-level array.
         */
        private class OTOpndNewarr: OTOpnd
        {
            public Type type;
            public OTOpnd index;

            public OTOpndNewarr(Type type, OTOpnd index)
            {
                this.type = type;
                this.index = index;
            }

            public override bool HasSideEffects
            {
                get
                {
                    return index.HasSideEffects;
                }
            }

            public override void CountRefs(bool writing)
            {
                index.CountRefs(false);
            }

            public override OTOpnd ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd, ref bool rc)
            {
                if(SameAs(oldopnd))
                {
                    rc = true;
                    return newopnd;
                }
                index = index.ReplaceOperand(oldopnd, newopnd, ref rc);
                return this;
            }

            public override bool SameAs(OTOpnd other)
            {
                return false;
            }

            public override string PrintableString
            {
                get
                {
                    return "newarr " + type.Name + "[" + index.PrintableString + "]";
                }
            }
        }

        /**
         * New C#-level object.
         */
        private class OTOpndNewobj: OTOpnd
        {
            public ConstructorInfo ctor;
            public OTOpnd[] args;

            public static OTOpnd Make(ConstructorInfo ctor, OTOpnd[] args)
            {
                // newobj LSL_Float (x)  =>  x
                if((ctor.DeclaringType == typeof(LSL_Float)) && (args.Length == 1))
                {
                    Type ptype = ctor.GetParameters()[0].ParameterType;
                    if(ptype == typeof(string))
                    {
                        return new OTOpndCast(typeof(double), args[0]);
                    }
                    return args[0];
                }

                // newobj LSL_Integer (x)  =>  x
                if((ctor.DeclaringType == typeof(LSL_Integer)) && (args.Length == 1))
                {
                    Type ptype = ctor.GetParameters()[0].ParameterType;
                    if(ptype == typeof(string))
                    {
                        return new OTOpndCast(typeof(int), args[0]);
                    }
                    return args[0];
                }

                // newobj LSL_String (x)  =>  x
                if((ctor.DeclaringType == typeof(LSL_String)) && (args.Length == 1))
                {
                    return args[0];
                }

                // newobj LSL_Rotation (x, y, z, w)  =>  <x, y, z, w>
                if((ctor.DeclaringType == typeof(LSL_Rotation)) && (args.Length == 4))
                {
                    return new OTOpndRot(args[0], args[1], args[2], args[3]);
                }

                // newobj LSL_Vector (x, y, z)  =>  <x, y, z>
                if((ctor.DeclaringType == typeof(LSL_Vector)) && (args.Length == 3))
                {
                    return new OTOpndVec(args[0], args[1], args[2]);
                }

                // newobj LSL_Rotation (string)  => (rotation) string
                if((ctor.DeclaringType == typeof(LSL_Rotation)) && (args.Length == 1))
                {
                    return new OTOpndCast(typeof(LSL_Rotation), args[0]);
                }

                // newobj LSL_Vector (string)  => (rotation) string
                if((ctor.DeclaringType == typeof(LSL_Vector)) && (args.Length == 1))
                {
                    return new OTOpndCast(typeof(LSL_Vector), args[0]);
                }

                // newobj LSL_List (newarr object[0])  =>  [ ]
                if((ctor.DeclaringType == typeof(LSL_List)) && (args.Length == 1) && (args[0] is OTOpndNewarr))
                {
                    OTOpndNewarr arg0 = (OTOpndNewarr)args[0];
                    if((arg0.type == typeof(object)) && (arg0.index is OTOpndInt) && (((OTOpndInt)arg0.index).value == 0))
                    {
                        OTOpndListIni listini = new OTOpndListIni();
                        listini.values = new OTOpnd[0];
                        return listini;
                    }
                }

                // something else, output as is
                OTOpndNewobj it = new OTOpndNewobj();
                it.ctor = ctor;
                it.args = args;
                return it;
            }

            private OTOpndNewobj()
            {
            }

            public override bool HasSideEffects
            {
                get
                {
                    foreach(OTOpnd arg in args)
                    {
                        if(arg.HasSideEffects)
                            return true;
                    }
                    return false;
                }
            }

            public override void CountRefs(bool writing)
            {
                foreach(OTOpnd arg in args)
                {
                    arg.CountRefs(false);
                }
            }

            public override OTOpnd ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd, ref bool rc)
            {
                if(SameAs(oldopnd))
                {
                    rc = true;
                    return newopnd;
                }
                for(int i = 0; i < args.Length; i++)
                {
                    args[i] = args[i].ReplaceOperand(oldopnd, newopnd, ref rc);
                }
                return this;
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndNewobj))
                    return false;
                OTOpndNewobj otherno = (OTOpndNewobj)other;
                if(otherno.ctor.DeclaringType != ctor.DeclaringType)
                    return false;
                if(otherno.args.Length != args.Length)
                    return false;
                for(int i = 0; i < args.Length; i++)
                {
                    if(!args[i].SameAs(otherno.args[i]))
                        return false;
                }
                return true;
            }

            public override string PrintableString
            {
                get
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("newobj ");
                    sb.Append(ctor.DeclaringType.Name);
                    sb.Append(" (");
                    bool first = true;
                    foreach(OTOpnd arg in args)
                    {
                        if(!first)
                            sb.Append(", ");
                        sb.Append(arg.PrintableString);
                        first = false;
                    }
                    sb.Append(')');
                    return sb.ToString();
                }
            }
        }

        /**
         * Rotation value.
         */
        private class OTOpndRot: OTOpnd
        {
            private OTOpnd x, y, z, w;

            public OTOpndRot(OTOpnd x, OTOpnd y, OTOpnd z, OTOpnd w)
            {
                this.x = StripFloatCast(x);
                this.y = StripFloatCast(y);
                this.z = StripFloatCast(z);
                this.w = StripFloatCast(w);
            }

            public override bool HasSideEffects
            {
                get
                {
                    return x.HasSideEffects || y.HasSideEffects || z.HasSideEffects || w.HasSideEffects;
                }
            }

            public override void CountRefs(bool writing)
            {
                x.CountRefs(false);
                y.CountRefs(false);
                z.CountRefs(false);
                w.CountRefs(false);
            }

            public override OTOpnd ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd, ref bool rc)
            {
                if(SameAs(oldopnd))
                {
                    rc = true;
                    return newopnd;
                }
                x = x.ReplaceOperand(oldopnd, newopnd, ref rc);
                y = y.ReplaceOperand(oldopnd, newopnd, ref rc);
                z = z.ReplaceOperand(oldopnd, newopnd, ref rc);
                w = w.ReplaceOperand(oldopnd, newopnd, ref rc);
                return this;
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndRot))
                    return false;
                OTOpndRot otherv = (OTOpndRot)other;
                return otherv.x.SameAs(x) && otherv.y.SameAs(y) && otherv.z.SameAs(z) && otherv.w.SameAs(w);
            }

            public override string PrintableString
            {
                get
                {
                    return "<" + x.PrintableString + ", " + y.PrintableString + ", " + z.PrintableString + ", " + w.PrintableString + ">";
                }
            }
        }

        /**
         * Static field.
         */
        private class OTOpndSField: OTOpnd
        {
            private FieldInfo field;

            public OTOpndSField(FieldInfo field)
            {
                this.field = field;
            }

            public override bool HasSideEffects
            {
                get
                {
                    return false;
                }
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndSField))
                    return false;
                OTOpndSField othersfield = (OTOpndSField)other;
                return (field.Name == othersfield.field.Name) && (field.DeclaringType == othersfield.field.DeclaringType);
            }

            public override string PrintableString
            {
                get
                {
                    if(field.DeclaringType == typeof(ScriptBaseClass))
                        return field.Name;
                    return field.DeclaringType.Name + "." + field.Name;
                }
            }
        }

        /**
         * Call to string.Compare().
         * See use cases in BinOpStr:
         *   strcmp (a, b) ceq  0
         *   (strcmp (a, b) ceq 0) xor 1  =>  we translate to:  strcmp (a, b) cne 0
         *   strcmp (a, b) clt  0
         *   strcmp (a, b) clt  1  // <=
         *   strcmp (a, b) cgt  0
         *   strcmp (a, b) cgt -1  // >=
         * ...but then optimized by ScriptCollector if followed by br{false,true}:
         *   ceq + xor 1 + brtrue  => bne.un
         *   ceq + xor 1 + brfalse => beq
         *   ceq + brtrue  => beq
         *   ceq + brfalse => bne.un
         *   cgt + brtrue  => bgt
         *   cgt + brfalse => ble
         *   clt + brtrue  => blt
         *   clt + brfalse => bge
         * So we end up with these cases:
         *   strcmp (a, b) ceq  0
         *   strcmp (a, b) cne  0
         *   strcmp (a, b) clt  0
         *   strcmp (a, b) clt  1
         *   strcmp (a, b) cgt  0
         *   strcmp (a, b) cgt -1
         *   strcmp (a, b) beq    0
         *   strcmp (a, b) bne.un 0
         *   strcmp (a, b) bgt  0
         *   strcmp (a, b) ble  0
         *   strcmp (a, b) bgt -1
         *   strcmp (a, b) ble -1
         *   strcmp (a, b) blt  0
         *   strcmp (a, b) bge  0
         *   strcmp (a, b) blt  1
         *   strcmp (a, b) bge  1
         * ... so we pretty them up in OTOpndBinOp
         */
        private class OTOpndStrCmp: OTOpnd
        {
            private static Dictionary<string, string> binops = InitBinops();
            private static Dictionary<string, string> InitBinops()
            {
                Dictionary<string, string> d = new Dictionary<string, string>();
                d["ceq 0"] = "ceq";
                d["cne 0"] = "cne";
                d["clt 0"] = "clt";
                d["clt 1"] = "cle";
                d["cgt 0"] = "cgt";
                d["cgt -1"] = "cge";
                d["beq 0"] = "ceq";
                d["bne.un 0"] = "cne";
                d["bgt 0"] = "cgt";
                d["ble 0"] = "cle";
                d["bgt -1"] = "cge";
                d["ble -1"] = "clt";
                d["blt 0"] = "clt";
                d["bge 0"] = "cge";
                d["blt 1"] = "cle";
                d["bge 1"] = "cgt";
                return d;
            }

            private OTOpnd arg0;
            private OTOpnd arg1;

            public OTOpndStrCmp(OTOpnd arg0, OTOpnd arg1)
            {
                this.arg0 = arg0;
                this.arg1 = arg1;
            }

            /**
             * Try to make something a script writer would recognize.
             * If we can't, then we leave it as a call to xmrStringCompare().
             *    this   = some strcmp(a,b)
             *    opCode = hopefully some cxx or bxx from above table
             *    rite   = hopefully some constant from above table
             */
            public OTOpnd MakeBinOp(MyOp opCode, OTOpnd rite)
            {
                if(!(rite is OTOpndInt))
                    return null;
                int riteint = ((OTOpndInt)rite).value;
                string key = opCode.name + ' ' + riteint;
                string cxxopname;
                if(!binops.TryGetValue(key, out cxxopname))
                    return null;
                return OTOpndBinOp.Make(arg0, MyOp.GetByName(cxxopname), arg1);
            }
            public OTOpnd MakeUnOp(MyOp opCode)
            {
                if(opCode == MyOp.Brfalse)
                    return OTOpndBinOp.Make(arg0, MyOp.Ceq, arg1);
                if(opCode == MyOp.Brtrue)
                    return OTOpndBinOp.Make(arg0, MyOp.Cne, arg1);
                return null;
            }

            public override bool HasSideEffects
            {
                get
                {
                    return false;
                }
            }

            public override void CountRefs(bool writing)
            {
                arg0.CountRefs(writing);
                arg1.CountRefs(writing);
            }

            public override OTOpnd ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd, ref bool rc)
            {
                if(SameAs(oldopnd))
                {
                    rc = true;
                    return newopnd;
                }
                arg0 = arg0.ReplaceOperand(oldopnd, newopnd, ref rc);
                arg1 = arg1.ReplaceOperand(oldopnd, newopnd, ref rc);
                return this;
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndStrCmp))
                    return false;
                return arg0.SameAs(((OTOpndStrCmp)other).arg0) && arg1.SameAs(((OTOpndStrCmp)other).arg1);
            }

            public override string PrintableString
            {
                get
                {
                    return "xmrStringCompare (" + arg0.PrintableString + ", " + arg1.PrintableString + ")";
                }
            }
        }

        /**
         * Unary operator.
         */
        private class OTOpndUnOp: OTOpnd
        {
            public MyOp opCode;
            public OTOpnd value;

            private static Dictionary<string, string> brfops = InitBrfOps();
            private static Dictionary<string, string> InitBrfOps()
            {
                Dictionary<string, string> d = new Dictionary<string, string>();
                d["beq"] = "cne";
                d["bge"] = "clt";
                d["bgt"] = "cle";
                d["ble"] = "cgt";
                d["blt"] = "cge";
                d["bne.un"] = "ceq";
                d["ceq"] = "cne";
                d["cge"] = "clt";
                d["cgt"] = "cle";
                d["cle"] = "cgt";
                d["clt"] = "cge";
                d["cne"] = "ceq";
                return d;
            }

            public static OTOpnd Make(MyOp opCode, OTOpnd value)
            {
                // (brfalse (brfalse (x)))  =>  (brtrue (x))
                if((opCode == MyOp.Brfalse) && (value is OTOpndUnOp) && (((OTOpndUnOp)value).opCode == MyOp.Brfalse))
                {
                    ((OTOpndUnOp)value).opCode = MyOp.Brtrue;
                    return value;
                }

                // (brfalse (brtrue (x)))  =>  (brfalse (x))
                if((opCode == MyOp.Brfalse) && (value is OTOpndUnOp) && (((OTOpndUnOp)value).opCode == MyOp.Brtrue))
                {
                    ((OTOpndUnOp)value).opCode = MyOp.Brfalse;
                    return value;
                }

                // (brtrue (brfalse (x)))  =>  (brfalse (x))
                if((opCode == MyOp.Brtrue) && (value is OTOpndUnOp) && (((OTOpndUnOp)value).opCode == MyOp.Brfalse))
                {
                    return value;
                }

                // (brtrue (brtrue (x)))  =>  (brtrue (x))
                if((opCode == MyOp.Brtrue) && (value is OTOpndUnOp) && (((OTOpndUnOp)value).opCode == MyOp.Brtrue))
                {
                    return value;
                }

                // (brfalse (x beq y))  =>  (x bne y)  etc
                string brfop;
                if((opCode == MyOp.Brfalse) && (value is OTOpndBinOp) && brfops.TryGetValue(((OTOpndBinOp)value).opCode.name, out brfop))
                {
                    ((OTOpndBinOp)value).opCode = MyOp.GetByName(brfop);
                    return value;
                }

                // (brtrue  (x beq y))  =>  (x beq y)  etc
                if((opCode == MyOp.Brtrue) && (value is OTOpndBinOp) && brfops.ContainsKey(((OTOpndBinOp)value).opCode.name))
                {
                    return value;
                }

                // strcmp() can be a special case
                if(value is OTOpndStrCmp)
                {
                    OTOpnd strcmp = ((OTOpndStrCmp)value).MakeUnOp(opCode);
                    if(strcmp != null)
                        return strcmp;
                }

                // nothing special, save opcode and value
                OTOpndUnOp it = new OTOpndUnOp();
                it.opCode = opCode;
                it.value = value;
                return it;
            }

            private OTOpndUnOp()
            {
            }

            public override bool HasSideEffects
            {
                get
                {
                    return value.HasSideEffects;
                }
            }

            public override void CountRefs(bool writing)
            {
                value.CountRefs(false);
            }

            public override OTOpnd ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd, ref bool rc)
            {
                if(SameAs(oldopnd))
                {
                    rc = true;
                    return newopnd;
                }
                value = value.ReplaceOperand(oldopnd, newopnd, ref rc);
                return this;
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndUnOp))
                    return false;
                OTOpndUnOp otherop = (OTOpndUnOp)other;
                return (opCode.ToString() == otherop.opCode.ToString()) && value.SameAs(otherop.value);
            }

            public override string PrintableString
            {
                get
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(opCode.source);
                    sb.Append(' ');
                    if(value is OTOpndBinOp)
                        sb.Append('(');
                    sb.Append(value.PrintableString);
                    if(value is OTOpndBinOp)
                        sb.Append(')');
                    return sb.ToString();
                }
            }
        }

        /**
         * Vector value.
         */
        private class OTOpndVec: OTOpnd
        {
            private OTOpnd x, y, z;

            public OTOpndVec(OTOpnd x, OTOpnd y, OTOpnd z)
            {
                this.x = StripFloatCast(x);
                this.y = StripFloatCast(y);
                this.z = StripFloatCast(z);
            }

            public override bool HasSideEffects
            {
                get
                {
                    return x.HasSideEffects || y.HasSideEffects || z.HasSideEffects;
                }
            }

            public override void CountRefs(bool writing)
            {
                x.CountRefs(false);
                y.CountRefs(false);
                z.CountRefs(false);
            }

            public override OTOpnd ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd, ref bool rc)
            {
                if(SameAs(oldopnd))
                {
                    rc = true;
                    return newopnd;
                }
                x = x.ReplaceOperand(oldopnd, newopnd, ref rc);
                y = y.ReplaceOperand(oldopnd, newopnd, ref rc);
                z = z.ReplaceOperand(oldopnd, newopnd, ref rc);
                return this;
            }

            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndVec))
                    return false;
                OTOpndVec otherv = (OTOpndVec)other;
                return otherv.x.SameAs(x) && otherv.y.SameAs(y) && otherv.z.SameAs(z);
            }

            public override string PrintableString
            {
                get
                {
                    return "<" + x.PrintableString + ", " + y.PrintableString + ", " + z.PrintableString + ">";
                }
            }
        }

        /**
         * Constants.
         */
        private class OTOpndDouble: OTOpnd
        {
            public double value;
            public OTOpndDouble(double value)
            {
                this.value = value;
            }
            public override bool HasSideEffects
            {
                get
                {
                    return false;
                }
            }
            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndDouble))
                    return false;
                return ((OTOpndDouble)other).value == value;
            }
            public override string PrintableString
            {
                get
                {
                    string s = value.ToString();
                    long i;
                    if(long.TryParse(s, out i))
                    {
                        s += ".0";
                    }
                    return s;
                }
            }
        }
        private class OTOpndFloat: OTOpnd
        {
            public float value;
            public OTOpndFloat(float value)
            {
                this.value = value;
            }
            public override bool HasSideEffects
            {
                get
                {
                    return false;
                }
            }
            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndFloat))
                    return false;
                return ((OTOpndFloat)other).value == value;
            }
            public override string PrintableString
            {
                get
                {
                    string s = value.ToString();
                    long i;
                    if(long.TryParse(s, out i))
                    {
                        s += ".0";
                    }
                    return s;
                }
            }
        }
        private class OTOpndInt: OTOpnd
        {
            public int value;
            public OTOpndInt(int value)
            {
                this.value = value;
            }
            public override bool HasSideEffects
            {
                get
                {
                    return false;
                }
            }
            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndInt))
                    return false;
                return ((OTOpndInt)other).value == value;
            }
            public override string PrintableString
            {
                get
                {
                    return value.ToString();
                }
            }
        }
        private class OTOpndNull: OTOpnd
        {
            public override bool HasSideEffects
            {
                get
                {
                    return false;
                }
            }
            public override bool SameAs(OTOpnd other)
            {
                return other is OTOpndNull;
            }
            public override string PrintableString
            {
                get
                {
                    return "undef";
                }
            }
        }
        private class OTOpndString: OTOpnd
        {
            public string value;
            public OTOpndString(string value)
            {
                this.value = value;
            }
            public override bool HasSideEffects
            {
                get
                {
                    return false;
                }
            }
            public override bool SameAs(OTOpnd other)
            {
                if(!(other is OTOpndString))
                    return false;
                return ((OTOpndString)other).value == value;
            }
            public override string PrintableString
            {
                get
                {
                    StringBuilder sb = new StringBuilder();
                    TokenDeclInline.PrintParamString(sb, value);
                    return sb.ToString();
                }
            }
        }

        /****************************************\
         *  Tokens what are in statement list.  *
        \****************************************/

        public abstract class OTStmt
        {

            /**
             * Increment reference counts.
             */
            public abstract void CountRefs();

            /**
             * Strip out any of the behind-the-scenes code such as stack capture/restore.
             * By default, there is no change.
             */
            public virtual bool StripStuff(LinkedListNode<OTStmt> link)
            {
                return false;
            }

            /**
             * Replace the oldopnd operand with the newopnd operand if it is present.
             * Return whether or not it was found and replaced.
             */
            public abstract bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd);

            /**
             * Detect and modify for do/for/if/while structures.
             */
            public virtual bool DetectDoForIfWhile(LinkedListNode<OTStmt> link)
            {
                return false;
            }

            /**
             * If this statement is the old statement, replace it with the given new statement.
             * Also search any sub-ordinate statements.
             * **NOTE**: minimally implemented to replace a Jump with a Break or Continue
             */
            public abstract OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt);

            /**
             * Print the statement out on the given printer with the given indenting.
             * The first line is already indented, subsequent lines must be indented as given.
             * This method should leave the printer at the end of the line.
             */
            public abstract void PrintStmt(TextWriter twout, string indent);

            /**
             * Strip all statements following this statement
             * because this statement jumps somewhere.
             */
            protected bool StripStuffForTerminal(LinkedListNode<OTStmt> link)
            {
                // strip all statements following jump until seeing some label
                bool rc = false;
                if(link != null)
                {
                    LinkedListNode<OTStmt> nextlink;
                    while((nextlink = link.Next) != null)
                    {
                        if(nextlink.Value is OTStmtLabel)
                            break;
                        nextlink.List.Remove(nextlink);
                        rc = true;
                    }
                }
                return rc;
            }
        }

        /**************************\
         *  Primitive statements  *
        \**************************/

        /**
         * Begin catch block (catch).
         */
        private class OTStmtBegCatBlk: OTStmt
        {
            public OTStmtBegExcBlk tryblock;
            public OTStmtBlock catchblock;

            private Type excType;

            public OTStmtBegCatBlk(Type excType)
            {
                this.excType = excType;
            }

            public override void CountRefs()
            {
                catchblock.CountRefs();
            }

            public override bool StripStuff(LinkedListNode<OTStmt> link)
            {
                return catchblock.StripStuff(null);
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                return catchblock.ReplaceOperand(oldopnd, newopnd);
            }

            public override bool DetectDoForIfWhile(LinkedListNode<OTStmt> link)
            {
                return catchblock.DetectDoForIfWhile(link);
            }

            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                catchblock = (OTStmtBlock)catchblock.ReplaceStatement(oldstmt, newstmt);
                return this;
            }

            /**
             * Print out the catch block including its enclosed statements.
             */
            public override void PrintStmt(TextWriter twout, string indent)
            {
                twout.Write("catch (" + excType.Name + ") ");
                catchblock.PrintStmt(twout, indent);
            }
        }

        /**
         * Begin exception block (try).
         */
        private class OTStmtBegExcBlk: OTStmt
        {

            // statements within the try { } not including any catch or finally
            public OTStmtBlock tryblock;

            // list of all catch { } blocks associated with this try { }
            public LinkedList<OTStmtBegCatBlk> catches = new LinkedList<OTStmtBegCatBlk>();

            // possible single finally { } associated with this try
            public OTStmtBegFinBlk finblock;  // might be null

            public override void CountRefs()
            {
                tryblock.CountRefs();
                foreach(OTStmtBegCatBlk catblock in catches)
                {
                    catblock.CountRefs();
                }
                if(finblock != null)
                    finblock.CountRefs();
            }

            /**
             * Strip behind-the-scenes info from all the sub-blocks.
             */
            public override bool StripStuff(LinkedListNode<OTStmt> link)
            {
                // strip behind-the-scenes info from all the sub-blocks.
                bool rc = tryblock.StripStuff(null);
                foreach(OTStmtBegCatBlk catblk in catches)
                {
                    rc |= catblk.StripStuff(null);
                }
                if(finblock != null)
                    rc |= finblock.StripStuff(null);
                if(rc)
                    return true;

                // change:
                //    try {
                //       ...
                //    }
                // to:
                //    {
                //       ...
                //    }
                // note that an empty catch () { } has meaning so can't be stripped
                // empty finally { } blocks strips itself from the try
                if((catches.Count == 0) && (finblock == null) && (link != null))
                {
                    link.List.AddAfter(link, tryblock);
                    tryblock = null;
                    link.List.Remove(link);
                    return true;
                }

                return false;
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                bool rc = tryblock.ReplaceOperand(oldopnd, newopnd);
                foreach(OTStmtBegCatBlk catblk in catches)
                {
                    rc |= catblk.ReplaceOperand(oldopnd, newopnd);
                }
                if(finblock != null)
                    rc |= finblock.ReplaceOperand(oldopnd, newopnd);
                return rc;
            }

            public override bool DetectDoForIfWhile(LinkedListNode<OTStmt> link)
            {
                bool rc = tryblock.DetectDoForIfWhile(link);
                foreach(OTStmtBegCatBlk catblk in catches)
                {
                    rc |= catblk.DetectDoForIfWhile(link);
                }
                if(finblock != null)
                    rc |= finblock.DetectDoForIfWhile(link);
                return rc;
            }

            /**
             * Assume we will never try to replace the try block itself.
             * But go through all our sub-ordinates statements.
             */
            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                tryblock = (OTStmtBlock)tryblock.ReplaceStatement(oldstmt, newstmt);
                for(LinkedListNode<OTStmtBegCatBlk> catlink = catches.First; catlink != null; catlink = catlink.Next)
                {
                    catlink.Value = (OTStmtBegCatBlk)catlink.Value.ReplaceStatement(oldstmt, newstmt);
                }
                if(finblock != null)
                    finblock = (OTStmtBegFinBlk)finblock.ReplaceStatement(oldstmt, newstmt);
                return this;
            }

            /**
             * Print out the try block including its enclosed statements.
             * And since the try is the only thing pushed to the outer block,
             * we also print out all the catch and finally blocks.
             */
            public override void PrintStmt(TextWriter twout, string indent)
            {
                twout.Write("try ");
                tryblock.PrintStmt(twout, indent);
                foreach(OTStmtBegCatBlk catblk in catches)
                {
                    twout.Write(' ');
                    catblk.PrintStmt(twout, indent);
                }
                if(finblock != null)
                {
                    twout.Write(' ');
                    finblock.PrintStmt(twout, indent);
                }
            }
        }

        /**
         * Begin finally block (finally).
         */
        private class OTStmtBegFinBlk: OTStmt
        {
            public OTStmtBegExcBlk tryblock;
            public OTStmtBlock finblock;

            public override void CountRefs()
            {
                finblock.CountRefs();
            }

            /**
             * Strip behind-the-scene parts from the finally block.
             */
            public override bool StripStuff(LinkedListNode<OTStmt> link)
            {
                // strip behind-the-scenes parts from finally block itself
                if(finblock.StripStuff(null))
                    return true;

                // if finblock is empty, delete the finally from the try
                if(finblock.blkstmts.Count == 0)
                {
                    tryblock.finblock = null;
                    return true;
                }

                return false;
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                return finblock.ReplaceOperand(oldopnd, newopnd);
            }

            public override bool DetectDoForIfWhile(LinkedListNode<OTStmt> link)
            {
                return finblock.DetectDoForIfWhile(link);
            }

            /**
             * Assume we will never try to replace the finally block itself.
             * But go through all our sub-ordinates statements.
             */
            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                finblock = (OTStmtBlock)finblock.ReplaceStatement(oldstmt, newstmt);
                return this;
            }

            /**
             * Print out the finally block including its enclosed statements.
             */
            public override void PrintStmt(TextWriter twout, string indent)
            {
                twout.Write("finally ");
                finblock.PrintStmt(twout, indent);
            }
        }

        /**
         * Simple if jump/break/continue statement.
         */
        private class OTStmtCond: OTStmt
        {
            public OTOpnd valu;
            public OTStmt stmt;  // jump, break, continue only

            public OTStmtCond(OTOpnd valu, OTStmt stmt)
            {
                this.valu = valu;
                this.stmt = stmt;
            }

            public override void CountRefs()
            {
                valu.CountRefs(false);
                stmt.CountRefs();
            }

            public override bool StripStuff(LinkedListNode<OTStmt> link)
            {
                // we assume that callMode is always CallMode_NORMAL, ie, not doing a stack capture or restore
                // so the 'if (arg$0.callMode bne.un 0) ...' is deleted
                // and the 'if (arg$0.callMode bne.un 1) ...' becomes unconditional
                // it can also be __xmrinst.callMode instead of arg$0
                if(valu is OTOpndBinOp)
                {
                    OTOpndBinOp binop = (OTOpndBinOp)valu;
                    if((binop.left is OTOpndField) && (binop.opCode.ToString() == "bne.un") && (binop.rite is OTOpndInt))
                    {
                        OTOpndField leftfield = (OTOpndField)binop.left;
                        if(leftfield.field.Name == _callMode)
                        {
                            bool ok = false;
                            if(leftfield.obj is OTOpndArg)
                            {
                                ok = ((OTOpndArg)leftfield.obj).index == 0;
                            }
                            if(leftfield.obj is OTOpndLocal)
                            {
                                ok = ((OTOpndLocal)leftfield.obj).local.name.StartsWith(_xmrinstlocal);
                            }
                            if(ok)
                            {
                                OTOpndInt riteint = (OTOpndInt)binop.rite;

                                // delete 'if ((arg$0).callMode bne.un 0) ...'
                                if(riteint.value == XMRInstAbstract.CallMode_NORMAL)
                                {
                                    link.List.Remove(link);
                                    return true;
                                }

                                // make 'if ((arg$0).callMode bne.un 1) ...' unconditional
                                if(riteint.value == XMRInstAbstract.CallMode_SAVE)
                                {
                                    link.Value = stmt;
                                    return true;
                                }
                            }
                        }
                    }
                }

                // similarly we assume that doGblInit is always 0 to eliminate the code at beginning of default state_entry()
                // so the 'if (brfalse __xmrinst.doGblInit) ...' is made unconditional
                if(valu is OTOpndUnOp)
                {
                    OTOpndUnOp unop = (OTOpndUnOp)valu;
                    if((unop.opCode == MyOp.Brfalse) && (unop.value is OTOpndField))
                    {
                        OTOpndField valuefield = (OTOpndField)unop.value;
                        if(valuefield.field.Name == _doGblInit)
                        {
                            bool ok = false;
                            if(valuefield.obj is OTOpndLocal)
                            {
                                ok = ((OTOpndLocal)valuefield.obj).local.name.StartsWith(_xmrinstlocal);
                            }
                            if(ok)
                            {

                                // make 'if (brfalse __xmrinst.doGblInit) ...' unconditional
                                link.Value = stmt;
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                bool rc = stmt.ReplaceOperand(oldopnd, newopnd);
                valu = valu.ReplaceOperand(oldopnd, newopnd, ref rc);
                return rc;
            }

            /**
             * Maybe this simple if statement is part of a script-level if/then/else statement.
             */
            public override bool DetectDoForIfWhile(LinkedListNode<OTStmt> link)
            {
                return OTStmtIf.Detect(link);
            }

            /**
             * Assume we won't replace the if statement itself.
             * But search all our sub-ordinate statements.
             */
            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                stmt = stmt.ReplaceStatement(oldstmt, newstmt);
                return this;
            }

            public override void PrintStmt(TextWriter twout, string indent)
            {
                twout.Write("if (" + StripBrtrue(valu).PrintableString + ") ");
                stmt.PrintStmt(twout, indent);
            }

            /**
             * Scan forward for a given label definition.
             * Put intervening statements in a statement block.
             * @param link = start scanning after this statement
             * @param label = look for this label definition
             * @param block = where to return intervening statement block
             * @returns null: label definition not found
             *          else: label definition statement
             */
            private static LinkedListNode<OTStmt> ScanForLabel(LinkedListNode<OTStmt> link,
                        OTLabel label, out OTStmtBlock block)
            {
                block = new OTStmtBlock();
                while((link = link.Next) != null)
                {
                    if(link.Value is OTStmtLabel)
                    {
                        if(((OTStmtLabel)link.Value).label == label)
                            break;
                    }
                    block.blkstmts.AddLast(link.Value);
                }
                return link;
            }

            /**
             * Strip statements after link up to and including donelink.
             */
            private static void StripInterveningStatements(LinkedListNode<OTStmt> link, LinkedListNode<OTStmt> donelink)
            {
                LinkedListNode<OTStmt> striplink;
                do
                {
                    striplink = link.Next;
                    striplink.List.Remove(striplink);
                } while(striplink != donelink);
            }
        }

        /**
         * Jump to a label.
         */
        private class OTStmtJump: OTStmt
        {
            public OTLabel label;

            public static OTStmt Make(OTLabel label)
            {
                // jumps to __retlbl are return statements
                // note that is is safe to say it is a valueless return because
                // valued returns are done with this construct:
                //    __retval = ....;
                //    jump __retlbl;
                // and those __retval = statements have been changed to return statements already
                if(label.name.StartsWith(_retlbl))
                    return new OTStmtRet(null);

                // other jumps are really jumps
                OTStmtJump it = new OTStmtJump();
                it.label = label;
                return it;
            }

            private OTStmtJump()
            {
            }

            public override void CountRefs()
            {
                label.lbljumps++;
            }

            public override bool StripStuff(LinkedListNode<OTStmt> link)
            {
                if(link == null)
                    return false;

                // strip statements following unconditional jump until next label
                bool rc = StripStuffForTerminal(link);

                // if we (now) have:
                //      jump label;
                //   @label;
                // ... delete this jump
                if(link.Next != null)
                {
                    OTStmtLabel nextlabel = (OTStmtLabel)link.Next.Value;
                    if(nextlabel.label == label)
                    {
                        link.List.Remove(link);
                        rc = true;
                    }
                }

                return rc;
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                return false;
            }

            /**
             * This is actually what ReplaceStatement() is currently used for.
             * It replaces a jump with a break or a continue.
             */
            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                if((oldstmt is OTStmtJump) && (((OTStmtJump)oldstmt).label == label))
                    return newstmt;
                return this;
            }

            public override void PrintStmt(TextWriter twout, string indent)
            {
                twout.Write("jump " + label.PrintableName + ';');
            }
        }

        /**
         * Label definition point.
         */
        private class OTStmtLabel: OTStmt
        {
            public OTLabel label;

            private OTDecompile decompile;

            public static void AddLast(OTDecompile decompile, OTLabel label)
            {
                OTStmtLabel it = new OTStmtLabel();
                it.label = label;
                it.decompile = decompile;
                decompile.AddLastStmt(it);
            }

            private OTStmtLabel()
            {
            }

            public override void CountRefs()
            {
                // don't increment label.lbljumps
                // cuz we don't want the positioning
                // to count as a reference, only jumps
                // to the label should count
            }

            public override bool StripStuff(LinkedListNode<OTStmt> link)
            {
                // if label has nothing jumping to it, remove the label
                if(link != null)
                {
                    label.lbljumps = 0;
                    decompile.topBlock.CountRefs();
                    if(label.lbljumps == 0)
                    {
                        link.List.Remove(link);
                        return true;
                    }
                }

                return false;
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                return false;
            }

            public override bool DetectDoForIfWhile(LinkedListNode<OTStmt> link)
            {
                if(OTStmtDo.Detect(link))
                    return true;
                if(OTStmtFor.Detect(link, true))
                    return true;
                if(OTStmtFor.Detect(link, false))
                    return true;
                return false;
            }

            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                return this;
            }

            public override void PrintStmt(TextWriter twout, string indent)
            {
                twout.Write("@" + label.PrintableName + ';');
            }
        }

        /**
         * Return with or without value.
         */
        private class OTStmtRet: OTStmt
        {
            public OTOpnd value;  // might be null

            public OTStmtRet(OTOpnd value)
            {
                this.value = value;
            }

            public override void CountRefs()
            {
                if(value != null)
                    value.CountRefs(false);
            }

            public override bool StripStuff(LinkedListNode<OTStmt> link)
            {
                return StripStuffForTerminal(link);
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                bool rc = false;
                if(value != null)
                    value = value.ReplaceOperand(oldopnd, newopnd, ref rc);
                return rc;
            }

            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                return this;
            }

            public override void PrintStmt(TextWriter twout, string indent)
            {
                if(value == null)
                {
                    twout.Write("return;");
                }
                else
                {
                    twout.Write("return " + value.PrintableString + ';');
                }
            }
        }

        /**
         * Store value in variable.
         */
        private class OTStmtStore: OTStmt
        {
            public OTOpnd varwr;
            public OTOpnd value;

            private OTDecompile decompile;

            public static void AddLast(OTDecompile decompile, OTOpnd varwr, OTOpnd value)
            {
                OTStmtStore it = new OTStmtStore(varwr, value, decompile);
                decompile.AddLastStmt(it);
            }

            public OTStmtStore(OTOpnd varwr, OTOpnd value, OTDecompile decompile)
            {
                this.varwr = varwr;
                this.value = value;
                this.decompile = decompile;
            }

            public override void CountRefs()
            {
                varwr.CountRefs(true);
                value.CountRefs(false);
            }

            public override bool StripStuff(LinkedListNode<OTStmt> link)
            {
                // strip out stores to __mainCallNo
                if(varwr is OTOpndLocal)
                {
                    OTOpndLocal local = (OTOpndLocal)varwr;
                    if(local.local.name.StartsWith(_mainCallNo))
                    {
                        link.List.Remove(link);
                        return true;
                    }
                }

                // strip out stores to local vars where the var is not read
                // but convert the value to an OTStmtVoid in case it is a call
                if(varwr is OTOpndLocal)
                {
                    OTOpndLocal local = (OTOpndLocal)varwr;
                    local.local.nlclreads = 0;
                    decompile.topBlock.CountRefs();
                    if(local.local.nlclreads == 0)
                    {
                        OTStmt voidstmt = OTStmtVoid.Make(value);
                        if(voidstmt == null)
                            link.List.Remove(link);
                        else
                            link.Value = voidstmt;
                        return true;
                    }
                }

                // strip out bla = newobj HeapTrackerList (...);
                if(value is OTOpndNewobj)
                {
                    OTOpndNewobj valueno = (OTOpndNewobj)value;
                    if(valueno.ctor.DeclaringType == typeof(HeapTrackerList))
                    {
                        link.List.Remove(link);
                        return true;
                    }
                }

                // strip out bla = newobj HeapTrackerObject (...);
                if(value is OTOpndNewobj)
                {
                    OTOpndNewobj valueno = (OTOpndNewobj)value;
                    if(valueno.ctor.DeclaringType == typeof(HeapTrackerObject))
                    {
                        link.List.Remove(link);
                        return true;
                    }
                }

                // strip out bla = newobj HeapTrackerString (...);
                if(value is OTOpndNewobj)
                {
                    OTOpndNewobj valueno = (OTOpndNewobj)value;
                    if(valueno.ctor.DeclaringType == typeof(HeapTrackerString))
                    {
                        link.List.Remove(link);
                        return true;
                    }
                }

                // convert tmp$n = bla bla;
                //         ....  tmp$n  ....;
                // to
                //         ....  bla bla  ....;
                // gets rid of vast majority of temps
                if(varwr is OTOpndLocal)
                {
                    OTOpndLocal temp = (OTOpndLocal)varwr;
                    if(temp.local.name.StartsWith("tmp$"))
                    {
                        temp.local.nlclreads = 0;
                        temp.local.nlclwrites = 0;
                        decompile.topBlock.CountRefs();
                        if((temp.local.nlclreads == 1) && (temp.local.nlclwrites == 1) && (link.Next != null))
                        {
                            OTStmt nextstmt = link.Next.Value;
                            if(!(nextstmt is OTStmtBlock))
                            {
                                if(nextstmt.ReplaceOperand(varwr, value))
                                {
                                    link.List.Remove(link);
                                    return true;
                                }
                            }
                        }

                        // also try to convert:
                        //    tmp$n = ... asdf ...  << we are here (link)
                        //    lcl = tmp$n;          << nextstore
                        //    ... qwer tmp$n ...
                        //    ... no further references to tmp$n
                        // to:
                        //    lcl = ... asdf ...
                        //    ... qwer lcl ...
                        if((temp.local.nlclreads == 2) && (temp.local.nlclwrites == 1) &&
                                (link.Next != null) && (link.Next.Value is OTStmtStore))
                        {
                            OTStmtStore nextstore = (OTStmtStore)link.Next.Value;
                            if((nextstore.varwr is OTOpndLocal) && (nextstore.value is OTOpndLocal) && (link.Next.Next != null))
                            {
                                OTOpndLocal localopnd = (OTOpndLocal)nextstore.varwr;
                                OTOpndLocal tempopnd = (OTOpndLocal)nextstore.value;
                                if(tempopnd.local == temp.local)
                                {
                                    OTStmt finalstmt = link.Next.Next.Value;
                                    if(finalstmt.ReplaceOperand(tempopnd, localopnd))
                                    {
                                        nextstore.value = value;
                                        link.List.Remove(link);
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }

                // convert:
                //    dup$n = ... asdf ...  << we are here
                //    lcl = dup$n;
                //    ... qwer dup$n ...
                //    ... no further references to dup$n
                // to:
                //    lcl = ... asdf ...
                //    ... qwer lcl ...
                if((varwr is OTOpndDup) && (link != null))
                {
                    OTOpndDup vardup = (OTOpndDup)varwr;
                    LinkedListNode<OTStmt> nextlink = link.Next;
                    vardup.ndupreads = 0;
                    decompile.topBlock.CountRefs();
                    if((vardup.ndupreads == 2) && (nextlink != null) && (nextlink.Value is OTStmtStore))
                    {

                        // point to the supposed lcl = dup$n statement
                        OTStmtStore nextstore = (OTStmtStore)nextlink.Value;
                        LinkedListNode<OTStmt> nextlink2 = nextlink.Next;
                        if((nextstore.varwr is OTOpndLocal) && (nextstore.value == vardup) && (nextlink2 != null))
                        {

                            // get the local var being written and point to the ... qwer dup$n ... statement
                            OTOpndLocal varlcl = (OTOpndLocal)nextstore.varwr;
                            OTStmt nextstmt2 = nextlink2.Value;

                            // try to replace dup$n in qwer with lcl
                            if(nextstmt2.ReplaceOperand(vardup, varlcl))
                            {

                                // successful, replace dup$n in asdf with lcl
                                // and delete the lcl = dup$n statement
                                varwr = varlcl;
                                nextlink.List.Remove(nextlink);
                                return true;
                            }
                        }
                    }
                }

                // convert:
                //    dup$n = ... asdf ...  << we are here
                //    ... qwer dup$n ...
                //    ... no further references to dup$n
                // to:
                //    ... qwer ... asdf ... ...
                if((varwr is OTOpndDup) && (link != null))
                {
                    OTOpndDup vardup = (OTOpndDup)varwr;
                    LinkedListNode<OTStmt> nextlink = link.Next;
                    vardup.ndupreads = 0;
                    decompile.topBlock.CountRefs();
                    if((vardup.ndupreads == 1) && (nextlink != null))
                    {

                        // point to the ... qwer dup$n ... statement
                        OTStmt nextstmt = nextlink.Value;

                        // try to replace dup$n in qwer with ... asdf ...
                        if(nextstmt.ReplaceOperand(vardup, value))
                        {

                            // successful, delete the dup$n = ... asdf ... statement
                            link.List.Remove(link);
                            return true;
                        }
                    }
                }

                // look for list initialization [ ... ]
                if(OTOpndListIni.Detect(link))
                    return true;

                // __xmrinst = (XMRInstAbstract) arg$0 indicates this is an event handler
                // so strip it out and set the flag
                if((varwr is OTOpndLocal) && (value is OTOpndCast))
                {
                    OTOpndLocal lcl = (OTOpndLocal)varwr;
                    OTOpndCast cast = (OTOpndCast)value;
                    if(lcl.local.name.StartsWith(_xmrinstlocal) && (cast.value is OTOpndArg))
                    {
                        link.List.Remove(link);
                        return true;
                    }
                }

                // local = [ (optional cast) ] __xmrinst.ehArgs[n] is a definition of event handler arg #n
                // if found, make it event handler arg list definition
                OTOpnd valuenocast = value;
                if(valuenocast is OTOpndCast)
                    valuenocast = ((OTOpndCast)value).value;
                if((varwr is OTOpndLocal) && (valuenocast is OTOpndArrayElem))
                {
                    OTOpndArrayElem array = (OTOpndArrayElem)valuenocast;
                    if((array.array is OTOpndField) && (array.index is OTOpndInt))
                    {
                        OTOpndField arrayfield = (OTOpndField)array.array;
                        if((arrayfield.obj is OTOpndLocal) &&
                                ((OTOpndLocal)arrayfield.obj).local.name.StartsWith(_xmrinstlocal) &&
                                (arrayfield.field.Name == _ehArgs))
                        {
                            int index = ((OTOpndInt)array.index).value;
                            decompile.eharglist[index] = ((OTOpndLocal)varwr).local;
                            link.List.Remove(link);
                            return true;
                        }
                    }
                }

                // __retval$n = ...;  =>  return ...;
                if(varwr is OTOpndLocal)
                {
                    OTOpndLocal lcl = (OTOpndLocal)varwr;
                    if(lcl.local.name.StartsWith(_retval))
                    {
                        link.Value = new OTStmtRet(value);
                        return true;
                    }
                }

                return false;
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                bool rc = false;
                if(value != null)
                    value = value.ReplaceOperand(oldopnd, newopnd, ref rc);
                return rc;
            }

            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                return this;
            }

            public override void PrintStmt(TextWriter twout, string indent)
            {
                // print x = x + 1 as x += 1, but don't print x = x < 3 as x <= 3
                if(value is OTOpndBinOp)
                {
                    OTOpndBinOp valuebo = (OTOpndBinOp)value;
                    if(varwr.SameAs(valuebo.left) && " add and div mul or rem shl shr sub xor ".Contains(' ' + valuebo.opCode.name + ' '))
                    {
                        twout.Write(varwr.PrintableString + ' ' + valuebo.opCode.source + "= " + valuebo.rite.PrintableString + ';');
                        return;
                    }
                }

                twout.Write(varwr.PrintableString + " = " + value.PrintableString + ';');
            }
        }

        /**
         * Dispatch to a table of labels.
         */
        private class OTStmtSwitch: OTStmt
        {
            private OTOpnd index;
            private OTLabel[] labels;

            public OTStmtSwitch(OTOpnd index, OTLabel[] labels)
            {
                this.index = index;
                this.labels = labels;
            }

            public override void CountRefs()
            {
                index.CountRefs(false);
                foreach(OTLabel label in labels)
                {
                    label.lbljumps++;
                }
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                bool rc = false;
                if(index != null)
                    index = index.ReplaceOperand(oldopnd, newopnd, ref rc);
                return rc;
            }

            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                return this;
            }

            public override void PrintStmt(TextWriter twout, string indent)
            {
                twout.Write("switch (" + index.PrintableString + ") {\n");
                for(int i = 0; i < labels.Length; i++)
                {
                    twout.Write(indent + INDENT + "case " + i + ": jump " + labels[i].name + ";\n");
                }
                twout.Write(indent + '}');
            }
        }

        /**
         * Throw an exception.
         */
        private class OTStmtThrow: OTStmt
        {
            private OTOpnd value;
            private OTDecompile decompile;

            public OTStmtThrow(OTOpnd value, OTDecompile decompile)
            {
                this.value = value;
                this.decompile = decompile;
            }

            public override void CountRefs()
            {
                value.CountRefs(false);
            }

            public override bool StripStuff(LinkedListNode<OTStmt> link)
            {
                return StripStuffForTerminal(link);
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                bool rc = false;
                if(value != null)
                    value = value.ReplaceOperand(oldopnd, newopnd, ref rc);
                return rc;
            }

            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                return this;
            }

            public override void PrintStmt(TextWriter twout, string indent)
            {
                // throw newobj ScriptUndefinedStateException ("x")  =>  state x
                if(value is OTOpndNewobj)
                {
                    OTOpndNewobj valueno = (OTOpndNewobj)value;
                    if((valueno.ctor.DeclaringType == typeof(ScriptUndefinedStateException)) &&
                        (valueno.args.Length == 1) && (valueno.args[0] is OTOpndString))
                    {
                        OTOpndString arg0 = (OTOpndString)valueno.args[0];
                        twout.Write("state " + arg0.value + ";  /* throws undefined state exception */");
                        return;
                    }
                }

                // throw newobj ScriptChangeStateException (n)  =>  state n
                if(value is OTOpndNewobj)
                {
                    OTOpndNewobj valueno = (OTOpndNewobj)value;
                    if((valueno.ctor.DeclaringType == typeof(ScriptChangeStateException)) &&
                        (valueno.args.Length == 1) && (valueno.args[0] is OTOpndInt))
                    {
                        OTOpndInt arg0 = (OTOpndInt)valueno.args[0];
                        twout.Write("state " + decompile.scriptObjCode.stateNames[arg0.value] + ';');
                        return;
                    }
                }

                // throwing something else, output as is
                twout.Write("throw " + value.PrintableString + ';');
            }
        }

        /**
         * Call with void return, or really anything that we discard the value of after computing it.
         */
        private class OTStmtVoid: OTStmt
        {
            private OTOpnd value;

            public static void AddLast(OTDecompile decompile, OTOpnd value)
            {
                OTStmt it = OTStmtVoid.Make(value);
                if(it != null)
                    decompile.AddLastStmt(it);
            }

            public static OTStmt Make(OTOpnd value)
            {
                if(!value.HasSideEffects)
                    return null;
                OTStmtVoid it = new OTStmtVoid();
                it.value = value;
                return it;
            }

            private OTStmtVoid()
            {
            }

            public override void CountRefs()
            {
                value.CountRefs(false);
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                bool rc = false;
                value = value.ReplaceOperand(oldopnd, newopnd, ref rc);
                return rc;
            }

            public override bool StripStuff(LinkedListNode<OTStmt> link)
            {
                // strip out calls to CheckRunQuick() and CheckRunStack()
                if(value is OTOpndCall)
                {
                    OTOpndCall call = (OTOpndCall)value;
                    MethodInfo method = call.method;
                    if((method.Name == _checkRunQuick) || (method.Name == _checkRunStack))
                    {
                        link.List.Remove(link);
                        return true;
                    }
                }

                return false;
            }

            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                return this;
            }

            public override void PrintStmt(TextWriter twout, string indent)
            {
                twout.Write(value.PrintableString + ';');
            }
        }

        /***************************\
         *  Structured statements  *
        \***************************/

        /**
         * Block of statements.
         */
        private class OTStmtBlock: OTStmt
        {
            public LinkedList<OTStmt> blkstmts = new LinkedList<OTStmt>();

            public override void CountRefs()
            {
                foreach(OTStmt stmt in blkstmts)
                {
                    stmt.CountRefs();
                }
            }

            /**
             * Scrub out all references to behind-the-scenes parts and simplify.
             */
            public override bool StripStuff(LinkedListNode<OTStmt> link)
            {
                // loop through all sub-statements to strip out behind-the-scenes references
                bool rc = false;
                loop:
                for(LinkedListNode<OTStmt> stmtlink = blkstmts.First; stmtlink != null; stmtlink = stmtlink.Next)
                {
                    if(stmtlink.Value.StripStuff(stmtlink))
                    {
                        rc = true;
                        goto loop;
                    }
                }
                if(rc)
                    return true;

                // try to merge this block into outer block
                // change:
                //   {
                //       ...
                //       {          << link points here
                //           ...
                //       }
                //       ...
                //   }
                // to:
                //   {
                //       ...
                //       ...
                //       ...
                //   }
                if(link != null)
                {
                    LinkedListNode<OTStmt> nextlink;
                    while((nextlink = blkstmts.Last) != null)
                    {
                        nextlink.List.Remove(nextlink);
                        link.List.AddAfter(link, nextlink);
                    }
                    link.List.Remove(link);
                    return true;
                }

                return rc;
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                bool rc = false;
                foreach(OTStmt stmt in blkstmts)
                {
                    rc |= stmt.ReplaceOperand(oldopnd, newopnd);
                }
                return rc;
            }

            /**
             * Check each statement in the block to see if it starts a do/for/if/while statement.
             */
            public override bool DetectDoForIfWhile(LinkedListNode<OTStmt> link)
            {
                bool rc = false;
                loop:
                for(link = blkstmts.First; link != null; link = link.Next)
                {
                    if(link.Value.DetectDoForIfWhile(link))
                    {
                        rc = true;
                        goto loop;
                    }
                }
                return rc;
            }

            /**
             * Assume we will never try to replace the block itself.
             * But go through all our sub-ordinates statements.
             */
            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                for(LinkedListNode<OTStmt> childlink = blkstmts.First; childlink != null; childlink = childlink.Next)
                {
                    childlink.Value = childlink.Value.ReplaceStatement(oldstmt, newstmt);
                }
                return this;
            }

            /**
             * Print out the block including its enclosed statements.
             */
            public override void PrintStmt(TextWriter twout, string indent)
            {
                switch(blkstmts.Count)
                {
                    case 0:
                        {
                            twout.Write("{ }");
                            break;
                        }
                    ////case 1: {
                    ////    blkstmts.First.Value.PrintStmt (twout, indent);
                    ////    break;
                    ////}
                    default:
                        {
                            twout.Write('{');
                            PrintBodyAndEnd(twout, indent);
                            break;
                        }
                }
            }

            public void PrintBodyAndEnd(TextWriter twout, string indent)
            {
                string newindent = indent + INDENT;
                foreach(OTStmt stmt in blkstmts)
                {
                    twout.Write('\n' + indent);
                    if(!(stmt is OTStmtLabel))
                        twout.Write(INDENT);
                    else
                        twout.Write(LABELINDENT);
                    stmt.PrintStmt(twout, newindent);
                }
                twout.Write('\n' + indent + '}');
            }
        }

        /**
         * 'do' statement.
         */
        private class OTStmtDo: OTStmt
        {
            private OTOpnd dotest;
            private OTStmtBlock dobody;

            /**
             * See if we have a do loop...
             *   @doloop_<suffix>;     << link points here
             *     ... <dobody> ...
             *     [ if (dotest) ] jump doloop_<suffix>;
             */
            public static bool Detect(LinkedListNode<OTStmt> link)
            {
                // see if we have label starting with 'doloop_'
                OTLabel looplabel = ((OTStmtLabel)link.Value).label;
                if(!looplabel.name.StartsWith(_doLoop))
                    return false;

                // good chance we have a do loop
                OTStmtDo it = new OTStmtDo();

                // scan ahead looking for the terminating cond/jump loop
                // also gather up the statements for the do body block
                it.dobody = new OTStmtBlock();
                LinkedListNode<OTStmt> nextlink;
                for(nextlink = link.Next; nextlink != null; nextlink = nextlink.Next)
                {
                    OTStmt nextstmt = nextlink.Value;

                    // add statement to do body
                    it.dobody.blkstmts.AddLast(nextlink.Value);

                    // check for something what jumps to loop label
                    // that gives us the end of the loop
                    OTStmt maybejump = nextstmt;
                    if(nextstmt is OTStmtCond)
                    {
                        maybejump = ((OTStmtCond)nextstmt).stmt;
                    }
                    if((maybejump is OTStmtJump) && (((OTStmtJump)maybejump).label == looplabel))
                    {
                        break;
                    }
                }

                // make sure we found the jump back to the loop label
                if(nextlink == null)
                    return false;

                // remove all statements from caller's block including the continue label if any
                // but leave the break label alone it will be removed later if unreferenced
                // and leave the initial loop label intact for now
                for(LinkedListNode<OTStmt> remlink = null; (remlink = link.Next) != null;)
                {
                    link.List.Remove(remlink);
                    if(remlink == nextlink)
                        break;
                }

                // take test condition from last statement of body
                // it should be an cond/jump or just a jump to the loop label
                LinkedListNode<OTStmt> lastlink = it.dobody.blkstmts.Last;
                OTStmt laststmt = lastlink.Value;
                if(laststmt is OTStmtCond)
                {
                    it.dotest = ((OTStmtCond)laststmt).valu;
                }
                else
                {
                    it.dotest = new OTOpndInt(1);
                }
                lastlink.List.Remove(lastlink);

                // finally replace the loop label with the whole do statement
                link.Value = it;

                // tell caller we made a change
                return true;
            }

            public override void CountRefs()
            {
                if(dotest != null)
                    dotest.CountRefs(false);
                if(dobody != null)
                    dobody.CountRefs();
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                return dobody.ReplaceOperand(oldopnd, newopnd);
            }

            public override bool DetectDoForIfWhile(LinkedListNode<OTStmt> link)
            {
                return dobody.DetectDoForIfWhile(link);
            }

            /**
             * Assume we won't replace the do statement itself.
             * But search all our sub-ordinate statements.
             */
            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                dobody = (OTStmtBlock)dobody.ReplaceStatement(oldstmt, newstmt);
                return this;
            }

            public override void PrintStmt(TextWriter twout, string indent)
            {
                // output do body
                twout.Write("do ");
                dobody.PrintStmt(twout, indent);

                // output while part
                twout.Write(" while (" + StripBrtrue(dotest).PrintableString + ");");
            }
        }

        /**
         * 'for' or 'while' statement.
         */
        private class OTStmtFor: OTStmt
        {
            private bool iswhile;
            private OTOpnd fortest;
            private OTStmtBlock forbody;
            private OTStmt forinit;
            private OTStmt forstep;

            /**
             * See if we have a for or while loop...
             *     <forinit>
             *   @forloop_<suffix>;     << link points here
             *     [ if (<fortest>) jump forbreak_<suffix>; ]
             *     ... <forbody> ...
             *     jump forloop_<suffix>;
             *   [ @forbreak_<suffix>; ]
             */
            public static bool Detect(LinkedListNode<OTStmt> link, bool iswhile)
            {
                string loopname = iswhile ? _whileLoop : _forLoop;
                string breakname = iswhile ? _whileBreak : _forBreak;

                // see if we have label starting with 'forloop_'
                OTLabel looplabel = ((OTStmtLabel)link.Value).label;
                if(!looplabel.name.StartsWith(loopname))
                    return false;

                // good chance we have a for loop
                OTStmtFor it = new OTStmtFor();
                it.iswhile = iswhile;

                // all labels end with this suffix
                string suffix = looplabel.name.Substring(loopname.Length);

                // scan ahead looking for the 'jump forloop_<suffix>;' statement
                // also gather up the statements for the for body block
                it.forbody = new OTStmtBlock();
                LinkedListNode<OTStmt> lastlink;
                for(lastlink = link; (lastlink = lastlink.Next) != null;)
                {

                    // check for jump forloop that tells us where loop ends
                    if(lastlink.Value is OTStmtJump)
                    {
                        OTStmtJump lastjump = (OTStmtJump)lastlink.Value;
                        if(lastjump.label == looplabel)
                            break;
                    }

                    // add to body block
                    it.forbody.blkstmts.AddLast(lastlink.Value);
                }

                // make sure we found the 'jump forloop' where the for loop ends
                if(lastlink == null)
                    return false;

                // remove all statements from caller's block including final jump
                // but leave the loop label in place
                for(LinkedListNode<OTStmt> nextlink = null; (nextlink = link.Next) != null;)
                {
                    link.List.Remove(nextlink);
                    if(nextlink == lastlink)
                        break;
                }

                // if statement before loop label is an assignment, use it for the init statement
                if(!iswhile && (link.Previous != null) && (link.Previous.Value is OTStmtStore))
                {
                    it.forinit = link.Previous.Value;
                    link.List.Remove(link.Previous);
                }

                // if first statement of for body is 'if (...) jump breaklabel' use it for the test value
                if((it.forbody.blkstmts.First != null) && (it.forbody.blkstmts.First.Value is OTStmtCond))
                {
                    OTStmtCond condstmt = (OTStmtCond)it.forbody.blkstmts.First.Value;
                    if((condstmt.stmt is OTStmtJump) && (((OTStmtJump)condstmt.stmt).label.name == breakname + suffix))
                    {
                        it.fortest = OTOpndUnOp.Make(MyOp.Brfalse, condstmt.valu);
                        it.forbody.blkstmts.RemoveFirst();
                    }
                }

                // if last statement of body is an assigment,
                // use the assignment as the step statement
                if(!iswhile && (it.forbody.blkstmts.Last != null) &&
                        (it.forbody.blkstmts.Last.Value is OTStmtStore))
                {
                    LinkedListNode<OTStmt> storelink = it.forbody.blkstmts.Last;
                    storelink.List.Remove(storelink);
                    it.forstep = storelink.Value;
                }

                // finally replace the loop label with the whole for statement
                link.Value = it;

                // tell caller we made a change
                return true;
            }

            public override void CountRefs()
            {
                if(fortest != null)
                    fortest.CountRefs(false);
                if(forbody != null)
                    forbody.CountRefs();
                if(forinit != null)
                    forinit.CountRefs();
                if(forstep != null)
                    forstep.CountRefs();
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                return forbody.ReplaceOperand(oldopnd, newopnd) |
                        ((forinit != null) && forinit.ReplaceOperand(oldopnd, newopnd)) |
                        ((forstep != null) && forstep.ReplaceOperand(oldopnd, newopnd));
            }

            public override bool DetectDoForIfWhile(LinkedListNode<OTStmt> link)
            {
                return forbody.DetectDoForIfWhile(link) |
                            ((forinit != null) && forinit.DetectDoForIfWhile(link)) |
                            ((forstep != null) && forstep.DetectDoForIfWhile(link));
            }

            /**
             * Assume we won't replace the for statement itself.
             * But search all our sub-ordinate statements.
             */
            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                forbody = (OTStmtBlock)forbody.ReplaceStatement(oldstmt, newstmt);
                if(forinit != null)
                    forinit = forinit.ReplaceStatement(oldstmt, newstmt);
                if(forstep != null)
                    forstep = forstep.ReplaceStatement(oldstmt, newstmt);
                return this;
            }

            public override void PrintStmt(TextWriter twout, string indent)
            {
                if(iswhile)
                {
                    twout.Write("while (");
                    if(fortest == null)
                    {
                        twout.Write("TRUE");
                    }
                    else
                    {
                        twout.Write(StripBrtrue(fortest).PrintableString);
                    }
                }
                else
                {
                    twout.Write("for (");
                    if(forinit != null)
                    {
                        forinit.PrintStmt(twout, indent + INDENT);
                    }
                    else
                    {
                        twout.Write(';');
                    }
                    if(fortest != null)
                    {
                        twout.Write(' ' + StripBrtrue(fortest).PrintableString);
                    }
                    twout.Write(';');
                    if(forstep != null)
                    {
                        StringWriter sw = new StringWriter();
                        sw.Write(' ');
                        forstep.PrintStmt(sw, indent + INDENT);
                        StringBuilder sb = sw.GetStringBuilder();
                        int sl = sb.Length;
                        if((sl > 0) && (sb[sl - 1] == ';'))
                            sb.Remove(--sl, 1);
                        twout.Write(sb.ToString());
                    }
                }

                twout.Write(") ");
                forbody.PrintStmt(twout, indent);
            }
        }

        /**
         * if/then/else block.
         */
        private class OTStmtIf: OTStmt
        {
            private OTOpnd testvalu;
            private OTStmt thenstmt;
            private OTStmt elsestmt;  // might be null

            /**
             * Try to detect a structured if statement.
             *
             *   if (condition) jump ifdone_<suffix>;   << link points here
             *      ... then body ...
             * @ifdone_<suffix>;
             *
             *   if (condition) jump ifelse_<suffix>;
             *      ... then body ...
             *   jump ifdone_<suffix>;  << optional if true body doesn't fall through
             * @ifelse_<suffix>;
             *      ... else body ...
             * @ifdone_<suffix>;
             */
            public static bool Detect(LinkedListNode<OTStmt> link)
            {
                OTStmtCond condstmt = (OTStmtCond)link.Value;
                if(!(condstmt.stmt is OTStmtJump))
                    return false;

                OTStmtJump jumpstmt = (OTStmtJump)condstmt.stmt;
                if(jumpstmt.label.name.StartsWith(_ifDone))
                {

                    // then-only if

                    // skip forward to find the ifdone_<suffix> label
                    // also save the intervening statements for the then body
                    OTStmtBlock thenbody;
                    LinkedListNode<OTStmt> donelink = ScanForLabel(link, jumpstmt.label, out thenbody);

                    // make sure we found matching label
                    if(donelink == null)
                        return false;

                    // replace the jump ifdone_<suffix> with the <then body>
                    OTStmtIf it = new OTStmtIf();
                    it.thenstmt = thenbody;

                    // replace the test value with the opposite
                    it.testvalu = OTOpndUnOp.Make(MyOp.Brfalse, condstmt.valu);
                    condstmt.valu = null;

                    // strip out the true body statements from the main code including the ifdone_<suffix> label
                    StripInterveningStatements(link, donelink);

                    // replace the simple conditional with the if/then/else block
                    link.Value = it;

                    // tell caller we changed something
                    return true;
                }

                if(jumpstmt.label.name.StartsWith(_ifElse))
                {
                    string suffix = jumpstmt.label.name.Substring(_ifElse.Length);

                    // if/then/else
                    OTStmtIf it = new OTStmtIf();

                    // skip forward to find the ifelse_<suffix> label
                    // also save the intervening statements for the true body
                    OTStmtBlock thenbody;
                    LinkedListNode<OTStmt> elselink = ScanForLabel(link, jumpstmt.label, out thenbody);

                    // make sure we found matching label
                    if(elselink != null)
                    {

                        // the last statement of the then body might be a jump ifdone_<suffix>
                        LinkedListNode<OTStmt> lastthenlink = thenbody.blkstmts.Last;
                        if((lastthenlink != null) && (lastthenlink.Value is OTStmtJump))
                        {
                            OTStmtJump jumpifdone = (OTStmtJump)lastthenlink.Value;
                            if(jumpifdone.label.name == _ifDone + suffix)
                            {

                                lastthenlink.List.Remove(lastthenlink);

                                // skip forward to find the ifdone_<suffix> label
                                // also save the intervening statements for the else body
                                OTStmtBlock elsebody;
                                LinkedListNode<OTStmt> donelink = ScanForLabel(elselink, jumpifdone.label, out elsebody);
                                if(donelink != null)
                                {

                                    // replace the jump ifdone_<suffix> with the <true body>
                                    it.thenstmt = thenbody;

                                    // save the else body as well
                                    it.elsestmt = elsebody;

                                    // replace the test value with the opposite
                                    it.testvalu = OTOpndUnOp.Make(MyOp.Brfalse, condstmt.valu);
                                    condstmt.valu = null;

                                    // strip out the true and else body statements from the main code including the ifdone_<suffix> label
                                    StripInterveningStatements(link, donelink);

                                    // replace the simple conditional with the if/then/else block
                                    link.Value = it;

                                    // tell caller we changed something
                                    return true;
                                }
                            }
                        }

                        // missing the jump _ifDone_<suffix>, so make it a simple if/then
                        //   if (condition) jump ifelse_<suffix>;   << link
                        //      ... then body ...                   << encapsulated in block thenbody
                        // @ifelse_<suffix>;                        << elselink
                        //      ... else body ...                   << still inline and leave it there
                        // @ifdone_<suffix>;                        << strip this out

                        // replace the jump ifelse_<suffix> with the <true body>
                        it.thenstmt = thenbody;

                        // replace the test value with the opposite
                        it.testvalu = OTOpndUnOp.Make(MyOp.Brfalse, condstmt.valu);
                        condstmt.valu = null;

                        // strip out the then body statements from the main code including the ifelse_<suffix> label
                        StripInterveningStatements(link, elselink);

                        // there's a dangling unused ifdone_<suffix> label ahead that has to be stripped
                        for(LinkedListNode<OTStmt> donelink = link; (donelink = donelink.Next) != null;)
                        {
                            if((donelink.Value is OTStmtLabel) && (((OTStmtLabel)donelink.Value).label.name == _ifDone + suffix))
                            {
                                donelink.List.Remove(donelink);
                                break;
                            }
                        }

                        // replace the simple conditional with the if/then/else block
                        link.Value = it;

                        // tell caller we changed something
                        return true;
                    }
                }

                return false;
            }

            private OTStmtIf()
            {
            }

            public override void CountRefs()
            {
                if(testvalu != null)
                    testvalu.CountRefs(false);
                if(thenstmt != null)
                    thenstmt.CountRefs();
                if(elsestmt != null)
                    elsestmt.CountRefs();
            }

            public override bool ReplaceOperand(OTOpnd oldopnd, OTOpnd newopnd)
            {
                bool rc = thenstmt.ReplaceOperand(oldopnd, newopnd);
                testvalu = testvalu.ReplaceOperand(oldopnd, newopnd, ref rc);
                return rc;
            }

            public override bool DetectDoForIfWhile(LinkedListNode<OTStmt> link)
            {
                return ((thenstmt != null) && thenstmt.DetectDoForIfWhile(link)) |
                       ((elsestmt != null) && elsestmt.DetectDoForIfWhile(link));
            }

            /**
             * Assume we won't replace the if statement itself.
             * But search all our sub-ordinate statements.
             */
            public override OTStmt ReplaceStatement(OTStmt oldstmt, OTStmt newstmt)
            {
                thenstmt = thenstmt.ReplaceStatement(oldstmt, newstmt);
                if(elsestmt != null)
                    elsestmt = elsestmt.ReplaceStatement(oldstmt, newstmt);
                return this;
            }

            public override void PrintStmt(TextWriter twout, string indent)
            {
                twout.Write("if (" + StripBrtrue(testvalu).PrintableString + ") ");
                OTStmt thenst = ReduceStmtBody(thenstmt, false);
                thenst.PrintStmt(twout, indent);
                if(elsestmt != null)
                {
                    twout.Write('\n' + indent + "else ");
                    OTStmt elsest = ReduceStmtBody(elsestmt, true);
                    elsest.PrintStmt(twout, indent);
                }
            }

            // strip block off a single jump so it prints inline instead of with braces around it
            // also, if this is part of else, strip block for ifs to make else if statement
            private static OTStmt ReduceStmtBody(OTStmt statement, bool stripif)
            {
                OTStmt onestmt = statement;
                if((onestmt is OTStmtBlock) && (((OTStmtBlock)onestmt).blkstmts.Count == 1))
                {
                    onestmt = ((OTStmtBlock)onestmt).blkstmts.First.Value;
                    if((onestmt is OTStmtJump) || (stripif && (onestmt is OTStmtIf)))
                    {
                        return onestmt;
                    }
                }
                return statement;
            }

            /**
             * Scan forward for a given label definition.
             * Put intervening statements in a statement block.
             * @param link = start scanning after this statement
             * @param label = look for this label definition
             * @param block = where to return intervening statement block
             * @returns null: label definition not found
             *          else: label definition statement
             */
            private static LinkedListNode<OTStmt> ScanForLabel(LinkedListNode<OTStmt> link,
                        OTLabel label, out OTStmtBlock block)
            {
                block = new OTStmtBlock();
                while((link = link.Next) != null)
                {
                    if(link.Value is OTStmtLabel)
                    {
                        if(((OTStmtLabel)link.Value).label == label)
                            break;
                    }
                    block.blkstmts.AddLast(link.Value);
                }
                return link;
            }

            /**
             * Strip statements after link up to and including donelink.
             */
            private static void StripInterveningStatements(LinkedListNode<OTStmt> link, LinkedListNode<OTStmt> donelink)
            {
                LinkedListNode<OTStmt> striplink;
                do
                {
                    striplink = link.Next;
                    striplink.List.Remove(striplink);
                } while(striplink != donelink);
            }
        }

        private class MyOp
        {
            public int index;
            public OpCode sysop;
            public string name;
            public string source;

            private static Dictionary<string, MyOp> myopsbyname = new Dictionary<string, MyOp>();
            private static int nextindex = 0;

            public MyOp(OpCode sysop)
            {
                this.index = nextindex++;
                this.sysop = sysop;
                this.name = sysop.Name;
                myopsbyname.Add(name, this);
            }

            public MyOp(OpCode sysop, string source)
            {
                this.index = nextindex++;
                this.sysop = sysop;
                this.name = sysop.Name;
                this.source = source;
                myopsbyname.Add(name, this);
            }

            public MyOp(string name)
            {
                this.index = nextindex++;
                this.name = name;
                myopsbyname.Add(name, this);
            }

            public MyOp(string name, string source)
            {
                this.index = nextindex++;
                this.name = name;
                this.source = source;
                myopsbyname.Add(name, this);
            }

            public static MyOp GetByName(string name)
            {
                return myopsbyname[name];
            }

            public override string ToString()
            {
                return name;
            }

            // these copied from OpCodes.cs
            public static readonly MyOp Nop = new MyOp(OpCodes.Nop);
            public static readonly MyOp Break = new MyOp(OpCodes.Break);
            public static readonly MyOp Ldarg_0 = new MyOp(OpCodes.Ldarg_0);
            public static readonly MyOp Ldarg_1 = new MyOp(OpCodes.Ldarg_1);
            public static readonly MyOp Ldarg_2 = new MyOp(OpCodes.Ldarg_2);
            public static readonly MyOp Ldarg_3 = new MyOp(OpCodes.Ldarg_3);
            public static readonly MyOp Ldloc_0 = new MyOp(OpCodes.Ldloc_0);
            public static readonly MyOp Ldloc_1 = new MyOp(OpCodes.Ldloc_1);
            public static readonly MyOp Ldloc_2 = new MyOp(OpCodes.Ldloc_2);
            public static readonly MyOp Ldloc_3 = new MyOp(OpCodes.Ldloc_3);
            public static readonly MyOp Stloc_0 = new MyOp(OpCodes.Stloc_0);
            public static readonly MyOp Stloc_1 = new MyOp(OpCodes.Stloc_1);
            public static readonly MyOp Stloc_2 = new MyOp(OpCodes.Stloc_2);
            public static readonly MyOp Stloc_3 = new MyOp(OpCodes.Stloc_3);
            public static readonly MyOp Ldarg_S = new MyOp(OpCodes.Ldarg_S);
            public static readonly MyOp Ldarga_S = new MyOp(OpCodes.Ldarga_S);
            public static readonly MyOp Starg_S = new MyOp(OpCodes.Starg_S);
            public static readonly MyOp Ldloc_S = new MyOp(OpCodes.Ldloc_S);
            public static readonly MyOp Ldloca_S = new MyOp(OpCodes.Ldloca_S);
            public static readonly MyOp Stloc_S = new MyOp(OpCodes.Stloc_S);
            public static readonly MyOp Ldnull = new MyOp(OpCodes.Ldnull);
            public static readonly MyOp Ldc_I4_M1 = new MyOp(OpCodes.Ldc_I4_M1);
            public static readonly MyOp Ldc_I4_0 = new MyOp(OpCodes.Ldc_I4_0);
            public static readonly MyOp Ldc_I4_1 = new MyOp(OpCodes.Ldc_I4_1);
            public static readonly MyOp Ldc_I4_2 = new MyOp(OpCodes.Ldc_I4_2);
            public static readonly MyOp Ldc_I4_3 = new MyOp(OpCodes.Ldc_I4_3);
            public static readonly MyOp Ldc_I4_4 = new MyOp(OpCodes.Ldc_I4_4);
            public static readonly MyOp Ldc_I4_5 = new MyOp(OpCodes.Ldc_I4_5);
            public static readonly MyOp Ldc_I4_6 = new MyOp(OpCodes.Ldc_I4_6);
            public static readonly MyOp Ldc_I4_7 = new MyOp(OpCodes.Ldc_I4_7);
            public static readonly MyOp Ldc_I4_8 = new MyOp(OpCodes.Ldc_I4_8);
            public static readonly MyOp Ldc_I4_S = new MyOp(OpCodes.Ldc_I4_S);
            public static readonly MyOp Ldc_I4 = new MyOp(OpCodes.Ldc_I4);
            public static readonly MyOp Ldc_I8 = new MyOp(OpCodes.Ldc_I8);
            public static readonly MyOp Ldc_R4 = new MyOp(OpCodes.Ldc_R4);
            public static readonly MyOp Ldc_R8 = new MyOp(OpCodes.Ldc_R8);
            public static readonly MyOp Dup = new MyOp(OpCodes.Dup);
            public static readonly MyOp Pop = new MyOp(OpCodes.Pop);
            public static readonly MyOp Jmp = new MyOp(OpCodes.Jmp);
            public static readonly MyOp Call = new MyOp(OpCodes.Call);
            public static readonly MyOp Calli = new MyOp(OpCodes.Calli);
            public static readonly MyOp Ret = new MyOp(OpCodes.Ret);
            public static readonly MyOp Br_S = new MyOp(OpCodes.Br_S);
            public static readonly MyOp Brfalse_S = new MyOp(OpCodes.Brfalse_S);
            public static readonly MyOp Brtrue_S = new MyOp(OpCodes.Brtrue_S);
            public static readonly MyOp Beq_S = new MyOp(OpCodes.Beq_S, "==");
            public static readonly MyOp Bge_S = new MyOp(OpCodes.Bge_S, ">=");
            public static readonly MyOp Bgt_S = new MyOp(OpCodes.Bgt_S, ">");
            public static readonly MyOp Ble_S = new MyOp(OpCodes.Ble_S, "<=");
            public static readonly MyOp Blt_S = new MyOp(OpCodes.Blt_S, "<");
            public static readonly MyOp Bne_Un_S = new MyOp(OpCodes.Bne_Un_S, "!=");
            public static readonly MyOp Bge_Un_S = new MyOp(OpCodes.Bge_Un_S);
            public static readonly MyOp Bgt_Un_S = new MyOp(OpCodes.Bgt_Un_S);
            public static readonly MyOp Ble_Un_S = new MyOp(OpCodes.Ble_Un_S);
            public static readonly MyOp Blt_Un_S = new MyOp(OpCodes.Blt_Un_S);
            public static readonly MyOp Br = new MyOp(OpCodes.Br);
            public static readonly MyOp Brfalse = new MyOp(OpCodes.Brfalse, "!");
            public static readonly MyOp Brtrue = new MyOp(OpCodes.Brtrue, "!!");
            public static readonly MyOp Beq = new MyOp(OpCodes.Beq, "==");
            public static readonly MyOp Bge = new MyOp(OpCodes.Bge, ">=");
            public static readonly MyOp Bgt = new MyOp(OpCodes.Bgt, ">");
            public static readonly MyOp Ble = new MyOp(OpCodes.Ble, "<=");
            public static readonly MyOp Blt = new MyOp(OpCodes.Blt, "<");
            public static readonly MyOp Bne_Un = new MyOp(OpCodes.Bne_Un, "!=");
            public static readonly MyOp Bge_Un = new MyOp(OpCodes.Bge_Un);
            public static readonly MyOp Bgt_Un = new MyOp(OpCodes.Bgt_Un);
            public static readonly MyOp Ble_Un = new MyOp(OpCodes.Ble_Un);
            public static readonly MyOp Blt_Un = new MyOp(OpCodes.Blt_Un);
            public static readonly MyOp Switch = new MyOp(OpCodes.Switch);
            public static readonly MyOp Ldind_I1 = new MyOp(OpCodes.Ldind_I1);
            public static readonly MyOp Ldind_U1 = new MyOp(OpCodes.Ldind_U1);
            public static readonly MyOp Ldind_I2 = new MyOp(OpCodes.Ldind_I2);
            public static readonly MyOp Ldind_U2 = new MyOp(OpCodes.Ldind_U2);
            public static readonly MyOp Ldind_I4 = new MyOp(OpCodes.Ldind_I4);
            public static readonly MyOp Ldind_U4 = new MyOp(OpCodes.Ldind_U4);
            public static readonly MyOp Ldind_I8 = new MyOp(OpCodes.Ldind_I8);
            public static readonly MyOp Ldind_I = new MyOp(OpCodes.Ldind_I);
            public static readonly MyOp Ldind_R4 = new MyOp(OpCodes.Ldind_R4);
            public static readonly MyOp Ldind_R8 = new MyOp(OpCodes.Ldind_R8);
            public static readonly MyOp Ldind_Ref = new MyOp(OpCodes.Ldind_Ref);
            public static readonly MyOp Stind_Ref = new MyOp(OpCodes.Stind_Ref);
            public static readonly MyOp Stind_I1 = new MyOp(OpCodes.Stind_I1);
            public static readonly MyOp Stind_I2 = new MyOp(OpCodes.Stind_I2);
            public static readonly MyOp Stind_I4 = new MyOp(OpCodes.Stind_I4);
            public static readonly MyOp Stind_I8 = new MyOp(OpCodes.Stind_I8);
            public static readonly MyOp Stind_R4 = new MyOp(OpCodes.Stind_R4);
            public static readonly MyOp Stind_R8 = new MyOp(OpCodes.Stind_R8);
            public static readonly MyOp Add = new MyOp(OpCodes.Add, "+");
            public static readonly MyOp Sub = new MyOp(OpCodes.Sub, "-");
            public static readonly MyOp Mul = new MyOp(OpCodes.Mul, "*");
            public static readonly MyOp Div = new MyOp(OpCodes.Div, "/");
            public static readonly MyOp Div_Un = new MyOp(OpCodes.Div_Un);
            public static readonly MyOp Rem = new MyOp(OpCodes.Rem, "%");
            public static readonly MyOp Rem_Un = new MyOp(OpCodes.Rem_Un);
            public static readonly MyOp And = new MyOp(OpCodes.And, "&");
            public static readonly MyOp Or = new MyOp(OpCodes.Or, "|");
            public static readonly MyOp Xor = new MyOp(OpCodes.Xor, "^");
            public static readonly MyOp Shl = new MyOp(OpCodes.Shl, "<<");
            public static readonly MyOp Shr = new MyOp(OpCodes.Shr, ">>");
            public static readonly MyOp Shr_Un = new MyOp(OpCodes.Shr_Un);
            public static readonly MyOp Neg = new MyOp(OpCodes.Neg, "-");
            public static readonly MyOp Not = new MyOp(OpCodes.Not, "~");
            public static readonly MyOp Conv_I1 = new MyOp(OpCodes.Conv_I1);
            public static readonly MyOp Conv_I2 = new MyOp(OpCodes.Conv_I2);
            public static readonly MyOp Conv_I4 = new MyOp(OpCodes.Conv_I4);
            public static readonly MyOp Conv_I8 = new MyOp(OpCodes.Conv_I8);
            public static readonly MyOp Conv_R4 = new MyOp(OpCodes.Conv_R4);
            public static readonly MyOp Conv_R8 = new MyOp(OpCodes.Conv_R8);
            public static readonly MyOp Conv_U4 = new MyOp(OpCodes.Conv_U4);
            public static readonly MyOp Conv_U8 = new MyOp(OpCodes.Conv_U8);
            public static readonly MyOp Callvirt = new MyOp(OpCodes.Callvirt);
            public static readonly MyOp Cpobj = new MyOp(OpCodes.Cpobj);
            public static readonly MyOp Ldobj = new MyOp(OpCodes.Ldobj);
            public static readonly MyOp Ldstr = new MyOp(OpCodes.Ldstr);
            public static readonly MyOp Newobj = new MyOp(OpCodes.Newobj);
            public static readonly MyOp Castclass = new MyOp(OpCodes.Castclass);
            public static readonly MyOp Isinst = new MyOp(OpCodes.Isinst);
            public static readonly MyOp Conv_R_Un = new MyOp(OpCodes.Conv_R_Un);
            public static readonly MyOp Unbox = new MyOp(OpCodes.Unbox);
            public static readonly MyOp Throw = new MyOp(OpCodes.Throw);
            public static readonly MyOp Ldfld = new MyOp(OpCodes.Ldfld);
            public static readonly MyOp Ldflda = new MyOp(OpCodes.Ldflda);
            public static readonly MyOp Stfld = new MyOp(OpCodes.Stfld);
            public static readonly MyOp Ldsfld = new MyOp(OpCodes.Ldsfld);
            public static readonly MyOp Ldsflda = new MyOp(OpCodes.Ldsflda);
            public static readonly MyOp Stsfld = new MyOp(OpCodes.Stsfld);
            public static readonly MyOp Stobj = new MyOp(OpCodes.Stobj);
            public static readonly MyOp Conv_Ovf_I1_Un = new MyOp(OpCodes.Conv_Ovf_I1_Un);
            public static readonly MyOp Conv_Ovf_I2_Un = new MyOp(OpCodes.Conv_Ovf_I2_Un);
            public static readonly MyOp Conv_Ovf_I4_Un = new MyOp(OpCodes.Conv_Ovf_I4_Un);
            public static readonly MyOp Conv_Ovf_I8_Un = new MyOp(OpCodes.Conv_Ovf_I8_Un);
            public static readonly MyOp Conv_Ovf_U1_Un = new MyOp(OpCodes.Conv_Ovf_U1_Un);
            public static readonly MyOp Conv_Ovf_U2_Un = new MyOp(OpCodes.Conv_Ovf_U2_Un);
            public static readonly MyOp Conv_Ovf_U4_Un = new MyOp(OpCodes.Conv_Ovf_U4_Un);
            public static readonly MyOp Conv_Ovf_U8_Un = new MyOp(OpCodes.Conv_Ovf_U8_Un);
            public static readonly MyOp Conv_Ovf_I_Un = new MyOp(OpCodes.Conv_Ovf_I_Un);
            public static readonly MyOp Conv_Ovf_U_Un = new MyOp(OpCodes.Conv_Ovf_U_Un);
            public static readonly MyOp Box = new MyOp(OpCodes.Box);
            public static readonly MyOp Newarr = new MyOp(OpCodes.Newarr);
            public static readonly MyOp Ldlen = new MyOp(OpCodes.Ldlen);
            public static readonly MyOp Ldelema = new MyOp(OpCodes.Ldelema);
            public static readonly MyOp Ldelem_I1 = new MyOp(OpCodes.Ldelem_I1);
            public static readonly MyOp Ldelem_U1 = new MyOp(OpCodes.Ldelem_U1);
            public static readonly MyOp Ldelem_I2 = new MyOp(OpCodes.Ldelem_I2);
            public static readonly MyOp Ldelem_U2 = new MyOp(OpCodes.Ldelem_U2);
            public static readonly MyOp Ldelem_I4 = new MyOp(OpCodes.Ldelem_I4);
            public static readonly MyOp Ldelem_U4 = new MyOp(OpCodes.Ldelem_U4);
            public static readonly MyOp Ldelem_I8 = new MyOp(OpCodes.Ldelem_I8);
            public static readonly MyOp Ldelem_I = new MyOp(OpCodes.Ldelem_I);
            public static readonly MyOp Ldelem_R4 = new MyOp(OpCodes.Ldelem_R4);
            public static readonly MyOp Ldelem_R8 = new MyOp(OpCodes.Ldelem_R8);
            public static readonly MyOp Ldelem_Ref = new MyOp(OpCodes.Ldelem_Ref);
            public static readonly MyOp Stelem_I = new MyOp(OpCodes.Stelem_I);
            public static readonly MyOp Stelem_I1 = new MyOp(OpCodes.Stelem_I1);
            public static readonly MyOp Stelem_I2 = new MyOp(OpCodes.Stelem_I2);
            public static readonly MyOp Stelem_I4 = new MyOp(OpCodes.Stelem_I4);
            public static readonly MyOp Stelem_I8 = new MyOp(OpCodes.Stelem_I8);
            public static readonly MyOp Stelem_R4 = new MyOp(OpCodes.Stelem_R4);
            public static readonly MyOp Stelem_R8 = new MyOp(OpCodes.Stelem_R8);
            public static readonly MyOp Stelem_Ref = new MyOp(OpCodes.Stelem_Ref);
            public static readonly MyOp Ldelem = new MyOp(OpCodes.Ldelem);
            public static readonly MyOp Stelem = new MyOp(OpCodes.Stelem);
            public static readonly MyOp Unbox_Any = new MyOp(OpCodes.Unbox_Any);
            public static readonly MyOp Conv_Ovf_I1 = new MyOp(OpCodes.Conv_Ovf_I1);
            public static readonly MyOp Conv_Ovf_U1 = new MyOp(OpCodes.Conv_Ovf_U1);
            public static readonly MyOp Conv_Ovf_I2 = new MyOp(OpCodes.Conv_Ovf_I2);
            public static readonly MyOp Conv_Ovf_U2 = new MyOp(OpCodes.Conv_Ovf_U2);
            public static readonly MyOp Conv_Ovf_I4 = new MyOp(OpCodes.Conv_Ovf_I4);
            public static readonly MyOp Conv_Ovf_U4 = new MyOp(OpCodes.Conv_Ovf_U4);
            public static readonly MyOp Conv_Ovf_I8 = new MyOp(OpCodes.Conv_Ovf_I8);
            public static readonly MyOp Conv_Ovf_U8 = new MyOp(OpCodes.Conv_Ovf_U8);
            public static readonly MyOp Refanyval = new MyOp(OpCodes.Refanyval);
            public static readonly MyOp Ckfinite = new MyOp(OpCodes.Ckfinite);
            public static readonly MyOp Mkrefany = new MyOp(OpCodes.Mkrefany);
            public static readonly MyOp Ldtoken = new MyOp(OpCodes.Ldtoken);
            public static readonly MyOp Conv_U2 = new MyOp(OpCodes.Conv_U2);
            public static readonly MyOp Conv_U1 = new MyOp(OpCodes.Conv_U1);
            public static readonly MyOp Conv_I = new MyOp(OpCodes.Conv_I);
            public static readonly MyOp Conv_Ovf_I = new MyOp(OpCodes.Conv_Ovf_I);
            public static readonly MyOp Conv_Ovf_U = new MyOp(OpCodes.Conv_Ovf_U);
            public static readonly MyOp Add_Ovf = new MyOp(OpCodes.Add_Ovf);
            public static readonly MyOp Add_Ovf_Un = new MyOp(OpCodes.Add_Ovf_Un);
            public static readonly MyOp Mul_Ovf = new MyOp(OpCodes.Mul_Ovf);
            public static readonly MyOp Mul_Ovf_Un = new MyOp(OpCodes.Mul_Ovf_Un);
            public static readonly MyOp Sub_Ovf = new MyOp(OpCodes.Sub_Ovf);
            public static readonly MyOp Sub_Ovf_Un = new MyOp(OpCodes.Sub_Ovf_Un);
            public static readonly MyOp Endfinally = new MyOp(OpCodes.Endfinally);
            public static readonly MyOp Leave = new MyOp(OpCodes.Leave);
            public static readonly MyOp Leave_S = new MyOp(OpCodes.Leave_S);
            public static readonly MyOp Stind_I = new MyOp(OpCodes.Stind_I);
            public static readonly MyOp Conv_U = new MyOp(OpCodes.Conv_U);
            public static readonly MyOp Prefix7 = new MyOp(OpCodes.Prefix7);
            public static readonly MyOp Prefix6 = new MyOp(OpCodes.Prefix6);
            public static readonly MyOp Prefix5 = new MyOp(OpCodes.Prefix5);
            public static readonly MyOp Prefix4 = new MyOp(OpCodes.Prefix4);
            public static readonly MyOp Prefix3 = new MyOp(OpCodes.Prefix3);
            public static readonly MyOp Prefix2 = new MyOp(OpCodes.Prefix2);
            public static readonly MyOp Prefix1 = new MyOp(OpCodes.Prefix1);
            public static readonly MyOp Prefixref = new MyOp(OpCodes.Prefixref);
            public static readonly MyOp Arglist = new MyOp(OpCodes.Arglist);
            public static readonly MyOp Ceq = new MyOp(OpCodes.Ceq, "==");
            public static readonly MyOp Cgt = new MyOp(OpCodes.Cgt, ">");
            public static readonly MyOp Cgt_Un = new MyOp(OpCodes.Cgt_Un);
            public static readonly MyOp Clt = new MyOp(OpCodes.Clt, "<");
            public static readonly MyOp Clt_Un = new MyOp(OpCodes.Clt_Un);
            public static readonly MyOp Ldftn = new MyOp(OpCodes.Ldftn);
            public static readonly MyOp Ldvirtftn = new MyOp(OpCodes.Ldvirtftn);
            public static readonly MyOp Ldarg = new MyOp(OpCodes.Ldarg);
            public static readonly MyOp Ldarga = new MyOp(OpCodes.Ldarga);
            public static readonly MyOp Starg = new MyOp(OpCodes.Starg);
            public static readonly MyOp Ldloc = new MyOp(OpCodes.Ldloc);
            public static readonly MyOp Ldloca = new MyOp(OpCodes.Ldloca);
            public static readonly MyOp Stloc = new MyOp(OpCodes.Stloc);
            public static readonly MyOp Localloc = new MyOp(OpCodes.Localloc);
            public static readonly MyOp Endfilter = new MyOp(OpCodes.Endfilter);
            public static readonly MyOp Unaligned = new MyOp(OpCodes.Unaligned);
            public static readonly MyOp Volatile = new MyOp(OpCodes.Volatile);
            public static readonly MyOp Tailcall = new MyOp(OpCodes.Tailcall);
            public static readonly MyOp Initobj = new MyOp(OpCodes.Initobj);
            public static readonly MyOp Constrained = new MyOp(OpCodes.Constrained);
            public static readonly MyOp Cpblk = new MyOp(OpCodes.Cpblk);
            public static readonly MyOp Initblk = new MyOp(OpCodes.Initblk);
            public static readonly MyOp Rethrow = new MyOp(OpCodes.Rethrow);
            public static readonly MyOp Sizeof = new MyOp(OpCodes.Sizeof);
            public static readonly MyOp Refanytype = new MyOp(OpCodes.Refanytype);
            public static readonly MyOp Readonly = new MyOp(OpCodes.Readonly);

            // used internally
            public static readonly MyOp Cge = new MyOp("cge", ">=");
            public static readonly MyOp Cle = new MyOp("cle", "<=");
            public static readonly MyOp Cne = new MyOp("cne", "!=");
        }
    }
}
