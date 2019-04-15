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


/**
 * @brief Wrapper class for ScriptMyILGen to do simple optimizations.
 *        The main one is to figure out which locals are active at the labels
 *        so the stack capture/restore code doesn't have to do everything.
 *        Second is it removes unnecessary back-to-back stloc/ldloc's.
 */

namespace OpenSim.Region.ScriptEngine.Yengine
{
    /**
     * @brief This is a list that keeps track of types pushed on the evaluation stack.
     */
    public class StackDepth: List<Type>
    {
        public List<bool> isBoxeds = new List<bool>();

        /**
         * @brief Clear both stacks.
         */
        public new void Clear()
        {
            base.Clear();
            isBoxeds.Clear();
        }

        /**
         * @brief Pop call parameters and validate the types.
         */
        public void Pop(ParameterInfo[] pis)
        {
            int n = pis.Length;
            int c = this.Count;
            if(n > c)
                throw new Exception("stack going negative");
            for(int i = n; --i >= 0;)
            {
                --c;
                ExpectedVsOnStack(pis[i].ParameterType, this[c], isBoxeds[c]);
            }
            Pop(n);
        }

        /**
         * @brief Pop values and validate the types.
         */
        public void Pop(Type[] ts)
        {
            int n = ts.Length;
            int c = this.Count;
            if(n > c)
                throw new Exception("stack going negative");
            for(int i = ts.Length; --i >= 0;)
            {
                --c;
                ExpectedVsOnStack(ts[i], this[c], isBoxeds[c]);
            }
            Pop(n);
        }

        /**
         * @brief Pop a single value and validate the type.
         */
        public void Pop(Type t)
        {
            int c = this.Count;
            if(c < 1)
                throw new Exception("stack going negative");
            ExpectedVsOnStack(t, this[c - 1], isBoxeds[c - 1]);
            Pop(1);
        }

        /**
         * @brief Pop a single value and validate that it is a numeric type.
         */
        public Type PopNumVal()
        {
            int c = this.Count;
            if(c < 1)
                throw new Exception("stack going negative");
            Type st = this[--c];
            if(st == null)
            {
                throw new Exception("stack has null, expecting a numeric");
            }
            if(isBoxeds[c])
            {
                throw new Exception("stack is boxed " + st.Name + ", expecting a numeric");
            }
            if((st != typeof(bool)) && (st != typeof(char)) && (st != typeof(int)) &&
                (st != typeof(long)) && (st != typeof(float)) && (st != typeof(double)))
            {
                throw new Exception("stack has " + st.Name + ", expecting a numeric");
            }
            return Pop(1);
        }

        /**
         * @brief Pop a single value and validate that it is a reference type
         */
        public Type PopRef()
        {
            int c = this.Count;
            if(c < 1)
                throw new Exception("stack going negative");
            Type st = this[--c];
            if((st != null) && !isBoxeds[c] && st.IsValueType)
            {
                throw new Exception("stack has " + st.Name + ", expecting a ref type");
            }
            return Pop(1);
        }

        /**
         * @brief Pop a single value and validate that it is a value type
         */
        public Type PopValue()
        {
            int c = this.Count;
            if(c < 1)
                throw new Exception("stack going negative");
            Type st = this[--c];
            if(st == null)
            {
                throw new Exception("stack has null, expecting a value type");
            }
            if(!st.IsValueType)
            {
                throw new Exception("stack has " + st.Name + ", expecting a value type");
            }
            if(isBoxeds[c])
            {
                throw new Exception("stack has boxed " + st.Name + ", expecting an unboxed value type");
            }
            return Pop(1);
        }

        // ex = what is expected to be on stack
        // st = what is actually on stack (null for ldnull)
        // stBoxed = stack value is boxed
        public static void ExpectedVsOnStack(Type ex, Type st, bool stBoxed)
        {
            // ldnull pushed on stack can go into any pointer type
            if(st == null)
            {
                if(ex.IsByRef || ex.IsPointer || ex.IsClass || ex.IsInterface)
                    return;
                throw new Exception("stack has null, expect " + ex.Name);
            }

            // simple case of expecting an object
            // ...so the stack can have object,string, etc
            // but we cant allow int = boxed int here
            if(ex.IsAssignableFrom(st) && !stBoxed)
                return;

            // case of expecting an enum on the stack
            // but all the CIL code knows about are ints etc
            // so convert the Enum type to integer or whatever
            // and that should be assignable from what's on stack
            if(ex.IsEnum && typeof(int).IsAssignableFrom(st))
                return;

            // bool, char, int are interchangeable on the stack
            if((ex == typeof(bool) || ex == typeof(char) || ex == typeof(int)) &&
                (st == typeof(bool) || st == typeof(char) || st == typeof(int)))
                return;

            // float and double are interchangeable on the stack
            if((ex == typeof(float) || ex == typeof(double)) &&
                (st == typeof(float) || st == typeof(double)))
                return;

            // object can accept any boxed type
            if((ex == typeof(object)) && stBoxed)
                return;

            // otherwise, it is disallowed
            throw new Exception("stack has " + StackTypeString(st, stBoxed) + ", expect " + ex.Name);
        }

        /**
         * @brief Pop values without any validation.
         */
        public Type Pop(int n)
        {
            if(this.Count != isBoxeds.Count)
                throw new Exception("isBoxeds count bad");
            Type lastPopped = null;
            int c = this.Count;
            if(n > c)
                throw new Exception("stack going negative");
            if(n > 0)
            {
                lastPopped = this[c - n];
                this.RemoveRange(c - n, n);
                isBoxeds.RemoveRange(c - n, n);
            }
            if(this.Count != isBoxeds.Count)
                throw new Exception("isBoxeds count bad");
            return lastPopped;
        }

        /**
         * @brief Peek at the n'th stack value.
         *        n = 0 : top of stack
         *            1 : next to top
         *                ...
         */
        public Type Peek(int n)
        {
            int c = this.Count;
            if(n > c - 1)
                throw new Exception("stack going negative");
            if(this.Count != isBoxeds.Count)
                throw new Exception("isBoxeds count bad");
            return this[c - n - 1];
        }
        public bool PeekBoxed(int n)
        {
            int c = isBoxeds.Count;
            if(n > c - 1)
                throw new Exception("stack going negative");
            if(this.Count != isBoxeds.Count)
                throw new Exception("isBoxeds count bad");
            return isBoxeds[c - n - 1];
        }

        /**
         * @brief Push a single value of the given type.
         */
        public void Push(Type t)
        {
            Push(t, false);
        }
        public void Push(Type t, bool isBoxed)
        {
            if(this.Count != isBoxeds.Count)
                throw new Exception("isBoxeds count bad");
            this.Add(t);
            isBoxeds.Add(isBoxed);
        }

        /**
         * @brief See if the types at a given label exactly match those on the stack.
         *        We should have the stack types be the same no matter how we branched 
         *        or fell through to a particular label.
         */
        public void Matches(ScriptMyLabel label)
        {
            Type[] ts = label.stackDepth;
            bool[] tsBoxeds = label.stackBoxeds;
            int i;

            if(this.Count != isBoxeds.Count)
                throw new Exception("isBoxeds count bad");

            if(ts == null)
            {
                label.stackDepth = this.ToArray();
                label.stackBoxeds = isBoxeds.ToArray();
            }
            else if(ts.Length != this.Count)
            {
                throw new Exception("stack depth mismatch");
            }
            else
            {
                for(i = this.Count; --i >= 0;)
                {
                    if(tsBoxeds[i] != this.isBoxeds[i])
                        goto mismatch;
                    if(ts[i] == this[i])
                        continue;
                    if((ts[i] == typeof(bool) || ts[i] == typeof(char) || ts[i] == typeof(int)) &&
                        (this[i] == typeof(bool) || this[i] == typeof(char) || this[i] == typeof(int)))
                        continue;
                    if((ts[i] == typeof(double) || ts[i] == typeof(float)) &&
                        (this[i] == typeof(double) || this[i] == typeof(float)))
                        continue;
                    goto mismatch;
                }
            }
            return;
            mismatch:
            throw new Exception("stack type mismatch: " + StackTypeString(ts[i], tsBoxeds[i]) + " vs " + StackTypeString(this[i], this.isBoxeds[i]));
        }

        private static string StackTypeString(Type ts, bool isBoxed)
        {
            if(!isBoxed)
                return ts.Name;
            return "[" + ts.Name + "]";
        }
    }

    /**
     * @brief One of these per opcode and label in the function plus other misc markers.
     *        They form the CIL instruction stream of the function.
     */
    public abstract class GraphNode
    {
        private static readonly bool DEBUG = false;

        public const int OPINDENT = 4;
        public const int OPDEBLEN = 12;

        public ScriptCollector coll;
        public GraphNodeBeginExceptionBlock tryBlock;  // start of enclosing try block
                                                       // valid in the try section
                                                       // null in the catch/finally sections
                                                       // null outside of try block
                                                       // for the try node itself, links to outer try block
        public GraphNodeBeginExceptionBlock excBlock;  // start of enclosing try block
                                                       // valid in the try/catch/finally sections
                                                       // null outside of try/catch/finally block
                                                       // for the try node itself, links to outer try block

        /*
         * List of nodes in order as originally given.
         */
        public GraphNode nextLin, prevLin;
        public int linSeqNo;

        /**
         * @brief Save pointer to collector.
         */
        public GraphNode(ScriptCollector coll)
        {
            this.coll = coll;
        }

        /**
         * @brief Chain graph node to end of linear list.
         */
        public virtual void ChainLin()
        {
            coll.lastLin.nextLin = this;
            this.prevLin = coll.lastLin;
            coll.lastLin = this;
            this.tryBlock = coll.curTryBlock;
            this.excBlock = coll.curExcBlock;

            if(DEBUG)
            {
                StringBuilder sb = new StringBuilder("ChainLin*:");
                sb.Append(coll.stackDepth.Count.ToString("D2"));
                sb.Append(' ');
                this.DebString(sb);
                Console.WriteLine(sb.ToString());
            }
        }

        /**
         * @brief Append full info to debugging string for printing out the instruction.
         */
        public void DebStringExt(StringBuilder sb)
        {
            int x = sb.Length;
            sb.Append(this.linSeqNo.ToString().PadLeft(5));
            sb.Append(": ");
            this.DebString(sb);

            if(this.ReadsLocal() != null)
                ScriptCollector.PadToLength(sb, x + 60, " [read]");
            if(this.WritesLocal() != null)
                ScriptCollector.PadToLength(sb, x + 68, " [write]");
            ScriptCollector.PadToLength(sb, x + 72, " ->");
            bool first = true;
            foreach(GraphNode nn in this.NextNodes)
            {
                if(first)
                {
                    sb.Append(nn.linSeqNo.ToString().PadLeft(5));
                    first = false;
                }
                else
                {
                    sb.Append(',');
                    sb.Append(nn.linSeqNo);
                }
            }
        }

