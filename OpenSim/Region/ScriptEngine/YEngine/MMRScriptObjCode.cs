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

using OpenSim.Region.ScriptEngine.Yengine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace OpenSim.Region.ScriptEngine.Yengine
{
    public delegate void ScriptEventHandler(XMRInstAbstract instance);

    /*
     * This object represents the output of the compilation.
     * Once the compilation is complete, its contents should be
     * considered 'read-only', so it can be shared among multiple
     * instances of the script.
     *
     * It gets created by ScriptCodeGen.
     * It gets used by XMRInstance to create script instances.
     */
    public class ScriptObjCode
    {
        public string sourceHash;         // source text hash code
        public XMRInstArSizes glblSizes = new XMRInstArSizes();
        // number of global variables of various types

        public string[] stateNames;       // convert state number to corresponding string
        public ScriptEventHandler[,] scriptEventHandlerTable;
        // entrypoints to all event handler functions
        // 1st subscript = state code number (0=default)
        // 2nd subscript = event code number
        // null entry means no handler defined for that state,event

        public Dictionary<string, TokenDeclSDType> sdObjTypesName;
        // all script-defined types by name

        public TokenDeclSDType[] sdObjTypesIndx;
        // all script-defined types by sdTypeIndex

        public Dictionary<Type, string> sdDelTypes;
        // all script-defined delegates (including anonymous)

        public Dictionary<string, DynamicMethod> dynamicMethods;
        // all dyanmic methods

        public Dictionary<string, KeyValuePair<int, ScriptSrcLoc>[]> scriptSrcLocss;
        // method,iloffset -> source file,line,posn

        public int refCount;              // used by engine to keep track of number of 
                                          // instances that are using this object code

        public Dictionary<string, Dictionary<int, string>> globalVarNames = new Dictionary<string, Dictionary<int, string>>();

        /**
         * @brief Fill in ScriptObjCode from an YEngine object file.
         *   'objFileReader' is a serialized form of the CIL code we generated
         *   'asmFileWriter' is where we write the disassembly to (or null if not wanted)
         *   'srcFileWriter' is where we write the decompilation to (or null if not wanted)
         * Throws an exception if there is any error (theoretically).
         */
        public ScriptObjCode(BinaryReader objFileReader, TextWriter asmFileWriter, TextWriter srcFileWriter)
        {
             // Check version number to make sure we know how to process file contents.
            char[] ocm = objFileReader.ReadChars(ScriptCodeGen.OBJECT_CODE_MAGIC.Length);
            if(new String(ocm) != ScriptCodeGen.OBJECT_CODE_MAGIC)
                throw new CVVMismatchException("Not an Yengine object file (bad magic)");

            int cvv = objFileReader.ReadInt32();
            if(cvv != ScriptCodeGen.COMPILED_VERSION_VALUE)
                throw new CVVMismatchException(
                    "Object version is " + cvv.ToString() + " but accept only " + ScriptCodeGen.COMPILED_VERSION_VALUE.ToString());
            // Fill in simple parts of scriptObjCode object.
            sourceHash = objFileReader.ReadString();
            glblSizes.ReadFromFile(objFileReader);
            int nStates = objFileReader.ReadInt32();

            stateNames = new string[nStates];
            for(int i = 0; i < nStates; i++)
            {
                stateNames[i] = objFileReader.ReadString();
                if(asmFileWriter != null)
                    asmFileWriter.WriteLine("  state[{0}] = {1}", i, stateNames[i]);
            }

            if(asmFileWriter != null)
                glblSizes.WriteAsmFile(asmFileWriter, "numGbl");

            string gblName;
            while((gblName = objFileReader.ReadString()) != "")
            {
                string gblType = objFileReader.ReadString();
                int gblIndex = objFileReader.ReadInt32();
                Dictionary<int, string> names;
                if(!globalVarNames.TryGetValue(gblType, out names))
                {
                    names = new Dictionary<int, string>();
                    globalVarNames.Add(gblType, names);
                }
                names.Add(gblIndex, gblName);
                if(asmFileWriter != null)
                    asmFileWriter.WriteLine("  {0} = {1}[{2}]", gblName, gblType, gblIndex);
            }

            // Read in script-defined types.
            sdObjTypesName = new Dictionary<string, TokenDeclSDType>();
            sdDelTypes = new Dictionary<Type, string>();
            int maxIndex = -1;
            while((gblName = objFileReader.ReadString()) != "")
            {
                TokenDeclSDType sdt = TokenDeclSDType.ReadFromFile(sdObjTypesName,
                                                      gblName, objFileReader, asmFileWriter);
                sdObjTypesName.Add(gblName, sdt);
                if(maxIndex < sdt.sdTypeIndex)
                    maxIndex = sdt.sdTypeIndex;
                if(sdt is TokenDeclSDTypeDelegate)
                    sdDelTypes.Add(sdt.GetSysType(), gblName);
            }
            sdObjTypesIndx = new TokenDeclSDType[maxIndex + 1];
            foreach(TokenDeclSDType sdt in sdObjTypesName.Values)
                sdObjTypesIndx[sdt.sdTypeIndex] = sdt;

            // Now fill in the methods (the hard part).
            scriptEventHandlerTable = new ScriptEventHandler[nStates, (int)ScriptEventCode.Size];
            dynamicMethods = new Dictionary<string, DynamicMethod>();
            scriptSrcLocss = new Dictionary<string, KeyValuePair<int, ScriptSrcLoc>[]>();

            ObjectTokens objectTokens = null;
            if(asmFileWriter != null)
                objectTokens = new OTDisassemble(this, asmFileWriter);
            else if(srcFileWriter != null)
                objectTokens = new OTDecompile(this, srcFileWriter);

            try
            {
                ScriptObjWriter.CreateObjCode(sdObjTypesName, objFileReader, this, objectTokens);
            }
            finally
            {
                if(objectTokens != null)
                    objectTokens.Close();
            }

            // We enter all script event handler methods in the ScriptEventHandler table.
            // They are named:  <statename> <eventname>
            foreach(KeyValuePair<string, DynamicMethod> kvp in dynamicMethods)
            {
                string methName = kvp.Key;
                int i = methName.IndexOf(' ');
                if(i < 0)
                    continue;
                string stateName = methName.Substring(0, i);
                string eventName = methName.Substring(++i);
                int stateCode;
                for(stateCode = stateNames.Length; --stateCode >= 0;)
                    if(stateNames[stateCode] == stateName)
                        break;

                int eventCode = (int)Enum.Parse(typeof(ScriptEventCode), eventName);
                scriptEventHandlerTable[stateCode, eventCode] =
                            (ScriptEventHandler)kvp.Value.CreateDelegate(typeof(ScriptEventHandler));
            }

            // Fill in all script-defined class vtables.
            foreach(TokenDeclSDType sdt in sdObjTypesIndx)
            {
                if((sdt != null) && (sdt is TokenDeclSDTypeClass))
                {
                    TokenDeclSDTypeClass sdtc = (TokenDeclSDTypeClass)sdt;
                    sdtc.FillVTables(this);
                }
            }
        }

        /**
         * @brief Called once for every method found in objFileReader file.
         *        It enters the method in the ScriptObjCode object table so it can be called.
         */
        public void EndMethod(DynamicMethod method, Dictionary<int, ScriptSrcLoc> srcLocs)
        {
             // Save method object code pointer.
            dynamicMethods.Add(method.Name, method);

             // Build and sort iloffset -> source code location array.
            int n = srcLocs.Count;
            KeyValuePair<int, ScriptSrcLoc>[] srcLocArray = new KeyValuePair<int, ScriptSrcLoc>[n];
            n = 0;
            foreach(KeyValuePair<int, ScriptSrcLoc> kvp in srcLocs)
                srcLocArray[n++] = kvp;
            Array.Sort(srcLocArray, endMethodWrapper);

             // Save sorted array.
            scriptSrcLocss.Add(method.Name, srcLocArray);
        }

        /**
         * @brief Called once for every method found in objFileReader file.
         *        It enters the method in the ScriptObjCode object table so it can be called.
         */
        private static EndMethodWrapper endMethodWrapper = new EndMethodWrapper();
        private class EndMethodWrapper: System.Collections.IComparer
        {
            public int Compare(object x, object y)
            {
                KeyValuePair<int, ScriptSrcLoc> kvpx = (KeyValuePair<int, ScriptSrcLoc>)x;
                KeyValuePair<int, ScriptSrcLoc> kvpy = (KeyValuePair<int, ScriptSrcLoc>)y;
                return kvpx.Key - kvpy.Key;
            }
        }
    }
}
