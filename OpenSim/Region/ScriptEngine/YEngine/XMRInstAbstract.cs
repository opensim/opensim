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
using System.Globalization;
using System.IO;
using System.Reflection.Emit;
using System.Runtime.Serialization;
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
    public class XMRInstArrays
    {
        public XMR_Array[] iarArrays;
        public char[] iarChars;
        public double[] iarFloats;
        public int[] iarIntegers;
        public LSL_List[] iarLists;
        public object[] iarObjects;
        public LSL_Rotation[] iarRotations;
        public string[] iarStrings;
        public LSL_Vector[] iarVectors;
        public XMRSDTypeClObj[] iarSDTClObjs;
        public Delegate[][] iarSDTIntfObjs;

        private XMRInstAbstract instance;
        public int arraysHeapUse;

        private static readonly XMR_Array[] noArrays = new XMR_Array[0];
        private static readonly char[] noChars = new char[0];
        private static readonly double[] noFloats = new double[0];
        private static readonly int[] noIntegers = new int[0];
        private static readonly LSL_List[] noLists = new LSL_List[0];
        private static readonly object[] noObjects = new object[0];
        private static readonly LSL_Rotation[] noRotations = new LSL_Rotation[0];
        private static readonly string[] noStrings = new string[0];
        private static readonly LSL_Vector[] noVectors = new LSL_Vector[0];
        private static readonly XMRSDTypeClObj[] noSDTClObjs = new XMRSDTypeClObj[0];
        private static readonly Delegate[][] noSDTIntfObjs = new Delegate[0][];

        public XMRInstArrays(XMRInstAbstract inst)
        {
            instance = inst;
        }

        /*
        ~XMRInstArrays()
        {
            arraysHeapUse = instance.UpdateArraysHeapUse(arraysHeapUse, 0);
        }
        */

        public void Clear()
        {
            int newheapUse = 0;
            if(iarArrays != null)
            {
                foreach(XMR_Array xa in iarArrays)
                    xa.__pub_clear();
            }
            if(iarChars != null)
                iarChars = new char[iarChars.Length];
            if (iarLists != null)
                iarLists = new LSL_List[iarLists.Length];
            if (iarObjects != null)
                iarObjects = new object[iarObjects.Length];
            if(iarStrings != null)
                iarStrings = new string[iarStrings.Length];
            if (iarFloats != null)
                newheapUse += iarFloats.Length * HeapTrackerObject.HT_DOUB;
            if (iarIntegers != null)
                newheapUse += iarIntegers.Length * HeapTrackerObject.HT_INT;
            if (iarRotations != null)
                newheapUse += iarRotations.Length * HeapTrackerObject.HT_ROT;
            if (iarVectors != null)
                newheapUse += iarVectors.Length * HeapTrackerObject.HT_VEC;

            arraysHeapUse = instance.UpdateArraysHeapUse(0, newheapUse);
        }

    public void AllocVarArrays(XMRInstArSizes ars)
        {
            ClearOldArrays();
            int newuse = arraysHeapUse +
               ars.iasChars* HeapTrackerObject.HT_CHAR +
               ars.iasFloats * HeapTrackerObject.HT_DOUB +
               ars.iasIntegers * HeapTrackerObject.HT_INT +
               ars.iasRotations * HeapTrackerObject.HT_ROT +
               ars.iasVectors * HeapTrackerObject.HT_VEC +
               ars.iasSDTIntfObjs * HeapTrackerObject.HT_DELE;

            arraysHeapUse = instance.UpdateArraysHeapUse(arraysHeapUse, newuse);

            iarArrays = (ars.iasArrays > 0) ? new XMR_Array[ars.iasArrays] : noArrays;
            iarChars = (ars.iasChars > 0) ? new char[ars.iasChars] : noChars;
            iarFloats = (ars.iasFloats > 0) ? new double[ars.iasFloats] : noFloats;
            iarIntegers = (ars.iasIntegers > 0) ? new int[ars.iasIntegers] : noIntegers;
            iarLists = (ars.iasLists > 0) ? new LSL_List[ars.iasLists] : noLists;
            iarObjects = (ars.iasObjects > 0) ? new object[ars.iasObjects] : noObjects;
            iarRotations = (ars.iasRotations > 0) ? new LSL_Rotation[ars.iasRotations] : noRotations;
            iarStrings = (ars.iasStrings > 0) ? new string[ars.iasStrings] : noStrings;
            iarVectors = (ars.iasVectors > 0) ? new LSL_Vector[ars.iasVectors] : noVectors;
            iarSDTClObjs = (ars.iasSDTClObjs > 0) ? new XMRSDTypeClObj[ars.iasSDTClObjs] : noSDTClObjs;
            iarSDTIntfObjs = (ars.iasSDTIntfObjs > 0) ? new Delegate[ars.iasSDTIntfObjs][] : noSDTIntfObjs;
        }

        /**
         * @brief Do not write directly to iarLists[index], rather use this method.
         */
        public void PopList(int index, LSL_List lis)
        {
            int delta = HeapTrackerObject.Size(lis) - HeapTrackerObject.Size(iarLists[index]);
            instance.UpdateArraysHeapUse(0, delta);
            Interlocked.Add(ref arraysHeapUse, delta);
            iarLists[index] = lis;
        }

        /**
         * @brief Do not write directly to iarObjects[index], rather use this method.
         */
        public void PopObject(int index, object obj)
        {
            int delta = HeapTrackerObject.Size(obj) - HeapTrackerObject.Size(iarObjects[index]);
            instance.UpdateArraysHeapUse(0, delta);
            Interlocked.Add(ref arraysHeapUse, delta);
            iarObjects[index] = obj;
        }

        /**
         * @brief Do not write directly to iarStrings[index], rather use this method.
         */
        public void PopString(int index, string str)
        {
            int delta = HeapTrackerString.Size(str) - HeapTrackerString.Size(iarStrings[index]);
            instance.UpdateArraysHeapUse(0, delta);
            Interlocked.Add(ref arraysHeapUse, delta);
            iarStrings[index] = str;
        }

        /**
         * @brief Write all arrays out to a file.
         */
        public delegate void Sender(object value);
        public void SendArrays(Sender sender)
        {
            sender(iarArrays);
            sender(iarChars);
            sender(iarFloats);
            sender(iarIntegers);
            sender(iarLists);
            sender(iarObjects);
            sender(iarRotations);
            sender(iarStrings);
            sender(iarVectors);
            sender(iarSDTClObjs);
            sender(iarSDTIntfObjs);
        }

        /**
         * @brief Read all arrays in from a file.
         */
        public delegate object Recver();
        public void RecvArrays(Recver recver)
        {
            ClearOldArrays();

            iarArrays = (XMR_Array[])recver();
            char[] chrs = (char[])recver();
            double[] flts = (double[])recver();
            int[] ints = (int[])recver();
            LSL_List[] liss = (LSL_List[])recver();
            object[] objs = (object[])recver();
            LSL_Rotation[] rots = (LSL_Rotation[])recver();
            string[] strs = (string[])recver();
            LSL_Vector[] vecs = (LSL_Vector[])recver();
            iarSDTClObjs = (XMRSDTypeClObj[])recver();
            Delegate[][] dels = (Delegate[][])recver();

            int newheapuse = arraysHeapUse;

            // value types simply are the size of the value * number of values
            newheapuse += chrs.Length * HeapTrackerObject.HT_CHAR;
            newheapuse += flts.Length * HeapTrackerObject.HT_DOUB;
            newheapuse += ints.Length * HeapTrackerObject.HT_INT;
            newheapuse += rots.Length * HeapTrackerObject.HT_ROT;
            newheapuse += vecs.Length * HeapTrackerObject.HT_VEC;
            newheapuse += dels.Length * HeapTrackerObject.HT_DELE;

            // lists, objects, strings are the sum of the size of each element
            foreach(LSL_List lis in liss)
                newheapuse += HeapTrackerList.Size(lis);

            foreach(object obj in objs)
                newheapuse += HeapTrackerObject.Size(obj);

            foreach(string str in strs)
                newheapuse += HeapTrackerString.Size(str);

            // others (XMR_Array, XMRSDTypeClObj) keep track of their own heap usage

            // update script heap usage, throwing an exception before finalizing changes
            arraysHeapUse = instance.UpdateArraysHeapUse(arraysHeapUse, newheapuse);

            iarChars = chrs;
            iarFloats = flts;
            iarIntegers = ints;
            iarLists = liss;
            iarObjects = objs;
            iarRotations = rots;
            iarStrings = strs;
            iarVectors = vecs;
            iarSDTIntfObjs = dels;
        }

        private void ClearOldArrays()
        {
            int newheapuse = arraysHeapUse;

            iarArrays = null;
            if(iarChars != null)
            {
                newheapuse -= iarChars.Length * HeapTrackerObject.HT_CHAR;
                iarChars = null;
            }
            if(iarFloats != null)
            {
                newheapuse -= iarFloats.Length * HeapTrackerObject.HT_DOUB;
                iarFloats = null;
            }
            if(iarIntegers != null)
            {
                newheapuse -= iarIntegers.Length * HeapTrackerObject.HT_INT;
                iarIntegers = null;
            }
            if(iarLists != null)
            {
                foreach(LSL_List lis in iarLists)
                    newheapuse -= HeapTrackerList.Size(lis);
                iarLists = null;
            }
            if(iarObjects != null)
            {
                foreach(object obj in iarObjects)
                    newheapuse -= HeapTrackerObject.Size(obj);
                iarObjects = null;
            }
            if(iarRotations != null)
            {
                newheapuse -= iarRotations.Length * HeapTrackerObject.HT_ROT;
                iarRotations = null;
            }
            if(iarStrings != null)
            {
                foreach(string str in iarStrings)
                    newheapuse -= HeapTrackerString.Size(str);
                iarStrings = null;
            }
            if(iarVectors != null)
            {
                newheapuse -= iarVectors.Length * HeapTrackerObject.HT_VEC;
                iarVectors = null;
            }
            iarSDTClObjs = null;
            if(iarSDTIntfObjs != null)
            {
                newheapuse -= iarSDTIntfObjs.Length * HeapTrackerObject.HT_DELE;
                iarSDTIntfObjs = null;
            }

            arraysHeapUse = instance.UpdateArraysHeapUse(arraysHeapUse, newheapuse);
        }
    }

    public class XMRInstArSizes
    {
        public int iasArrays;
        public int iasChars;
        public int iasFloats;
        public int iasIntegers;
        public int iasLists;
        public int iasObjects;
        public int iasRotations;
        public int iasStrings;
        public int iasVectors;
        public int iasSDTClObjs;
        public int iasSDTIntfObjs;

        public void WriteAsmFile(TextWriter asmFileWriter, string label)
        {
            asmFileWriter.WriteLine("  {0}Arrays       {1}", label, iasArrays);
            asmFileWriter.WriteLine("  {0}Chars        {1}", label, iasChars);
            asmFileWriter.WriteLine("  {0}Floats       {1}", label, iasFloats);
            asmFileWriter.WriteLine("  {0}Integers     {1}", label, iasIntegers);
            asmFileWriter.WriteLine("  {0}Lists        {1}", label, iasLists);
            asmFileWriter.WriteLine("  {0}Objects      {1}", label, iasObjects);
            asmFileWriter.WriteLine("  {0}Rotations    {1}", label, iasRotations);
            asmFileWriter.WriteLine("  {0}Strings      {1}", label, iasStrings);
            asmFileWriter.WriteLine("  {0}Vectors      {1}", label, iasVectors);
            asmFileWriter.WriteLine("  {0}SDTClObjs    {1}", label, iasSDTClObjs);
            asmFileWriter.WriteLine("  {0}SDTIntfObjs  {1}", label, iasSDTIntfObjs);
        }

        public void WriteToFile(BinaryWriter objFileWriter)
        {
            objFileWriter.Write(iasArrays);
            objFileWriter.Write(iasChars);
            objFileWriter.Write(iasFloats);
            objFileWriter.Write(iasIntegers);
            objFileWriter.Write(iasLists);
            objFileWriter.Write(iasObjects);
            objFileWriter.Write(iasRotations);
            objFileWriter.Write(iasStrings);
            objFileWriter.Write(iasVectors);
            objFileWriter.Write(iasSDTClObjs);
            objFileWriter.Write(iasSDTIntfObjs);
        }

        public void ReadFromFile(BinaryReader objFileReader)
        {
            iasArrays = objFileReader.ReadInt32();
            iasChars = objFileReader.ReadInt32();
            iasFloats = objFileReader.ReadInt32();
            iasIntegers = objFileReader.ReadInt32();
            iasLists = objFileReader.ReadInt32();
            iasObjects = objFileReader.ReadInt32();
            iasRotations = objFileReader.ReadInt32();
            iasStrings = objFileReader.ReadInt32();
            iasVectors = objFileReader.ReadInt32();
            iasSDTClObjs = objFileReader.ReadInt32();
            iasSDTIntfObjs = objFileReader.ReadInt32();
        }
    }

    public class XMRStackFrame
    {
        public XMRStackFrame nextSF;
        public string funcName;
        public int callNo;
        public object[] objArray;
    }

    /*
     * Contains only items required by the stand-alone compiler
     * so the compiler doesn't need to pull in all of OpenSim.
     *
     * Inherit from ScriptBaseClass so we can be used as 'this'
     * parameter for backend-API calls, eg llSay().
     */
    public abstract class XMRInstAbstract: ScriptBaseClass
    {
        public const int CallMode_NORMAL = 0;  // when function is called, it proceeds normally
        public const int CallMode_SAVE = 1;  // StackSaveException() was thrown, push args/locals to stackFrames
        public const int CallMode_RESTORE = 2;  // when function is called, it pops state from stackFrames

        public bool suspendOnCheckRunHold;  // suspend script execution until explicitly set false
        public bool suspendOnCheckRunTemp;  // suspend script execution for single step only
        public int stackLimit;              // stack must have at least this many bytes free on entry to functions
        public int m_StackLeft;             // total number of stack bytes yet to be used (init to stacksize)

        public ScriptObjCode m_ObjCode;     // script object code this instance was created from

        public object[] ehArgs;             // event handler argument array
        public bool doGblInit = true;       // default state_entry() needs to initialize global variables
        public int stateCode = 0;           // state the script is in (0 = 'default')
        public int newStateCode = -1;       // if >= 0, in the middle of exiting 'stateCode' and entering 'newStateCode'
        public ScriptEventCode eventCode = ScriptEventCode.None;
        // what event handler is executing (or None if not)

        public int callMode = CallMode_NORMAL;
        // to capture stack frames on stackFrames:
        //    set to CallMode_SAVE just before throwing StackSaveException()
        //    from within CheckRun() and cleared to CallMode_NORMAL when
        //    the exception is caught
        // to restore stack frames from stackFrames:
        //    set to CallMode_RESTORE just before calling CallSEH() and 
        //    cleared to CallMode_NORMAL by CheckRun()
        public XMRStackFrame stackFrames;   // stack frames being saved/restored

        private static readonly char[] justacomma = { ',' };

        /*
         * These arrays hold the global variable values for the script instance.
         * The array lengths are determined by the script compilation,
         * and are found in ScriptObjCode.glblSizes.
         */
        public XMRInstArrays glblVars;

        public XMRInstAbstract()
        {
            glblVars = new XMRInstArrays(this);
        }

        /****************************************************************\
         *  Abstract function prototypes.                               *
         *  These functions require access to the OpenSim environment.  *
        \****************************************************************/

        public abstract void CheckRunWork();
        public abstract void StateChange();

        [xmrMethodCallsCheckRunAttribute] // calls CheckRun()
        [xmrMethodIsNoisyAttribute]       // calls Stub<somethingorother>()
        public abstract LSL_List xmrEventDequeue(double timeout, int returnMask1, int returnMask2,
                                                  int backgroundMask1, int backgroundMask2);

        [xmrMethodIsNoisyAttribute]       // calls Stub<somethingorother>()
        public abstract void xmrEventEnqueue(LSL_List ev);

        [xmrMethodIsNoisyAttribute]       // calls Stub<somethingorother>()
        public abstract LSL_List xmrEventSaveDets();

        [xmrMethodIsNoisyAttribute]       // calls Stub<somethingorother>()
        public abstract void xmrEventLoadDets(LSL_List dpList);


        /**************************************************\
         *  Functions what don't require runtime support  *
         *  beyond what the compiler provides.            *
        \**************************************************/

        protected int heapLimit;
        public int m_localsHeapUsed;
        public int m_arraysHeapUsed;

        public virtual int UpdateLocalsHeapUse(int olduse, int newuse)
        {
            int newtotal = Interlocked.Add(ref m_localsHeapUsed, newuse - olduse);
            if (newtotal + glblVars.arraysHeapUse > heapLimit)
                throw new OutOfHeapException(m_arraysHeapUsed + newtotal + olduse - newuse, newtotal, heapLimit);
            return newuse;
        }
        // not in use
        public virtual int UpdateArraysHeapUse(int olduse, int newuse)
        {
            //int newtotal = Interlocked.Add(ref m_arraysheapUsed, newuse - olduse);
            if(newuse + glblVars.arraysHeapUse > heapLimit)
                throw new OutOfHeapException(m_arraysHeapUsed + newuse + olduse - newuse, newuse, heapLimit);
            return newuse;
        }

        public virtual void AddLocalsHeapUse(int delta)
        {
            Interlocked.Add(ref m_localsHeapUsed, delta);
        }

        public virtual void AddArraysHeapUse(int delta)
        {
            Interlocked.Add(ref m_arraysHeapUsed, delta);
        }

        public int xmrHeapLeft()
        {
            return heapLimit - m_localsHeapUsed - glblVars.arraysHeapUse;
        }

        public int xmrHeapUsed()
        {
            return m_localsHeapUsed + glblVars.arraysHeapUse;
        }

        /**
         * @brief Call script's event handler function from the very beginning.
         * @param instance.stateCode = which state the event is happening in
         * @param instance.eventCode = which event is happening in that state
         * @returns when event handler has completed or throws an exception
         *          with instance.eventCode = ScriptEventCode.None
         */
        public void CallSEH()
        {
            ScriptEventHandler seh;

            // CallMode_NORMAL:  run event handler from the beginning normally
            // CallMode_RESTORE: restore event handler stack from stackFrames
            callMode = (stackFrames == null) ? XMRInstAbstract.CallMode_NORMAL :
                                               XMRInstAbstract.CallMode_RESTORE;

            while(true)
            {
                if(this.newStateCode < 0)
                {
                    // Process event given by 'stateCode' and 'eventCode'.
                    // The event handler should call CheckRun() as often as convenient.
                    int newState = this.stateCode;
                    seh = this.m_ObjCode.scriptEventHandlerTable[newState, (int)this.eventCode];
                    if(seh != null)
                    {
                        try
                        {
                            seh(this);
                        }
                        catch(ScriptChangeStateException scse)
                        {
                            newState = scse.newState;
                        }
                    }
                    this.ehArgs = null;  // we are done with them and no args for
                                         // exit_state()/enter_state() anyway

                    // The usual case is no state change.
                    // Even a 'state <samestate>;' statement has no effect except to exit out.
                    // It does not execute the state_exit() or state_entry() handlers.
                    // See http://wiki.secondlife.com/wiki/State
                    if(newState == this.stateCode)
                        break;

                    // Save new state in a more permanent location in case we
                    // get serialized out while in the state_exit() handler.
                    this.newStateCode = newState;
                }

                // Call old state's state_exit() handler.
                this.eventCode = ScriptEventCode.state_exit;
                seh = this.m_ObjCode.scriptEventHandlerTable[this.stateCode, (int)ScriptEventCode.state_exit];
                if(seh != null)
                {
                    try
                    {
                        seh(this);
                    }
                    catch(ScriptChangeStateException scse)
                    {
                        this.newStateCode = scse.newState;
                    }
                }

                // Switch over to the new state's state_entry() handler.
                this.stateCode = this.newStateCode;
                this.eventCode = ScriptEventCode.state_entry;
                this.newStateCode = -1;

                // Now that the old state can't possibly start any more activity,
                // cancel any listening handlers, etc, of the old state.
                this.StateChange();

                // Loop back to execute new state's state_entry() handler.
            }

            // Event no longer being processed.
            this.eventCode = ScriptEventCode.None;
        }

        /**
         * @brief For compatibility with old code.
         */
        public void CheckRun(int line)
        {
            CheckRunStack();
        }

        /**
         * @brief Called at beginning of complex functions to see if they
         *        are nested too deep possibly in a recursive loop.
         */
        public void CheckRunStack()
        {
            if(m_StackLeft < stackLimit)
                throw new OutOfStackException();

            CheckRunQuick();
        }

        /**
         * @brief Called in each iteration of a loop to see if running too long.
         */
        public void CheckRunQuick()
        {
            //            if (suspendOnCheckRunHold || suspendOnCheckRunTemp)
            CheckRunWork();
        }

        /**
         * @brief Called during CallMode_SAVE to create a stackframe save object that saves 
         *        local variables and calling point within the function.
         * @param funcName = name of function whose frame is being saved
         * @param callNo = call number (ie, return address) within function to restart at
         * @param nSaves = number of variables the function will save
         * @returns an object[nSaves] where function can save variables
         */
        public object[] CaptureStackFrame(string funcName, int callNo, int nSaves)
        {
            XMRStackFrame sf = new XMRStackFrame();
            sf.nextSF = stackFrames;
            sf.funcName = funcName;
            sf.callNo = callNo;
            sf.objArray = new object[nSaves];
            stackFrames = sf;
            return sf.objArray;
        }

        /**
         * @brief Called during CallMode_RESTORE to pop a stackframe object to restore 
         *        local variables and calling point within the function.
         * @param funcName = name of function whose frame is being restored
         * @returns the object[nSaves] where function can retrieve variables
         *          callNo = as passed to CaptureStackFrame() indicating restart point
         */
        public object[] RestoreStackFrame(string funcName, out int callNo)
        {
            XMRStackFrame sf = stackFrames;
            if(sf.funcName != funcName)
                throw new Exception("frame mismatch " + sf.funcName + " vs " + funcName);

            callNo = sf.callNo;
            stackFrames = sf.nextSF;
            return sf.objArray;
        }

        /**
         * @brief Convert all LSL_Integers in a list to System.Int32s, 
         *        as required by llParcelMediaQuery().
         */
        public static LSL_List FixLLParcelMediaQuery(LSL_List oldlist)
        {
            object[] oldarray = oldlist.Data;
            int len = oldarray.Length;
            object[] newarray = new object[len];
            for(int i = 0; i < len; i++)
            {
                object obj = oldarray[i];
                if(obj is LSL_Integer)
                    obj = (int)(LSL_Integer)obj;
                newarray[i] = obj;
            }
            return new LSL_List(newarray);
        }

        /**
         * @brief Convert *SOME* LSL_Integers in a list to System.Int32s, 
         *        as required by llParcelMediaCommandList().
         */
        public static LSL_List FixLLParcelMediaCommandList(LSL_List oldlist)
        {
            object[] oldarray = oldlist.Data;
            int len = oldarray.Length;
            object[] newarray = new object[len];
            int verbatim = 0;
            for(int i = 0; i < len; i++)
            {
                object obj = oldarray[i];
                if(--verbatim < 0)
                {
                    if(obj is LSL_Integer)
                        obj = (int)(LSL_Integer)obj;
                    if(obj is int)
                    {
                        switch((int)obj)
                        {
                            case ScriptBaseClass.PARCEL_MEDIA_COMMAND_AUTO_ALIGN:
                                // leave next integer as LSL_Integer
                                verbatim = 1;
                                break;

                            case ScriptBaseClass.PARCEL_MEDIA_COMMAND_SIZE:
                                // leave next two integers as LSL_Integer
                                verbatim = 2;
                                break;

                        }
                    }
                }
                newarray[i] = obj;
            }
            return new LSL_List(newarray);
        }

        public static int xmrHashCode(int i)
        {
            return i.GetHashCode();
        }

        public static int xmrHashCode(double f)
        {
            return f.GetHashCode();
        }

        public static int xmrHashCode(object o)
        {
            return o.GetHashCode();
        }

        public static int xmrHashCode(string s)
        {
            return s.GetHashCode();
        }

        public string xmrTypeName(object o)
        {
            /*
             * Basic types return constant strings of the script-visible type name.
             */
            if(o is XMR_Array)
                return "array";
            if(o is bool)
                return "bool";
            if(o is char)
                return "char";
            if(o is Exception)
                return "exception";
            if(o is double)
                return "float";
            if(o is float)
                return "float";
            if(o is LSL_Float)
                return "float";
            if(o is int)
                return "integer";
            if(o is LSL_Integer)
                return "integer";
            if(o is LSL_List)
                return "list";
            if(o is LSL_Rotation)
                return "rotation";
            if(o is LSL_String)
                return "string";
            if(o is string)
                return "string";
            if(o is LSL_Vector)
                return "vector";

            // A script-defined interface is represented as an array of delegates.
            // If that is the case, convert it to the object of the script-defined 
            // class that is implementing the interface.  This should let the next 
            // step get the script-defined type name of the object.
            if(o is Delegate[])
                o = ((Delegate[])o)[0].Target;

            // If script-defined class instance, get the script-defined 
            // type name.
            if(o is XMRSDTypeClObj)
                return ((XMRSDTypeClObj)o).sdtcClass.longName.val;

            // If it's a delegate, maybe we can look up its script-defined type name.
            Type ot = o.GetType();
            if(o is Delegate)
            {
                String os;
                if(m_ObjCode.sdDelTypes.TryGetValue(ot, out os))
                    return os;
            }

            // Don't know what it is, get the C#-level type name.
            return ot.ToString();
        }

        /**
         * @brief Call the current state's event handler.
         * @param ev = as returned by xmrEventDequeue saying which event handler to call
         *             and what argument list to pass to it.  The llDetect...() parameters
         *             are as currently set for the script (use xmrEventLoadDets to set how
         *             you want them to be different).
         */
        public void xmrEventCallHandler(LSL_List ev)
        {
            object[] data = ev.Data;
            int evc = (int)(ev.GetLSLIntegerItem(0).value & 0xFFFFFFFF);
            ScriptEventHandler seh = m_ObjCode.scriptEventHandlerTable[stateCode, evc];
            if(seh != null)
            {
                int nargs = data.Length - 1;
                object[] args = new object[nargs];
                Array.Copy(data, 1, args, 0, nargs);

                object[] saveEHArgs = this.ehArgs;
                ScriptEventCode saveEventCode = this.eventCode;

                this.ehArgs = args;
                this.eventCode = (ScriptEventCode)evc;

                seh(this);

                this.ehArgs = saveEHArgs;
                this.eventCode = saveEventCode;
            }
        }

        /**
         * @brief These conversions throw exceptions if there is anything stinky...
         */
        public double xmrString2Float(string s)
        {
            return double.Parse(s, CultureInfo.InvariantCulture);
        }

        public int xmrString2Integer(string s)
        {
            s = s.Trim();
            if(s.StartsWith("0x") || s.StartsWith("0X"))
                return int.Parse(s.Substring(2), NumberStyles.HexNumber);

            return int.Parse(s, CultureInfo.InvariantCulture);
        }

        public LSL_Rotation xmrString2Rotation(string s)
        {
            s = s.Trim();
            if(!s.StartsWith("<") || !s.EndsWith(">"))
                throw new FormatException("doesn't begin with < and end with >");

            s = s.Substring(1, s.Length - 2);
            string[] splitup = s.Split(justacomma, 5);
            if(splitup.Length != 4)
                throw new FormatException("doesn't have exactly 3 commas");

            double x = double.Parse(splitup[0], CultureInfo.InvariantCulture);
            double y = double.Parse(splitup[1], CultureInfo.InvariantCulture);
            double z = double.Parse(splitup[2], CultureInfo.InvariantCulture);
            double w = double.Parse(splitup[3], CultureInfo.InvariantCulture);
            return new LSL_Rotation(x, y, z, w);
        }

        public LSL_Vector xmrString2Vector(string s)
        {
            s = s.Trim();
            if(!s.StartsWith("<") || !s.EndsWith(">"))
                throw new FormatException("doesn't begin with < and end with >");

            s = s.Substring(1, s.Length - 2);
            string[] splitup = s.Split(justacomma, 4);
            if(splitup.Length != 3)
                throw new FormatException("doesn't have exactly 2 commas");

            double x = double.Parse(splitup[0], CultureInfo.InvariantCulture);
            double y = double.Parse(splitup[1], CultureInfo.InvariantCulture);
            double z = double.Parse(splitup[2], CultureInfo.InvariantCulture);
            return new LSL_Vector(x, y, z);
        }

        /**
         * @brief Access C#-style formatted numeric conversions.
         */
        public string xmrFloat2String(double val, string fmt)
        {
            return val.ToString(fmt, CultureInfo.InvariantCulture);
        }

        public string xmrInteger2String(int val, string fmt)
        {
            return val.ToString(fmt, CultureInfo.InvariantCulture);
        }

        public string xmrRotation2String(LSL_Rotation val, string fmt)
        {
            return "<" + val.x.ToString(fmt, CultureInfo.InvariantCulture) + "," +
                         val.y.ToString(fmt, CultureInfo.InvariantCulture) + "," +
                         val.z.ToString(fmt, CultureInfo.InvariantCulture) + "," +
                         val.s.ToString(fmt, CultureInfo.InvariantCulture) + ">";
        }

        public string xmrVector2String(LSL_Vector val, string fmt)
        {
            return "<" + val.x.ToString(fmt, CultureInfo.InvariantCulture) + "," +
                         val.y.ToString(fmt, CultureInfo.InvariantCulture) + "," +
                         val.z.ToString(fmt, CultureInfo.InvariantCulture) + ">";
        }

        /**
         * @brief Get a delegate for a script-defined function.
         * @param name = name of the function including arg types, eg,
         *               "Verify(array,list,string)"
         * @param sig  = script-defined type name
         * @param targ = function's 'this' pointer or null if static
         * @returns delegate for the script-defined function
         */
        public Delegate GetScriptMethodDelegate(string name, string sig, object targ)
        {
            DynamicMethod dm = m_ObjCode.dynamicMethods[name];
            TokenDeclSDTypeDelegate dt = (TokenDeclSDTypeDelegate)m_ObjCode.sdObjTypesName[sig];
            return dm.CreateDelegate(dt.GetSysType(), targ);
        }

        /**
         * @brief Try to cast the thrown object to the given script-defined type.
         * @param thrown = what object was thrown
         * @param inst = what script instance we are running in
         * @param sdtypeindex = script-defined type to try to cast it to
         * @returns null: thrown is not castable to sdtypename
         *          else: an object casted to sdtypename
         */
        public static object XMRSDTypeCatchTryCastToSDType(object thrown, XMRInstAbstract inst, int sdtypeindex)
        {
            TokenDeclSDType sdType = inst.m_ObjCode.sdObjTypesIndx[sdtypeindex];

            // If it is a script-defined interface object, convert to the original XMRSDTypeClObj.
            if(thrown is Delegate[])
            {
                thrown = ((Delegate[])thrown)[0].Target;
            }

            // If it is a script-defined delegate object, make sure it is an instance of the expected type.
            if(thrown is Delegate)
            {
                Type ot = thrown.GetType();
                Type tt = sdType.GetSysType();
                return (ot == tt) ? thrown : null;
            }

            // If it is a script-defined class object, make sure it is an instance of the expected class.
            if(thrown is XMRSDTypeClObj)
            {
                // Step from the object's actual class rootward.
                // If we find the requested class along the way, the cast is valid.
                // If we run off the end of the root, the cast is not valid.
                for(TokenDeclSDTypeClass ac = ((XMRSDTypeClObj)thrown).sdtcClass; ac != null; ac = ac.extends)
                {
                    if(ac == sdType)
                        return thrown;
                }
            }

            // Don't know what it is, assume it is not what caller wants.
            return null;
        }

        /**
         * @brief Allocate and access fixed-dimension arrays.
         */
        public static object xmrFixedArrayAllocC(int len)
        {
            return new char[len];
        }
        public static object xmrFixedArrayAllocF(int len)
        {
            return new double[len];
        }
        public static object xmrFixedArrayAllocI(int len)
        {
            return new int[len];
        }
        public static object xmrFixedArrayAllocO(int len)
        {
            return new object[len];
        }

        public static char xmrFixedArrayGetC(object arr, int idx)
        {
            return ((char[])arr)[idx];
        }
        public static double xmrFixedArrayGetF(object arr, int idx)
        {
            return ((double[])arr)[idx];
        }
        public static int xmrFixedArrayGetI(object arr, int idx)
        {
            return ((int[])arr)[idx];
        }
        public static object xmrFixedArrayGetO(object arr, int idx)
        {
            return ((object[])arr)[idx];
        }

        public static void xmrFixedArraySetC(object arr, int idx, char val)
        {
            ((char[])arr)[idx] = val;
        }
        public static void xmrFixedArraySetF(object arr, int idx, double val)
        {
            ((double[])arr)[idx] = val;
        }
        public static void xmrFixedArraySetI(object arr, int idx, int val)
        {
            ((int[])arr)[idx] = val;
        }
        public static void xmrFixedArraySetO(object arr, int idx, object val)
        {
            ((object[])arr)[idx] = val;
        }

        /**
         * @brief Copy from one script-defined array to another.
         * @param srcobj = source script-defined array class object pointer
         * @param srcstart = offset in source array to start copying from
         * @param dstobj = destination script-defined array class object pointer
         * @param dststart = offset in destination arry to start copying to
         * @param count = number of elements to copy
         */
        public static void xmrArrayCopy(object srcobj, int srcstart, object dstobj, int dststart, int count)
        {
            // The script writer should only pass us script-defined class objects.
            // Throw exception otherwise.
            XMRSDTypeClObj srcsdt = (XMRSDTypeClObj)srcobj;
            XMRSDTypeClObj dstsdt = (XMRSDTypeClObj)dstobj;

            // Get the script-visible type name of the arrays, brackets and all.
            string srctypename = srcsdt.sdtcClass.longName.val;
            string dsttypename = dstsdt.sdtcClass.longName.val;

            // The part before the first '[' of each should match exactly,
            // meaning the basic data type (eg, float, List<string>) is the same.
            // And there must be a '[' in each meaning that it is a script-defined array type.
            int i = srctypename.IndexOf('[');
            int j = dsttypename.IndexOf('[');
            if((i < 0) || (j < 0))
                throw new InvalidCastException("non-array passed: " + srctypename + " and/or " + dsttypename);
            if((i != j) || !srctypename.StartsWith(dsttypename.Substring(0, j)))
                throw new ArrayTypeMismatchException(srctypename + " vs " + dsttypename);

            // The number of brackets must match exactly.
            // This permits copying from something like a float[,][] to something like a float[][].
            // But you cannot copy from a float[][] to a float[] or wisa wersa.
            // Counting either '[' or ']' would work equally well.
            int srclen = srctypename.Length;
            int dstlen = dsttypename.Length;
            int srcjags = 0;
            int dstjags = 0;
            while(++i < srclen)
                if(srctypename[i] == ']')
                    srcjags++;
            while(++j < dstlen)
                if(dsttypename[j] == ']')
                    dstjags++;
            if(dstjags != srcjags)
                throw new ArrayTypeMismatchException(srctypename + " vs " + dsttypename);

            // Perform the copy.
            Array srcarray = (Array)srcsdt.instVars.iarObjects[0];
            Array dstarray = (Array)dstsdt.instVars.iarObjects[0];
            Array.Copy(srcarray, srcstart, dstarray, dststart, count);
        }

        /**
         * @brief Copy from an array to a list.
         * @param srcar = the array to copy from
         * @param start = where to start in the array
         * @param count = number of elements
         * @returns the list
         */
        public static LSL_List xmrArray2List(object srcar, int start, int count)
        {
            // Get the script-visible type of the array.
            // We only do arrays.
            XMRSDTypeClObj array = (XMRSDTypeClObj)srcar;
            TokenDeclSDTypeClass sdtClass = array.sdtcClass;
            if(sdtClass.arrayOfRank == 0)
                throw new InvalidCastException("only do arrays not " + sdtClass.longName.val);

            // Validate objects they want to put in the list.
            // We can't allow anything funky that OpenSim runtime doesn't expect.
            Array srcarray = (Array)array.instVars.iarObjects[0];
            object[] output = new object[count];
            for(int i = 0; i < count; i++)
            {
                object src = srcarray.GetValue(i + start);
                if(src == null)
                    throw new NullReferenceException("null element " + i);
                if(src is double)
                {
                    output[i] = new LSL_Float((double)src);
                    continue;
                }
                if(src is int)
                {
                    output[i] = new LSL_Integer((int)src);
                    continue;
                }
                if(src is LSL_Rotation)
                {
                    output[i] = src;
                    continue;
                }
                if(src is LSL_Vector)
                {
                    output[i] = src;
                    continue;
                }
                if(src is string)
                {
                    output[i] = new LSL_String((string)src);
                    continue;
                }
                throw new InvalidCastException("invalid element " + i + " type " + src.GetType().Name);
            }

            // Make a list out of that now immutable array.
            return new LSL_List(output);
        }

        /**
         * @brief Copy from a list to an array.
         * @param srclist  = list to copy from
         * @param srcstart = where to start in the list
         * @param dstobj   = array to copy to
         * @param dststart = where to start in the array
         * @param count    = number of elements
         */
        public static void xmrList2Array(LSL_List srclist, int srcstart, object dstobj, int dststart, int count)
        {
            // Get the script-visible type of the destination.
            // We only do arrays.
            XMRSDTypeClObj dstarray = (XMRSDTypeClObj)dstobj;
            TokenDeclSDTypeClass sdtClass = dstarray.sdtcClass;
            if(sdtClass.arrayOfType == null)
                throw new InvalidCastException("only do arrays not " + sdtClass.longName.val);

            // Copy from the immutable array to the mutable array.
            // Strip off any LSL wrappers as the script code doesn't expect any.
            object[] srcarr = srclist.Data;
            Array dstarr = (Array)dstarray.instVars.iarObjects[0];

            for(int i = 0; i < count; i++)
            {
                object obj = srcarr[i + srcstart];
                if(obj is LSL_Float)
                    obj = ((LSL_Float)obj).value;
                else if(obj is LSL_Integer)
                    obj = ((LSL_Integer)obj).value;
                else if(obj is LSL_String)
                    obj = ((LSL_String)obj).m_string;
                dstarr.SetValue(obj, i + dststart);
            }
        }

        /**
         * @brief Copy from an array of characters to a string.
         * @param srcar = the array to copy from
         * @param start = where to start in the array
         * @param count = number of elements
         * @returns the string
         */
        public static string xmrChars2String(object srcar, int start, int count)
        {
            // Make sure they gave us a script-defined array object.
            XMRSDTypeClObj array = (XMRSDTypeClObj)srcar;
            TokenDeclSDTypeClass sdtClass = array.sdtcClass;
            if(sdtClass.arrayOfRank == 0)
                throw new InvalidCastException("only do arrays not " + sdtClass.longName.val);

            // We get a type cast error from mono if they didn't give us a character array.
            // But if it is ok, create a string from the requested characters.
            char[] srcarray = (char[])array.instVars.iarObjects[0];
            return new string(srcarray, start, count);
        }

        /**
         * @brief Copy from a string to a character array.
         * @param srcstr   = string to copy from
         * @param srcstart = where to start in the string
         * @param dstobj   = array to copy to
         * @param dststart = where to start in the array
         * @param count    = number of elements
         */
        public static void xmrString2Chars(string srcstr, int srcstart, object dstobj, int dststart, int count)
        {
            // Make sure they gave us a script-defined array object.
            XMRSDTypeClObj dstarray = (XMRSDTypeClObj)dstobj;
            TokenDeclSDTypeClass sdtClass = dstarray.sdtcClass;
            if(sdtClass.arrayOfType == null)
                throw new InvalidCastException("only do arrays not " + sdtClass.longName.val);

            // We get a type cast error from mono if they didn't give us a character array.
            // But if it is ok, copy from the string to the character array.
            char[] dstarr = (char[])dstarray.instVars.iarObjects[0];
            for(int i = 0; i < count; i++)
                dstarr[i + dststart] = srcstr[i + srcstart];
        }

        /**
         * @brief Exception-related runtime calls.
         */
        // Return exception message (no type information just the message)
        public static string xmrExceptionMessage(Exception ex)
        {
            return ex.Message;
        }

        // Return stack trace (no type or message, just stack trace lines: at ... \n)
        public string xmrExceptionStackTrace(Exception ex)
        {
            return XMRExceptionStackString(ex);
        }

        // Return value thrown by a throw statement
        public static object xmrExceptionThrownValue(Exception ex)
        {
            return ((ScriptThrownException)ex).thrown;
        }

        // Return exception's short type name, eg, NullReferenceException, ScriptThrownException, etc.
        public static string xmrExceptionTypeName(Exception ex)
        {
            return ex.GetType().Name;
        }

        // internal use only: converts any IL addresses in script-defined methods to source location equivalent
        // Mono ex.StackTrace:
        //   at OpenSim.Region.ScriptEngine.YEngine.TypeCast.ObjectToInteger (System.Object x) [0x0005e] in /home/kunta/opensim-0.9/addon-modules/YEngine/Module/MMRScriptTypeCast.cs:750
        //   at (wrapper dynamic-method) System.Object:default state_entry (OpenSim.Region.ScriptEngine.YEngine.XMRInstAbstract) [0x00196]

        // Microsoft ex.StackTrace:
        //    at OpenSim.Region.ScriptEngine.YEngine.TypeCast.ObjectToInteger(Object x) in C:\Users\mrieker\opensim-0.9-source\addon-modules\YEngine\Module\MMRScriptTypeCast.cs:line 750
        //    at default state_entry (XMRInstAbstract )
        public string XMRExceptionStackString(Exception ex)
        {
            string stwhole = ex.StackTrace;
            string[] stlines = stwhole.Split(new char[] { '\n' });
            StringBuilder sb = new StringBuilder();
            foreach(string st in stlines)
            {
                string stline = st.Trim();
                if(stline == "")
                    continue;

                // strip 'at' off the front of line
                if(stline.StartsWith("at "))
                {
                    stline = stline.Substring(3);
                }

                // strip '(wrapper ...' off front of line
                if(stline.StartsWith("(wrapper dynamic-method) System.Object:"))
                {
                    stline = stline.Substring(39);
                }

                // strip the (systemargtypes...) from our dynamic method names cuz it's messy
                //  'default state_entry (XMRInstAbstract )'
                //      => 'default state_entry'
                //  'CallSomethingThatThrows(string) (OpenSim.Region.ScriptEngine.YEngine.XMRInstance,string)'
                //      => 'CallSomethingThatThrows(string)'
                int kwin = stline.IndexOf(" in ");
                int br0x = stline.IndexOf(" [0x");
                int pastCloseParen = stline.Length;
                if((kwin >= 0) && (br0x >= 0))
                    pastCloseParen = Math.Min(kwin, br0x);
                else if(kwin >= 0)
                    pastCloseParen = kwin;
                else if(br0x >= 0)
                    pastCloseParen = br0x;
                else
                    pastCloseParen = stline.Length;
                int endFuncName = pastCloseParen;
                while(endFuncName > 0)
                {
                    if(stline[--endFuncName] == '(')
                        break;
                }
                while(endFuncName > 0)
                {
                    if(stline[endFuncName - 1] != ' ')
                        break;
                    --endFuncName;
                }
                string funcName = stline.Substring(0, endFuncName);
                KeyValuePair<int, ScriptSrcLoc>[] srcLocs;
                if(m_ObjCode.scriptSrcLocss.TryGetValue(funcName, out srcLocs))
                {
                    stline = stline.Substring(0, endFuncName) + stline.Substring(pastCloseParen);
                    kwin = stline.IndexOf(" in ");
                    br0x = stline.IndexOf(" [0x");
                }

                // keyword 'in' is just before filename:linenumber that goes to end of line
                // trim up the corresponding filename (ie, remove useless path info)
                if(kwin >= 0)
                {
                    int begfn = kwin + 4;
                    int slash = begfn;
                    for(int i = begfn; i < stline.Length; i++)
                    {
                        char c = stline[i];
                        if((c == '/') || (c == '\\'))
                            slash = i + 1;
                    }
                    stline = stline.Substring(0, begfn) + stline.Substring(slash);
                }
                else if(srcLocs != null)
                {

                    // no filename:linenumber info, try to convert IL offset
                    if(br0x >= 0)
                    {
                        try
                        {
                            int begiloffs = br0x + 4;
                            int endiloffs = stline.IndexOf("]", begiloffs);
                            int iloffset = int.Parse(stline.Substring(begiloffs, endiloffs - begiloffs),
                                                       System.Globalization.NumberStyles.HexNumber);

                            int srcLocIdx;
                            int srcLocLen = srcLocs.Length;
                            for(srcLocIdx = 0; ++srcLocIdx < srcLocLen;)
                            {
                                if(iloffset < srcLocs[srcLocIdx].Key)
                                    break;
                            }
                            ScriptSrcLoc srcLoc = srcLocs[--srcLocIdx].Value;

                            stline = stline.Substring(0, br0x) + " <" +
                                        srcLoc.file + '(' + srcLoc.line + ',' + srcLoc.posn + ")>";
                        }
                        catch
                        {
                        }
                    }
                }

                // put edited line in output string
                if(sb.Length > 0)
                    sb.AppendLine();
                sb.Append("  at ");
                sb.Append(stline);
            }
            return sb.ToString();
        }

        /**
         * @brief List fonts available.
         */
        public LSL_List xmrFontsAvailable()
        {
            System.Drawing.FontFamily[] families = System.Drawing.FontFamily.Families;
            object[] output = new object[families.Length];
            for(int i = 0; i < families.Length; i++)
                output[i] = new LSL_String(families[i].Name);

            return new LSL_List(output);
        }

        /************************\
         *  Used by decompiler  *
        \************************/

        public bool xmrRotationToBool(LSL_Rotation x)
        {
            return TypeCast.RotationToBool(x);
        }
        public bool xmrStringToBool(string x)
        {
            return TypeCast.StringToBool(x);
        }
        public bool xmrVectorToBool(LSL_Vector x)
        {
            return TypeCast.VectorToBool(x);
        }
        public bool xmrKeyToBool(string x)
        {
            return TypeCast.KeyToBool(x);
        }
        public bool xmrListToBool(LSL_List x)
        {
            return TypeCast.ListToBool(x);
        }

        public int xmrStringCompare(string x, string y)
        {
            return string.Compare(x, y);
        }

        /**
         * @brief types of data we serialize
         */
        private enum Ser: byte
        {
            NULL,
            EVENTCODE,
            LSLFLOAT,
            LSLINT,
            LSLKEY,
            LSLLIST,
            LSLROT,
            LSLSTR,
            LSLVEC,
            SYSARRAY,
            SYSDOUB,
            SYSFLOAT,
            SYSINT,
            SYSSTR,
            XMRARRAY,
            DUPREF,
            SYSBOOL,
            XMRINST,
            DELEGATE,
            SDTCLOBJ,
            SYSCHAR,
            SYSERIAL,
            THROWNEX
        }

        /**
         * @brief Write state out to a stream.
         *        Do not change script state.
         */
        public void MigrateOut(BinaryWriter mow)
        {
            try
            {
                this.migrateOutWriter = mow;
                this.migrateOutObjects = new Dictionary<object, int>();
                this.migrateOutLists = new Dictionary<object[], ObjLslList>();
                this.SendObjValue(this.ehArgs);
                mow.Write(this.doGblInit);
                mow.Write(this.stateCode);
                mow.Write((int)this.eventCode);
                this.glblVars.SendArrays(this.SendObjValue);
                if(this.newStateCode >= 0)
                {
                    mow.Write("**newStateCode**");
                    mow.Write(this.newStateCode);
                }
                for(XMRStackFrame thisSF = this.stackFrames; thisSF != null; thisSF = thisSF.nextSF)
                {
                    mow.Write(thisSF.funcName);
                    mow.Write(thisSF.callNo);
                    this.SendObjValue(thisSF.objArray);
                }
                mow.Write("");
            }
            finally
            {
                this.migrateOutWriter = null;
                this.migrateOutObjects = null;
                this.migrateOutLists = null;
            }
        }

        /**
         * @brief Write an object to the output stream.
         * @param graph = object to send
         */
        private BinaryWriter migrateOutWriter;
        private Dictionary<object, int> migrateOutObjects;
        private Dictionary<object[], ObjLslList> migrateOutLists;
        public void SendObjValue(object graph)
        {
            BinaryWriter mow = this.migrateOutWriter;

            // Value types (including nulls) are always output directly.
            if(graph == null)
            {
                mow.Write((byte)Ser.NULL);
                return;
            }
            if(graph is ScriptEventCode)
            {
                mow.Write((byte)Ser.EVENTCODE);
                mow.Write((int)graph);
                return;
            }
            if(graph is LSL_Float)
            {
                mow.Write((byte)Ser.LSLFLOAT);
                mow.Write((double)((LSL_Float)graph).value);
                return;
            }
            if(graph is LSL_Integer)
            {
                mow.Write((byte)Ser.LSLINT);
                mow.Write((int)((LSL_Integer)graph).value);
                return;
            }
            if(graph is LSL_Key)
            {
                mow.Write((byte)Ser.LSLKEY);
                LSL_Key key = (LSL_Key)graph;
                SendObjValue(key.m_string);  // m_string can be null
                return;
            }
            if(graph is LSL_Rotation)
            {
                mow.Write((byte)Ser.LSLROT);
                mow.Write((double)((LSL_Rotation)graph).x);
                mow.Write((double)((LSL_Rotation)graph).y);
                mow.Write((double)((LSL_Rotation)graph).z);
                mow.Write((double)((LSL_Rotation)graph).s);
                return;
            }
            if(graph is LSL_String)
            {
                mow.Write((byte)Ser.LSLSTR);
                LSL_String str = (LSL_String)graph;
                SendObjValue(str.m_string);  // m_string can be null
                return;
            }
            if(graph is LSL_Vector)
            {
                mow.Write((byte)Ser.LSLVEC);
                mow.Write((double)((LSL_Vector)graph).x);
                mow.Write((double)((LSL_Vector)graph).y);
                mow.Write((double)((LSL_Vector)graph).z);
                return;
            }
            if(graph is bool)
            {
                mow.Write((byte)Ser.SYSBOOL);
                mow.Write((bool)graph);
                return;
            }
            if(graph is double)
            {
                mow.Write((byte)Ser.SYSDOUB);
                mow.Write((double)graph);
                return;
            }
            if(graph is float)
            {
                mow.Write((byte)Ser.SYSFLOAT);
                mow.Write((float)graph);
                return;
            }
            if(graph is int)
            {
                mow.Write((byte)Ser.SYSINT);
                mow.Write((int)graph);
                return;
            }
            if(graph is char)
            {
                mow.Write((byte)Ser.SYSCHAR);
                mow.Write((char)graph);
                return;
            }

            // Script instance pointer is always just that.
            if(graph == this)
            {
                mow.Write((byte)Ser.XMRINST);
                return;
            }

            // Convert lists to object type.
            // This is compatible with old migration data and also
            // two vars pointing to same list won't duplicate it.
            if(graph is LSL_List)
            {
                object[] data = ((LSL_List)graph).Data;
                ObjLslList oll;
                if(!this.migrateOutLists.TryGetValue(data, out oll))
                {
                    oll = new ObjLslList();
                    oll.objarray = data;
                    this.migrateOutLists[data] = oll;
                }
                graph = oll;
            }

            // If this same exact object was already serialized,
            // just output an index telling the receiver to use
            // that same old object, rather than creating a whole
            // new object with the same values.  Also this prevents
            // self-referencing objects (like arrays) from causing
            // an infinite loop.
            int ident;
            if(this.migrateOutObjects.TryGetValue(graph, out ident))
            {
                mow.Write((byte)Ser.DUPREF);
                mow.Write(ident);
                return;
            }

            // Object not seen before, save its address with an unique
            // ident number that the receiver can easily regenerate.
            ident = this.migrateOutObjects.Count;
            this.migrateOutObjects.Add(graph, ident);

            // Now output the object's value(s).
            // If the object self-references, the object is alreay entered
            // in the dictionary and so the self-reference will just emit
            // a DUPREF tag instead of trying to output the whole object 
            // again.
            if(graph is ObjLslList)
            {
                mow.Write((byte)Ser.LSLLIST);
                ObjLslList oll = (ObjLslList)graph;
                SendObjValue(oll.objarray);
            }
            else if(graph is XMR_Array)
            {
                mow.Write((byte)Ser.XMRARRAY);
                ((XMR_Array)graph).SendArrayObj(this.SendObjValue);
            }
            else if(graph is Array)
            {
                Array array = (Array)graph;
                mow.Write((byte)Ser.SYSARRAY);
                mow.Write(SysType2String(array.GetType().GetElementType()));
                mow.Write((int)array.Length);
                for(int i = 0; i < array.Length; i++)
                    this.SendObjValue(array.GetValue(i));
            }
            else if(graph is string)
            {
                mow.Write((byte)Ser.SYSSTR);
                mow.Write((string)graph);
            }
            else if(graph is Delegate)
            {
                Delegate del = (Delegate)graph;
                mow.Write((byte)Ser.DELEGATE);
                mow.Write(del.Method.Name);
                Type delType = del.GetType();
                foreach(KeyValuePair<string, TokenDeclSDType> kvp in m_ObjCode.sdObjTypesName)
                {
                    TokenDeclSDType sdt = kvp.Value;
                    if(sdt is TokenDeclSDTypeDelegate)
                    {
                        TokenDeclSDTypeDelegate sdtd = (TokenDeclSDTypeDelegate)sdt;
                        if(sdtd.GetSysType() == delType)
                        {
                            mow.Write(kvp.Key);
                            goto found;
                        }
                    }
                }
                throw new Exception("cant find script-defined delegate for " + del.Method.Name + " type " + del.GetType());
                found:
                SendObjValue(del.Target);
            }
            else if(graph is XMRSDTypeClObj)
            {
                mow.Write((byte)Ser.SDTCLOBJ);
                ((XMRSDTypeClObj)graph).Capture(this.SendObjValue);
            }
            else if(graph is ScriptThrownException)
            {
                MemoryStream memoryStream = new MemoryStream();
                System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bformatter =
                        new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                bformatter.Serialize(memoryStream, graph);
                byte[] rawBytes = memoryStream.ToArray();
                mow.Write((byte)Ser.THROWNEX);
                mow.Write((int)rawBytes.Length);
                mow.Write(rawBytes);
                SendObjValue(((ScriptThrownException)graph).thrown);
            }
            else
            {
                MemoryStream memoryStream = new MemoryStream();
                System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bformatter =
                        new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                bformatter.Serialize(memoryStream, graph);
                byte[] rawBytes = memoryStream.ToArray();
                mow.Write((byte)Ser.SYSERIAL);
                mow.Write((int)rawBytes.Length);
                mow.Write(rawBytes);
            }
        }

        /**
         * @brief Use short strings for known type names.
         */
        private static string SysType2String(Type type)
        {
            if(type.IsArray && (type.GetArrayRank() == 1))
            {
                string str = KnownSysType2String(type.GetElementType());
                if(str != null)
                    return str + "[]";
            }
            else
            {
                string str = KnownSysType2String(type);
                if(str != null)
                    return str;
            }
            return type.ToString();
        }
        private static string KnownSysType2String(Type type)
        {
            if(type == typeof(bool))
                return "bo";
            if(type == typeof(char))
                return "ch";
            if(type == typeof(Delegate))
                return "de";
            if(type == typeof(double))
                return "do";
            if(type == typeof(float))
                return "fl";
            if(type == typeof(int))
                return "in";
            if(type == typeof(LSL_List))
                return "li";
            if(type == typeof(object))
                return "ob";
            if(type == typeof(LSL_Rotation))
                return "ro";
            if(type == typeof(XMRSDTypeClObj))
                return "sc";
            if(type == typeof(string))
                return "st";
            if(type == typeof(LSL_Vector))
                return "ve";
            if(type == typeof(XMR_Array))
                return "xa";
            return null;
        }
        private static Type String2SysType(string str)
        {
            if(str.EndsWith("[]"))
                return String2SysType(str.Substring(0, str.Length - 2)).MakeArrayType();

            if(str == "bo")
                return typeof(bool);
            if(str == "ch")
                return typeof(char);
            if(str == "de")
                return typeof(Delegate);
            if(str == "do")
                return typeof(double);
            if(str == "fl")
                return typeof(float);
            if(str == "in")
                return typeof(int);
            if(str == "li")
                return typeof(LSL_List);
            if(str == "ob")
                return typeof(object);
            if(str == "ro")
                return typeof(LSL_Rotation);
            if(str == "sc")
                return typeof(XMRSDTypeClObj);
            if(str == "st")
                return typeof(string);
            if(str == "ve")
                return typeof(LSL_Vector);
            if(str == "xa")
                return typeof(XMR_Array);
            return Type.GetType(str, true);
        }

        /**
         * @brief Read state in from a stream.
         */
        public void MigrateIn(BinaryReader mir)
        {
            try
            {
                this.migrateInReader = mir;
                this.migrateInObjects = new Dictionary<int, object>();
                this.ehArgs = (object[])this.RecvObjValue();
                this.doGblInit = mir.ReadBoolean();
                this.stateCode = mir.ReadInt32();
                this.eventCode = (ScriptEventCode)mir.ReadInt32();
                this.newStateCode = -1;
                this.glblVars.RecvArrays(this.RecvObjValue);
                XMRStackFrame lastSF = null;
                string funcName;
                while((funcName = mir.ReadString()) != "")
                {
                    if(funcName == "**newStateCode**")
                    {
                        this.newStateCode = mir.ReadInt32();
                        continue;
                    }
                    XMRStackFrame thisSF = new XMRStackFrame();
                    thisSF.funcName = funcName;
                    thisSF.callNo = mir.ReadInt32();
                    thisSF.objArray = (object[])this.RecvObjValue();
                    if(lastSF == null)
                        this.stackFrames = thisSF;
                    else
                        lastSF.nextSF = thisSF;
                    lastSF = thisSF;
                }
            }
            finally
            {
                this.migrateInReader = null;
                this.migrateInObjects = null;
            }
        }

        /**
         * @brief Read a single value from the stream.
         * @returns value (boxed as needed)
         */
        private BinaryReader migrateInReader;
        private Dictionary<int, object> migrateInObjects;
        public object RecvObjValue()
        {
            BinaryReader mir = this.migrateInReader;
            int ident = this.migrateInObjects.Count;
            Ser code = (Ser)mir.ReadByte();
            switch(code)
            {
                case Ser.NULL:
                    return null;

                case Ser.EVENTCODE:
                    return (ScriptEventCode)mir.ReadInt32();

                case Ser.LSLFLOAT:
                    return new LSL_Float(mir.ReadDouble());

                case Ser.LSLINT:
                    return new LSL_Integer(mir.ReadInt32());

                case Ser.LSLKEY:
                    return new LSL_Key((string)RecvObjValue());

                case Ser.LSLLIST:
                {
                    this.migrateInObjects.Add(ident, null);    // placeholder
                    object[] data = (object[])RecvObjValue();  // read data, maybe using another index
                    LSL_List list = new LSL_List(data);        // make LSL-level list
                    this.migrateInObjects[ident] = list;        // fill in slot
                    return list;
                }

                case Ser.LSLROT:
                {
                    double x = mir.ReadDouble();
                    double y = mir.ReadDouble();
                    double z = mir.ReadDouble();
                    double w = mir.ReadDouble();
                    return new LSL_Rotation(x, y, z, w);
                }
                case Ser.LSLSTR:
                    return new LSL_String((string)RecvObjValue());

                case Ser.LSLVEC:
                {
                    double x = mir.ReadDouble();
                    double y = mir.ReadDouble();
                    double z = mir.ReadDouble();
                    return new LSL_Vector(x, y, z);
                }

                case Ser.SYSARRAY:
                {
                    Type eletype = String2SysType(mir.ReadString());
                    int length = mir.ReadInt32();
                    Array array = Array.CreateInstance(eletype, length);
                    this.migrateInObjects.Add(ident, array);
                    for(int i = 0; i < length; i++)
                        array.SetValue(RecvObjValue(), i);
                    return array;
                }

                case Ser.SYSBOOL:
                    return mir.ReadBoolean();

                case Ser.SYSDOUB:
                    return mir.ReadDouble();

                case Ser.SYSFLOAT:
                    return mir.ReadSingle();

                case Ser.SYSINT:
                    return mir.ReadInt32();

                case Ser.SYSCHAR:
                    return mir.ReadChar();

                case Ser.SYSSTR:
                    string s = mir.ReadString();
                    this.migrateInObjects.Add(ident, s);
                    return s;

                case Ser.XMRARRAY:
                {
                    XMR_Array array = new XMR_Array(this);
                    this.migrateInObjects.Add(ident, array);
                    array.RecvArrayObj(this.RecvObjValue);
                    return array;
                }

                case Ser.DUPREF:
                {
                    ident = mir.ReadInt32();
                    object obj = this.migrateInObjects[ident];
                    if(obj is ObjLslList)
                        obj = new LSL_List(((ObjLslList)obj).objarray);
                    return obj;
                }

                case Ser.XMRINST:
                    return this;

                case Ser.DELEGATE:
                    this.migrateInObjects.Add(ident, null);  // placeholder
                    string name = mir.ReadString();         // function name
                    string sig = mir.ReadString();         // delegate type
                    object targ = this.RecvObjValue();      // 'this' object
                    Delegate del = this.GetScriptMethodDelegate(name, sig, targ);
                    this.migrateInObjects[ident] = del;       // actual value
                    return del;

                case Ser.SDTCLOBJ:
                    XMRSDTypeClObj clobj = new XMRSDTypeClObj();
                    this.migrateInObjects.Add(ident, clobj);
                    clobj.Restore(this, this.RecvObjValue);
                    return clobj;

                case Ser.SYSERIAL:
                {
                    int rawLength = mir.ReadInt32();
                    byte[] rawBytes = mir.ReadBytes(rawLength);
                    MemoryStream memoryStream = new MemoryStream(rawBytes);
                    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bformatter =
                            new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    object graph = bformatter.Deserialize(memoryStream);
                    this.migrateInObjects.Add(ident, graph);
                    return graph;
                }

                case Ser.THROWNEX:
                {
                    int rawLength = mir.ReadInt32();
                    byte[] rawBytes = mir.ReadBytes(rawLength);
                    MemoryStream memoryStream = new MemoryStream(rawBytes);
                    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bformatter =
                            new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    object graph = bformatter.Deserialize(memoryStream);
                    this.migrateInObjects.Add(ident, graph);
                    ((ScriptThrownException)graph).thrown = RecvObjValue();
                    return graph;
                }

                default:
                    throw new Exception("bad stream code " + code.ToString());
            }
        }

        // wrapper around list object arrays to make sure they are always object types for migration purposes
        private class ObjLslList
        {
            public object[] objarray;
        }
    }

    // Any xmr...() methods that call CheckRun() must be tagged with this attribute
    // so the ScriptCodeGen will know the method is non-trivial.
    public class xmrMethodCallsCheckRunAttribute: Attribute
    {
    }

    // Any xmr...() methods in xmrengtest that call Stub<somethingorother>() must be 
    // tagged with this attribute so the -builtins option will tell the user that 
    // they are a stub function.
    public class xmrMethodIsNoisyAttribute: Attribute
    {
    }

    // Any script callable methods that really return a key not a string should be
    // tagged with this attribute so the compiler will know they return type key and
    // not type string.
    public class xmrMethodReturnsKeyAttribute: Attribute
    {
    }

    [SerializableAttribute]
    public class OutOfHeapException: Exception
    {
        public OutOfHeapException(int oldtotal, int newtotal, int limit)
                : base("oldtotal=" + oldtotal + ", newtotal=" + newtotal + ", limit=" + limit)
        {
        }
    }

    [SerializableAttribute]
    public class OutOfStackException: Exception
    {
    }
}