        /**
         * @brief See if it's possible for it to fall through to the next inline (nextLin) instruction.
         */
        public virtual bool CanFallThrough()
        {
            return true;
        }

        /**
         * @brief Append to debugging string for printing out the instruction.
         */
        public abstract void DebString(StringBuilder sb);
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            this.DebString(sb);
            return sb.ToString();
        }

        /**
         * @brief See if this instruction reads a local variable.
         */
        public virtual ScriptMyLocal ReadsLocal()
        {
            return null;
        }

        /**
         * @brief See if this instruction writes a local variable.
         */
        public virtual ScriptMyLocal WritesLocal()
        {
            return null;
        }

        /**
         * @brief Write this instruction out to the wrapped object file.
         */
        public abstract void WriteOutOne(ScriptMyILGen ilGen);

        /**
         * @brief Iterate through all the possible next nodes, including the next inline node, if any.
         *        The next inline code is excluded if the instruction never falls through, eg, return, unconditional branch.
         *        It includes a possible conditional branch to the beginning of the corresponding catch/finally of every 
         *        instruction in a try section.
         */
        private System.Collections.Generic.IEnumerable<GraphNode> nextNodes, nextNodesCatchFinally;
        public System.Collections.Generic.IEnumerable<GraphNode> NextNodes
        {
            get
            {
                if(nextNodes == null)
                {
                    nextNodes = GetNNEnumerable();
                    nextNodesCatchFinally = new NNEnumerableCatchFinally(this);
                }
                return nextNodesCatchFinally;
            }
        }

        /**
         * @brief This acts as a wrapper around all the other NNEnumerable's below.
         *        It assumes every instruction in a try { } can throw an exception so it 
         *        says that every instruction in a try { } can conditionally branch to 
         *        the beginning of the corresponding catch { } or finally { }.
         */
        private class NNEnumerableCatchFinally: System.Collections.Generic.IEnumerable<GraphNode>
        {
            private GraphNode gn;
            public NNEnumerableCatchFinally(GraphNode gn)
            {
                this.gn = gn;
            }
            System.Collections.Generic.IEnumerator<GraphNode> System.Collections.Generic.IEnumerable<GraphNode>.GetEnumerator()
            {
                return new NNEnumeratorCatchFinally(gn);
            }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return new NNEnumeratorCatchFinally(gn);
            }
        }
        private class NNEnumeratorCatchFinally: NNEnumeratorBase
        {
            private GraphNode gn;
            private int index = 0;
            private System.Collections.Generic.IEnumerator<GraphNode> realEnumerator;
            public NNEnumeratorCatchFinally(GraphNode gn)
            {
                this.gn = gn;
                this.realEnumerator = gn.nextNodes.GetEnumerator();
            }
            public override bool MoveNext()
            {
                // First off, return any targets the instruction can come up with.
                if(realEnumerator.MoveNext())
                {
                    nn = realEnumerator.Current;
                    return true;
                }

                // Then if this instruction is in a try section, say this instruction 
                // can potentially branch to the beginning of the corresponding 
                // catch/finally.
                if((index == 0) && (gn.tryBlock != null))
                {
                    index++;
                    nn = gn.tryBlock.catchFinallyBlock;
                    return true;
                }

                // That's all we can do.
                nn = null;
                return false;
            }
            public override void Reset()
            {
                realEnumerator.Reset();
                index = 0;
                nn = null;
            }
        }

        /**
         * @brief This default iterator always returns the next inline node as the one-and-only next node.
         *        Other instructions need to override it if they can possibly do other than that.
         */

        /**
         * @brief GetNNEnumerable() gets the nextnode enumerable part of a GraphNode,
         *        which in turn gives the list of nodes that can possibly be next in 
         *        a flow-control sense.  It simply instantiates the NNEnumerator sub-
         *        class which does the actual enumeration.
         */
        protected virtual System.Collections.Generic.IEnumerable<GraphNode> GetNNEnumerable()
        {
            return new NNEnumerable(this, typeof(NNEnumerator));
        }

        private class NNEnumerator: NNEnumeratorBase
        {
            private GraphNode gn;
            private int index;
            public NNEnumerator(GraphNode gn)
            {
                this.gn = gn;
            }
            public override bool MoveNext()
            {
                switch(index)
                {
                    case 0:
                    {
                        index++;
                        nn = gn.nextLin;
                        return nn != null;
                    }
                    case 1:
                    {
                        nn = null;
                        return false;
                    }
                }
                throw new Exception();
            }
            public override void Reset()
            {
                index = 0;
                nn = null;
            }
        }
    }

    /**
     * @brief Things that derive from this are the beginning of a block.
     *        A block of code is that which begins with a label or is the beginning of all code
     *        and it contains no labels, ie, it can't be jumped into other than at its beginning.
     */
    public abstract class GraphNodeBlock: GraphNode
    {
        public List<ScriptMyLocal> localsWrittenBeforeRead = new List<ScriptMyLocal>();
        public List<ScriptMyLocal> localsReadBeforeWritten = new List<ScriptMyLocal>();
        public int hasBeenResolved;
        public GraphNodeBlock(ScriptCollector coll) : base(coll) { }
    }

    /**
     * @brief This placeholder is at the beginning of the code so the first few instructions 
     *        belong to some block.
     */
    public class GraphNodeBegin: GraphNodeBlock
    {
        public GraphNodeBegin(ScriptCollector coll) : base(coll) { }
        public override void DebString(StringBuilder sb)
        {
            sb.Append("begin");
        }
        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
        }
    }

    /**
     * @brief Beginning of try block.
     */
    public class GraphNodeBeginExceptionBlock: GraphNodeBlock
    {
        public GraphNodeBeginExceptionBlock outerTryBlock;      // next outer try opcode or null
        public GraphNodeCatchFinallyBlock catchFinallyBlock;  // start of associated catch or finally
        public GraphNodeEndExceptionBlock endExcBlock;        // end of associated catch or finally
        public int excBlkSeqNo;                                 // debugging

        public GraphNodeBeginExceptionBlock(ScriptCollector coll) : base(coll)
        {
        }

        public override void ChainLin()
        {
            base.ChainLin();

            // we should always start try blocks with nothing on stack
            // ...as CLI wipes stack for various conditions
            if(coll.stackDepth.Count != 0)
            {
                throw new Exception("stack depth " + coll.stackDepth.Count);
            }
        }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("  beginexceptionblock_");
            sb.Append(excBlkSeqNo);
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.BeginExceptionBlock();
        }
    }

    /**
     * @brief Beginning of catch or finally block.
     */
    public abstract class GraphNodeCatchFinallyBlock: GraphNodeBlock
    {
        public GraphNodeCatchFinallyBlock(ScriptCollector coll) : base(coll)
        {
        }

        public override void ChainLin()
        {
            base.ChainLin();

            // we should always start catch/finally blocks with nothing on stack
            // ...as CLI wipes stack for various conditions
            if(coll.stackDepth.Count != 0)
            {
                throw new Exception("stack depth " + coll.stackDepth.Count);
            }
        }
    }

    /**
     * @brief Beginning of catch block.
     */
    public class GraphNodeBeginCatchBlock: GraphNodeCatchFinallyBlock
    {
        public Type excType;

        public GraphNodeBeginCatchBlock(ScriptCollector coll, Type excType) : base(coll)
        {
            this.excType = excType;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            // catch block always enters with one value on stack
            if(coll.stackDepth.Count != 0)
            {
                throw new Exception("stack depth " + coll.stackDepth.Count);
            }
            coll.stackDepth.Push(excType);
        }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("  begincatchblock_");
            sb.Append(excBlock.excBlkSeqNo);
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.BeginCatchBlock(excType);
        }

        /**
         * @brief The beginning of every catch { } conditinally branches to the beginning 
         *        of all outer catch { }s up to and including the next outer finally { }.
         */
        protected override System.Collections.Generic.IEnumerable<GraphNode> GetNNEnumerable()
        {
            return new NNEnumerable(this, typeof(NNEnumerator));
        }

        private class NNEnumerator: NNEnumeratorBase
        {
            private GraphNodeBeginCatchBlock gn;
            private int index;
            public NNEnumerator(GraphNodeBeginCatchBlock gn)
            {
                this.gn = gn;
            }
            public override bool MoveNext()
            {
                while(true)
                {
                    switch(index)
                    {
                        case 0:
                        {
                            // start with the fallthru
                            nn = gn.nextLin;
                            index++;
                            return true;
                        }

                        case 1:
                        {
                            // get the first outer catch { } or finally { }
                            // pretend we last returned beginning of this catch { }
                            // then loop back to get next outer catch { } or finally { }
                            nn = gn;
                            break;
                        }

                        case 2:
                        {
                            // nn points to a catch { } previously returned
                            // get the corresponding try { }
                            GraphNodeBeginExceptionBlock nntry = nn.excBlock;

                            // step out to next outer try { }
                            nntry = nntry.excBlock;
                            if(nntry == null)
                                break;

                            // return corresponding catch { } or finally { }
                            nn = nntry.catchFinallyBlock;

                            // if it's a finally { } we don't do anything after that
                            if(nn is GraphNodeBeginFinallyBlock)
                                index++;
                            return true;
                        }

                        case 3:
                        {
                            // we've returned the fallthru, catches and one finally
                            // so there's nothing more to say
                            nn = null;
                            return false;
                        }

                        default:
                            throw new Exception();
                    }
                    index++;
                }
            }
            public override void Reset()
            {
                index = 0;
                nn = null;
            }
        }
    }

    /**
     * @brief Beginning of finally block.
     */
    public class GraphNodeBeginFinallyBlock: GraphNodeCatchFinallyBlock
    {

        // leaveTargets has a list of all the targets of any contained 
        // leave instructions, ie, where an endfinally can possibly jump.
        // But only those targets within the next outer finally { }, we 
        // don't contain any targets outside of that, those targets are 
        // stored in the actual finally that will jump to the target.
        // The endfinally enumerator assumes that it is always possible 
        // for it to jump to the next outer finally (as would happen for
        // an uncaught exception), so no need to do anything special.
        public List<GraphNodeBlock> leaveTargets = new List<GraphNodeBlock>();

        public GraphNodeBeginFinallyBlock(ScriptCollector coll) : base(coll)
        {
        }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("  beginfinallyblock_");
            sb.Append(excBlock.excBlkSeqNo);
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.BeginFinallyBlock();
        }
    }

    /**
     * @brief End of try/catch/finally block.
     */
    public class GraphNodeEndExceptionBlock: GraphNode
    {
        public GraphNodeEndExceptionBlock(ScriptCollector coll) : base(coll)
        {
        }

        public override void ChainLin()
        {
            base.ChainLin();

            // we should always end exception blocks with nothing on stack
            // ...as CLI wipes stack for various conditions
            if(coll.stackDepth.Count != 0)
            {
                throw new Exception("stack depth " + coll.stackDepth.Count);
            }
        }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("  endexceptionblock_");
            sb.Append(excBlock.excBlkSeqNo);
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.EndExceptionBlock();
        }
    }

    /**
     * @brief Actual instruction emits...
     */
    public abstract class GraphNodeEmit: GraphNode
    {
        public OpCode opcode;
        public Token errorAt;

        public GraphNodeEmit(ScriptCollector coll, Token errorAt, OpCode opcode) : base(coll)
        {
            this.opcode = opcode;
            this.errorAt = errorAt;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            // compute resultant stack depth
            int stack = coll.stackDepth.Count;

            if((stack != 0) && ((opcode == OpCodes.Endfinally) || (opcode == OpCodes.Leave) || (opcode == OpCodes.Rethrow)))
            {
                throw new Exception(opcode + " stack depth " + stack);
            }
            if((stack != 1) && (opcode == OpCodes.Throw))
            {
                throw new Exception(opcode + " stack depth " + stack);
            }
        }

        /**
         * @brief See if it's possible for it to fall through to the next inline (nextLin) instruction.
         */
        public override bool CanFallThrough()
        {
            switch(opcode.FlowControl)
            {
                case FlowControl.Branch:
                    return false;  // unconditional branch
                case FlowControl.Break:
                    return true;   // break
                case FlowControl.Call:
                    return true;   // call
                case FlowControl.Cond_Branch:
                    return true;   // conditional branch
                case FlowControl.Next:
                    return true;   // falls through to next instruction
                case FlowControl.Return:
                    return false;  // return
                case FlowControl.Throw:
                    return false;  // throw
                default:
                {
                    string op = opcode.ToString();
                    if(op == "volatile.")
                        return true;
                    throw new Exception("unknown flow control " + opcode.FlowControl + " for " + op);
                }
            }
        }

        // if followed by OpCodes.Pop, it can be discarded
        public bool isPoppable
        {
            get
            {
                return
                    ((opcode.StackBehaviourPop == StackBehaviour.Pop0) &&    // ldarg,ldloc,ldsfld
                     (opcode.StackBehaviourPush == StackBehaviour.Push1)) ||
                    ((opcode.StackBehaviourPop == StackBehaviour.Pop0) &&    // ldarga,ldloca,ldc,ldsflda,...
                     (opcode.StackBehaviourPush == StackBehaviour.Pushi)) ||
                    (opcode == OpCodes.Ldnull) ||
                    (opcode == OpCodes.Ldc_R4) ||
                    (opcode == OpCodes.Ldc_R8) ||
                    (opcode == OpCodes.Ldstr) ||
                    (opcode == OpCodes.Ldc_I8) ||
                    (opcode == OpCodes.Dup);
            }
        }

        public override void DebString(StringBuilder sb)
        {
            sb.Append("".PadRight(OPINDENT));
            sb.Append(opcode.ToString().PadRight(OPDEBLEN));
        }

        /**
         * @brief If instruction is terminating, we say there is nothing following (eg, return).
         *        Otherwise, say the one-and-only next instruction is the next instruction inline.
         */
        protected override System.Collections.Generic.IEnumerable<GraphNode> GetNNEnumerable()
        {
            return new NNEnumerable(this, typeof(NNEnumerator));
        }

        private class NNEnumerator: NNEnumeratorBase
        {
            private GraphNodeEmit gn;
            private int index;
            public NNEnumerator(GraphNodeEmit gn)
            {
                this.gn = gn;
            }
            public override bool MoveNext()
            {
                switch(index)
                {
                    case 0:
                    {
                        if(gn.CanFallThrough())
                        {
                            index++;
                            nn = gn.nextLin;
                            return nn != null;
                        }
                        return false;
                    }
                    case 1:
                    {
                        nn = null;
                        return false;
                    }
                }
                throw new Exception();
            }
            public override void Reset()
            {
                index = 0;
                nn = null;
            }
        }
    }

    public class GraphNodeEmitNull: GraphNodeEmit
    {
        public GraphNodeEmitNull(ScriptCollector coll, Token errorAt, OpCode opcode) : base(coll, errorAt, opcode)
        {
        }

        public override void ChainLin()
        {
            base.ChainLin();

            switch(opcode.ToString())
            {
                case "nop":
                    break;
                case "break":
                    break;
                case "volatile.":
                    break;
                case "ldarg.0":
                    coll.stackDepth.Push(coll.wrapped.argTypes[0]);
                    break;
                case "ldarg.1":
                    coll.stackDepth.Push(coll.wrapped.argTypes[1]);
                    break;
                case "ldarg.2":
                    coll.stackDepth.Push(coll.wrapped.argTypes[2]);
                    break;
                case "ldarg.3":
                    coll.stackDepth.Push(coll.wrapped.argTypes[3]);
                    break;
                case "ldnull":
                    coll.stackDepth.Push(null);
                    break;
                case "ldc.i4.m1":
                case "ldc.i4.0":
                case "ldc.i4.1":
                case "ldc.i4.2":
                case "ldc.i4.3":
                case "ldc.i4.4":
                case "ldc.i4.5":
                case "ldc.i4.6":
                case "ldc.i4.7":
                case "ldc.i4.8":
                {
                    coll.stackDepth.Push(typeof(int));
                    break;
                }
                case "dup":
                {
                    Type t = coll.stackDepth.Peek(0);
                    bool b = coll.stackDepth.PeekBoxed(0);
                    coll.stackDepth.Push(t, b);
                    break;
                }
                case "pop":
                {
                    coll.stackDepth.Pop(1);
                    break;
                }
                case "ret":
                {
                    int sd = (coll.wrapped.retType != typeof(void)) ? 1 : 0;
                    if(coll.stackDepth.Count != sd)
                        throw new Exception("bad stack depth");
                    if(sd > 0)
                    {
                        coll.stackDepth.Pop(coll.wrapped.retType);
                    }
                    break;
                }
                case "add":
                case "sub":
                case "mul":
                case "div":
                case "div.un":
                case "rem":
                case "rem.un":
                case "and":
                case "or":
                case "xor":
                case "shl":
                case "shr":
                case "shr.un":
                case "add.ovf":
                case "add.ovf.un":
                case "mul.ovf":
                case "mul.ovf.un":
                case "sub.ovf":
                case "sub.ovf.un":
                {
                    coll.stackDepth.PopNumVal();
                    Type t = coll.stackDepth.PopNumVal();
                    coll.stackDepth.Push(t);
                    break;
                }
                case "neg":
                case "not":
                {
                    Type t = coll.stackDepth.PopNumVal();
                    coll.stackDepth.Push(t);
                    break;
                }
                case "conv.i1":
                case "conv.i2":
                case "conv.i4":
                case "conv.i8":
                case "conv.r4":
                case "conv.r8":
                case "conv.u4":
                case "conv.u8":
                case "conv.r.un":
                case "conv.ovf.i1.un":
                case "conv.ovf.i2.un":
                case "conv.ovf.i4.un":
                case "conv.ovf.i8.un":
                case "conv.ovf.u1.un":
                case "conv.ovf.u2.un":
                case "conv.ovf.u4.un":
                case "conv.ovf.u8.un":
                case "conv.ovf.i.un":
                case "conv.ovf.u.un":
                case "conv.ovf.i1":
                case "conv.ovf.u1":
                case "conv.ovf.i2":
                case "conv.ovf.u2":
                case "conv.ovf.i4":
                case "conv.ovf.u4":
                case "conv.ovf.i8":
                case "conv.ovf.u8":
                case "conv.u2":
                case "conv.u1":
                case "conv.i":
                case "conv.ovf.i":
                case "conv.ovf.u":
                case "conv.u":
                {
                    coll.stackDepth.PopNumVal();
                    coll.stackDepth.Push(ConvToType(opcode));
                    break;
                }
                case "throw":
                {
                    if(coll.stackDepth.Count != 1)
                        throw new Exception("bad stack depth " + coll.stackDepth.Count);
                    coll.stackDepth.PopRef();
                    break;
                }
                case "ldlen":
                {
                    coll.stackDepth.Pop(typeof(string));
                    coll.stackDepth.Push(typeof(int));
                    break;
                }
                case "ldelem.i1":
                case "ldelem.u1":
                case "ldelem.i2":
                case "ldelem.u2":
                case "ldelem.i4":
                case "ldelem.u4":
                case "ldelem.i8":
                case "ldelem.i":
                case "ldelem.r4":
                case "ldelem.r8":
                case "ldelem.ref":
                {
                    Type t = coll.stackDepth.Peek(1).GetElementType();
                    coll.stackDepth.Pop(typeof(int));
                    coll.stackDepth.Pop(t.MakeArrayType());
                    coll.stackDepth.Push(t);
                    break;
                }
                case "stelem.i":
                case "stelem.i1":
                case "stelem.i2":
                case "stelem.i4":
                case "stelem.i8":
                case "stelem.r4":
                case "stelem.r8":
                case "stelem.ref":
                {
                    Type t = coll.stackDepth.Peek(2).GetElementType();
                    coll.stackDepth.Pop(t);
                    coll.stackDepth.Pop(typeof(int));
                    coll.stackDepth.Pop(t.MakeArrayType());
                    break;
                }
                case "endfinally":
                case "rethrow":
                {
                    if(coll.stackDepth.Count != 0)
                        throw new Exception("bad stack depth " + coll.stackDepth.Count);
                    break;
                }
                case "ceq":
                {
                    Type t = coll.stackDepth.Pop(1);
                    if(t == null)
                    {
                        coll.stackDepth.PopRef();
                    }
                    else
                    {
                        coll.stackDepth.Pop(t);
                    }
                    coll.stackDepth.Push(typeof(int));
                    break;
                }
                case "cgt":
                case "cgt.un":
                case "clt":
                case "clt.un":
                {
                    coll.stackDepth.PopNumVal();
                    coll.stackDepth.PopNumVal();
                    coll.stackDepth.Push(typeof(int));
                    break;
                }
                case "ldind.i4":
                {
                    coll.stackDepth.Pop(typeof(int).MakeByRefType());
                    coll.stackDepth.Push(typeof(int));
                    break;
                }
                case "stind.i4":
                {
                    coll.stackDepth.Pop(typeof(int));
                    coll.stackDepth.Pop(typeof(int).MakeByRefType());
                    break;
                }
                default:
                    throw new Exception("unknown opcode " + opcode.ToString());
            }
        }

        private static Type ConvToType(OpCode opcode)
        {
            string s = opcode.ToString();
            s = s.Substring(5);  // strip off "conv."
            if(s.StartsWith("ovf."))
                s = s.Substring(4);
            if(s.EndsWith(".un"))
                s = s.Substring(0, s.Length - 3);

            switch(s)
            {
                case "i":
                    return typeof(IntPtr);
                case "i1":
                    return typeof(sbyte);
                case "i2":
                    return typeof(short);
                case "i4":
                    return typeof(int);
                case "i8":
                    return typeof(long);
                case "r":
                case "r4":
                    return typeof(float);
                case "r8":
                    return typeof(double);
                case "u1":
                    return typeof(byte);
                case "u2":
                    return typeof(ushort);
                case "u4":
                    return typeof(uint);
                case "u8":
                    return typeof(ulong);
                case "u":
                    return typeof(UIntPtr);
                default:
                    throw new Exception("unknown opcode " + opcode.ToString());
            }
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, opcode);
        }
    }

    public class GraphNodeEmitNullEndfinally: GraphNodeEmitNull
    {
        public GraphNodeEmitNullEndfinally(ScriptCollector coll, Token errorAt) : base(coll, errorAt, OpCodes.Endfinally)
        {
        }

        /**
         * @brief Endfinally can branch to:
         *          1) the corresponding EndExceptionBlock
         *          2) any of the corresponding BeginFinallyBlock's leaveTargets
         *          3) the next outer BeginFinallyBlock
         */
        protected override System.Collections.Generic.IEnumerable<GraphNode> GetNNEnumerable()
        {
            return new NNEnumerable(this, typeof(NNEnumerator));
        }

        private class NNEnumerator: NNEnumeratorBase
        {
            private GraphNodeEmitNullEndfinally gn;
            private IEnumerator<GraphNodeBlock> leaveTargetEnumerator;
            private int index;
            public NNEnumerator(GraphNodeEmitNullEndfinally gn)
            {
                this.gn = gn;

                // endfinally instruction must be within some try/catch/finally mess
                GraphNodeBeginExceptionBlock thistry = gn.excBlock;

                // endfinally instruction must be within some finally { } mess
                GraphNodeBeginFinallyBlock thisfin = (GraphNodeBeginFinallyBlock)thistry.catchFinallyBlock;

                // get the list of the finally { } leave instruction targets
                this.leaveTargetEnumerator = thisfin.leaveTargets.GetEnumerator();
            }
            public override bool MoveNext()
            {
                while(true)
                {
                    switch(index)
                    {

                        // to start, return end of our finally { }
                        case 0:
                        {
                            GraphNodeBeginExceptionBlock thistry = gn.excBlock;
                            nn = thistry.endExcBlock;
                            if(nn == null)
                                throw new NullReferenceException("thistry.endExcBlock");
                            index++;
                            return true;
                        }

                        // return next one of our finally { }'s leave targets
                        // ie, where any leave instructions in the try { } want 
                        // the finally { } to go to when it finishes
                        case 1:
                        {
                            if(this.leaveTargetEnumerator.MoveNext())
                            {
                                nn = this.leaveTargetEnumerator.Current;
                                if(nn == null)
                                    throw new NullReferenceException("this.leaveTargetEnumerator.Current");
                                return true;
                            }
                            break;
                        }

                        // return beginning of next outer finally { }
                        case 2:
                        {
                            GraphNodeBeginExceptionBlock nntry = gn.excBlock;
                            while((nntry = nntry.excBlock) != null)
                            {
                                if(nntry.catchFinallyBlock is GraphNodeBeginFinallyBlock)
                                {
                                    nn = nntry.catchFinallyBlock;
                                    if(nn == null)
                                        throw new NullReferenceException("nntry.catchFinallyBlock");
                                    index++;
                                    return true;
                                }
                            }
                            break;
                        }

                        // got nothing more
                        case 3:
                        {
                            return false;
                        }

                        default:
                            throw new Exception();
                    }
                    index++;
                }
            }
            public override void Reset()
            {
                leaveTargetEnumerator.Reset();
                index = 0;
                nn = null;
            }
        }
    }

    public class GraphNodeEmitField: GraphNodeEmit
    {
        public FieldInfo field;

        public GraphNodeEmitField(ScriptCollector coll, Token errorAt, OpCode opcode, FieldInfo field) : base(coll, errorAt, opcode)
        {
            this.field = field;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            switch(opcode.ToString())
            {
                case "ldfld":
                    PopPointer();
                    coll.stackDepth.Push(field.FieldType);
                    break;
                case "ldflda":
                    PopPointer();
                    coll.stackDepth.Push(field.FieldType.MakeByRefType());
                    break;
                case "stfld":
                    coll.stackDepth.Pop(field.FieldType);
                    PopPointer();
                    break;
                case "ldsfld":
                    coll.stackDepth.Push(field.FieldType);
                    break;
                case "ldsflda":
                    coll.stackDepth.Push(field.FieldType.MakeByRefType());
                    break;
                case "stsfld":
                    coll.stackDepth.Pop(field.FieldType);
                    break;
                default:
                    throw new Exception("unknown opcode " + opcode.ToString());
            }
        }
        private void PopPointer()
        {
            Type t = field.DeclaringType;               // get class/field type
            if(t.IsValueType)
            {
                Type brt = t.MakeByRefType();      // if value type, eg Vector, it can be pushed by reference or by value
                int c = coll.stackDepth.Count;
                if((c > 0) && (coll.stackDepth[c - 1] == brt))
                    t = brt;
            }
            coll.stackDepth.Pop(t);                    // type of what should be on the stack pointing to object or struct
        }

        public override void DebString(StringBuilder sb)
        {
            base.DebString(sb);
            sb.Append(field.Name);
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, opcode, field);
        }
    }

    public class GraphNodeEmitLocal: GraphNodeEmit
    {
        public ScriptMyLocal myLocal;

        public GraphNodeEmitLocal(ScriptCollector coll, Token errorAt, OpCode opcode, ScriptMyLocal myLocal) : base(coll, errorAt, opcode)
        {
            this.myLocal = myLocal;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            switch(opcode.ToString())
            {
                case "ldloc":
                    coll.stackDepth.Push(myLocal.type);
                    break;
                case "ldloca":
                    coll.stackDepth.Push(myLocal.type.MakeByRefType());
                    break;
                case "stloc":
                    coll.stackDepth.Pop(myLocal.type);
                    break;
                default:
                    throw new Exception("unknown opcode " + opcode.ToString());
            }
        }

        public override void DebString(StringBuilder sb)
        {
            base.DebString(sb);
            sb.Append(myLocal.name);
        }

        public override ScriptMyLocal ReadsLocal()
        {
            if(opcode == OpCodes.Ldloc)
                return myLocal;
            if(opcode == OpCodes.Ldloca)
                return myLocal;
            if(opcode == OpCodes.Stloc)
                return null;
            throw new Exception("unknown opcode " + opcode);
        }
        public override ScriptMyLocal WritesLocal()
        {
            if(opcode == OpCodes.Ldloc)
                return null;
            if(opcode == OpCodes.Ldloca)
                return myLocal;
            if(opcode == OpCodes.Stloc)
                return myLocal;
            throw new Exception("unknown opcode " + opcode);
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, opcode, myLocal);
        }
    }

    public class GraphNodeEmitType: GraphNodeEmit
    {
        public Type type;

        public GraphNodeEmitType(ScriptCollector coll, Token errorAt, OpCode opcode, Type type) : base(coll, errorAt, opcode)
        {
            this.type = type;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            switch(opcode.ToString())
            {
                case "castclass":
                case "isinst":
                {
                    coll.stackDepth.PopRef();
                    coll.stackDepth.Push(type, type.IsValueType);
                    break;
                }
                case "box":
                {
                    if(!type.IsValueType)
                        throw new Exception("can't box a non-value type");
                    coll.stackDepth.Pop(type);
                    coll.stackDepth.Push(type, true);
                    break;
                }
                case "unbox":
                case "unbox.any":
                {
                    if(!type.IsValueType)
                        throw new Exception("can't unbox to a non-value type");
                    coll.stackDepth.PopRef();
                    coll.stackDepth.Push(type);
                    break;
                }
                case "newarr":
                {
                    coll.stackDepth.Pop(typeof(int));
                    coll.stackDepth.Push(type.MakeArrayType());
                    break;
                }
                case "sizeof":
                {
                    coll.stackDepth.Pop(1);
                    coll.stackDepth.Push(typeof(int));
                    break;
                }
                case "ldelem":
                {
                    coll.stackDepth.Pop(typeof(int));
                    coll.stackDepth.Pop(type.MakeArrayType());
                    coll.stackDepth.Push(type);
                    break;
                }
                case "ldelema":
                {
                    coll.stackDepth.Pop(typeof(int));
                    coll.stackDepth.Pop(type.MakeArrayType());
                    coll.stackDepth.Push(type.MakeByRefType());
                    break;
                }
                case "stelem":
                {
                    coll.stackDepth.Pop(type);
                    coll.stackDepth.Pop(typeof(int));
                    coll.stackDepth.Pop(type.MakeArrayType());
                    break;
                }
                default:
                    throw new Exception("unknown opcode " + opcode.ToString());
            }
        }

        public override void DebString(StringBuilder sb)
        {
            base.DebString(sb);
            sb.Append(type.Name);
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, opcode, type);
        }
    }

    public class GraphNodeEmitLabel: GraphNodeEmit
    {
        public ScriptMyLabel myLabel;

        public GraphNodeEmitLabel(ScriptCollector coll, Token errorAt, OpCode opcode, ScriptMyLabel myLabel) : base(coll, errorAt, opcode)
        {
            this.myLabel = myLabel;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            switch(opcode.ToString())
            {
                case "brfalse.s":
                case "brtrue.s":
                case "brfalse":
                case "brtrue":
                {
                    coll.stackDepth.Pop(1);
                    break;
                }
                case "beq.s":
                case "bge.s":
                case "bgt.s":
                case "ble.s":
                case "blt.s":
                case "bne.un.s":
                case "bge.un.s":
                case "bgt.un.s":
                case "ble.un.s":
                case "blt.un.s":
                case "beq":
                case "bge":
                case "bgt":
                case "ble":
                case "blt":
                case "bne.un":
                case "bge.un":
                case "bgt.un":
                case "ble.un":
                case "blt.un":
                {
                    coll.stackDepth.PopNumVal();
                    coll.stackDepth.PopNumVal();
                    break;
                }
                case "br":
                case "br.s":
                    break;
                case "leave":
                {
                    if(coll.stackDepth.Count != 0)
                        throw new Exception("bad stack depth " + coll.stackDepth.Count);
                    break;
                }
                default:
                    throw new Exception("unknown opcode " + opcode.ToString());
            }

            // if a target doesn't have a depth yet, set its depth to the depth after instruction executes
            // otherwise, make sure it matches all other branches to that target and what fell through to it
            coll.stackDepth.Matches(myLabel);
        }

        public override void DebString(StringBuilder sb)
        {
            base.DebString(sb);
            sb.Append(myLabel.name);
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, opcode, myLabel);
        }

        /**
         * @brief Conditional branches return the next inline followed by the branch target
         *        Unconditional branches return only the branch target
         *        But if the target is outside our scope (eg __retlbl), omit it from the list
         */
        protected override System.Collections.Generic.IEnumerable<GraphNode> GetNNEnumerable()
        {
            return new NNEnumerable(this, typeof(NNEnumerator));
        }

        private class NNEnumerator: NNEnumeratorBase
        {
            private GraphNodeEmitLabel gn;
            private int index;
            public NNEnumerator(GraphNodeEmitLabel gn)
            {
                this.gn = gn;
            }
            public override bool MoveNext()
            {
                switch(gn.opcode.FlowControl)
                {
                    case FlowControl.Branch:
                    {
                        // unconditional branch just goes to target and nothing else
                        switch(index)
                        {
                            case 0:
                            {
                                nn = gn.myLabel.whereAmI;
                                index++;
                                return nn != null;
                            }
                            case 1:
                            {
                                return false;
                            }
                        }
                        throw new Exception();
                    }
                    case FlowControl.Cond_Branch:
                    {
                        // conditional branch goes inline and to target
                        switch(index)
                        {
                            case 0:
                            {
                                nn = gn.nextLin;
                                index++;
                                return true;
                            }
                            case 1:
                            {
                                nn = gn.myLabel.whereAmI;
                                index++;
                                return nn != null;
                            }
                            case 2:
                            {
                                return false;
                            }
                        }
                        throw new Exception();
                    }
                    default:
                        throw new Exception("unknown flow control " + gn.opcode.FlowControl.ToString() +
                                             " of " + gn.opcode.ToString());
                }
            }
            public override void Reset()
            {
                index = 0;
                nn = null;
            }
        }
    }

    public class GraphNodeEmitLabelLeave: GraphNodeEmitLabel
    {
        public GraphNodeBlock unwindTo;  // if unwinding, innermost finally block being unwound
                                         //         else, same as myTarget.whereAmI
                                         // null if unwinding completely out of scope, eg, __retlbl

        public GraphNodeEmitLabelLeave(ScriptCollector coll, Token errorAt, ScriptMyLabel myLabel) : base(coll, errorAt, OpCodes.Leave, myLabel)
        {
        }

        /**
         * @brief Leave instructions have exactly one unconditional next node.
         *        Either the given target if within the same try block 
         *        or the beginning of the intervening finally block.
         */
        protected override System.Collections.Generic.IEnumerable<GraphNode> GetNNEnumerable()
        {
            return new NNEnumerable(this, typeof(NNEnumerator));
        }

        private class NNEnumerator: NNEnumeratorBase
        {
            private GraphNodeEmitLabelLeave gn;
            private int index;
            public NNEnumerator(GraphNodeEmitLabelLeave gn)
            {
                this.gn = gn;
            }
            public override bool MoveNext()
            {
                if(index == 0)
                {
                    nn = gn.unwindTo;
                    index++;
                    return nn != null;
                }
                nn = null;
                return false;
            }
            public override void Reset()
            {
                index = 0;
                nn = null;
            }
        }
    }

    public class GraphNodeEmitLabels: GraphNodeEmit
    {
        public ScriptMyLabel[] myLabels;

        public GraphNodeEmitLabels(ScriptCollector coll, Token errorAt, OpCode opcode, ScriptMyLabel[] myLabels) : base(coll, errorAt, opcode)
        {
            this.myLabels = myLabels;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            switch(opcode.ToString())
            {
                case "switch":
                {
                    coll.stackDepth.Pop(typeof(int));
                    break;
                }
                default:
                    throw new Exception("unknown opcode " + opcode.ToString());
            }

            // if a target doesn't have a depth yet, set its depth to the depth after instruction executes
            // otherwise, make sure it matches all other branches to that target and what fell through to it
            foreach(ScriptMyLabel myLabel in myLabels)
            {
                coll.stackDepth.Matches(myLabel);
            }
        }

        public override void DebString(StringBuilder sb)
        {
            base.DebString(sb);
            bool first = true;
            foreach(ScriptMyLabel lbl in myLabels)
            {
                if(!first)
                    sb.Append(',');
                sb.Append(lbl.name);
                first = false;
            }
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, opcode, myLabels);
        }

        /**
         * @brief Return list of all labels followed by the next linear instruction
         *        But if the target is outside our scope (eg __retlbl), omit it from the list
         */
        protected override System.Collections.Generic.IEnumerable<GraphNode> GetNNEnumerable()
        {
            return new NNEnumerable(this, typeof(NNEnumerator));
        }

        private class NNEnumerator: NNEnumeratorBase
        {
            private GraphNodeEmitLabels gn;
            private int index;
            public NNEnumerator(GraphNodeEmitLabels gn)
            {
                this.gn = gn;
            }
            public override bool MoveNext()
            {
                // Return next from list of switch case labels.
                while(index < gn.myLabels.Length)
                {
                    nn = gn.myLabels[index++].whereAmI;
                    if(nn != null)
                        return true;
                }

                // If all ran out, the switch instruction falls through.
                if(index == gn.myLabels.Length)
                {
                    index++;
                    nn = gn.nextLin;
                    return true;
                }

                // Even ran out of that, say there's nothing more.
                nn = null;
                return false;
            }
            public override void Reset()
            {
                index = 0;
                nn = null;
            }
        }
    }

    public class GraphNodeEmitIntMeth: GraphNodeEmit
    {
        public ScriptObjWriter method;

        public GraphNodeEmitIntMeth(ScriptCollector coll, Token errorAt, OpCode opcode, ScriptObjWriter method) : base(coll, errorAt, opcode)
        {
            this.method = method;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            switch(opcode.ToString())
            {
                case "call":
                {

                    // calls have Varpop so pop the number of arguments
                    // they are all static so there is no separate 'this' parameter
                    coll.stackDepth.Pop(this.method.argTypes);

                    // calls are also Varpush so they push a return value iff non-void
                    if(this.method.retType != typeof(void))
                        coll.stackDepth.Push(this.method.retType);
                    break;
                }

                default:
                    throw new Exception("unknown opcode " + opcode.ToString());
            }
        }

        public override void DebString(StringBuilder sb)
        {
            base.DebString(sb);
            sb.Append(method.methName);
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, opcode, method);
        }
    }

    public class GraphNodeEmitExtMeth: GraphNodeEmit
    {
        public MethodInfo method;

        public GraphNodeEmitExtMeth(ScriptCollector coll, Token errorAt, OpCode opcode, MethodInfo method) : base(coll, errorAt, opcode)
        {
            this.method = method;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            switch(opcode.ToString())
            {
                case "call":
                case "callvirt":
                {

                    // calls have Varpop so pop the number of arguments
                    coll.stackDepth.Pop(this.method.GetParameters());
                    if((this.method.CallingConvention & CallingConventions.HasThis) != 0)
                    {
                        coll.stackDepth.Pop(method.DeclaringType);
                    }

                    // calls are also Varpush so they push a return value iff non-void
                    if(this.method.ReturnType != typeof(void))
                        coll.stackDepth.Push(this.method.ReturnType);
                    break;
                }

                default:
                    throw new Exception("unknown opcode " + opcode.ToString());
            }
        }

        public override void DebString(StringBuilder sb)
        {
            base.DebString(sb);
            sb.Append(method.Name);
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, opcode, method);
        }
    }

    public class GraphNodeEmitCtor: GraphNodeEmit
    {
        public ConstructorInfo ctor;

        public GraphNodeEmitCtor(ScriptCollector coll, Token errorAt, OpCode opcode, ConstructorInfo ctor) : base(coll, errorAt, opcode)
        {
            this.ctor = ctor;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            switch(opcode.ToString())
            {
                case "newobj":
                {
                    coll.stackDepth.Pop(ctor.GetParameters());
                    coll.stackDepth.Push(ctor.DeclaringType);
                    break;
                }

                default:
                    throw new Exception("unknown opcode " + opcode.ToString());
            }
        }

        public override void DebString(StringBuilder sb)
        {
            base.DebString(sb);
            sb.Append(ctor.ReflectedType.Name);
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, opcode, ctor);
        }
    }

    public class GraphNodeEmitDouble: GraphNodeEmit
    {
        public double value;

        public GraphNodeEmitDouble(ScriptCollector coll, Token errorAt, OpCode opcode, double value) : base(coll, errorAt, opcode)
        {
            this.value = value;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            switch(opcode.ToString())
            {
                case "ldc.r8":
                    coll.stackDepth.Push(typeof(double));
                    break;
                default:
                    throw new Exception("unknown opcode " + opcode.ToString());
            }
        }

        public override void DebString(StringBuilder sb)
        {
            base.DebString(sb);
            sb.Append(value);
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, opcode, value);
        }
    }

    public class GraphNodeEmitFloat: GraphNodeEmit
    {
        public float value;

        public GraphNodeEmitFloat(ScriptCollector coll, Token errorAt, OpCode opcode, float value) : base(coll, errorAt, opcode)
        {
            this.value = value;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            switch(opcode.ToString())
            {
                case "ldc.r4":
                    coll.stackDepth.Push(typeof(float));
                    break;
                default:
                    throw new Exception("unknown opcode " + opcode.ToString());
            }
        }

        public override void DebString(StringBuilder sb)
        {
            base.DebString(sb);
            sb.Append(value);
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, opcode, value);
        }
    }

    public class GraphNodeEmitInt: GraphNodeEmit
    {
        public int value;

        public GraphNodeEmitInt(ScriptCollector coll, Token errorAt, OpCode opcode, int value) : base(coll, errorAt, opcode)
        {
            this.value = value;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            switch(opcode.ToString())
            {
                case "ldarg":
                case "ldarg.s":
                    coll.stackDepth.Push(coll.wrapped.argTypes[value]);
                    break;
                case "ldarga":
                case "ldarga.s":
                    coll.stackDepth.Push(coll.wrapped.argTypes[value].MakeByRefType());
                    break;
                case "starg":
                case "starg.s":
                    coll.stackDepth.Pop(coll.wrapped.argTypes[value]);
                    break;
                case "ldc.i4":
                case "ldc.i4.s":
                    coll.stackDepth.Push(typeof(int));
                    break;
                default:
                    throw new Exception("unknown opcode " + opcode.ToString());
            }
        }

        public override void DebString(StringBuilder sb)
        {
            base.DebString(sb);
            sb.Append(value);
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, opcode, value);
        }
    }

    public class GraphNodeEmitString: GraphNodeEmit
    {
        public string value;

        public GraphNodeEmitString(ScriptCollector coll, Token errorAt, OpCode opcode, string value) : base(coll, errorAt, opcode)
        {
            this.value = value;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            switch(opcode.ToString())
            {
                case "ldstr":
                    coll.stackDepth.Push(typeof(string));
                    break;
                default:
                    throw new Exception("unknown opcode " + opcode.ToString());
            }
        }

        public override void DebString(StringBuilder sb)
        {
            base.DebString(sb);
            sb.Append("\"");
            sb.Append(value);
            sb.Append("\"");
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, opcode, value);
        }
    }

    public class GraphNodeMarkLabel: GraphNodeBlock
    {
        public ScriptMyLabel myLabel;

        public GraphNodeMarkLabel(ScriptCollector coll, ScriptMyLabel myLabel) : base(coll)
        {
            this.myLabel = myLabel;
        }

        public override void ChainLin()
        {
            base.ChainLin();

            // if previous instruction can fall through to this label,
            //     if the label doesn't yet have a stack depth, mark it with current stack depth
            //     else, the label's stack depth from forward branches and current stack depth must match
            // else,
            //     label must have had a forward branch to it so we can know stack depth
            //     set the current stack depth to the label's stack depth as of that forward branch
            if(myLabel.whereAmI.prevLin.CanFallThrough())
            {
                coll.stackDepth.Matches(myLabel);
            }
            else
            {
                if(myLabel.stackDepth == null)
                {
                    throw new Exception("stack depth unknown at " + myLabel.name);
                }
                coll.stackDepth.Clear();
                int n = myLabel.stackDepth.Length;
                for(int i = 0; i < n; i++)
                {
                    coll.stackDepth.Push(myLabel.stackDepth[i], myLabel.stackBoxeds[i]);
                }
            }
        }

        public override void DebString(StringBuilder sb)
        {
            sb.Append(myLabel.name);
            sb.Append(':');
            if(myLabel.stackDepth != null)
            {
                sb.Append("  [");
                sb.Append(myLabel.stackDepth.Length);
                sb.Append(']');
            }
        }

        public override void WriteOutOne(ScriptMyILGen ilGen)
        {
            ilGen.MarkLabel(myLabel);
        }
    }


    /**
     * @brief Generates enumerator that steps through list of nodes that can
     *        possibly be next in a flow-control sense.
     */
    public class NNEnumerable: System.Collections.Generic.IEnumerable<GraphNode>
    {
        private object[] cps;
        private ConstructorInfo ci;

        public NNEnumerable(GraphNode gn, Type nnEnumeratorType)
        {
            this.cps = new object[] { gn };
            this.ci = nnEnumeratorType.GetConstructor(new Type[] { gn.GetType() });
        }
        System.Collections.Generic.IEnumerator<GraphNode> System.Collections.Generic.IEnumerable<GraphNode>.GetEnumerator()
        {
            return (System.Collections.Generic.IEnumerator<GraphNode>)ci.Invoke(cps);
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return (System.Collections.IEnumerator)ci.Invoke(cps);
        }
    }


    /**
     * @brief Steps through list of nodes that can possible be next in a flow-control sense.
     */
    public abstract class NNEnumeratorBase: System.Collections.Generic.IEnumerator<GraphNode>
    {
        protected GraphNode nn;

        public abstract bool MoveNext();
        public abstract void Reset();

        GraphNode System.Collections.Generic.IEnumerator<GraphNode>.Current
        {
            get
            {
                return this.nn;
            }
        }
        object System.Collections.IEnumerator.Current
        {
            get
            {
                return this.nn;
            }
        }
        void System.IDisposable.Dispose()
        {
        }
    }


    public class ScriptCollector: ScriptMyILGen
    {
        public static readonly bool DEBUG = false;

        public ScriptObjWriter wrapped;
        public GraphNode firstLin, lastLin;
        private bool resolvedSomething;
        private int resolveSequence;
        private int excBlkSeqNos;
        public StackDepth stackDepth = new StackDepth();

        public GraphNodeBeginExceptionBlock curTryBlock = null;  // pushed at beginning of try
                                                                 // popped at BEGINNING of catch/finally
        public GraphNodeBeginExceptionBlock curExcBlock = null;  // pushed at beginning of try
                                                                 // popped at END of catch/finally

        private List<ScriptMyLocal> declaredLocals = new List<ScriptMyLocal>();
        private List<ScriptMyLabel> definedLabels = new List<ScriptMyLabel>();

        public string methName
        {
            get
            {
                return wrapped.methName;
            }
        }

        /**
         * @brief Wrap the optimizer around the ScriptObjWriter to collect the instruction stream.
         *        All stream-writing calls get saved to our graph nodes instead of being written to object file.
         */
        public ScriptCollector(ScriptObjWriter wrapped)
        {
            this.wrapped = wrapped;
            GraphNodeBegin gnb = new GraphNodeBegin(this);
            this.firstLin = gnb;
            this.lastLin = gnb;
        }

        public ScriptMyLocal DeclareLocal(Type type, string name)
        {
            ScriptMyLocal loc = new ScriptMyLocal();
            loc.name = name;
            loc.type = type;
            loc.number = wrapped.localNumber++;
            declaredLocals.Add(loc);
            return loc;
        }

        public ScriptMyLabel DefineLabel(string name)
        {
            ScriptMyLabel lbl = new ScriptMyLabel();
            lbl.name = name;
            lbl.number = wrapped.labelNumber++;
            definedLabels.Add(lbl);
            return lbl;
        }

        public void BeginExceptionBlock()
        {
            GraphNodeBeginExceptionBlock tryBlock = new GraphNodeBeginExceptionBlock(this);
            tryBlock.ChainLin();
            tryBlock.excBlkSeqNo = ++this.excBlkSeqNos;
            this.curExcBlock = tryBlock;
            this.curTryBlock = tryBlock;
        }

        public void BeginCatchBlock(Type excType)
        {
            GraphNodeBeginCatchBlock catchBlock = new GraphNodeBeginCatchBlock(this, excType);
            catchBlock.ChainLin();
            if(curExcBlock.catchFinallyBlock != null)
                throw new Exception("only one catch/finally allowed per try");
            curExcBlock.catchFinallyBlock = catchBlock;
            curTryBlock = curExcBlock.tryBlock;
        }

        public void BeginFinallyBlock()
        {
            GraphNodeBeginFinallyBlock finallyBlock = new GraphNodeBeginFinallyBlock(this);
            finallyBlock.ChainLin();
            if(curExcBlock.catchFinallyBlock != null)
                throw new Exception("only one catch/finally allowed per try");
            curExcBlock.catchFinallyBlock = finallyBlock;
            curTryBlock = curExcBlock.tryBlock;
        }

        public void EndExceptionBlock()
        {
            GraphNodeEndExceptionBlock endExcBlock = new GraphNodeEndExceptionBlock(this);
            endExcBlock.ChainLin();
            curExcBlock.endExcBlock = endExcBlock;
            curTryBlock = curExcBlock.tryBlock;
            curExcBlock = curExcBlock.excBlock;
        }

        public void Emit(Token errorAt, OpCode opcode)
        {
            if(opcode == OpCodes.Endfinally)
            {
                new GraphNodeEmitNullEndfinally(this, errorAt).ChainLin();
            }
            else
            {
                new GraphNodeEmitNull(this, errorAt, opcode).ChainLin();
            }
        }

        public void Emit(Token errorAt, OpCode opcode, FieldInfo field)
        {
            if(field == null)
                throw new ArgumentNullException("field");
            new GraphNodeEmitField(this, errorAt, opcode, field).ChainLin();
        }

        public void Emit(Token errorAt, OpCode opcode, ScriptMyLocal myLocal)
        {
            new GraphNodeEmitLocal(this, errorAt, opcode, myLocal).ChainLin();
        }

        public void Emit(Token errorAt, OpCode opcode, Type type)
        {
            new GraphNodeEmitType(this, errorAt, opcode, type).ChainLin();
        }

        public void Emit(Token errorAt, OpCode opcode, ScriptMyLabel myLabel)
        {
            if(opcode == OpCodes.Leave)
            {
                new GraphNodeEmitLabelLeave(this, errorAt, myLabel).ChainLin();
            }
            else
            {
                new GraphNodeEmitLabel(this, errorAt, opcode, myLabel).ChainLin();
            }
        }

        public void Emit(Token errorAt, OpCode opcode, ScriptMyLabel[] myLabels)
        {
            new GraphNodeEmitLabels(this, errorAt, opcode, myLabels).ChainLin();
        }

        public void Emit(Token errorAt, OpCode opcode, ScriptObjWriter method)
        {
            if(method == null)
                throw new ArgumentNullException("method");
            new GraphNodeEmitIntMeth(this, errorAt, opcode, method).ChainLin();
        }

        public void Emit(Token errorAt, OpCode opcode, MethodInfo method)
        {
            if(method == null)
                throw new ArgumentNullException("method");
            new GraphNodeEmitExtMeth(this, errorAt, opcode, method).ChainLin();
        }

        public void Emit(Token errorAt, OpCode opcode, ConstructorInfo ctor)
        {
            if(ctor == null)
                throw new ArgumentNullException("ctor");
            new GraphNodeEmitCtor(this, errorAt, opcode, ctor).ChainLin();
        }

        public void Emit(Token errorAt, OpCode opcode, double value)
        {
            new GraphNodeEmitDouble(this, errorAt, opcode, value).ChainLin();
        }

        public void Emit(Token errorAt, OpCode opcode, float value)
        {
            new GraphNodeEmitFloat(this, errorAt, opcode, value).ChainLin();
        }

        public void Emit(Token errorAt, OpCode opcode, int value)
        {
            new GraphNodeEmitInt(this, errorAt, opcode, value).ChainLin();
        }

        public void Emit(Token errorAt, OpCode opcode, string value)
        {
            new GraphNodeEmitString(this, errorAt, opcode, value).ChainLin();
        }

        public void MarkLabel(ScriptMyLabel myLabel)
        {
            myLabel.whereAmI = new GraphNodeMarkLabel(this, myLabel);
            myLabel.whereAmI.ChainLin();
        }

        /**
         * @brief Write the whole graph out to the object file.
         */
        public ScriptMyILGen WriteOutAll()
        {
            foreach(ScriptMyLocal loc in declaredLocals)
            {
                if(loc.isReferenced)
                    wrapped.DeclareLocal(loc);
            }
            foreach(ScriptMyLabel lbl in definedLabels)
            {
                wrapped.DefineLabel(lbl);
            }
            for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
            {
                gn.WriteOutOne(wrapped);
            }
            return wrapped;
        }

        /**
         * @brief Perform optimizations.
         */
        public void Optimize()
        {
            if(curExcBlock != null)
                throw new Exception("exception block still open");

            // If an instruction says it doesn't fall through, remove all instructions to
            // the end of the block.
            for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
            {
                if(!gn.CanFallThrough())
                {
                    GraphNode nn;
                    while(((nn = gn.nextLin) != null) && !(nn is GraphNodeBlock) &&
                                              !(nn is GraphNodeEndExceptionBlock))
                    {
                        if((gn.nextLin = nn.nextLin) != null)
                        {
                            nn.nextLin.prevLin = gn;
                        }
                    }
                }
            }

            // Scan for OpCodes.Leave instructions.
            // For each found, its target for flow analysis purposes is the beginning of the corresponding
            // finally block.  And the end of the finally block gets a conditional branch target of the 
            // leave instruction's target.  A leave instruction can unwind zero or more finally blocks.
            for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
            {
                if(gn is GraphNodeEmitLabelLeave)
                {
                    GraphNodeEmitLabelLeave leaveInstr = (GraphNodeEmitLabelLeave)gn;         // the leave instruction
                    GraphNodeMarkLabel leaveTarget = leaveInstr.myLabel.whereAmI;             // label being targeted by leave
                    GraphNodeBeginExceptionBlock leaveTargetsTryBlock =                       // try block directly enclosing leave target
                        (leaveTarget == null) ? null : leaveTarget.tryBlock;              // ...it must not be unwound

                    // Step through try { }s from the leave instruction towards its target looking for try { }s with finally { }s.
                    // The leave instruction unconditionally branches to the beginning of the innermost one found.
                    // The end of the last one found conditionally branches to the leave instruction's target.
                    // If none found, the leave is a simple unconditional branch to its target.
                    GraphNodeBeginFinallyBlock innerFinallyBlock = null;
                    for(GraphNodeBeginExceptionBlock tryBlock = leaveInstr.tryBlock;
                         tryBlock != leaveTargetsTryBlock;
                         tryBlock = tryBlock.tryBlock)
                    {
                        if(tryBlock == null)
                            throw new Exception("leave target not at or outer to leave instruction");
                        GraphNodeCatchFinallyBlock cfb = tryBlock.catchFinallyBlock;
                        if(cfb is GraphNodeBeginFinallyBlock)
                        {
                            if(innerFinallyBlock == null)
                            {
                                leaveInstr.unwindTo = cfb;
                            }
                            innerFinallyBlock = (GraphNodeBeginFinallyBlock)cfb;
                        }
                    }

                    // The end of the outermost finally being unwound can conditionally jump to the target of the leave instruction.
                    // In the case of no finallies being unwound, the leave is just a simple unconditional branch.
                    if(innerFinallyBlock == null)
                    {
                        leaveInstr.unwindTo = leaveTarget;
                    }
                    else if(!innerFinallyBlock.leaveTargets.Contains(leaveTarget))
                    {
                        innerFinallyBlock.leaveTargets.Add(leaveTarget);
                    }
                }
            }

            // See which variables a particular block reads before writing.
            // This just considers the block itself and nothing that it branches to or fallsthru to.
            GraphNodeBlock currentBlock = null;
            for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
            {
                if(gn is GraphNodeBlock)
                    currentBlock = (GraphNodeBlock)gn;
                ScriptMyLocal rdlcl = gn.ReadsLocal();
                if((rdlcl != null) &&
                    !currentBlock.localsWrittenBeforeRead.Contains(rdlcl) &&
                    !currentBlock.localsReadBeforeWritten.Contains(rdlcl))
                {
                    currentBlock.localsReadBeforeWritten.Add(rdlcl);
                }
                ScriptMyLocal wrlcl = gn.WritesLocal();
                if((wrlcl != null) &&
                    !currentBlock.localsWrittenBeforeRead.Contains(wrlcl) &&
                    !currentBlock.localsReadBeforeWritten.Contains(wrlcl))
                {
                    currentBlock.localsWrittenBeforeRead.Add(wrlcl);
                }
            }

            // For every block we branch to, add that blocks readables to our list of readables,
            // because we need to have those values valid on entry to our block.  But if we write the 
            // variable before we can possibly branch to that block, then we don't need to have it valid 
            // on entry to our block.  So basically it looks like the branch instruction is reading 
            // everything required by any blocks it can branch to.
            do
            {
                resolvedSomething = false;
                resolveSequence++;
                ResolveBlock((GraphNodeBlock)firstLin);
            } while(resolvedSomething);

            // Repeat the cutting loops as long as we keep finding stuff.
            bool didSomething;
            do
            {
                didSomething = false;

                // Strip out ldc.i4.1/xor/ldc.i4.1/xor
                for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
                {
                    if(!(gn is GraphNodeEmit))
                        continue;
                    GraphNodeEmit xor2 = (GraphNodeEmit)gn;
                    if(xor2.opcode != OpCodes.Xor)
                        continue;
                    if(!(xor2.prevLin is GraphNodeEmit))
                        continue;
                    GraphNodeEmit ld12 = (GraphNodeEmit)xor2.prevLin;
                    if(ld12.opcode != OpCodes.Ldc_I4_1)
                        continue;
                    if(!(ld12.prevLin is GraphNodeEmit))
                        continue;
                    GraphNodeEmit xor1 = (GraphNodeEmit)ld12.prevLin;
                    if(xor1.opcode != OpCodes.Xor)
                        continue;
                    if(!(xor2.prevLin is GraphNodeEmit))
                        continue;
                    GraphNodeEmit ld11 = (GraphNodeEmit)xor1.prevLin;
                    if(ld11.opcode != OpCodes.Ldc_I4_1)
                        continue;
                    ld11.prevLin.nextLin = xor2.nextLin;
                    xor2.nextLin.prevLin = ld11.prevLin;
                    didSomething = true;
                }

                // Replace c{cond}/ldc.i4.1/xor/br{false,true} -> c{cond}/br{true,false}
                for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
                {
                    if(!(gn is GraphNodeEmit))
                        continue;
                    GraphNodeEmit brft = (GraphNodeEmit)gn;
                    if((brft.opcode != OpCodes.Brfalse) && (brft.opcode != OpCodes.Brtrue))
                        continue;
                    if(!(brft.prevLin is GraphNodeEmit))
                        continue;
                    GraphNodeEmit xor = (GraphNodeEmit)brft.prevLin;
                    if(xor.opcode != OpCodes.Xor)
                        continue;
                    if(!(xor.prevLin is GraphNodeEmit))
                        continue;
                    GraphNodeEmit ldc = (GraphNodeEmit)xor.prevLin;
                    if(ldc.opcode != OpCodes.Ldc_I4_1)
                        continue;
                    if(!(ldc.prevLin is GraphNodeEmit))
                        continue;
                    GraphNodeEmit cmp = (GraphNodeEmit)ldc.prevLin;
                    if(cmp.opcode.StackBehaviourPop != StackBehaviour.Pop1_pop1)
                        continue;
                    if(cmp.opcode.StackBehaviourPush != StackBehaviour.Pushi)
                        continue;
                    cmp.nextLin = brft;
                    brft.prevLin = cmp;
                    brft.opcode = (brft.opcode == OpCodes.Brfalse) ? OpCodes.Brtrue : OpCodes.Brfalse;
                    didSomething = true;
                }

                // Replace c{cond}/br{false,true} -> b{!,}{cond}
                for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
                {
                    if(!(gn is GraphNodeEmit))
                        continue;
                    GraphNodeEmit brft = (GraphNodeEmit)gn;
                    if((brft.opcode != OpCodes.Brfalse) && (brft.opcode != OpCodes.Brtrue))
                        continue;
                    if(!(brft.prevLin is GraphNodeEmit))
                        continue;
                    GraphNodeEmit cmp = (GraphNodeEmit)brft.prevLin;
                    if(cmp.opcode.StackBehaviourPop != StackBehaviour.Pop1_pop1)
                        continue;
                    if(cmp.opcode.StackBehaviourPush != StackBehaviour.Pushi)
                        continue;
                    cmp.prevLin.nextLin = brft;
                    brft.prevLin = cmp.prevLin;
                    bool brtru = (brft.opcode == OpCodes.Brtrue);
                    if(cmp.opcode == OpCodes.Ceq)
                        brft.opcode = brtru ? OpCodes.Beq : OpCodes.Bne_Un;
                    else if(cmp.opcode == OpCodes.Cgt)
                        brft.opcode = brtru ? OpCodes.Bgt : OpCodes.Ble;
                    else if(cmp.opcode == OpCodes.Cgt_Un)
                        brft.opcode = brtru ? OpCodes.Bgt_Un : OpCodes.Ble_Un;
                    else if(cmp.opcode == OpCodes.Clt)
                        brft.opcode = brtru ? OpCodes.Blt : OpCodes.Bge;
                    else if(cmp.opcode == OpCodes.Clt_Un)
                        brft.opcode = brtru ? OpCodes.Blt_Un : OpCodes.Bge_Un;
                    else
                        throw new Exception();
                    didSomething = true;
                }

                // Replace ld{c.i4.0,null}/br{ne.un,eq} -> br{true,false}
                for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
                {
                    if(!(gn is GraphNodeEmit))
                        continue;
                    GraphNodeEmit brcc = (GraphNodeEmit)gn;
                    if((brcc.opcode != OpCodes.Bne_Un) && (brcc.opcode != OpCodes.Beq))
                        continue;
                    if(!(brcc.prevLin is GraphNodeEmit))
                        continue;
                    GraphNodeEmit ldc0 = (GraphNodeEmit)brcc.prevLin;
                    if((ldc0.opcode != OpCodes.Ldc_I4_0) && (ldc0.opcode != OpCodes.Ldnull))
                        continue;
                    ldc0.prevLin.nextLin = brcc;
                    brcc.prevLin = ldc0.prevLin;
                    brcc.opcode = (brcc.opcode == OpCodes.Bne_Un) ? OpCodes.Brtrue : OpCodes.Brfalse;
                    didSomething = true;
                }

                // Replace:
                //    ldloc v1
                //    stloc v2
                //    ld<anything> except ld<anything> v2
                //    ldloc v2
                //      ...v2 unreferenced hereafter
                // With:
                //    ld<anything> except ld<anything> v2
                //    ldloc v1
                for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
                {

                    // check for 'ldloc v1' instruction
                    if(!(gn is GraphNodeEmitLocal))
                        continue;
                    GraphNodeEmitLocal ldlv1 = (GraphNodeEmitLocal)gn;
                    if(ldlv1.opcode != OpCodes.Ldloc)
                        continue;

                    // check for 'stloc v2' instruction
                    if(!(ldlv1.nextLin is GraphNodeEmitLocal))
                        continue;
                    GraphNodeEmitLocal stlv2 = (GraphNodeEmitLocal)ldlv1.nextLin;
                    if(stlv2.opcode != OpCodes.Stloc)
                        continue;

                    // check for 'ld<anything> except ld<anything> v2' instruction
                    if(!(stlv2.nextLin is GraphNodeEmit))
                        continue;
                    GraphNodeEmit ldany = (GraphNodeEmit)stlv2.nextLin;
                    if(!ldany.opcode.ToString().StartsWith("ld"))
                        continue;
                    if((ldany is GraphNodeEmitLocal) &&
                        ((GraphNodeEmitLocal)ldany).myLocal == stlv2.myLocal)
                        continue;

                    // check for 'ldloc v2' instruction
                    if(!(ldany.nextLin is GraphNodeEmitLocal))
                        continue;
                    GraphNodeEmitLocal ldlv2 = (GraphNodeEmitLocal)ldany.nextLin;
                    if(ldlv2.opcode != OpCodes.Ldloc)
                        continue;
                    if(ldlv2.myLocal != stlv2.myLocal)
                        continue;

                    // check that v2 is not needed after this at all
                    if(IsLocalNeededAfterThis(ldlv2, ldlv2.myLocal))
                        continue;

                    // make 'ld<anything>...' the first instruction
                    ldany.prevLin = ldlv1.prevLin;
                    ldany.prevLin.nextLin = ldany;

                    // make 'ldloc v1' the second instruction
                    ldany.nextLin = ldlv1;
                    ldlv1.prevLin = ldany;

                    // and make 'ldloc v1' the last instruction
                    ldlv1.nextLin = ldlv2.nextLin;
                    ldlv1.nextLin.prevLin = ldlv1;

                    didSomething = true;
                }

                // Remove all the stloc/ldloc that are back-to-back without the local
                // being needed afterwards.  If it is needed afterwards, replace the 
                // stloc/ldloc with dup/stloc.
                for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
                {
                    if((gn is GraphNodeEmitLocal) &&
                        (gn.prevLin is GraphNodeEmitLocal))
                    {
                        GraphNodeEmitLocal stloc = (GraphNodeEmitLocal)gn.prevLin;
                        GraphNodeEmitLocal ldloc = (GraphNodeEmitLocal)gn;
                        if((stloc.opcode == OpCodes.Stloc) &&
                            (ldloc.opcode == OpCodes.Ldloc) &&
                            (stloc.myLocal == ldloc.myLocal))
                        {
                            if(IsLocalNeededAfterThis(ldloc, ldloc.myLocal))
                            {
                                GraphNodeEmitNull dup = new GraphNodeEmitNull(this, stloc.errorAt, OpCodes.Dup);
                                dup.nextLin = stloc;
                                dup.prevLin = stloc.prevLin;
                                stloc.nextLin = ldloc.nextLin;
                                stloc.prevLin = dup;
                                dup.prevLin.nextLin = dup;
                                stloc.nextLin.prevLin = stloc;
                                gn = stloc;
                            }
                            else
                            {
                                stloc.prevLin.nextLin = ldloc.nextLin;
                                ldloc.nextLin.prevLin = stloc.prevLin;
                                gn = stloc.prevLin;
                            }
                            didSomething = true;
                        }
                    }
                }

                // Remove all write-only local variables, ie, those with no ldloc[a] references.
                // Replace any stloc instructions with pops.
                for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
                {
                    ScriptMyLocal rdlcl = gn.ReadsLocal();
                    if(rdlcl != null)
                        rdlcl.isReferenced = true;
                }
                for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
                {
                    ScriptMyLocal wrlcl = gn.WritesLocal();
                    if((wrlcl != null) && !wrlcl.isReferenced)
                    {
                        if(!(gn is GraphNodeEmitLocal) || (((GraphNodeEmitLocal)gn).opcode != OpCodes.Stloc))
                        {
                            throw new Exception("expecting stloc");
                        }
                        GraphNodeEmitNull pop = new GraphNodeEmitNull(this, ((GraphNodeEmit)gn).errorAt, OpCodes.Pop);
                        pop.nextLin = gn.nextLin;
                        pop.prevLin = gn.prevLin;
                        gn.nextLin.prevLin = pop;
                        gn.prevLin.nextLin = pop;
                        gn = pop;
                        didSomething = true;
                    }
                }

                // Remove any Ld<const>/Dup,Pop.
                for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
                {
                    if((gn is GraphNodeEmit) &&
                        (gn.nextLin is GraphNodeEmit))
                    {
                        GraphNodeEmit gne = (GraphNodeEmit)gn;
                        GraphNodeEmit nne = (GraphNodeEmit)gn.nextLin;
                        if(gne.isPoppable && (nne.opcode == OpCodes.Pop))
                        {
                            gne.prevLin.nextLin = nne.nextLin;
                            nne.nextLin.prevLin = gne.prevLin;
                            gn = gne.prevLin;
                            didSomething = true;
                        }
                    }
                }
            } while(didSomething);

            // Dump out the results.
            if(DEBUG)
            {
                Console.WriteLine("");
                Console.WriteLine(methName);
                Console.WriteLine("  resolveSequence=" + this.resolveSequence);

                Console.WriteLine("  Locals:");
                foreach(ScriptMyLocal loc in declaredLocals)
                {
                    Console.WriteLine("    " + loc.type.Name + "  " + loc.name);
                }

                Console.WriteLine("  Labels:");
                foreach(ScriptMyLabel lbl in definedLabels)
                {
                    Console.WriteLine("    " + lbl.name);
                }

                Console.WriteLine("  Code:");
                DumpCode();
            }
        }

        private void DumpCode()
        {
            int linSeqNos = 0;
            for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
            {
                gn.linSeqNo = ++linSeqNos;
            }
            for(GraphNode gn = firstLin; gn != null; gn = gn.nextLin)
            {
                StringBuilder sb = new StringBuilder();
                gn.DebStringExt(sb);
                Console.WriteLine(sb.ToString());
                if(gn is GraphNodeBlock)
                {
                    GraphNodeBlock gnb = (GraphNodeBlock)gn;
                    foreach(ScriptMyLocal lcl in gnb.localsReadBeforeWritten)
                    {
                        Console.WriteLine("         reads " + lcl.name);
                    }
                }
            }
        }

        /**
         * @brief Scan the given block for branches to other blocks.
         *        For any locals read by those blocks, mark them as being read by this block, 
         *        provided this block has not written them by that point.  This makes it look 
         *        as though the branch instruction is reading all the locals needed by any 
         *        target blocks.
         */
        private void ResolveBlock(GraphNodeBlock currentBlock)
        {
            if(currentBlock.hasBeenResolved == this.resolveSequence)
                return;

            // So we don't recurse forever on a backward branch.
            currentBlock.hasBeenResolved = resolveSequence;

            // Assume we haven't written any locals yet.
            List<ScriptMyLocal> localsWrittenSoFar = new List<ScriptMyLocal>();

            // Scan through the instructions in this block.
            for(GraphNode gn = currentBlock; gn != null;)
            {

                // See if the instruction writes a local we don't know about yet.
                ScriptMyLocal wrlcl = gn.WritesLocal();
                if((wrlcl != null) && !localsWrittenSoFar.Contains(wrlcl))
                {
                    localsWrittenSoFar.Add(wrlcl);
                }

                // Scan through all the possible next instructions after this.
                // Note that if we are in the first part of a try/catch/finally block, 
                // every instruction conditionally branches to the beginning of the 
                // second part (the catch/finally block).
                GraphNode nextFallthruNode = null;
                foreach(GraphNode nn in gn.NextNodes)
                {
                    if(nn is GraphNodeBlock)
                    {
                        // Start of a block, go through all locals needed by that block on entry.
                        GraphNodeBlock nextBlock = (GraphNodeBlock)nn;
                        ResolveBlock(nextBlock);
                        foreach(ScriptMyLocal readByNextBlock in nextBlock.localsReadBeforeWritten)
                        {
                            // If this block hasn't written it by now and this block doesn't already
                            // require it on entry, say this block requires it on entry.
                            if(!localsWrittenSoFar.Contains(readByNextBlock) &&
                                !currentBlock.localsReadBeforeWritten.Contains(readByNextBlock))
                            {
                                currentBlock.localsReadBeforeWritten.Add(readByNextBlock);
                                resolvedSomething = true;
                            }
                        }
                    }
                    else
                    {
                        // Not start of a block, should be normal fallthru instruction.
                        if(nextFallthruNode != null)
                            throw new Exception("more than one fallthru from " + gn.ToString());
                        nextFallthruNode = nn;
                    }
                }

                // Process next instruction if it isn't the start of a block.
                if(nextFallthruNode == gn)
                    throw new Exception("can't fallthru to self");
                gn = nextFallthruNode;
            }
        }

        /**
         * @brief Figure out whether the value in a local var is needed after the given instruction.
         *        True if we reach the end of the program on all branches before reading it
         *        True if we write the local var on all branches before reading it
         *        False otherwise
         */
        private bool IsLocalNeededAfterThis(GraphNode node, ScriptMyLocal local)
        {
            do
            {
                GraphNode nextFallthruNode = null;
                foreach(GraphNode nn in node.NextNodes)
                {
                    if(nn is GraphNodeBlock)
                    {
                        if(((GraphNodeBlock)nn).localsReadBeforeWritten.Contains(local))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        nextFallthruNode = nn;
                    }
                }
                node = nextFallthruNode;
                if(node == null)
                    return false;
                if(node.ReadsLocal() == local)
                    return true;
            } while(node.WritesLocal() != local);
            return false;
        }

        public static void PadToLength(StringBuilder sb, int len, string str)
        {
            int pad = len - sb.Length;
            if(pad < 0)
                pad = 0;
            sb.Append(str.PadLeft(pad));
        }
    }
}
