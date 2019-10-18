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
using OpenSim.Region.ScriptEngine.Yengine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

/**
 * @brief translate a reduced script token into corresponding CIL code.
 * The single script token contains a tokenized and textured version of the whole script file.
 */

namespace OpenSim.Region.ScriptEngine.Yengine
{
    public interface IScriptCodeGen
    {
        ScriptMyILGen ilGen
        {
            get;
        } // the output instruction stream
        void ErrorMsg(Token token, string message);
        void PushDefaultValue(TokenType type);
        void PushXMRInst();
    }

    public class ScriptCodeGen: IScriptCodeGen
    {
        public static readonly string OBJECT_CODE_MAGIC = "YObjectCode";
        // reserve positive version values for original xmr
        public static int COMPILED_VERSION_VALUE = -2;  // decremented when compiler or object file changes

        public static readonly int CALL_FRAME_MEMUSE = 64;
        public static readonly int STRING_LEN_TO_MEMUSE = 2;

        public static Type xmrInstSuperType = null;  // typeof whatever is actually malloc'd for script instances
                                                     // - must inherit from XMRInstAbstract

         // Static tables that there only needs to be one copy of for all.
        private static VarDict legalEventHandlers = CreateLegalEventHandlers();
        private static CompValu[] zeroCompValus = new CompValu[0];
        private static TokenType[] zeroArgs = new TokenType[0];
        private static TokenTypeBool tokenTypeBool = new TokenTypeBool(null);
        private static TokenTypeExc tokenTypeExc = new TokenTypeExc(null);
        private static TokenTypeFloat tokenTypeFlt = new TokenTypeFloat(null);
        private static TokenTypeInt tokenTypeInt = new TokenTypeInt(null);
        private static TokenTypeObject tokenTypeObj = new TokenTypeObject(null);
        private static TokenTypeRot tokenTypeRot = new TokenTypeRot(null);
        private static TokenTypeStr tokenTypeStr = new TokenTypeStr(null);
        private static TokenTypeVec tokenTypeVec = new TokenTypeVec(null);
        private static Type[] instanceTypeArg = new Type[] { typeof(XMRInstAbstract) };
        private static string[] instanceNameArg = new string[] { "$xmrthis" };

        private static ConstructorInfo lslFloatConstructorInfo = typeof(LSL_Float).GetConstructor(new Type[] { typeof(double) });
        private static ConstructorInfo lslIntegerConstructorInfo = typeof(LSL_Integer).GetConstructor(new Type[] { typeof(int) });
        private static ConstructorInfo lslListConstructorInfo = typeof(LSL_List).GetConstructor(new Type[] { typeof(object[]) });
        public static ConstructorInfo lslRotationConstructorInfo = typeof(LSL_Rotation).GetConstructor(new Type[] { typeof(double), typeof(double), typeof(double), typeof(double) });
        private static ConstructorInfo lslStringConstructorInfo = typeof(LSL_String).GetConstructor(new Type[] { typeof(string) });
        public static ConstructorInfo lslVectorConstructorInfo = typeof(LSL_Vector).GetConstructor(new Type[] { typeof(double), typeof(double), typeof(double) });
        private static ConstructorInfo scriptBadCallNoExceptionConstructorInfo = typeof(ScriptBadCallNoException).GetConstructor(new Type[] { typeof(int) });
        private static ConstructorInfo scriptChangeStateExceptionConstructorInfo = typeof(ScriptChangeStateException).GetConstructor(new Type[] { typeof(int) });
        private static ConstructorInfo scriptRestoreCatchExceptionConstructorInfo = typeof(ScriptRestoreCatchException).GetConstructor(new Type[] { typeof(Exception) });
        private static ConstructorInfo scriptUndefinedStateExceptionConstructorInfo = typeof(ScriptUndefinedStateException).GetConstructor(new Type[] { typeof(string) });
        private static ConstructorInfo sdtClassConstructorInfo = typeof(XMRSDTypeClObj).GetConstructor(new Type[] { typeof(XMRInstAbstract), typeof(int) });
        private static ConstructorInfo xmrArrayConstructorInfo = typeof(XMR_Array).GetConstructor(new Type[] { typeof(XMRInstAbstract) });
        private static FieldInfo callModeFieldInfo = typeof(XMRInstAbstract).GetField("callMode");
        private static FieldInfo doGblInitFieldInfo = typeof(XMRInstAbstract).GetField("doGblInit");
        private static FieldInfo ehArgsFieldInfo = typeof(XMRInstAbstract).GetField("ehArgs");
        private static FieldInfo rotationXFieldInfo = typeof(LSL_Rotation).GetField("x");
        private static FieldInfo rotationYFieldInfo = typeof(LSL_Rotation).GetField("y");
        private static FieldInfo rotationZFieldInfo = typeof(LSL_Rotation).GetField("z");
        private static FieldInfo rotationSFieldInfo = typeof(LSL_Rotation).GetField("s");
        private static FieldInfo sdtXMRInstFieldInfo = typeof(XMRSDTypeClObj).GetField("xmrInst");
        private static FieldInfo stackLeftFieldInfo = typeof(XMRInstAbstract).GetField("m_StackLeft");
        private static FieldInfo vectorXFieldInfo = typeof(LSL_Vector).GetField("x");
        private static FieldInfo vectorYFieldInfo = typeof(LSL_Vector).GetField("y");
        private static FieldInfo vectorZFieldInfo = typeof(LSL_Vector).GetField("z");

        private static MethodInfo arrayClearMethodInfo = typeof(XMR_Array).GetMethod("__pub_clear", new Type[] { });
        private static MethodInfo arrayCountMethodInfo = typeof(XMR_Array).GetMethod("__pub_count", new Type[] { });
        private static MethodInfo arrayIndexMethodInfo = typeof(XMR_Array).GetMethod("__pub_index", new Type[] { typeof(int) });
        private static MethodInfo arrayValueMethodInfo = typeof(XMR_Array).GetMethod("__pub_value", new Type[] { typeof(int) });
        private static MethodInfo checkRunStackMethInfo = typeof(XMRInstAbstract).GetMethod("CheckRunStack", new Type[] { });
        private static MethodInfo checkRunQuickMethInfo = typeof(XMRInstAbstract).GetMethod("CheckRunQuick", new Type[] { });
        private static MethodInfo ehArgUnwrapFloat = GetStaticMethod(typeof(TypeCast), "EHArgUnwrapFloat", new Type[] { typeof(object) });
        private static MethodInfo ehArgUnwrapInteger = GetStaticMethod(typeof(TypeCast), "EHArgUnwrapInteger", new Type[] { typeof(object) });
        private static MethodInfo ehArgUnwrapRotation = GetStaticMethod(typeof(TypeCast), "EHArgUnwrapRotation", new Type[] { typeof(object) });
        private static MethodInfo ehArgUnwrapString = GetStaticMethod(typeof(TypeCast), "EHArgUnwrapString", new Type[] { typeof(object) });
        private static MethodInfo ehArgUnwrapVector = GetStaticMethod(typeof(TypeCast), "EHArgUnwrapVector", new Type[] { typeof(object) });
        private static MethodInfo xmrArrPubIndexMethod = typeof(XMR_Array).GetMethod("__pub_index", new Type[] { typeof(int) });
        private static MethodInfo xmrArrPubValueMethod = typeof(XMR_Array).GetMethod("__pub_value", new Type[] { typeof(int) });
        private static MethodInfo captureStackFrameMethodInfo = typeof(XMRInstAbstract).GetMethod("CaptureStackFrame", new Type[] { typeof(string), typeof(int), typeof(int) });
        private static MethodInfo restoreStackFrameMethodInfo = typeof(XMRInstAbstract).GetMethod("RestoreStackFrame", new Type[] { typeof(string), typeof(int).MakeByRefType() });
        private static MethodInfo stringCompareMethodInfo = GetStaticMethod(typeof(String), "Compare", new Type[] { typeof(string), typeof(string), typeof(StringComparison) });
        private static MethodInfo stringConcat2MethodInfo = GetStaticMethod(typeof(String), "Concat", new Type[] { typeof(string), typeof(string) });
        private static MethodInfo stringConcat3MethodInfo = GetStaticMethod(typeof(String), "Concat", new Type[] { typeof(string), typeof(string), typeof(string) });
        private static MethodInfo stringConcat4MethodInfo = GetStaticMethod(typeof(String), "Concat", new Type[] { typeof(string), typeof(string), typeof(string), typeof(string) });
        private static MethodInfo lslRotationNegateMethodInfo = GetStaticMethod(typeof(ScriptCodeGen),
                                                                                 "LSLRotationNegate",
                                                                                 new Type[] { typeof(LSL_Rotation) });
        private static MethodInfo lslVectorNegateMethodInfo = GetStaticMethod(typeof(ScriptCodeGen),
                                                                               "LSLVectorNegate",
                                                                               new Type[] { typeof(LSL_Vector) });
        private static MethodInfo scriptRestoreCatchExceptionUnwrap = GetStaticMethod(typeof(ScriptRestoreCatchException), "Unwrap", new Type[] { typeof(Exception) });
        private static MethodInfo thrownExceptionWrapMethodInfo = GetStaticMethod(typeof(ScriptThrownException), "Wrap", new Type[] { typeof(object) });

        private static MethodInfo catchExcToStrMethodInfo = GetStaticMethod(typeof(ScriptCodeGen),
                                                                             "CatchExcToStr",
                                                                             new Type[] { typeof(Exception) });

        private static MethodInfo consoleWriteMethodInfo = GetStaticMethod(typeof(ScriptCodeGen), "ConsoleWrite", new Type[] { typeof(object) });
        public static void ConsoleWrite(object o)
        {
            if(o == null)
                o = "<<null>>";
            Console.Write(o.ToString());
        }

        public static bool CodeGen(TokenScript tokenScript, BinaryWriter objFileWriter, string sourceHash)
        {
             // Run compiler such that it has a 'this' context for convenience.
            ScriptCodeGen scg = new ScriptCodeGen(tokenScript, objFileWriter, sourceHash);

             // Return pointer to resultant script object code.
            return !scg.youveAnError;
        }

         // There is one set of these variables for each script being compiled.
        private bool mightGetHere = false;
        private bool youveAnError = false;
        private BreakContTarg curBreakTarg = null;
        private BreakContTarg curContTarg = null;
        private int lastErrorLine = 0;
        private int nStates = 0;
        private string sourceHash;
        private string lastErrorFile = "";
        private string[] stateNames;
        private XMRInstArSizes glblSizes = new XMRInstArSizes();
        private Token errorMessageToken = null;
        private TokenDeclVar curDeclFunc = null;
        private TokenStmtBlock curStmtBlock = null;
        private BinaryWriter objFileWriter = null;
        private TokenScript tokenScript = null;
        public int tempCompValuNum = 0;
        private TokenDeclSDTypeClass currentSDTClass = null;

        private Dictionary<string, int> stateIndices = null;

        // These get cleared at beginning of every function definition
        private ScriptMyLocal instancePointer;   // holds XMRInstanceSuperType pointer
        private ScriptMyLabel retLabel = null;  // where to jump to exit function
        private ScriptMyLocal retValue = null;
        private ScriptMyLocal actCallNo = null;  // for the active try/catch/finally stack or the big one outside them all
        private LinkedList<CallLabel> actCallLabels = new LinkedList<CallLabel>();  // for the active try/catch/finally stack or the big one outside them all
        private LinkedList<CallLabel> allCallLabels = new LinkedList<CallLabel>();  // this holds each and every one for all stacks in total
        public CallLabel openCallLabel = null;  // only one call label can be open at a time
                                                // - the call label is open from the time of CallPre() until corresponding CallPost()
                                                // - so no non-trivial pushes/pops etc allowed between a CallPre() and a CallPost()
        public List<ScriptMyLocal> HeapLocals = new List<ScriptMyLocal>();
        private ScriptMyILGen _ilGen;
        public ScriptMyILGen ilGen
        {
            get
            {
                return _ilGen;
            }
        }

        private ScriptCodeGen(TokenScript tokenScript, BinaryWriter objFileWriter, string sourceHash)
        {
            this.tokenScript = tokenScript;
            this.objFileWriter = objFileWriter;
            this.sourceHash = sourceHash;

            try
            {
                PerformCompilation();
            }
            catch
            {
                // if we've an error, just punt on any exception
                // it's probably just a null reference from something
                // not being filled in etc.
                if(!youveAnError)
                    throw;
            }
            finally
            {
                objFileWriter = null;
            }
        }

        /**
         * @brief Convert 'tokenScript' to 'objFileWriter' format.
         *   'tokenScript' is a parsed/reduced abstract syntax tree of the script source file
         *   'objFileWriter' is a serialized form of the CIL code that we generate
         */
        private void PerformCompilation()
        {
             // errorMessageToken is used only when the given token doesn't have a
             // output delegate associated with it such as for backend API functions
             // that only have one copy for the whole system.  It is kept up-to-date
             // approximately but is rarely needed so going to assume it doesn't have 
             // to be exact.
            errorMessageToken = tokenScript;

             // Set up dictionary to translate state names to their index number.
            stateIndices = new Dictionary<string, int>();

             // Assign each state its own unique index.
             // The default state gets 0.
            nStates = 0;
            tokenScript.defaultState.body.index = nStates++;
            stateIndices.Add("default", 0);
            foreach(KeyValuePair<string, TokenDeclState> kvp in tokenScript.states)
            {
                TokenDeclState declState = kvp.Value;
                declState.body.index = nStates++;
                stateIndices.Add(declState.name.val, declState.body.index);
            }

             // Make up an array that translates state indices to state name strings.
            stateNames = new string[nStates];
            stateNames[0] = "default";
            foreach(KeyValuePair<string, TokenDeclState> kvp in tokenScript.states)
            {
                TokenDeclState declState = kvp.Value;
                stateNames[declState.body.index] = declState.name.val;
            }

             // Make sure we have delegates for all script-defined functions and methods,
             // creating anonymous ones if needed.  Note that this includes all property 
             // getter and setter methods.
            foreach(TokenDeclVar declFunc in tokenScript.variablesStack)
            {
                if(declFunc.retType != null)
                {
                    declFunc.GetDelType();
                }
            }
            while(true)
            {
                bool itIsAGoodDayToDie = true;
                try
                {
                    foreach(TokenDeclSDType sdType in tokenScript.sdSrcTypesValues)
                    {
                        itIsAGoodDayToDie = false;
                        if(sdType is TokenDeclSDTypeClass)
                        {
                            TokenDeclSDTypeClass sdtClass = (TokenDeclSDTypeClass)sdType;
                            foreach(TokenDeclVar declFunc in sdtClass.members)
                            {
                                if(declFunc.retType != null)
                                {
                                    declFunc.GetDelType();
                                    if(declFunc.funcNameSig.val.StartsWith("$ctor("))
                                    {
                                        // this is for the "$new()" static method that we create below.
                                        // See GenerateStmtNewobj() etc.
                                        new TokenTypeSDTypeDelegate(declFunc, sdtClass.MakeRefToken(declFunc),
                                                declFunc.argDecl.types, tokenScript);
                                    }
                                }
                            }
                        }
                        if(sdType is TokenDeclSDTypeInterface)
                        {
                            TokenDeclSDTypeInterface sdtIFace = (TokenDeclSDTypeInterface)sdType;
                            foreach(TokenDeclVar declFunc in sdtIFace.methsNProps)
                            {
                                if(declFunc.retType != null)
                                {
                                    declFunc.GetDelType();
                                }
                            }
                        }
                        itIsAGoodDayToDie = true;
                    }
                    break;
                }
                catch(InvalidOperationException)
                {
                    if(!itIsAGoodDayToDie)
                        throw;
                    // fetching the delegate created an anonymous entry in tokenScript.sdSrcTypesValues
                    // which made the foreach statement puque, so start over...
                }
            }

             // No more types can be defined or we won't be able to write them to the object file.
            tokenScript.sdSrcTypesSeal();

             // Assign all global variables a slot in its corresponding XMRInstance.gbl<Type>s[] array.
             // Global variables are simply elements of those arrays at runtime, thus we don't need to create
             // an unique class for each script, we can just use XMRInstance as is for all.
            foreach(TokenDeclVar declVar in tokenScript.variablesStack)
            {
                 // Omit 'constant' variables as they are coded inline so don't need a slot.
                if(declVar.constant)
                    continue;

                 // Do functions later.
                if(declVar.retType != null)
                    continue;

                 // Create entry in the value array for the variable or property.
                declVar.location = new CompValuGlobalVar(declVar, glblSizes);
            }

             // Likewise for any static fields in script-defined classes.
             // They can be referenced anywhere by <typename>.<fieldname>, see 
             // GenerateFromLValSField().
            foreach(TokenDeclSDType sdType in tokenScript.sdSrcTypesValues)
            {
                if(!(sdType is TokenDeclSDTypeClass))
                    continue;
                TokenDeclSDTypeClass sdtClass = (TokenDeclSDTypeClass)sdType;

                foreach(TokenDeclVar declVar in sdtClass.members)
                {
                     // Omit 'constant' variables as they are coded inline so don't need a slot.
                    if(declVar.constant)
                        continue;

                     // Do methods later.
                    if(declVar.retType != null)
                        continue;

                     // Ignore non-static fields for now.
                     // They get assigned below.
                    if((declVar.sdtFlags & ScriptReduce.SDT_STATIC) == 0)
                        continue;

                     // Create entry in the value array for the static field or static property.
                    declVar.location = new CompValuGlobalVar(declVar, glblSizes);
                }
            }

             // Assign slots for all interface method prototypes.
             // These indices are used to index the array of delegates that holds a class' implementation of an 
             // interface.
             // Properties do not get a slot because they aren't called as such.  But their corresponding
             // <name>$get() and <name>$set(<type>) methods are in the table and they each get a slot.
            foreach(TokenDeclSDType sdType in tokenScript.sdSrcTypesValues)
            {
                if(!(sdType is TokenDeclSDTypeInterface))
                    continue;
                TokenDeclSDTypeInterface sdtIFace = (TokenDeclSDTypeInterface)sdType;
                int vti = 0;
                foreach(TokenDeclVar im in sdtIFace.methsNProps)
                {
                    if((im.getProp == null) && (im.setProp == null))
                    {
                        im.vTableIndex = vti++;
                    }
                }
            }

             // Assign slots for all instance fields and virtual methods of script-defined classes.
            int maxExtends = tokenScript.sdSrcTypesCount;
            bool didOne;
            do
            {
                didOne = false;
                foreach(TokenDeclSDType sdType in tokenScript.sdSrcTypesValues)
                {
                    if(!(sdType is TokenDeclSDTypeClass))
                        continue;
                    TokenDeclSDTypeClass sdtClass = (TokenDeclSDTypeClass)sdType;
                    if(sdtClass.slotsAssigned)
                        continue;

                     // If this class extends another, the extended class has to already 
                     // be set up, because our slots add on to the end of the extended class.
                    TokenDeclSDTypeClass extends = sdtClass.extends;
                    if(extends != null)
                    {
                        if(!extends.slotsAssigned)
                            continue;
                        sdtClass.instSizes = extends.instSizes;
                        sdtClass.numVirtFuncs = extends.numVirtFuncs;
                        sdtClass.numInterfaces = extends.numInterfaces;

                        int n = maxExtends;
                        for(TokenDeclSDTypeClass ex = extends; ex != null; ex = ex.extends)
                        {
                            if(--n < 0)
                                break;
                        }
                        if(n < 0)
                        {
                            ErrorMsg(sdtClass, "loop in extended classes");
                            sdtClass.slotsAssigned = true;
                            continue;
                        }
                    }

                     // Extended class's slots all assigned, assign our instance fields 
                     // slots in the XMRSDTypeClObj arrays.
                    foreach(TokenDeclVar declVar in sdtClass.members)
                    {
                        if(declVar.retType != null)
                            continue;
                        if(declVar.constant)
                            continue;
                        if((declVar.sdtFlags & ScriptReduce.SDT_STATIC) != 0)
                            continue;
                        if((declVar.getProp == null) && (declVar.setProp == null))
                        {
                            declVar.type.AssignVarSlot(declVar, sdtClass.instSizes);
                        }
                    }

                     // ... and assign virtual method vtable slots.
                     //
                     //                   - : error if any overridden method, doesn't need a slot
                     //            abstract : error if any overridden method, alloc new slot but leave it empty
                     //                 new : ignore any overridden method, doesn't need a slot
                     //        new abstract : ignore any overridden method, alloc new slot but leave it empty
                     //            override : must have overridden abstract/virtual, use old slot
                     //   override abstract : must have overridden abstract, use old slot but it is still empty
                     //              static : error if any overridden method, doesn't need a slot
                     //          static new : ignore any overridden method, doesn't need a slot
                     //             virtual : error if any overridden method, alloc new slot and fill it in
                     //         virtual new : ignore any overridden method, alloc new slot and fill it in
                    foreach(TokenDeclVar declFunc in sdtClass.members)
                    {
                        if(declFunc.retType == null)
                            continue;
                        curDeclFunc = declFunc;

                         // See if there is a method in an extended class that this method overshadows.
                         // If so, check for various conflicts.
                         // In any case, SDT_NEW on our method means to ignore any overshadowed method.
                        string declLongName = sdtClass.longName.val + "." + declFunc.funcNameSig.val;
                        uint declFlags = declFunc.sdtFlags;
                        TokenDeclVar overridden = null;
                        if((declFlags & ScriptReduce.SDT_NEW) == 0)
                        {
                            for(TokenDeclSDTypeClass sdtd = extends; sdtd != null; sdtd = sdtd.extends)
                            {
                                overridden = FindExactWithRet(sdtd.members, declFunc.name, declFunc.retType, declFunc.argDecl.types);
                                if(overridden != null)
                                    break;
                            }
                        }
                        if(overridden != null)
                            do
                            {
                                string overLongName = overridden.sdtClass.longName.val;
                                uint overFlags = overridden.sdtFlags;

                                 // See if overridden method allows itself to be overridden.
                                if((overFlags & ScriptReduce.SDT_ABSTRACT) != 0)
                                {
                                    if((declFlags & (ScriptReduce.SDT_ABSTRACT | ScriptReduce.SDT_OVERRIDE)) == 0)
                                    {
                                        ErrorMsg(declFunc, declLongName + " overshadows abstract " + overLongName + " but is not marked abstract, new or override");
                                        break;
                                    }
                                }
                                else if((overFlags & ScriptReduce.SDT_FINAL) != 0)
                                {
                                    ErrorMsg(declFunc, declLongName + " overshadows final " + overLongName + " but is not marked new");
                                }
                                else if((overFlags & (ScriptReduce.SDT_OVERRIDE | ScriptReduce.SDT_VIRTUAL)) != 0)
                                {
                                    if((declFlags & (ScriptReduce.SDT_NEW | ScriptReduce.SDT_OVERRIDE)) == 0)
                                    {
                                        ErrorMsg(declFunc, declLongName + " overshadows virtual " + overLongName + " but is not marked new or override");
                                        break;
                                    }
                                }
                                else
                                {
                                    ErrorMsg(declFunc, declLongName + " overshadows non-virtual " + overLongName + " but is not marked new");
                                    break;
                                }

                                 // See if our method is capable of overriding the other method.
                                if((declFlags & ScriptReduce.SDT_ABSTRACT) != 0)
                                {
                                    if((overFlags & ScriptReduce.SDT_ABSTRACT) == 0)
                                    {
                                        ErrorMsg(declFunc, declLongName + " abstract overshadows non-abstract " + overLongName + " but is not marked new");
                                        break;
                                    }
                                }
                                else if((declFlags & ScriptReduce.SDT_OVERRIDE) != 0)
                                {
                                    if((overFlags & (ScriptReduce.SDT_ABSTRACT | ScriptReduce.SDT_OVERRIDE | ScriptReduce.SDT_VIRTUAL)) == 0)
                                    {
                                        ErrorMsg(declFunc, declLongName + " override overshadows non-abstract/non-virtual " + overLongName);
                                        break;
                                    }
                                }
                                else
                                {
                                    ErrorMsg(declFunc, declLongName + " overshadows " + overLongName + " but is not marked new");
                                    break;
                                }
                            } while(false);

                         // Now we can assign it a vtable slot if it needs one (ie, it is virtual).
                        declFunc.vTableIndex = -1;
                        if(overridden != null)
                        {
                            declFunc.vTableIndex = overridden.vTableIndex;
                        }
                        else if((declFlags & ScriptReduce.SDT_OVERRIDE) != 0)
                        {
                            ErrorMsg(declFunc, declLongName + " marked override but nothing matching found that it overrides");
                        }
                        if((declFlags & (ScriptReduce.SDT_ABSTRACT | ScriptReduce.SDT_VIRTUAL)) != 0)
                        {
                            declFunc.vTableIndex = sdtClass.numVirtFuncs++;
                        }
                    }
                    curDeclFunc = null;

                     // ... and assign implemented interface slots.
                     // Note that our implementations of a given interface is completely independent of any 
                     // rootward class's implementation of that same interface.
                    int nIFaces = sdtClass.numInterfaces + sdtClass.implements.Count;
                    sdtClass.iFaces = new TokenDeclSDTypeInterface[nIFaces];
                    sdtClass.iImplFunc = new TokenDeclVar[nIFaces][];
                    for(int i = 0; i < sdtClass.numInterfaces; i++)
                    {
                        sdtClass.iFaces[i] = extends.iFaces[i];
                        sdtClass.iImplFunc[i] = extends.iImplFunc[i];
                    }

                    foreach(TokenDeclSDTypeInterface intf in sdtClass.implements)
                    {
                        int i = sdtClass.numInterfaces++;
                        sdtClass.iFaces[i] = intf;
                        sdtClass.intfIndices.Add(intf.longName.val, i);
                        int nMeths = 0;
                        foreach(TokenDeclVar m in intf.methsNProps)
                        {
                            if((m.getProp == null) && (m.setProp == null))
                                nMeths++;
                        }
                        sdtClass.iImplFunc[i] = new TokenDeclVar[nMeths];
                    }

                    foreach(TokenDeclVar classMeth in sdtClass.members)
                    {
                        if(classMeth.retType == null)
                            continue;
                        curDeclFunc = classMeth;
                        for(TokenIntfImpl intfImpl = classMeth.implements; intfImpl != null; intfImpl = (TokenIntfImpl)intfImpl.nextToken)
                        {
                             // One of the class methods implements an interface method.
                             // Try to find the interface method that is implemented and verify its signature.
                            TokenDeclSDTypeInterface intfType = intfImpl.intfType.decl;
                            TokenDeclVar intfMeth = FindExactWithRet(intfType.methsNProps, intfImpl.methName, classMeth.retType, classMeth.argDecl.types);
                            if(intfMeth == null)
                            {
                                ErrorMsg(intfImpl, "interface does not define method " + intfImpl.methName.val + classMeth.argDecl.GetArgSig());
                                continue;
                            }

                             // See if this class was declared to implement that interface.
                            bool found = false;
                            foreach(TokenDeclSDTypeInterface intf in sdtClass.implements)
                            {
                                if(intf == intfType)
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if(!found)
                            {
                                ErrorMsg(intfImpl, "class not declared to implement " + intfType.longName.val);
                                continue;
                            }

                             // Get index in iFaces[] and iImplFunc[] arrays.
                             // Start scanning from the end in case one of our rootward classes also implements the interface.
                             // We should always be successful because we know by now that this class implements the interface.
                            int i;
                            for(i = sdtClass.numInterfaces; --i >= 0;)
                            {
                                if(sdtClass.iFaces[i] == intfType)
                                    break;
                            }

                             // Now remember which of the class methods implements that interface method.
                            int j = intfMeth.vTableIndex;
                            if(sdtClass.iImplFunc[i][j] != null)
                            {
                                ErrorMsg(intfImpl, "also implemented by " + sdtClass.iImplFunc[i][j].funcNameSig.val);
                                continue;
                            }
                            sdtClass.iImplFunc[i][j] = classMeth;
                        }
                    }
                    curDeclFunc = null;

                     // Now make sure this class implements all methods for all declared interfaces.
                    for(int i = sdtClass.numInterfaces - sdtClass.implements.Count; i < sdtClass.numInterfaces; i++)
                    {
                        TokenDeclVar[] implementations = sdtClass.iImplFunc[i];
                        for(int j = implementations.Length; --j >= 0;)
                        {
                            if(implementations[j] == null)
                            {
                                TokenDeclSDTypeInterface intf = sdtClass.iFaces[i];
                                TokenDeclVar meth = null;
                                foreach(TokenDeclVar im in intf.methsNProps)
                                {
                                    if(im.vTableIndex == j)
                                    {
                                        meth = im;
                                        break;
                                    }
                                }
                                ErrorMsg(sdtClass, "does not implement " + intf.longName.val + "." + meth.funcNameSig.val);
                            }
                        }
                    }

                     // All slots for this class have been assigned.
                    sdtClass.slotsAssigned = true;
                    didOne = true;
                }
            } while(didOne);

             // Compute final values for all variables/fields declared as 'constant'.
             // Note that there may be forward references.
            do
            {
                didOne = false;
                foreach(TokenDeclVar tdv in tokenScript.variablesStack)
                {
                    if(tdv.constant && !(tdv.init is TokenRValConst))
                    {
                        tdv.init = tdv.init.TryComputeConstant(LookupInitConstants, ref didOne);
                    }
                }
                foreach(TokenDeclSDType sdType in tokenScript.sdSrcTypesValues)
                {
                    if(!(sdType is TokenDeclSDTypeClass))
                        continue;
                    currentSDTClass = (TokenDeclSDTypeClass)sdType;
                    foreach(TokenDeclVar tdv in currentSDTClass.members)
                    {
                        if(tdv.constant && !(tdv.init is TokenRValConst))
                        {
                            tdv.init = tdv.init.TryComputeConstant(LookupInitConstants, ref didOne);
                        }
                    }
                }
                currentSDTClass = null;
            } while(didOne);

             // Now we should be able to assign all those constants their type and location.
            foreach(TokenDeclVar tdv in tokenScript.variablesStack)
            {
                if(tdv.constant)
                {
                    if(tdv.init is TokenRValConst)
                    {
                        TokenRValConst rvc = (TokenRValConst)tdv.init;
                        tdv.type = rvc.tokType;
                        tdv.location = rvc.GetCompValu();
                    }
                    else
                    {
                        ErrorMsg(tdv, "value is not constant");
                    }
                }
            }
            foreach(TokenDeclSDType sdType in tokenScript.sdSrcTypesValues)
            {
                if(!(sdType is TokenDeclSDTypeClass))
                    continue;
                currentSDTClass = (TokenDeclSDTypeClass)sdType;
                foreach(TokenDeclVar tdv in currentSDTClass.members)
                {
                    if(tdv.constant)
                    {
                        if(tdv.init is TokenRValConst)
                        {
                            TokenRValConst rvc = (TokenRValConst)tdv.init;
                            tdv.type = rvc.tokType;
                            tdv.location = rvc.GetCompValu();
                        }
                        else
                        {
                            ErrorMsg(tdv, "value is not constant");
                        }
                    }
                }
            }
            currentSDTClass = null;

             // For all classes that define all the methods needed for the class, ie, they aren't abstract,
             // define a static class.$new() method with same args as the $ctor(s).  This will allow the
             // class to be instantiated via the new operator.
            foreach(TokenDeclSDType sdType in tokenScript.sdSrcTypesValues)
            {
                if(!(sdType is TokenDeclSDTypeClass))
                    continue;
                TokenDeclSDTypeClass sdtClass = (TokenDeclSDTypeClass)sdType;

                 // See if the class as it stands would be able to fill every slot of its vtable.
                bool[] filled = new bool[sdtClass.numVirtFuncs];
                int numFilled = 0;
                for(TokenDeclSDTypeClass sdtc = sdtClass; sdtc != null; sdtc = sdtc.extends)
                {
                    foreach(TokenDeclVar tdf in sdtc.members)
                    {
                        if((tdf.retType != null) && (tdf.vTableIndex >= 0) && ((tdf.sdtFlags & ScriptReduce.SDT_ABSTRACT) == 0))
                        {
                            if(!filled[tdf.vTableIndex])
                            {
                                filled[tdf.vTableIndex] = true;
                                numFilled++;
                            }
                        }
                    }
                }

                 // If so, define a static class.$new() method for every constructor defined for the class.
                 // Give it the same access (private/protected/public) as the script declared for the constructor.
                 // Note that the reducer made sure there is at least a default constructor for every class.
                if(numFilled >= sdtClass.numVirtFuncs)
                {
                    List<TokenDeclVar> newobjDeclFuncs = new List<TokenDeclVar>();
                    foreach(TokenDeclVar ctorDeclFunc in sdtClass.members)
                    {
                        if((ctorDeclFunc.funcNameSig != null) && ctorDeclFunc.funcNameSig.val.StartsWith("$ctor("))
                        {
                            TokenDeclVar newobjDeclFunc = DefineNewobjFunc(ctorDeclFunc);
                            newobjDeclFuncs.Add(newobjDeclFunc);
                        }
                    }
                    foreach(TokenDeclVar newobjDeclFunc in newobjDeclFuncs)
                    {
                        sdtClass.members.AddEntry(newobjDeclFunc);
                    }
                }
            }

             // Write fixed portion of object file.
            objFileWriter.Write(OBJECT_CODE_MAGIC.ToCharArray());
            objFileWriter.Write(COMPILED_VERSION_VALUE);
            objFileWriter.Write(sourceHash);
            glblSizes.WriteToFile(objFileWriter);

            objFileWriter.Write(nStates);
            for(int i = 0; i < nStates; i++)
            {
                objFileWriter.Write(stateNames[i]);
            }

             // For debugging, we also write out global variable array slot assignments.
            foreach(TokenDeclVar declVar in tokenScript.variablesStack)
            {
                if(declVar.retType == null)
                {
                    WriteOutGblAssignment("", declVar);
                }
            }
            foreach(TokenDeclSDType sdType in tokenScript.sdSrcTypesValues)
            {
                if(!(sdType is TokenDeclSDTypeClass))
                    continue;
                TokenDeclSDTypeClass sdtClass = (TokenDeclSDTypeClass)sdType;
                foreach(TokenDeclVar declVar in sdtClass.members)
                {
                    if((declVar.sdtFlags & ScriptReduce.SDT_STATIC) != 0)
                    {
                        WriteOutGblAssignment(sdtClass.longName.val + ".", declVar);
                    }
                }
            }
            objFileWriter.Write("");

             // Write out script-defined types.
            foreach(TokenDeclSDType sdType in tokenScript.sdSrcTypesValues)
            {
                objFileWriter.Write(sdType.longName.val);
                sdType.WriteToFile(objFileWriter);
            }
            objFileWriter.Write("");

             // Output function headers then bodies.
             // Do all headers first in case bodies do forward references.
             // Do both global functions, script-defined class static methods and 
             // script-defined instance methods, as we handle the differences
             // during compilation of the functions/methods themselves.

            // headers
            foreach(TokenDeclVar declFunc in tokenScript.variablesStack)
            {
                if(declFunc.retType != null)
                    GenerateMethodHeader(declFunc);
            }
            foreach(TokenDeclSDType sdType in tokenScript.sdSrcTypesValues)
            {
                if(sdType is TokenDeclSDTypeClass)
                {
                    TokenDeclSDTypeClass sdtClass = (TokenDeclSDTypeClass)sdType;
                    foreach(TokenDeclVar declFunc in sdtClass.members)
                    {
                        if((declFunc.retType != null) && ((declFunc.sdtFlags & ScriptReduce.SDT_ABSTRACT) == 0))
                            GenerateMethodHeader(declFunc);
                    }
                }
            }

            // now bodies
            foreach(TokenDeclVar declFunc in tokenScript.variablesStack)
            {
                if(declFunc.retType != null)
                    GenerateMethodBody(declFunc);
            }
            foreach(TokenDeclSDType sdType in tokenScript.sdSrcTypesValues)
            {
                if(sdType is TokenDeclSDTypeClass)
                {
                    TokenDeclSDTypeClass sdtClass = (TokenDeclSDTypeClass)sdType;
                    foreach(TokenDeclVar declFunc in sdtClass.members)
                    {
                        if((declFunc.retType != null) && ((declFunc.sdtFlags & ScriptReduce.SDT_ABSTRACT) == 0))
                            GenerateMethodBody(declFunc);
                    }
                }
            }

             // Output default state event handler functions.
             // Each event handler is a private static method named 'default <eventname>'.
             // Splice in a default state_entry() handler if none defined so we can init global vars.
            TokenDeclVar defaultStateEntry = null;
            for(defaultStateEntry = tokenScript.defaultState.body.eventFuncs;
                 defaultStateEntry != null;
                 defaultStateEntry = (TokenDeclVar)defaultStateEntry.nextToken)
            {
                if(defaultStateEntry.funcNameSig.val == "state_entry()")
                    break;
            }
            if(defaultStateEntry == null)
            {
                defaultStateEntry = new TokenDeclVar(tokenScript.defaultState.body, null, tokenScript);
                defaultStateEntry.name = new TokenName(tokenScript.defaultState.body, "state_entry");
                defaultStateEntry.retType = new TokenTypeVoid(tokenScript.defaultState.body);
                defaultStateEntry.argDecl = new TokenArgDecl(tokenScript.defaultState.body);
                defaultStateEntry.body = new TokenStmtBlock(tokenScript.defaultState.body);
                defaultStateEntry.body.function = defaultStateEntry;

                defaultStateEntry.nextToken = tokenScript.defaultState.body.eventFuncs;
                tokenScript.defaultState.body.eventFuncs = defaultStateEntry;
            }
            GenerateStateEventHandlers("default", tokenScript.defaultState.body);

             // Output script-defined state event handler methods.
             // Each event handler is a private static method named <statename> <eventname>
            foreach(KeyValuePair<string, TokenDeclState> kvp in tokenScript.states)
            {
                TokenDeclState declState = kvp.Value;
                GenerateStateEventHandlers(declState.name.val, declState.body);
            }

            ScriptObjWriter.TheEnd(objFileWriter);
        }

        /**
         * @brief Write out what slot was assigned for a global or sdtclass static variable.
         *        Constants, functions, instance fields, methods, properties do not have slots in the global variables arrays.
         */
        private void WriteOutGblAssignment(string pfx, TokenDeclVar declVar)
        {
            if(!declVar.constant && (declVar.retType == null) && (declVar.getProp == null) && (declVar.setProp == null))
            {
                objFileWriter.Write(pfx + declVar.name.val);    // string
                objFileWriter.Write(declVar.vTableArray.Name);  // string
                objFileWriter.Write(declVar.vTableIndex);       // int
            }
        }

        /**
         * @brief generate event handler code
         * Writes out a function definition for each state handler
         * named <statename> <eventname>
         *
         * However, each has just 'XMRInstance __sw' as its single argument
         * and each of its user-visible argments is extracted from __sw.ehArgs[].
         *
         * So we end up generating something like this:
         *
         *   private static void <statename> <eventname>(XMRInstance __sw)
         *   {
         *      <typeArg0> <nameArg0> = (<typeArg0>)__sw.ehArgs[0];
         *      <typeArg1> <nameArg1> = (<typeArg1>)__sw.ehArgs[1];
         *
         *      ... script code ...
         *   }
         *
         * The continuations code assumes there will be no references to ehArgs[]
         * after the first call to CheckRun() as CheckRun() makes no attempt to
         * serialize the ehArgs[] array, as doing so would be redundant.  Any values
         * from ehArgs[] that are being used will be in local stack variables and
         * thus preserved that way.
         */
        private void GenerateStateEventHandlers(string statename, TokenStateBody body)
        {
            Dictionary<string, TokenDeclVar> statehandlers = new Dictionary<string, TokenDeclVar>();
            for(Token t = body.eventFuncs; t != null; t = t.nextToken)
            {
                TokenDeclVar tdv = (TokenDeclVar)t;
                string eventname = tdv.GetSimpleName();
                if(statehandlers.ContainsKey(eventname))
                {
                    ErrorMsg(tdv, "event handler " + eventname + " already defined for state " + statename);
                }
                else
                {
                    statehandlers.Add(eventname, tdv);
                    GenerateEventHandler(statename, tdv);
                }
            }
        }

        private void GenerateEventHandler(string statename, TokenDeclVar declFunc)
        {
            string eventname = declFunc.GetSimpleName();
            TokenArgDecl argDecl = declFunc.argDecl;

            HeapLocals.Clear();

            // Make sure event handler name is valid and that number and type of arguments is correct.
            // Apparently some scripts exist with fewer than correct number of args in their declaration 
            // so allow for that.  It is ok because the handlers are called with the arguments in an
            // object[] array, and we just won't access the missing argments in the vector.  But the 
            // specified types must match one of the prototypes in legalEventHandlers.
            TokenDeclVar protoDeclFunc = legalEventHandlers.FindExact(eventname, argDecl.types);
            if(protoDeclFunc == null)
            {
                ErrorMsg(declFunc, "unknown event handler " + eventname + argDecl.GetArgSig());
                return;
            }

             // Output function header.
             // They just have the XMRInstAbstract pointer as the one argument.
            string functionName = statename + " " + eventname;
            _ilGen = new ScriptObjWriter(tokenScript,
                                          functionName,
                                          typeof(void),
                                          instanceTypeArg,
                                          instanceNameArg,
                                          objFileWriter);
            StartFunctionBody(declFunc);

             // Create a temp to hold XMRInstanceSuperType version of arg 0.
            instancePointer = ilGen.DeclareLocal(xmrInstSuperType, "__xmrinst");
            ilGen.Emit(declFunc, OpCodes.Ldarg_0);
            ilGen.Emit(declFunc, OpCodes.Castclass, xmrInstSuperType);
            ilGen.Emit(declFunc, OpCodes.Stloc, instancePointer);

             // Output args as variable definitions and initialize each from __sw.ehArgs[].
             // If the script writer goofed, the typecast will complain.
            int nArgs = argDecl.vars.Length;
            for(int i = 0; i < nArgs; i++)
            {
                 // Say that the argument variable is going to be located in a local var.
                TokenDeclVar argVar = argDecl.vars[i];
                TokenType argTokType = argVar.type;
                CompValuLocalVar local = new CompValuLocalVar(argTokType, argVar.name.val, this);
                argVar.location = local;

                 // Copy from the ehArgs[i] element to the temp var.
                 // Cast as needed, there is a lot of craziness like OpenMetaverse.Quaternion.
                local.PopPre(this, argVar.name);
                PushXMRInst();                                          // instance
                ilGen.Emit(declFunc, OpCodes.Ldfld, ehArgsFieldInfo);   // instance.ehArgs (array of objects)
                ilGen.Emit(declFunc, OpCodes.Ldc_I4, i);                // array index = i
                ilGen.Emit(declFunc, OpCodes.Ldelem, typeof(object));  // select the argument we want
                TokenType stkTokType = tokenTypeObj;                     // stack has a type 'object' on it now
                Type argSysType = argTokType.ToSysType();               // this is the type the script expects
                if(argSysType == typeof(double))
                {                // LSL_Float/double -> double
                    ilGen.Emit(declFunc, OpCodes.Call, ehArgUnwrapFloat);
                    stkTokType = tokenTypeFlt;                       // stack has a type 'double' on it now
                }
                if(argSysType == typeof(int))
                {                        // LSL_Integer/int -> int
                    ilGen.Emit(declFunc, OpCodes.Call, ehArgUnwrapInteger);
                    stkTokType = tokenTypeInt;                       // stack has a type 'int' on it now
                }
                if(argSysType == typeof(LSL_List))
                {                   // LSL_List -> LSL_List
                    TypeCast.CastTopOfStack(this, argVar.name, stkTokType, argTokType, true);
                    stkTokType = argTokType;                         // stack has a type 'LSL_List' on it now
                }
                if(argSysType == typeof(LSL_Rotation))
                {               // OpenMetaverse.Quaternion/LSL_Rotation -> LSL_Rotation
                    ilGen.Emit(declFunc, OpCodes.Call, ehArgUnwrapRotation);
                    stkTokType = tokenTypeRot;                       // stack has a type 'LSL_Rotation' on it now
                }
                if(argSysType == typeof(string))
                {                     // LSL_Key/LSL_String/string -> string
                    ilGen.Emit(declFunc, OpCodes.Call, ehArgUnwrapString);
                    stkTokType = tokenTypeStr;                       // stack has a type 'string' on it now
                }
                if(argSysType == typeof(LSL_Vector))
                {                 // OpenMetaverse.Vector3/LSL_Vector -> LSL_Vector
                    ilGen.Emit(declFunc, OpCodes.Call, ehArgUnwrapVector);
                    stkTokType = tokenTypeVec;                       // stack has a type 'LSL_Vector' on it now
                }
                local.PopPost(this, argVar.name, stkTokType);           // pop stack type into argtype
            }

             // Output code for the statements and clean up.
            GenerateFuncBody();
        }

        /**
         * @brief generate header for an arbitrary script-defined global function.
         * @param declFunc = function being defined
         */
        private void GenerateMethodHeader(TokenDeclVar declFunc)
        {
            curDeclFunc = declFunc;

             // Make up array of all argument types as seen by the code generator.
             // We splice in XMRInstanceSuperType or XMRSDTypeClObj for the first 
             // arg as the function itself is static, followed by script-visible
             // arg types.
            TokenArgDecl argDecl = declFunc.argDecl;
            int nArgs = argDecl.vars.Length;
            Type[] argTypes = new Type[nArgs + 1];
            string[] argNames = new string[nArgs + 1];
            if(IsSDTInstMethod())
            {
                argTypes[0] = typeof(XMRSDTypeClObj);
                argNames[0] = "$sdtthis";
            }
            else
            {
                argTypes[0] = xmrInstSuperType;
                argNames[0] = "$xmrthis";
            }
            for(int i = 0; i < nArgs; i++)
            {
                argTypes[i + 1] = argDecl.vars[i].type.ToSysType();
                argNames[i + 1] = argDecl.vars[i].name.val;
            }

             // Set up entrypoint.
            string objCodeName = declFunc.GetObjCodeName();
            declFunc.ilGen = new ScriptObjWriter(tokenScript,
                                                  objCodeName,
                                                  declFunc.retType.ToSysType(),
                                                  argTypes,
                                                  argNames,
                                                  objFileWriter);

             // This says how to generate a call to the function and to get a delegate.
            declFunc.location = new CompValuGlobalMeth(declFunc);

            curDeclFunc = null;
        }

        /**
         * @brief generate code for an arbitrary script-defined function.
         * @param name = name of the function
         * @param argDecl = argument declarations
         * @param body = function's code body
         */
        private void GenerateMethodBody(TokenDeclVar declFunc)
        {
            HeapLocals.Clear();

            // Set up code generator for the function's contents.
            _ilGen = declFunc.ilGen;
            StartFunctionBody(declFunc);

             // Create a temp to hold XMRInstanceSuperType version of arg 0.
             // For most functions, arg 0 is already XMRInstanceSuperType.
             // But for script-defined class instance methods, arg 0 holds
             // the XMRSDTypeClObj pointer and so we read the XMRInstAbstract
             // pointer from its XMRSDTypeClObj.xmrInst field then cast it to
             // XMRInstanceSuperType.
            if(IsSDTInstMethod())
            {
                instancePointer = ilGen.DeclareLocal(xmrInstSuperType, "__xmrinst");
                ilGen.Emit(declFunc, OpCodes.Ldarg_0);
                ilGen.Emit(declFunc, OpCodes.Ldfld, sdtXMRInstFieldInfo);
                ilGen.Emit(declFunc, OpCodes.Castclass, xmrInstSuperType);
                ilGen.Emit(declFunc, OpCodes.Stloc, instancePointer);
            }

             // Define location of all script-level arguments so script body can access them.
             // The argument indices need to have +1 added to them because XMRInstance or 
             // XMRSDTypeClObj is spliced in at arg 0.
            TokenArgDecl argDecl = declFunc.argDecl;
            int nArgs = argDecl.vars.Length;
            for(int i = 0; i < nArgs; i++)
            {
                TokenDeclVar argVar = argDecl.vars[i];
                argVar.location = new CompValuArg(argVar.type, i + 1);
            }

             // Output code for the statements and clean up.
            GenerateFuncBody();
        }

        private void StartFunctionBody(TokenDeclVar declFunc)
        {
             // Start current function being processed.
             // Set 'mightGetHere' as the code at the top is always executed.
            instancePointer = null;
            mightGetHere = true;
            curBreakTarg = null;
            curContTarg = null;
            curDeclFunc = declFunc;

             // Start generating code.
            ((ScriptObjWriter)ilGen).BegMethod();
        }

        /**
         * @brief Define function for a script-defined type's <typename>.$new(<argsig>) method.
         *        See GenerateStmtNewobj() for more info.
         */
        private TokenDeclVar DefineNewobjFunc(TokenDeclVar ctorDeclFunc)
        {
             // Set up 'static classname $new(params-same-as-ctor) { }'.
            TokenDeclVar newobjDeclFunc = new TokenDeclVar(ctorDeclFunc, null, tokenScript);
            newobjDeclFunc.name = new TokenName(newobjDeclFunc, "$new");
            newobjDeclFunc.retType = ctorDeclFunc.sdtClass.MakeRefToken(newobjDeclFunc);
            newobjDeclFunc.argDecl = ctorDeclFunc.argDecl;
            newobjDeclFunc.sdtClass = ctorDeclFunc.sdtClass;
            newobjDeclFunc.sdtFlags = ScriptReduce.SDT_STATIC | ctorDeclFunc.sdtFlags;

             // Declare local variable named '$objptr' in a frame just under 
             // what the '$new(...)' function's arguments are declared in.
            TokenDeclVar objptrVar = new TokenDeclVar(newobjDeclFunc, newobjDeclFunc, tokenScript);
            objptrVar.type = newobjDeclFunc.retType;
            objptrVar.name = new TokenName(newobjDeclFunc, "$objptr");
            VarDict newFrame = new VarDict(false);
            newFrame.outerVarDict = ctorDeclFunc.argDecl.varDict;
            newFrame.AddEntry(objptrVar);

             // Set up '$objptr.$ctor'
            TokenLValName objptrLValName = new TokenLValName(objptrVar.name, newFrame);

            // ref a var by giving its name
            TokenLValIField objptrDotCtor = new TokenLValIField(newobjDeclFunc);  // an instance member reference
            objptrDotCtor.baseRVal = objptrLValName;                        // '$objptr'
            objptrDotCtor.fieldName = ctorDeclFunc.name;                     // '.' '$ctor'

             // Set up '$objptr.$ctor(arglist)' call for use in the '$new(...)' body.
             // Copy the arglist from the constructor declaration so triviality 
             // processing will pick the correct overloaded constructor.
            TokenRValCall callCtorRVal = new TokenRValCall(newobjDeclFunc);   // doing a call of some sort
            callCtorRVal.meth = objptrDotCtor;                        // calling $objptr.$ctor()
            TokenDeclVar[] argList = newobjDeclFunc.argDecl.vars;          // get args $new() was declared with
            callCtorRVal.nArgs = argList.Length;                       // ...that is nArgs we are passing to $objptr.$ctor()
            for(int i = argList.Length; --i >= 0;)
            {
                TokenDeclVar arg = argList[i];                    // find out about one of the args
                TokenLValName argLValName = new TokenLValName(arg.name, ctorDeclFunc.argDecl.varDict);
                // pass arg of that name to $objptr.$ctor()
                argLValName.nextToken = callCtorRVal.args;             // link to list of args passed to $objptr.$ctor()
                callCtorRVal.args = argLValName;
            }

             // Set up a funky call to the constructor for the code body.
             // This will let code generator know there is some craziness.
             // See GenerateStmtNewobj().
             //
             // This is in essence:
             //    {
             //        classname $objptr = newobj (classname);
             //        $objptr.$ctor (...);
             //        return $objptr;
             //    }
            TokenStmtNewobj newobjStmtBody = new TokenStmtNewobj(ctorDeclFunc);
            newobjStmtBody.objptrVar = objptrVar;
            newobjStmtBody.rValCall = callCtorRVal;
            TokenStmtBlock newobjBody = new TokenStmtBlock(ctorDeclFunc);
            newobjBody.statements = newobjStmtBody;

             // Link that code as the body of the function.
            newobjDeclFunc.body = newobjBody;

             // Say the function calls '$objptr.$ctor(arglist)' so we will inherit ctor's triviality.
            newobjDeclFunc.unknownTrivialityCalls.AddLast(callCtorRVal);
            return newobjDeclFunc;
        }

        private class TokenStmtNewobj: TokenStmt
        {
            public TokenDeclVar objptrVar;
            public TokenRValCall rValCall;
            public TokenStmtNewobj(Token original) : base(original) { }
        }

        /**
         * @brief Output function body (either event handler or script-defined method).
         */
        private void GenerateFuncBody()
        {
             // We want to know if the function's code is trivial, ie,
             // if it doesn't have anything that might be an infinite 
             // loop and that is doesn't call anything that might have 
             // an infinite loop.  If it is, we don't need any CheckRun()
             // stuff or any of the frame save/restore stuff.
            bool isTrivial = curDeclFunc.IsFuncTrivial(this);

             // Clear list of all call labels.
             // A call label is inserted just before every call that can possibly
             // call CheckRun(), including any direct calls to CheckRun().
             // Then, when restoring stack, we can just switch to this label to
             // resume at the correct spot.
            actCallLabels.Clear();
            allCallLabels.Clear();
            openCallLabel = null;

             // Alloc stack space for local vars.
            int stackframesize = AllocLocalVarStackSpace();

             // Include argument variables in stack space for this frame.
            foreach(TokenType tokType in curDeclFunc.argDecl.types)
            {
                stackframesize += LocalVarStackSize(tokType);
            }

             // Any return statements inside function body jump to this label
             // after putting return value in __retval.
            retLabel = ilGen.DefineLabel("__retlbl");
            retValue = null;
            if(!(curDeclFunc.retType is TokenTypeVoid))
            {
                retValue = ilGen.DeclareLocal(curDeclFunc.retType.ToSysType(), "__retval");
            }

             // Output:
             //    int __mainCallNo = -1;
             //    instance.m_StackLeft -= stackframesize;
             //    try {
             //        if (instance.callMode != CallMode_NORMAL) goto __cmRestore;
            actCallNo = null;
            ScriptMyLabel cmRestore = null;
            if(!isTrivial)
            {
                actCallNo = ilGen.DeclareLocal(typeof(int), "__mainCallNo");
                SetCallNo(curDeclFunc, actCallNo, -1);
                PushXMRInst();
                ilGen.Emit(curDeclFunc, OpCodes.Dup);
                ilGen.Emit(curDeclFunc, OpCodes.Ldfld, stackLeftFieldInfo);
                ilGen.Emit(curDeclFunc, OpCodes.Ldc_I4, stackframesize);
                ilGen.Emit(curDeclFunc, OpCodes.Sub);
                ilGen.Emit(curDeclFunc, OpCodes.Stfld, stackLeftFieldInfo);
                cmRestore = ilGen.DefineLabel("__cmRestore");
                ilGen.BeginExceptionBlock();
                PushXMRInst();
                ilGen.Emit(curDeclFunc, OpCodes.Ldfld, ScriptCodeGen.callModeFieldInfo);
                ilGen.Emit(curDeclFunc, OpCodes.Ldc_I4, XMRInstAbstract.CallMode_NORMAL);
                ilGen.Emit(curDeclFunc, OpCodes.Bne_Un, cmRestore);
            }

             // Splice in the code optimizer for the body of the function.
            ScriptCollector collector = new ScriptCollector((ScriptObjWriter)ilGen);
            _ilGen = collector;

             // If this is the default state_entry() handler, output code to set all global
             // variables to their initial values.  Note that every script must have a
             // default state_entry() handler, we provide one if the script doesn't explicitly
             // define one.
            string methname = ilGen.methName;
            if(methname == "default state_entry")
            {

                // if (!doGblInit) goto skipGblInit;
                ScriptMyLabel skipGblInitLabel = ilGen.DefineLabel("__skipGblInit");
                PushXMRInst();                                  // instance
                ilGen.Emit(curDeclFunc, OpCodes.Ldfld, doGblInitFieldInfo);  // instance.doGblInit
                ilGen.Emit(curDeclFunc, OpCodes.Brfalse, skipGblInitLabel);

                // $globalvarinit();
                TokenDeclVar gviFunc = tokenScript.globalVarInit;
                if(gviFunc.body.statements != null)
                {
                    gviFunc.location.CallPre(this, gviFunc);
                    gviFunc.location.CallPost(this, gviFunc);
                }

                // various $staticfieldinit();
                foreach(TokenDeclSDType sdType in tokenScript.sdSrcTypesValues)
                {
                    if(sdType is TokenDeclSDTypeClass)
                    {
                        TokenDeclVar sfiFunc = ((TokenDeclSDTypeClass)sdType).staticFieldInit;
                        if((sfiFunc != null) && (sfiFunc.body.statements != null))
                        {
                            sfiFunc.location.CallPre(this, sfiFunc);
                            sfiFunc.location.CallPost(this, sfiFunc);
                        }
                    }
                }

                // doGblInit = 0;
                PushXMRInst();                                  // instance
                ilGen.Emit(curDeclFunc, OpCodes.Ldc_I4_0);
                ilGen.Emit(curDeclFunc, OpCodes.Stfld, doGblInitFieldInfo);  // instance.doGblInit

                //skipGblInit:
                ilGen.MarkLabel(skipGblInitLabel);
            }

             // If this is a script-defined type constructor, call the base constructor and call
             // this class's $instfieldinit() method to initialize instance fields.
            if((curDeclFunc.sdtClass != null) && curDeclFunc.funcNameSig.val.StartsWith("$ctor("))
            {
                if(curDeclFunc.baseCtorCall != null)
                {
                    GenerateFromRValCall(curDeclFunc.baseCtorCall);
                }
                TokenDeclVar ifiFunc = ((TokenDeclSDTypeClass)curDeclFunc.sdtClass).instFieldInit;
                if(ifiFunc.body.statements != null)
                {
                    CompValu thisCompValu = new CompValuArg(ifiFunc.sdtClass.MakeRefToken(ifiFunc), 0);
                    CompValu ifiFuncLocn = new CompValuInstMember(ifiFunc, thisCompValu, true);
                    ifiFuncLocn.CallPre(this, ifiFunc);
                    ifiFuncLocn.CallPost(this, ifiFunc);
                }
            }

             // See if time to suspend in case they are doing a loop with recursion.
            if(!isTrivial)
                EmitCallCheckRun(curDeclFunc, true);

             // Output code body.
            GenerateStmtBlock(curDeclFunc.body);

             // If code falls through to this point, means they are missing 
             // a return statement.  And that is legal only if the function 
             // returns 'void'.
            if(mightGetHere)
            {
                if(!(curDeclFunc.retType is TokenTypeVoid))
                {
                    ErrorMsg(curDeclFunc.body, "missing final return statement");
                }
                ilGen.Emit(curDeclFunc, OpCodes.Leave, retLabel);
            }

             // End of the code to be optimized.
             // Do optimizations then write it all out to object file.
             // After this, all code gets written directly to object file.
             // Optimization must be completed before we scan the allCallLabels
             // list below to look for active locals and temps.
            collector.Optimize();
            _ilGen = collector.WriteOutAll();
            collector = null;

            List<ScriptMyLocal> activeTemps = null;
            if (!isTrivial)
            {
                // Build list of locals and temps active at all the call labels.
                activeTemps = new List<ScriptMyLocal>();
                foreach (CallLabel cl in allCallLabels)
                {
                    foreach (ScriptMyLocal lcl in cl.callLabel.whereAmI.localsReadBeforeWritten)
                    {
                        if (!activeTemps.Contains(lcl))
                        {
                            activeTemps.Add(lcl);
                        }
                    }
                }

                // Output code to restore the args, locals and temps then jump to
                // the call label that we were interrupted at.
                ilGen.MarkLabel(cmRestore);
                GenerateFrameRestoreCode(activeTemps);
            }

             // Output epilog that saves stack frame state if CallMode_SAVE.
             //
             //   finally {
             //      instance.m_StackLeft += stackframesize;
             //      if (instance.callMode != CallMode_SAVE) goto __endFin;
             //      GenerateFrameCaptureCode();
             //   __endFin:
             //   }
            ScriptMyLabel endFin = null;
            if(!isTrivial)
            {
                ilGen.BeginFinallyBlock();
                PushXMRInst();
                ilGen.Emit(curDeclFunc, OpCodes.Dup);
                ilGen.Emit(curDeclFunc, OpCodes.Ldfld, stackLeftFieldInfo);
                ilGen.Emit(curDeclFunc, OpCodes.Ldc_I4, stackframesize);
                ilGen.Emit(curDeclFunc, OpCodes.Add);
                ilGen.Emit(curDeclFunc, OpCodes.Stfld, stackLeftFieldInfo);
                endFin = ilGen.DefineLabel("__endFin");
                PushXMRInst();
                ilGen.Emit(curDeclFunc, OpCodes.Ldfld, callModeFieldInfo);
                ilGen.Emit(curDeclFunc, OpCodes.Ldc_I4, XMRInstAbstract.CallMode_SAVE);
                ilGen.Emit(curDeclFunc, OpCodes.Bne_Un, endFin);
                GenerateFrameCaptureCode(activeTemps);
                ilGen.MarkLabel(endFin);
                ilGen.Emit(curDeclFunc, OpCodes.Endfinally);
                ilGen.EndExceptionBlock();
            }

             // Output the 'real' return opcode.
             // push return value
            ilGen.MarkLabel(retLabel);
            if (!(curDeclFunc.retType is TokenTypeVoid))
            {
                ilGen.Emit(curDeclFunc, OpCodes.Ldloc, retValue);
            }

            // pseudo free memory usage
            foreach (ScriptMyLocal sml in HeapLocals)
            {
                Type t = sml.type;
                if (t == typeof(HeapTrackerList))
                {
                    ilGen.Emit(curDeclFunc, OpCodes.Ldloc, sml);
                    HeapTrackerList.GenFree(curDeclFunc, ilGen);
                }
                else if (t == typeof(HeapTrackerString))
                {
                    ilGen.Emit(curDeclFunc, OpCodes.Ldloc, sml);
                    HeapTrackerString.GenFree(curDeclFunc, ilGen);
                }
                else if (t == typeof(HeapTrackerObject))
                {
                    ilGen.Emit(curDeclFunc, OpCodes.Ldloc, sml);
                    HeapTrackerObject.GenFree(curDeclFunc, ilGen);
                }
            }

            ilGen.Emit(curDeclFunc, OpCodes.Ret);
            retLabel = null;
            retValue = null;

             // No more instructions for this method.
            ((ScriptObjWriter)ilGen).EndMethod();
            _ilGen = null;

             // Not generating function code any more.
            curBreakTarg = null;
            curContTarg = null;
            curDeclFunc = null;
        }

        /**
         * @brief Allocate stack space for all local variables, regardless of
         *        which { } statement block they are actually defined in.
         * @returns approximate stack frame size
         */
        private int AllocLocalVarStackSpace()
        {
            int stackframesize = 64;  // RIP, RBX, RBP, R12..R15, one extra
            foreach(TokenDeclVar localVar in curDeclFunc.localVars)
            {
                 // Skip all 'constant' vars as they were handled by the reducer.
                if(localVar.constant)
                    continue;

                 // Get a stack location for the local variable.
                localVar.location = new CompValuLocalVar(localVar.type, localVar.name.val, this);

                 // Stack size for the local variable.
                stackframesize += LocalVarStackSize(localVar.type);
            }
            return stackframesize;
        }

        private static int LocalVarStackSize(TokenType tokType)
        {
            Type sysType = tokType.ToSysType();
            return sysType.IsValueType ? System.Runtime.InteropServices.Marshal.SizeOf(sysType) : 8;
        }

        /**
         * @brief Generate code to write all arguments and locals to the capture stack frame.
         *        This includes temp variables.
         *        We only need to save what is active at the point of callLabels through because 
         *        those are the only points we will jump to on restore.  This saves us from saving 
         *        all the little temp vars we create.
         * @param activeTemps = list of locals and temps that we care about, ie, which
         *                      ones get restored by GenerateFrameRestoreCode().
         */
        private void GenerateFrameCaptureCode(List<ScriptMyLocal> activeTemps)
        {
             // Compute total number of slots we need to save stuff.
             // Assume we need to save all call arguments.
            int nSaves = curDeclFunc.argDecl.vars.Length + activeTemps.Count;

             // Output code to allocate a stack frame object with an object array.
             // This also pushes the stack frame object on the instance.stackFrames list.
             // It returns a pointer to the object array it allocated.
            PushXMRInst();
            ilGen.Emit(curDeclFunc, OpCodes.Ldstr, ilGen.methName);
            GetCallNo(curDeclFunc, actCallNo);
            ilGen.Emit(curDeclFunc, OpCodes.Ldc_I4, nSaves);
            ilGen.Emit(curDeclFunc, OpCodes.Call, captureStackFrameMethodInfo);

             // Copy arg values to object array, boxing as needed.
            int i = 0;
            foreach(TokenDeclVar argVar in curDeclFunc.argDecl.varDict)
            {
                ilGen.Emit(curDeclFunc, OpCodes.Dup);
                ilGen.Emit(curDeclFunc, OpCodes.Ldc_I4, i);
                argVar.location.PushVal(this, argVar.name, tokenTypeObj);
                ilGen.Emit(curDeclFunc, OpCodes.Stelem_Ref);
                i++;
            }

             // Copy local and temp values to object array, boxing as needed.
            foreach(ScriptMyLocal lcl in activeTemps)
            {
                ilGen.Emit(curDeclFunc, OpCodes.Dup);
                ilGen.Emit(curDeclFunc, OpCodes.Ldc_I4, i++);
                ilGen.Emit(curDeclFunc, OpCodes.Ldloc, lcl);
                Type t = lcl.type;
                if(t == typeof(HeapTrackerList))
                {
                    t = HeapTrackerList.GenPush(curDeclFunc, ilGen);
                }
                if(t == typeof(HeapTrackerObject))
                {
                    t = HeapTrackerObject.GenPush(curDeclFunc, ilGen);
                }
                if(t == typeof(HeapTrackerString))
                {
                    t = HeapTrackerString.GenPush(curDeclFunc, ilGen);
                }
                if(t.IsValueType)
                {
                    ilGen.Emit(curDeclFunc, OpCodes.Box, t);
                }
                ilGen.Emit(curDeclFunc, OpCodes.Stelem_Ref);
            }

            ilGen.Emit(curDeclFunc, OpCodes.Pop);
        }

        /**
         * @brief Generate code to restore all arguments and locals from the restore stack frame.
         *        This includes temp variables.
         */
        private void GenerateFrameRestoreCode(List<ScriptMyLocal> activeTemps)
        {
            ScriptMyLocal objArray = ilGen.DeclareLocal(typeof(object[]), "__restObjArray");

             // Output code to pop stack frame from instance.stackFrames.
             // It returns a pointer to the object array that contains values to be restored.
            PushXMRInst();
            ilGen.Emit(curDeclFunc, OpCodes.Ldstr, ilGen.methName);
            ilGen.Emit(curDeclFunc, OpCodes.Ldloca, actCallNo);  // __mainCallNo
            ilGen.Emit(curDeclFunc, OpCodes.Call, restoreStackFrameMethodInfo);
            ilGen.Emit(curDeclFunc, OpCodes.Stloc, objArray);

             // Restore argument values from object array, unboxing as needed.
             // Although the caller has restored them to what it called us with, it's possible that this 
             // function has modified them since, so we need to do our own restore.
            int i = 0;
            foreach(TokenDeclVar argVar in curDeclFunc.argDecl.varDict)
            {
                CompValu argLoc = argVar.location;
                argLoc.PopPre(this, argVar.name);
                ilGen.Emit(curDeclFunc, OpCodes.Ldloc, objArray);
                ilGen.Emit(curDeclFunc, OpCodes.Ldc_I4, i);
                ilGen.Emit(curDeclFunc, OpCodes.Ldelem_Ref);
                TypeCast.CastTopOfStack(this, argVar.name, tokenTypeObj, argLoc.type, true);
                argLoc.PopPost(this, argVar.name);
                i++;
            }

             // Restore local and temp values from object array, unboxing as needed.
            foreach(ScriptMyLocal lcl in activeTemps)
            {
                Type t = lcl.type;
                Type u = t;
                if(t == typeof(HeapTrackerList))
                    u = typeof(LSL_List);
                if(t == typeof(HeapTrackerObject))
                    u = typeof(object);
                if(t == typeof(HeapTrackerString))
                    u = typeof(string);
                if(u != t)
                {
                    ilGen.Emit(curDeclFunc, OpCodes.Ldloc, lcl);
                }
                ilGen.Emit(curDeclFunc, OpCodes.Ldloc, objArray);
                ilGen.Emit(curDeclFunc, OpCodes.Ldc_I4, i++);
                ilGen.Emit(curDeclFunc, OpCodes.Ldelem_Ref);
                if(u.IsValueType)
                {
                    ilGen.Emit(curDeclFunc, OpCodes.Unbox_Any, u);
                }
                else if(u != typeof(object))
                {
                    ilGen.Emit(curDeclFunc, OpCodes.Castclass, u);
                }
                if(u != t)
                {
                    if(t == typeof(HeapTrackerList))
                        HeapTrackerList.GenRestore(curDeclFunc, ilGen);
                    if(t == typeof(HeapTrackerObject))
                        HeapTrackerObject.GenRestore(curDeclFunc, ilGen);
                    if(t == typeof(HeapTrackerString))
                        HeapTrackerString.GenRestore(curDeclFunc, ilGen);
                }
                else
                {
                    ilGen.Emit(curDeclFunc, OpCodes.Stloc, lcl);
                }
            }

            OutputCallNoSwitchStmt();
        }

        /**
         * @brief Output a switch statement with a case for each possible 
         *        value of whatever callNo is currently active, either 
         *        __mainCallNo or one of the try/catch/finally's callNos.
         *
         *   switch (callNo) {
         *      case 0: goto __call_0;
         *      case 1: goto __call_1;
         *      ...
         *   }
         *   throw new ScriptBadCallNoException (callNo);
         */
        private void OutputCallNoSwitchStmt()
        {
            ScriptMyLabel[] callLabels = new ScriptMyLabel[actCallLabels.Count];
            foreach(CallLabel cl in actCallLabels)
            {
                callLabels[cl.index] = cl.callLabel;
            }
            GetCallNo(curDeclFunc, actCallNo);
            ilGen.Emit(curDeclFunc, OpCodes.Switch, callLabels);

            GetCallNo(curDeclFunc, actCallNo);
            ilGen.Emit(curDeclFunc, OpCodes.Newobj, scriptBadCallNoExceptionConstructorInfo);
            ilGen.Emit(curDeclFunc, OpCodes.Throw);
        }

        /**
         * @brief There is one of these per call that can possibly call CheckRun(),
         *        including direct calls to CheckRun().
         *        They mark points that the stack capture/restore code will save & restore to.
         *        All object-code level local vars active at the call label's point will 
         *        be saved & restored.
         *
         *            callNo = 5;
         *        __call_5:
         *            push call arguments from temps
         *            call SomethingThatCallsCheckRun()
         *
         *        If SomethingThatCallsCheckRun() actually calls CheckRun(), our restore code
         *        will restore our args, locals & temps, then jump to __call_5, which will then 
         *        call SomethingThatCallsCheckRun() again, which will restore its stuff likewise.
         *        When eventually the actual CheckRun() call is restored, it will turn off restore 
         *        mode (by changing callMode from CallMode_RESTORE to CallMode_NORMAL) and return, 
         *        allowing the code to run normally from that point.
         */
        public class CallLabel
        {
            public int index;       // sequential integer, starting at 0, within actCallLabels
                                    // - used for the switch statement
            public ScriptMyLabel callLabel;   // the actual label token

            public CallLabel(ScriptCodeGen scg, Token errorAt)
            {
                if(scg.openCallLabel != null)
                    throw new Exception("call label already open");

                if(!scg.curDeclFunc.IsFuncTrivial(scg))
                {
                    this.index = scg.actCallLabels.Count;
                    string name = "__call_" + index + "_" + scg.allCallLabels.Count;

                     // Make sure eval stack is empty because the frame capture/restore 
                     // code expects such (restore switch stmt has an empty stack).
                    int depth = ((ScriptCollector)scg.ilGen).stackDepth.Count;
                    if(depth > 0)
                    {
                        // maybe need to call Trivialize()
                        throw new Exception("call label stack depth " + depth + " at " + errorAt.SrcLoc);
                    }

                     // Eval stack is empty so the restore code can handle it.
                    this.index = scg.actCallLabels.Count;
                    scg.actCallLabels.AddLast(this);
                    scg.allCallLabels.AddLast(this);
                    this.callLabel = scg.ilGen.DefineLabel(name);
                    scg.SetCallNo(errorAt, scg.actCallNo, this.index);
                    scg.ilGen.MarkLabel(this.callLabel);
                }

                scg.openCallLabel = this;
            }
        };

        /**
         * @brief generate code for an arbitrary statement.
         */
        private void GenerateStmt(TokenStmt stmt)
        {
            errorMessageToken = stmt;
            if(stmt is TokenDeclVar)
            {
                GenerateDeclVar((TokenDeclVar)stmt);
                return;
            }
            if(stmt is TokenStmtBlock)
            {
                GenerateStmtBlock((TokenStmtBlock)stmt);
                return;
            }
            if(stmt is TokenStmtBreak)
            {
                GenerateStmtBreak((TokenStmtBreak)stmt);
                return;
            }
            if(stmt is TokenStmtCont)
            {
                GenerateStmtCont((TokenStmtCont)stmt);
                return;
            }
            if(stmt is TokenStmtDo)
            {
                GenerateStmtDo((TokenStmtDo)stmt);
                return;
            }
            if(stmt is TokenStmtFor)
            {
                GenerateStmtFor((TokenStmtFor)stmt);
                return;
            }
            if(stmt is TokenStmtForEach)
            {
                GenerateStmtForEach((TokenStmtForEach)stmt);
                return;
            }
            if(stmt is TokenStmtIf)
            {
                GenerateStmtIf((TokenStmtIf)stmt);
                return;
            }
            if(stmt is TokenStmtJump)
            {
                GenerateStmtJump((TokenStmtJump)stmt);
                return;
            }
            if(stmt is TokenStmtLabel)
            {
                GenerateStmtLabel((TokenStmtLabel)stmt);
                return;
            }
            if(stmt is TokenStmtNewobj)
            {
                GenerateStmtNewobj((TokenStmtNewobj)stmt);
                return;
            }
            if(stmt is TokenStmtNull)
            {
                return;
            }
            if(stmt is TokenStmtRet)
            {
                GenerateStmtRet((TokenStmtRet)stmt);
                return;
            }
            if(stmt is TokenStmtRVal)
            {
                GenerateStmtRVal((TokenStmtRVal)stmt);
                return;
            }
            if(stmt is TokenStmtState)
            {
                GenerateStmtState((TokenStmtState)stmt);
                return;
            }
            if(stmt is TokenStmtSwitch)
            {
                GenerateStmtSwitch((TokenStmtSwitch)stmt);
                return;
            }
            if(stmt is TokenStmtThrow)
            {
                GenerateStmtThrow((TokenStmtThrow)stmt);
                return;
            }
            if(stmt is TokenStmtTry)
            {
                GenerateStmtTry((TokenStmtTry)stmt);
                return;
            }
            if(stmt is TokenStmtVarIniDef)
            {
                GenerateStmtVarIniDef((TokenStmtVarIniDef)stmt);
                return;
            }
            if(stmt is TokenStmtWhile)
            {
                GenerateStmtWhile((TokenStmtWhile)stmt);
                return;
            }
            throw new Exception("unknown TokenStmt type " + stmt.GetType().ToString());
        }

        /**
         * @brief generate statement block (ie, with braces)
         */
        private void GenerateStmtBlock(TokenStmtBlock stmtBlock)
        {
            if(!mightGetHere)
                return;

             // Push new current statement block pointer for anyone who cares.
            TokenStmtBlock oldStmtBlock = curStmtBlock;
            curStmtBlock = stmtBlock;

             // Output the statements that make up the block.
            for(Token t = stmtBlock.statements; t != null; t = t.nextToken)
            {
                GenerateStmt((TokenStmt)t);
            }

             // Pop the current statement block.
            curStmtBlock = oldStmtBlock;
        }

        /**
         * @brief output code for a 'break' statement
         */
        private void GenerateStmtBreak(TokenStmtBreak breakStmt)
        {
            if(!mightGetHere)
                return;

             // Make sure we are in a breakable situation.
            if(curBreakTarg == null)
            {
                ErrorMsg(breakStmt, "not in a breakable situation");
                return;
            }

             // Tell anyone who cares that the break target was actually used.
            curBreakTarg.used = true;

             // Output the instructions.
            EmitJumpCode(curBreakTarg.label, curBreakTarg.block, breakStmt);
        }

        /**
         * @brief output code for a 'continue' statement
         */
        private void GenerateStmtCont(TokenStmtCont contStmt)
        {
            if(!mightGetHere)
                return;

             // Make sure we are in a contable situation.
            if(curContTarg == null)
            {
                ErrorMsg(contStmt, "not in a continueable situation");
                return;
            }

             // Tell anyone who cares that the continue target was actually used.
            curContTarg.used = true;

             // Output the instructions.
            EmitJumpCode(curContTarg.label, curContTarg.block, contStmt);
        }

        /**
         * @brief output code for a 'do' statement
         */
        private void GenerateStmtDo(TokenStmtDo doStmt)
        {
            if(!mightGetHere)
                return;

            BreakContTarg oldBreakTarg = curBreakTarg;
            BreakContTarg oldContTarg = curContTarg;
            ScriptMyLabel loopLabel = ilGen.DefineLabel("doloop_" + doStmt.Unique);

            curBreakTarg = new BreakContTarg(this, "dobreak_" + doStmt.Unique);
            curContTarg = new BreakContTarg(this, "docont_" + doStmt.Unique);

            ilGen.MarkLabel(loopLabel);
            GenerateStmt(doStmt.bodyStmt);
            if(curContTarg.used)
            {
                ilGen.MarkLabel(curContTarg.label);
                mightGetHere = true;
            }

            if(mightGetHere)
            {
                EmitCallCheckRun(doStmt, false);
                CompValu testRVal = GenerateFromRVal(doStmt.testRVal);
                if(IsConstBoolExprTrue(testRVal))
                {
                     // Unconditional looping, unconditional branch and
                     // say we never fall through to next statement.
                    ilGen.Emit(doStmt, OpCodes.Br, loopLabel);
                    mightGetHere = false;
                }
                else
                {
                     // Conditional looping, test and brach back to top of loop.
                    testRVal.PushVal(this, doStmt.testRVal, tokenTypeBool);
                    ilGen.Emit(doStmt, OpCodes.Brtrue, loopLabel);
                }
            }

             // If 'break' statement was used, output target label.
             // And assume that since a 'break' statement was used, it's possible for the code to get here.
            if(curBreakTarg.used)
            {
                ilGen.MarkLabel(curBreakTarg.label);
                mightGetHere = true;
            }

            curBreakTarg = oldBreakTarg;
            curContTarg = oldContTarg;
        }

        /**
         * @brief output code for a 'for' statement
         */
        private void GenerateStmtFor(TokenStmtFor forStmt)
        {
            if(!mightGetHere)
                return;

            BreakContTarg oldBreakTarg = curBreakTarg;
            BreakContTarg oldContTarg = curContTarg;
            ScriptMyLabel loopLabel = ilGen.DefineLabel("forloop_" + forStmt.Unique);

            curBreakTarg = new BreakContTarg(this, "forbreak_" + forStmt.Unique);
            curContTarg = new BreakContTarg(this, "forcont_" + forStmt.Unique);

            if(forStmt.initStmt != null)
            {
                GenerateStmt(forStmt.initStmt);
            }
            ilGen.MarkLabel(loopLabel);

             // See if we have a test expression that is other than a constant TRUE.
             // If so, test it and conditionally branch to end if false.
            if(forStmt.testRVal != null)
            {
                CompValu testRVal = GenerateFromRVal(forStmt.testRVal);
                if(!IsConstBoolExprTrue(testRVal))
                {
                    testRVal.PushVal(this, forStmt.testRVal, tokenTypeBool);
                    ilGen.Emit(forStmt, OpCodes.Brfalse, curBreakTarg.label);
                    curBreakTarg.used = true;
                }
            }

             // Output loop body.
            GenerateStmt(forStmt.bodyStmt);

             // Here's where a 'continue' statement jumps to.
            if(curContTarg.used)
            {
                ilGen.MarkLabel(curContTarg.label);
                mightGetHere = true;
            }

            if(mightGetHere)
            {
                 // After checking for excessive CPU time, output increment statement, if any.
                EmitCallCheckRun(forStmt, false);
                if(forStmt.incrRVal != null)
                {
                    GenerateFromRVal(forStmt.incrRVal);
                }

                 // Unconditional branch back to beginning of loop.
                ilGen.Emit(forStmt, OpCodes.Br, loopLabel);
            }

             // If test needs label, output label for it to jump to.
             // Otherwise, clear mightGetHere as we know loop never
             // falls out the bottom.
            mightGetHere = curBreakTarg.used;
            if(mightGetHere)
            {
                ilGen.MarkLabel(curBreakTarg.label);
            }

            curBreakTarg = oldBreakTarg;
            curContTarg = oldContTarg;
        }

        private void GenerateStmtForEach(TokenStmtForEach forEachStmt)
        {
            if(!mightGetHere)
                return;

            BreakContTarg oldBreakTarg = curBreakTarg;
            BreakContTarg oldContTarg = curContTarg;
            CompValu keyLVal = null;
            CompValu valLVal = null;
            CompValu arrayRVal = GenerateFromRVal(forEachStmt.arrayRVal);

            if(forEachStmt.keyLVal != null)
            {
                keyLVal = GenerateFromLVal(forEachStmt.keyLVal);
                if(!(keyLVal.type is TokenTypeObject))
                {
                    ErrorMsg(forEachStmt.arrayRVal, "must be object");
                }
            }
            if(forEachStmt.valLVal != null)
            {
                valLVal = GenerateFromLVal(forEachStmt.valLVal);
                if(!(valLVal.type is TokenTypeObject))
                {
                    ErrorMsg(forEachStmt.arrayRVal, "must be object");
                }
            }
            if(!(arrayRVal.type is TokenTypeArray))
            {
                ErrorMsg(forEachStmt.arrayRVal, "must be an array");
            }

            curBreakTarg = new BreakContTarg(this, "foreachbreak_" + forEachStmt.Unique);
            curContTarg = new BreakContTarg(this, "foreachcont_" + forEachStmt.Unique);

            CompValuTemp indexVar = new CompValuTemp(new TokenTypeInt(forEachStmt), this);
            ScriptMyLabel loopLabel = ilGen.DefineLabel("foreachloop_" + forEachStmt.Unique);

            // indexVar = 0
            ilGen.Emit(forEachStmt, OpCodes.Ldc_I4_0);
            indexVar.Pop(this, forEachStmt);

            ilGen.MarkLabel(loopLabel);

            // key = array.__pub_index (indexVar);
            // if (key == null) goto curBreakTarg;
            if(keyLVal != null)
            {
                keyLVal.PopPre(this, forEachStmt.keyLVal);
                arrayRVal.PushVal(this, forEachStmt.arrayRVal);
                indexVar.PushVal(this, forEachStmt);
                ilGen.Emit(forEachStmt, OpCodes.Call, xmrArrPubIndexMethod);
                keyLVal.PopPost(this, forEachStmt.keyLVal);
                keyLVal.PushVal(this, forEachStmt.keyLVal);
                ilGen.Emit(forEachStmt, OpCodes.Brfalse, curBreakTarg.label);
                curBreakTarg.used = true;
            }

            // val = array._pub_value (indexVar);
            // if (val == null) goto curBreakTarg;
            if(valLVal != null)
            {
                valLVal.PopPre(this, forEachStmt.valLVal);
                arrayRVal.PushVal(this, forEachStmt.arrayRVal);
                indexVar.PushVal(this, forEachStmt);
                ilGen.Emit(forEachStmt, OpCodes.Call, xmrArrPubValueMethod);
                valLVal.PopPost(this, forEachStmt.valLVal);
                if(keyLVal == null)
                {
                    valLVal.PushVal(this, forEachStmt.valLVal);
                    ilGen.Emit(forEachStmt, OpCodes.Brfalse, curBreakTarg.label);
                    curBreakTarg.used = true;
                }
            }

            // indexVar ++;
            indexVar.PushVal(this, forEachStmt);
            ilGen.Emit(forEachStmt, OpCodes.Ldc_I4_1);
            ilGen.Emit(forEachStmt, OpCodes.Add);
            indexVar.Pop(this, forEachStmt);

            // body statement
            GenerateStmt(forEachStmt.bodyStmt);

            // continue label
            if(curContTarg.used)
            {
                ilGen.MarkLabel(curContTarg.label);
                mightGetHere = true;
            }

            // call CheckRun()
            if(mightGetHere)
            {
                EmitCallCheckRun(forEachStmt, false);
                ilGen.Emit(forEachStmt, OpCodes.Br, loopLabel);
            }

            // break label
            ilGen.MarkLabel(curBreakTarg.label);
            mightGetHere = true;

            curBreakTarg = oldBreakTarg;
            curContTarg = oldContTarg;
        }

        /**
         * @brief output code for an 'if' statement
         * Braces are necessary because what may be one statement for trueStmt or elseStmt in
         * the script may translate to more than one statement in the resultant C# code.
         */
        private void GenerateStmtIf(TokenStmtIf ifStmt)
        {
            if(!mightGetHere)
                return;

            bool constVal;

             // Test condition and see if constant test expression.
            CompValu testRVal = GenerateFromRVal(ifStmt.testRVal);
            if(IsConstBoolExpr(testRVal, out constVal))
            {
                 // Constant, output just either the true or else part.
                if(constVal)
                {
                    GenerateStmt(ifStmt.trueStmt);
                }
                else if(ifStmt.elseStmt != null)
                {
                    GenerateStmt(ifStmt.elseStmt);
                }
            }
            else if(ifStmt.elseStmt == null)
            {
                 // This is an 'if' statement without an 'else' clause.
                testRVal.PushVal(this, ifStmt.testRVal, tokenTypeBool);
                ScriptMyLabel doneLabel = ilGen.DefineLabel("ifdone_" + ifStmt.Unique);
                ilGen.Emit(ifStmt, OpCodes.Brfalse, doneLabel);  // brfalse doneLabel
                GenerateStmt(ifStmt.trueStmt);                   // generate true body code
                ilGen.MarkLabel(doneLabel);
                mightGetHere = true;                              // there's always a possibility of getting here
            }
            else
            {
                 // This is an 'if' statement with an 'else' clause.
                testRVal.PushVal(this, ifStmt.testRVal, tokenTypeBool);
                ScriptMyLabel elseLabel = ilGen.DefineLabel("ifelse_" + ifStmt.Unique);
                ilGen.Emit(ifStmt, OpCodes.Brfalse, elseLabel);  // brfalse elseLabel
                GenerateStmt(ifStmt.trueStmt);                   // generate true body code
                bool trueMightGetHere = mightGetHere;             // save whether or not true falls through
                ScriptMyLabel doneLabel = ilGen.DefineLabel("ifdone_" + ifStmt.Unique);
                ilGen.Emit(ifStmt, OpCodes.Br, doneLabel);       // branch to done
                ilGen.MarkLabel(elseLabel);                      // beginning of else code
                mightGetHere = true;                              // the top of the else might be executed
                GenerateStmt(ifStmt.elseStmt);                   // output else code
                ilGen.MarkLabel(doneLabel);                      // where end of true clause code branches to
                mightGetHere |= trueMightGetHere;                 // gets this far if either true or else falls through
            }
        }

        /**
         * @brief output code for a 'jump' statement
         */
        private void GenerateStmtJump(TokenStmtJump jumpStmt)
        {
            if(!mightGetHere)
                return;

             // Make sure the target label is defined somewhere in the function.
            TokenStmtLabel stmtLabel;
            if(!curDeclFunc.labels.TryGetValue(jumpStmt.label.val, out stmtLabel))
            {
                ErrorMsg(jumpStmt, "undefined label " + jumpStmt.label.val);
                return;
            }
            if(!stmtLabel.labelTagged)
            {
                stmtLabel.labelStruct = ilGen.DefineLabel("jump_" + stmtLabel.name.val);
                stmtLabel.labelTagged = true;
            }

             // Emit instructions to do the jump.
            EmitJumpCode(stmtLabel.labelStruct, stmtLabel.block, jumpStmt);
        }

        /**
         * @brief Emit code to jump to a label
         * @param target = label being jumped to
         * @param targetsBlock = { ... } the label is defined in
         */
        private void EmitJumpCode(ScriptMyLabel target, TokenStmtBlock targetsBlock, Token errorAt)
        {
             // Jumps never fall through.

            mightGetHere = false;

             // Find which block the target label is in.  Must be in this or an outer block,
             // no laterals allowed.  And if we exit a try/catch block, use Leave instead of Br.
             //
             //    jump lateral;
             //    {
             //        @lateral;
             //    }
            bool useLeave = false;
            TokenStmtBlock stmtBlock;
            Stack<TokenStmtTry> finallyBlocksCalled = new Stack<TokenStmtTry>();
            for(stmtBlock = curStmtBlock; stmtBlock != targetsBlock; stmtBlock = stmtBlock.outerStmtBlock)
            {
                if(stmtBlock == null)
                {
                    ErrorMsg(errorAt, "no lateral jumps allowed");
                    return;
                }
                if(stmtBlock.isFinally)
                {
                    ErrorMsg(errorAt, "cannot jump out of finally");
                    return;
                }
                if(stmtBlock.isTry || stmtBlock.isCatch)
                    useLeave = true;
                if((stmtBlock.tryStmt != null) && (stmtBlock.tryStmt.finallyStmt != null))
                {
                    finallyBlocksCalled.Push(stmtBlock.tryStmt);
                }
            }

             // If popping through more than one finally block, we have to break it down for the stack 
             // capture and restore code, one finally block at a time.
             //
             //     try {
             //         try {
             //             try {
             //                 jump exit;
             //             } finally {
             //                 llOwnerSay ("exiting inner");
             //             }
             //         } finally {
             //             llOwnerSay ("exiting middle");
             //         }
             //     } finally {
             //         llOwnerSay ("exiting outer");
             //     }
             //   @exit;
             //
             //     try {
             //         try {
             //             try {
             //                 jump intr2_exit;         <<< gets its own tryNo call label so inner try knows where to restore to
             //             } finally {
             //                 llOwnerSay ("exiting inner");
             //             }
             //             jump outtry2;
             //           @intr2_exit; jump intr1_exit;  <<< gets its own tryNo call label so middle try knows where to restore to
             //           @outtry2;
             //         } finally {
             //             llOwnerSay ("exiting middle");
             //         }
             //         jump outtry1;
             //       @intr1_exit: jump exit;            <<< gets its own tryNo call label so outer try knows where to restore to
             //       @outtry1;
             //     } finally {
             //         llOwnerSay ("exiting outer");
             //     }
             //   @exit;
            int level = 0;
            while(finallyBlocksCalled.Count > 1)
            {
                TokenStmtTry finallyBlock = finallyBlocksCalled.Pop();
                string intername = "intr" + (++level) + "_" + target.name;
                IntermediateLeave iLeave;
                if(!finallyBlock.iLeaves.TryGetValue(intername, out iLeave))
                {
                    iLeave = new IntermediateLeave();
                    iLeave.jumpIntoLabel = ilGen.DefineLabel(intername);
                    iLeave.jumpAwayLabel = target;
                    finallyBlock.iLeaves.Add(intername, iLeave);
                }
                target = iLeave.jumpIntoLabel;
            }

             // Finally output the branch/leave opcode.
             // If using Leave, prefix with a call label in case the corresponding finally block
             // calls CheckRun() and that CheckRun() captures the stack, it will have a point to 
             // restore to that will properly jump back into the finally block.
            if(useLeave)
            {
                new CallLabel(this, errorAt);
                ilGen.Emit(errorAt, OpCodes.Leave, target);
                openCallLabel = null;
            }
            else
            {
                ilGen.Emit(errorAt, OpCodes.Br, target);
            }
        }

        /**
         * @brief output code for a jump target label statement.
         * If there are any backward jumps to the label, do a CheckRun() also.
         */
        private void GenerateStmtLabel(TokenStmtLabel labelStmt)
        {
            if(!labelStmt.labelTagged)
            {
                labelStmt.labelStruct = ilGen.DefineLabel("jump_" + labelStmt.name.val);
                labelStmt.labelTagged = true;
            }
            ilGen.MarkLabel(labelStmt.labelStruct);
            if(labelStmt.hasBkwdRefs)
            {
                EmitCallCheckRun(labelStmt, false);
            }

             // We are going to say that the label falls through.
             // It would be nice if we could analyze all referencing
             // goto's to see if all of them are not used but we are
             // going to assume that if the script writer put a label
             // somewhere, it is probably going to be used.
            mightGetHere = true;
        }

        /**
         * @brief Generate code for a script-defined type's <typename>.$new(<argsig>) method.
         *        It is used to malloc the object and initialize it.
         *        It is defined as a script-defined type static method, so the object level
         *        method gets the XMRInstance pointer passed as arg 0, and the method is 
         *        supposed to return the allocated and constructed XMRSDTypeClObj
         *        object pointer.
         */
        private void GenerateStmtNewobj(TokenStmtNewobj newobjStmt)
        {
             // First off, malloc a new empty XMRSDTypeClObj object
             // then call the XMRSDTypeClObj()-level constructor.
             // Store the result in local var $objptr.
            newobjStmt.objptrVar.location.PopPre(this, newobjStmt);
            ilGen.Emit(newobjStmt, OpCodes.Ldarg_0);
            ilGen.Emit(newobjStmt, OpCodes.Ldc_I4, curDeclFunc.sdtClass.sdTypeIndex);
            ilGen.Emit(newobjStmt, OpCodes.Newobj, sdtClassConstructorInfo);
            newobjStmt.objptrVar.location.PopPost(this, newobjStmt);

             // Now call the script-level constructor.
             // Pass the object pointer in $objptr as it's 'this' argument.
             // The rest of the args are the script-visible args and are just copied from $new() call.
            GenerateFromRValCall(newobjStmt.rValCall);

             // Put object pointer in retval so it gets returned to caller.
            newobjStmt.objptrVar.location.PushVal(this, newobjStmt);
            ilGen.Emit(newobjStmt, OpCodes.Stloc, retValue);

             // Exit the function like a return statement.
             // And thus we don't fall through.
            ilGen.Emit(newobjStmt, OpCodes.Leave, retLabel);
            mightGetHere = false;
        }

        /**
         * @brief output code for a return statement.
         * @param retStmt = return statement token, including return value if any
         */
        private void GenerateStmtRet(TokenStmtRet retStmt)
        {
            if(!mightGetHere)
                return;

            for(TokenStmtBlock stmtBlock = curStmtBlock; stmtBlock != null; stmtBlock = stmtBlock.outerStmtBlock)
            {
                if(stmtBlock.isFinally)
                {
                    ErrorMsg(retStmt, "cannot return out of finally");
                    return;
                }
            }

            if(curDeclFunc.retType is TokenTypeVoid)
            {
                if(retStmt.rVal != null)
                {
                    ErrorMsg(retStmt, "function returns void, no value allowed");
                    return;
                }
            }
            else
            {
                if(retStmt.rVal == null)
                {
                    ErrorMsg(retStmt, "function requires return value type " + curDeclFunc.retType.ToString());
                    return;
                }
                CompValu rVal = GenerateFromRVal(retStmt.rVal);
                rVal.PushVal(this, retStmt.rVal, curDeclFunc.retType);
                ilGen.Emit(retStmt, OpCodes.Stloc, retValue);
            }

             // Use a OpCodes.Leave instruction to break out of any try { } blocks.
             // All Leave's inside script-defined try { } need call labels (see GenerateStmtTry()).
            bool brokeOutOfTry = false;
            for(TokenStmtBlock stmtBlock = curStmtBlock; stmtBlock != null; stmtBlock = stmtBlock.outerStmtBlock)
            {
                if(stmtBlock.isTry)
                {
                    brokeOutOfTry = true;
                    break;
                }
            }
            if(brokeOutOfTry)
                new CallLabel(this, retStmt);
            ilGen.Emit(retStmt, OpCodes.Leave, retLabel);
            if(brokeOutOfTry)
                openCallLabel = null;

             // 'return' statements never fall through.
            mightGetHere = false;
        }

        /**
         * @brief the statement is just an expression, most likely an assignment or a ++ or -- thing.
         */
        private void GenerateStmtRVal(TokenStmtRVal rValStmt)
        {
            if(!mightGetHere)
                return;

            GenerateFromRVal(rValStmt.rVal);
        }

        /**
         * @brief generate code for a 'state' statement that transitions state.
         * It sets the new state by throwing a ScriptChangeStateException.
         */
        private void GenerateStmtState(TokenStmtState stateStmt)
        {
            if(!mightGetHere)
                return;

            int index = 0;  // 'default' state

             // Set new state value by throwing an exception.
             // These exceptions aren't catchable by script-level try { } catch { }.
            if((stateStmt.state != null) && !stateIndices.TryGetValue(stateStmt.state.val, out index))
            {
                // The moron XEngine compiles scripts that reference undefined states.
                // So rather than produce a compile-time error, we'll throw an exception at runtime.
                // ErrorMsg (stateStmt, "undefined state " + stateStmt.state.val);

                // throw new UndefinedStateException (stateStmt.state.val);
                ilGen.Emit(stateStmt, OpCodes.Ldstr, stateStmt.state.val);
                ilGen.Emit(stateStmt, OpCodes.Newobj, scriptUndefinedStateExceptionConstructorInfo);
            }
            else
            {
                ilGen.Emit(stateStmt, OpCodes.Ldc_I4, index);  // new state's index
                ilGen.Emit(stateStmt, OpCodes.Newobj, scriptChangeStateExceptionConstructorInfo);
            }
            ilGen.Emit(stateStmt, OpCodes.Throw);

             // 'state' statements never fall through.
            mightGetHere = false;
        }

        /**
         * @brief output code for a 'switch' statement
         */
        private void GenerateStmtSwitch(TokenStmtSwitch switchStmt)
        {
            if(!mightGetHere)
                return;

             // Output code to calculate index.
            CompValu testRVal = GenerateFromRVal(switchStmt.testRVal);

             // Generate code based on string or integer index.
            if((testRVal.type is TokenTypeKey) || (testRVal.type is TokenTypeStr))
                GenerateStmtSwitchStr(testRVal, switchStmt);
            else
                GenerateStmtSwitchInt(testRVal, switchStmt);
        }

        private void GenerateStmtSwitchInt(CompValu testRVal, TokenStmtSwitch switchStmt)
        {
            testRVal.PushVal(this, switchStmt.testRVal, tokenTypeInt);

            BreakContTarg oldBreakTarg = curBreakTarg;
            ScriptMyLabel defaultLabel = null;
            TokenSwitchCase sortedCases = null;
            TokenSwitchCase defaultCase = null;

            curBreakTarg = new BreakContTarg(this, "switchbreak_" + switchStmt.Unique);

             // Build list of cases sorted by ascending values.
             // There should not be any overlapping of values.
            for(TokenSwitchCase thisCase = switchStmt.cases; thisCase != null; thisCase = thisCase.nextCase)
            {
                thisCase.label = ilGen.DefineLabel("case_" + thisCase.Unique);

                 // The default case if any, goes in its own separate slot.
                if(thisCase.rVal1 == null)
                {
                    if(defaultCase != null)
                    {
                        ErrorMsg(thisCase, "only one default case allowed");
                        ErrorMsg(defaultCase, "...prior default case");
                        return;
                    }
                    defaultCase = thisCase;
                    defaultLabel = thisCase.label;
                    continue;
                }

                 // Evaluate case operands, they must be compile-time integer constants.
                CompValu rVal = GenerateFromRVal(thisCase.rVal1);
                if(!IsConstIntExpr(rVal, out thisCase.val1))
                {
                    ErrorMsg(thisCase.rVal1, "must be compile-time char or integer constant");
                    return;
                }
                thisCase.val2 = thisCase.val1;
                if(thisCase.rVal2 != null)
                {
                    rVal = GenerateFromRVal(thisCase.rVal2);
                    if(!IsConstIntExpr(rVal, out thisCase.val2))
                    {
                        ErrorMsg(thisCase.rVal2, "must be compile-time char or integer constant");
                        return;
                    }
                }
                if(thisCase.val2 < thisCase.val1)
                {
                    ErrorMsg(thisCase.rVal2, "must be .ge. first value for the case");
                    return;
                }

                 // Insert into list, sorted by value.
                 // Note that both limits are inclusive.
                TokenSwitchCase lastCase = null;
                TokenSwitchCase nextCase;
                for(nextCase = sortedCases; nextCase != null; nextCase = nextCase.nextSortedCase)
                {
                    if(nextCase.val1 > thisCase.val2)
                        break;
                    if(nextCase.val2 >= thisCase.val1)
                    {
                        ErrorMsg(thisCase, "value used by previous case");
                        ErrorMsg(nextCase, "...previous case");
                        return;
                    }
                    lastCase = nextCase;
                }
                thisCase.nextSortedCase = nextCase;
                if(lastCase == null)
                {
                    sortedCases = thisCase;
                }
                else
                {
                    lastCase.nextSortedCase = thisCase;
                }
            }

            if(defaultLabel == null)
            {
                defaultLabel = ilGen.DefineLabel("default_" + switchStmt.Unique);
            }

             // Output code to jump to the case statement's labels based on integer index on stack.
             // Note that each case still has the integer index on stack when jumped to.
            int offset = 0;
            for(TokenSwitchCase thisCase = sortedCases; thisCase != null;)
            {
                 // Scan through list of cases to find the maximum number of cases who's numvalues-to-case ratio
                 // is from 0.5 to 2.0.  If such a group is found, use a CIL switch for them.  If not, just use a
                 // compare-and-branch for the current case.
                int numCases = 0;
                int numFound = 0;
                int lowValue = thisCase.val1;
                int numValues = 0;
                for(TokenSwitchCase scanCase = thisCase; scanCase != null; scanCase = scanCase.nextSortedCase)
                {
                    int nVals = scanCase.val2 - thisCase.val1 + 1;
                    double ratio = (double)nVals / (double)(++numCases);
                    if((ratio >= 0.5) && (ratio <= 2.0))
                    {
                        numFound = numCases;
                        numValues = nVals;
                    }
                }
                if(numFound > 1)
                {
                     // There is a group of case's, starting with thisCase, that fall within our criteria, ie, 
                     // that have a nice density of meaningful jumps.
                     //
                     // So first generate an array of jumps to the default label (explicit or implicit).
                    ScriptMyLabel[] labels = new ScriptMyLabel[numValues];
                    for(int i = 0; i < numValues; i++)
                    {
                        labels[i] = defaultLabel;
                    }

                     // Next, for each case in that group, fill in the corresponding array entries to jump to
                     // that case's label.
                    do
                    {
                        for(int i = thisCase.val1; i <= thisCase.val2; i++)
                        {
                            labels[i - lowValue] = thisCase.label;
                        }
                        thisCase = thisCase.nextSortedCase;
                    } while(--numFound > 0);

                     // Subtract the low value and do the computed jump.
                     // The OpCodes.Switch falls through if out of range (unsigned compare).
                    if(offset != lowValue)
                    {
                        ilGen.Emit(switchStmt, OpCodes.Ldc_I4, lowValue - offset);
                        ilGen.Emit(switchStmt, OpCodes.Sub);
                        offset = lowValue;
                    }
                    ilGen.Emit(switchStmt, OpCodes.Dup);
                    ilGen.Emit(switchStmt, OpCodes.Switch, labels);
                }
                else
                {
                     // It's not economical to do with a computed jump, so output a subtract/compare/branch
                     // for thisCase.
                    if(lowValue == thisCase.val2)
                    {
                        ilGen.Emit(switchStmt, OpCodes.Dup);
                        ilGen.Emit(switchStmt, OpCodes.Ldc_I4, lowValue - offset);
                        ilGen.Emit(switchStmt, OpCodes.Beq, thisCase.label);
                    }
                    else
                    {
                        if(offset != lowValue)
                        {
                            ilGen.Emit(switchStmt, OpCodes.Ldc_I4, lowValue - offset);
                            ilGen.Emit(switchStmt, OpCodes.Sub);
                            offset = lowValue;
                        }
                        ilGen.Emit(switchStmt, OpCodes.Dup);
                        ilGen.Emit(switchStmt, OpCodes.Ldc_I4, thisCase.val2 - offset);
                        ilGen.Emit(switchStmt, OpCodes.Ble_Un, thisCase.label);
                    }
                    thisCase = thisCase.nextSortedCase;
                }
            }
            ilGen.Emit(switchStmt, OpCodes.Br, defaultLabel);

             // Output code for the cases themselves, in the order given by the programmer, 
             // so they fall through as programmer wants.  This includes the default case, if any.
             //
             // Each label is jumped to with the index still on the stack.  So pop it off in case
             // the case body does a goto outside the switch or a return.  If the case body might
             // fall through to the next case or the bottom of the switch, push a zero so the stack
             // matches in all cases.
            for(TokenSwitchCase thisCase = switchStmt.cases; thisCase != null; thisCase = thisCase.nextCase)
            {
                ilGen.MarkLabel(thisCase.label);   // the branch comes here
                ilGen.Emit(thisCase, OpCodes.Pop); // pop the integer index off stack
                mightGetHere = true;            // it's possible to get here
                for(TokenStmt stmt = thisCase.stmts; stmt != null; stmt = (TokenStmt)(stmt.nextToken))
                {
                    GenerateStmt(stmt);        // output the case/explicit default body
                }
                if(mightGetHere)
                {
                    ilGen.Emit(thisCase, OpCodes.Ldc_I4_0);
                    // in case we fall through, push a dummy integer index
                }
            }

             // If no explicit default case, output the default label here.
            if(defaultCase == null)
            {
                ilGen.MarkLabel(defaultLabel);
                mightGetHere = true;
            }

             // If the last case of the switch falls through out the bottom,
             // we have to pop the index still on the stack.
            if(mightGetHere)
            {
                ilGen.Emit(switchStmt, OpCodes.Pop);
            }

             // Output the 'break' statement target label.
             // Note that the integer index is not on the stack at this point.
            if(curBreakTarg.used)
            {
                ilGen.MarkLabel(curBreakTarg.label);
                mightGetHere = true;
            }

            curBreakTarg = oldBreakTarg;
        }

        private void GenerateStmtSwitchStr(CompValu testRVal, TokenStmtSwitch switchStmt)
        {
            BreakContTarg oldBreakTarg = curBreakTarg;
            ScriptMyLabel defaultLabel = null;
            TokenSwitchCase caseTreeTop = null;
            TokenSwitchCase defaultCase = null;

            curBreakTarg = new BreakContTarg(this, "switchbreak_" + switchStmt.Unique);

             // Make sure value is in a temp so we don't compute it more than once.
            if(!(testRVal is CompValuTemp))
            {
                CompValuTemp temp = new CompValuTemp(testRVal.type, this);
                testRVal.PushVal(this, switchStmt);
                temp.Pop(this, switchStmt);
                testRVal = temp;
            }

             // Build tree of cases.
             // There should not be any overlapping of values.
            for(TokenSwitchCase thisCase = switchStmt.cases; thisCase != null; thisCase = thisCase.nextCase)
            {
                thisCase.label = ilGen.DefineLabel("case");

                 // The default case if any, goes in its own separate slot.
                if(thisCase.rVal1 == null)
                {
                    if(defaultCase != null)
                    {
                        ErrorMsg(thisCase, "only one default case allowed");
                        ErrorMsg(defaultCase, "...prior default case");
                        return;
                    }
                    defaultCase = thisCase;
                    defaultLabel = thisCase.label;
                    continue;
                }

                 // Evaluate case operands, they must be compile-time string constants.
                CompValu rVal = GenerateFromRVal(thisCase.rVal1);
                if(!IsConstStrExpr(rVal, out thisCase.str1))
                {
                    ErrorMsg(thisCase.rVal1, "must be compile-time string constant");
                    continue;
                }
                thisCase.str2 = thisCase.str1;
                if(thisCase.rVal2 != null)
                {
                    rVal = GenerateFromRVal(thisCase.rVal2);
                    if(!IsConstStrExpr(rVal, out thisCase.str2))
                    {
                        ErrorMsg(thisCase.rVal2, "must be compile-time string constant");
                        continue;
                    }
                }
                if(String.Compare(thisCase.str2, thisCase.str1, StringComparison.Ordinal) < 0)
                {
                    ErrorMsg(thisCase.rVal2, "must be .ge. first value for the case");
                    continue;
                }

                 // Insert into list, sorted by value.
                 // Note that both limits are inclusive.
                caseTreeTop = InsertCaseInTree(caseTreeTop, thisCase);
            }

             // Balance tree so we end up generating code that does O(log2 n) comparisons.
            caseTreeTop = BalanceTree(caseTreeTop);

             // Output compare and branch instructions in a tree-like fashion so we do O(log2 n) comparisons.
            if(defaultLabel == null)
            {
                defaultLabel = ilGen.DefineLabel("default");
            }
            OutputStrCase(testRVal, caseTreeTop, defaultLabel);

             // Output code for the cases themselves, in the order given by the programmer, 
             // so they fall through as programmer wants.  This includes the default case, if any.
            for(TokenSwitchCase thisCase = switchStmt.cases; thisCase != null; thisCase = thisCase.nextCase)
            {
                ilGen.MarkLabel(thisCase.label);   // the branch comes here
                mightGetHere = true;            // it's possible to get here
                for(TokenStmt stmt = thisCase.stmts; stmt != null; stmt = (TokenStmt)(stmt.nextToken))
                {
                    GenerateStmt(stmt);        // output the case/explicit default body
                }
            }

             // If no explicit default case, output the default label here.
            if(defaultCase == null)
            {
                ilGen.MarkLabel(defaultLabel);
                mightGetHere = true;
            }

             // Output the 'break' statement target label.
            if(curBreakTarg.used)
            {
                ilGen.MarkLabel(curBreakTarg.label);
                mightGetHere = true;
            }

            curBreakTarg = oldBreakTarg;
        }

        /**
         * @brief Insert a case in a tree of cases
         * @param r = root of existing cases to insert into
         * @param n = new case being inserted
         * @returns new root with new case inserted
         */
        private TokenSwitchCase InsertCaseInTree(TokenSwitchCase r, TokenSwitchCase n)
        {
            if(r == null)
                return n;

            TokenSwitchCase t = r;
            while(true)
            {
                if(String.Compare(n.str2, t.str1, StringComparison.Ordinal) < 0)
                {
                    if(t.lowerCase == null)
                    {
                        t.lowerCase = n;
                        break;
                    }
                    t = t.lowerCase;
                    continue;
                }
                if(String.Compare(n.str1, t.str2, StringComparison.Ordinal) > 0)
                {
                    if(t.higherCase == null)
                    {
                        t.higherCase = n;
                        break;
                    }
                    t = t.higherCase;
                    continue;
                }
                ErrorMsg(n, "duplicate case");
                ErrorMsg(r, "...duplicate of");
                break;
            }
            return r;
        }

        /**
         * @brief Balance a tree so left & right halves contain same number within +-1
         * @param r = root of tree to balance
         * @returns new root
         */
        private static TokenSwitchCase BalanceTree(TokenSwitchCase r)
        {
            if(r == null)
                return r;

            int lc = CountTree(r.lowerCase);
            int hc = CountTree(r.higherCase);
            TokenSwitchCase n, x;

             // If lower side is heavy, move highest nodes from lower side to 
             // higher side until balanced.
            while(lc > hc + 1)
            {
                x = ExtractHighest(r.lowerCase, out n);
                n.lowerCase = x;
                n.higherCase = r;
                r.lowerCase = null;
                r = n;
                lc--;
                hc++;
            }

             // If higher side is heavy, move lowest nodes from higher side to 
             // lower side until balanced.
            while(hc > lc + 1)
            {
                x = ExtractLowest(r.higherCase, out n);
                n.higherCase = x;
                n.lowerCase = r;
                r.higherCase = null;
                r = n;
                lc++;
                hc--;
            }

             // Now balance each side because they can be lopsided individually.
            r.lowerCase = BalanceTree(r.lowerCase);
            r.higherCase = BalanceTree(r.higherCase);
            return r;
        }

        /**
         * @brief Get number of nodes in a tree
         * @param n = root of tree to count
         * @returns number of nodes including root
         */
        private static int CountTree(TokenSwitchCase n)
        {
            if(n == null)
                return 0;
            return 1 + CountTree(n.lowerCase) + CountTree(n.higherCase);
        }

        // Extract highest node from a tree
        // @param r = root of tree to extract highest from
        // @returns new root after node has been extracted
        //          n = node that was extracted from tree
        private static TokenSwitchCase ExtractHighest(TokenSwitchCase r, out TokenSwitchCase n)
        {
            if(r.higherCase == null)
            {
                n = r;
                return r.lowerCase;
            }
            r.higherCase = ExtractHighest(r.higherCase, out n);
            return r;
        }

        // Extract lowest node from a tree
        // @param r = root of tree to extract lowest from
        // @returns new root after node has been extracted
        //          n = node that was extracted from tree
        private static TokenSwitchCase ExtractLowest(TokenSwitchCase r, out TokenSwitchCase n)
        {
            if(r.lowerCase == null)
            {
                n = r;
                return r.higherCase;
            }
            r.lowerCase = ExtractLowest(r.lowerCase, out n);
            return r;
        }

        /**
         * Output code for string-style case of a switch/case to jump to the script code associated with the case.
         * @param testRVal = value being switched on
         * @param thisCase = case that the code is being output for
         * @param defaultLabel = where the default clause is (or past all cases if none)
         * Note:
         *   Outputs code for this case and the lowerCase and higherCases if any.
         *   If no lowerCase or higherCase, outputs a br to defaultLabel so this code never falls through.
         */
        private void OutputStrCase(CompValu testRVal, TokenSwitchCase thisCase, ScriptMyLabel defaultLabel)
        {
             // If nothing lower on tree and there is a single case value, 
             // just do one compare for equality.
            if((thisCase.lowerCase == null) && (thisCase.higherCase == null) && (thisCase.str1 == thisCase.str2))
            {
                testRVal.PushVal(this, thisCase, tokenTypeStr);
                ilGen.Emit(thisCase, OpCodes.Ldstr, thisCase.str1);
                ilGen.Emit(thisCase, OpCodes.Ldc_I4, (int)StringComparison.Ordinal);
                ilGen.Emit(thisCase, OpCodes.Call, stringCompareMethodInfo);
                ilGen.Emit(thisCase, OpCodes.Brfalse, thisCase.label);
                ilGen.Emit(thisCase, OpCodes.Br, defaultLabel);
                return;
            }

             // Determine where to jump if switch value is lower than lower case value.
            ScriptMyLabel lowerLabel = defaultLabel;
            if(thisCase.lowerCase != null)
            {
                lowerLabel = ilGen.DefineLabel("lower");
            }

             // If single case value, put comparison result in this temp.
            CompValuTemp cmpv1 = null;
            if(thisCase.str1 == thisCase.str2)
            {
                cmpv1 = new CompValuTemp(tokenTypeInt, this);
            }

             // If switch value .lt. lower case value, jump to lower label.
             // Maybe save comparison result in a temp.
            testRVal.PushVal(this, thisCase, tokenTypeStr);
            ilGen.Emit(thisCase, OpCodes.Ldstr, thisCase.str1);
            ilGen.Emit(thisCase, OpCodes.Ldc_I4, (int)StringComparison.Ordinal);
            ilGen.Emit(thisCase, OpCodes.Call, stringCompareMethodInfo);
            if(cmpv1 != null)
            {
                ilGen.Emit(thisCase, OpCodes.Dup);
                cmpv1.Pop(this, thisCase);
            }
            ilGen.Emit(thisCase, OpCodes.Ldc_I4_0);
            ilGen.Emit(thisCase, OpCodes.Blt, lowerLabel);

             // If switch value .le. higher case value, jump to case code.
             // Maybe get comparison from the temp.
            if(cmpv1 == null)
            {
                testRVal.PushVal(this, thisCase, tokenTypeStr);
                ilGen.Emit(thisCase, OpCodes.Ldstr, thisCase.str2);
                ilGen.Emit(thisCase, OpCodes.Ldc_I4, (int)StringComparison.Ordinal);
                ilGen.Emit(thisCase, OpCodes.Call, stringCompareMethodInfo);
            }
            else
            {
                cmpv1.PushVal(this, thisCase);
            }
            ilGen.Emit(thisCase, OpCodes.Ldc_I4_0);
            ilGen.Emit(thisCase, OpCodes.Ble, thisCase.label);

             // Output code for higher comparison if any.
            if(thisCase.higherCase == null)
            {
                ilGen.Emit(thisCase, OpCodes.Br, defaultLabel);
            }
            else
            {
                OutputStrCase(testRVal, thisCase.higherCase, defaultLabel);
            }

             // Output code for lower comparison if any.
            if(thisCase.lowerCase != null)
            {
                ilGen.MarkLabel(lowerLabel);
                OutputStrCase(testRVal, thisCase.lowerCase, defaultLabel);
            }
        }

        /**
         * @brief output code for a throw statement.
         * @param throwStmt = throw statement token, including value to be thrown
         */
        private void GenerateStmtThrow(TokenStmtThrow throwStmt)
        {
            if(!mightGetHere)
                return;

             // 'throw' statements never fall through.
            mightGetHere = false;

             // Output code for either a throw or a rethrow.
            if(throwStmt.rVal == null)
            {
                for(TokenStmtBlock blk = curStmtBlock; blk != null; blk = blk.outerStmtBlock)
                {
                    if(curStmtBlock.isCatch)
                    {
                        ilGen.Emit(throwStmt, OpCodes.Rethrow);
                        return;
                    }
                }
                ErrorMsg(throwStmt, "rethrow allowed only in catch clause");
            }
            else
            {
                CompValu rVal = GenerateFromRVal(throwStmt.rVal);
                rVal.PushVal(this, throwStmt.rVal, tokenTypeObj);
                ilGen.Emit(throwStmt, OpCodes.Call, thrownExceptionWrapMethodInfo);
                ilGen.Emit(throwStmt, OpCodes.Throw);
            }
        }

        /**
         * @brief output code for a try/catch/finally block
         */
        private void GenerateStmtTry(TokenStmtTry tryStmt)
        {
            if(!mightGetHere)
                return;

            /*
             * Reducer should make sure we have exactly one of catch or finally.
             */
            if((tryStmt.catchStmt == null) && (tryStmt.finallyStmt == null))
            {
                throw new Exception("must have a catch or a finally on try");
            }
            if((tryStmt.catchStmt != null) && (tryStmt.finallyStmt != null))
            {
                throw new Exception("can't have both catch and finally on same try");
            }

             // Stack the call labels.
             // Try blocks have their own series of call labels.
            ScriptMyLocal saveCallNo = actCallNo;
            LinkedList<CallLabel> saveCallLabels = actCallLabels;

             // Generate code for either try { } catch { } or try { } finally { }.
            if(tryStmt.catchStmt != null)
                GenerateStmtTryCatch(tryStmt);
            if(tryStmt.finallyStmt != null)
                GenerateStmtTryFinally(tryStmt);

             // Restore call labels.
            actCallNo = saveCallNo;
            actCallLabels = saveCallLabels;
        }


        /**
         * @brief output code for a try/catch block
         *
         *      int    __tryCallNo = -1;                                   // call number within try { } subblock
         *      int    __catCallNo = -1;                                   // call number within catch { } subblock
         *      Exception __catThrown = null;                              // caught exception
         *    <oldCallLabel>:                                              // the outside world jumps here to restore us no matter ...
         *      try {                                                      // ... where we actually were inside of try/catch
         *          if (__tryCallNo >= 0) goto tryCallSw;                  // maybe go do restore
         *          <try body using __tryCallNo>                           // execute script-defined code
         *                                                                 // ...stack capture WILL run catch { } subblock
         *          leave tryEnd;                                          // exits
         *        tryThrow:<tryCallLabel>:
         *          throw new ScriptRestoreCatchException(__catThrown);    // catch { } was running, jump to its beginning
         *        tryCallSw:                                               // restoring...
         *          switch (__tryCallNo) back up into <try body>           // not catching, jump back inside try
         *      } catch (Exception exc) {
         *          exc = ScriptRestoreCatchException.Unwrap(exc);         // unwrap possible ScriptRestoreCatchException
         *          if (exc == null) goto catchRetro;                      // rethrow if IXMRUncatchable (eg, StackCaptureException)
         *          __catThrown = exc;                                     // save what was thrown so restoring try { } will throw it again
         *          catchVar = exc;                                        // set up script-visible variable
         *          __tryCallNo = tryThrow:<tryCallLabel>
         *          if (__catCallNo >= 0) goto catchCallSw;                // if restoring, go check below
         *          <catch body using __catCallNo>                         // normal, execute script-defined code
         *          leave tryEnd;                                          // all done, exit catch { }
         *        catchRetro:
         *          rethrow;
         *        catchCallSw:
         *          switch (__catCallNo) back up into <catch body>         // restart catch { } code wherever it was
         *      }
         *    tryEnd:
         */
        private void GenerateStmtTryCatch(TokenStmtTry tryStmt)
        {
            CompValuTemp tryCallNo = new CompValuTemp(tokenTypeInt, this);
            CompValuTemp catCallNo = new CompValuTemp(tokenTypeInt, this);
            CompValuTemp catThrown = new CompValuTemp(tokenTypeExc, this);

            ScriptMyLabel tryCallSw = ilGen.DefineLabel("__tryCallSw_" + tryStmt.Unique);
            ScriptMyLabel catchRetro = ilGen.DefineLabel("__catchRetro_" + tryStmt.Unique);
            ScriptMyLabel catchCallSw = ilGen.DefineLabel("__catchCallSw_" + tryStmt.Unique);
            ScriptMyLabel tryEnd = ilGen.DefineLabel("__tryEnd_" + tryStmt.Unique);

            SetCallNo(tryStmt, tryCallNo, -1);
            SetCallNo(tryStmt, catCallNo, -1);
            ilGen.Emit(tryStmt, OpCodes.Ldnull);
            catThrown.Pop(this, tryStmt);

            new CallLabel(this, tryStmt);              //   <oldcalllabel>:
            ilGen.BeginExceptionBlock();               //     try {
            openCallLabel = null;

            GetCallNo(tryStmt, tryCallNo);             //         if (__tryCallNo >= 0) goto tryCallSw;
            ilGen.Emit(tryStmt, OpCodes.Ldc_I4_0);
            ilGen.Emit(tryStmt, OpCodes.Bge, tryCallSw);

            actCallNo = tryCallNo.localBuilder;                     // set up __tryCallNo for call labels
            actCallLabels = new LinkedList<CallLabel>();

            GenerateStmtBlock(tryStmt.tryStmt);            // output the try block statement subblock

            bool tryBlockFallsOutBottom = mightGetHere;
            if(tryBlockFallsOutBottom)
            {
                new CallLabel(this, tryStmt);          //       <tryCallLabel>:
                ilGen.Emit(tryStmt, OpCodes.Leave, tryEnd);    //         leave tryEnd;
                openCallLabel = null;
            }

            CallLabel tryThrow = new CallLabel(this, tryStmt); //       tryThrow:<tryCallLabel>:
            catThrown.PushVal(this, tryStmt);          //         throw new ScriptRestoreCatchException (__catThrown);
            ilGen.Emit(tryStmt, OpCodes.Newobj, scriptRestoreCatchExceptionConstructorInfo);
            ilGen.Emit(tryStmt, OpCodes.Throw);
            openCallLabel = null;

            ilGen.MarkLabel(tryCallSw);                //       tryCallSw:
            OutputCallNoSwitchStmt();              //         switch (tryCallNo) ...

            CompValuLocalVar catchVarLocExc = null;
            CompValuTemp catchVarLocStr = null;

            if(tryStmt.catchVar.type.ToSysType() == typeof(Exception))
            {
                catchVarLocExc = new CompValuLocalVar(tryStmt.catchVar.type, tryStmt.catchVar.name.val, this);
            }
            else if(tryStmt.catchVar.type.ToSysType() == typeof(String))
            {
                catchVarLocStr = new CompValuTemp(tryStmt.catchVar.type, this);
            }

            ScriptMyLocal excLocal = ilGen.DeclareLocal(typeof(String), "catchstr_" + tryStmt.Unique);

            ilGen.BeginCatchBlock(typeof(Exception));     // start of the catch block that can catch any exception
            ilGen.Emit(tryStmt.catchStmt, OpCodes.Call, scriptRestoreCatchExceptionUnwrap);
            // exc = ScriptRestoreCatchException.Unwrap (exc);
            ilGen.Emit(tryStmt.catchStmt, OpCodes.Dup);        // rethrow if IXMRUncatchable (eg, StackCaptureException)
            ilGen.Emit(tryStmt.catchStmt, OpCodes.Brfalse, catchRetro);
            if(tryStmt.catchVar.type.ToSysType() == typeof(Exception))
            {
                tryStmt.catchVar.location = catchVarLocExc;
                ilGen.Emit(tryStmt.catchStmt, OpCodes.Dup);
                catThrown.Pop(this, tryStmt);              // store exception object in catThrown
                catchVarLocExc.Pop(this, tryStmt.catchVar.name);      // also store in script-visible variable
            }
            else if(tryStmt.catchVar.type.ToSysType() == typeof(String))
            {
                tryStmt.catchVar.location = catchVarLocStr;
                ilGen.Emit(tryStmt.catchStmt, OpCodes.Dup);
                catThrown.Pop(this, tryStmt);              // store exception object in catThrown
                ilGen.Emit(tryStmt.catchStmt, OpCodes.Call, catchExcToStrMethodInfo);

                ilGen.Emit(tryStmt.catchStmt, OpCodes.Stloc, excLocal);
                catchVarLocStr.PopPre(this, tryStmt.catchVar.name);
                ilGen.Emit(tryStmt.catchStmt, OpCodes.Ldloc, excLocal);
                catchVarLocStr.PopPost(this, tryStmt.catchVar.name, tokenTypeStr);
            }
            else
            {
                throw new Exception("bad catch var type " + tryStmt.catchVar.type.ToString());
            }

            SetCallNo(tryStmt, tryCallNo, tryThrow.index);     // __tryCallNo = tryThrow so it knows to do 'throw catThrown' on restore

            GetCallNo(tryStmt, catCallNo);             // if (__catCallNo >= 0) goto catchCallSw;
            ilGen.Emit(tryStmt.catchStmt, OpCodes.Ldc_I4_0);
            ilGen.Emit(tryStmt.catchStmt, OpCodes.Bge, catchCallSw);

            actCallNo = catCallNo.localBuilder;                 // set up __catCallNo for call labels
            actCallLabels.Clear();
            mightGetHere = true;                    // if we can get to the 'try' assume we can get to the 'catch'
            GenerateStmtBlock(tryStmt.catchStmt);          // output catch clause statement subblock

            if(mightGetHere)
            {
                new CallLabel(this, tryStmt.catchStmt);
                ilGen.Emit(tryStmt.catchStmt, OpCodes.Leave, tryEnd);
                openCallLabel = null;
            }

            ilGen.MarkLabel(catchRetro);               // not a script-visible exception, rethrow it
            ilGen.Emit(tryStmt.catchStmt, OpCodes.Pop);
            ilGen.Emit(tryStmt.catchStmt, OpCodes.Rethrow);

            ilGen.MarkLabel(catchCallSw);
            OutputCallNoSwitchStmt();              // restoring, jump back inside script-defined body

            ilGen.EndExceptionBlock();
            ilGen.MarkLabel(tryEnd);

            mightGetHere |= tryBlockFallsOutBottom;         // also get here if try body falls out bottom
        }

        /**
         * @brief output code for a try/finally block
         *
         * This is such a mess because there is hidden state for the finally { } that we have to recreate.
         * The finally { } can be entered either via an exception being thrown in the try { } or a leave 
         * being executed in the try { } whose target is outside the try { } finally { }.
         *
         * For the thrown exception case, we slip in a try { } catch { } wrapper around the original try { }
         * body.  This will sense any thrown exception that would execute the finally { }.  Then we have our
         * try { } throw the exception on restore which gets the finally { } called and on its way again.
         *
         * For the leave case, we prefix all leave instructions with a call label and we explicitly chain
         * all leaves through each try { } that has an associated finally { } that the leave would unwind 
         * through.  This gets each try { } to simply jump to the correct leave instruction which immediately 
         * invokes the corresponding finally { } and then chains to the next leave instruction on out until 
         * it gets to its target.
         *
         *      int    __finCallNo = -1;                                     // call number within finally { } subblock
         *      int    __tryCallNo = -1;                                     // call number within try { } subblock
         *      Exception __catThrown = null;                                // caught exception
         *    <oldCallLabel>:                                                // the outside world jumps here to restore us no matter ...
         *      try {                                                        // ... where we actually were inside of try/finally
         *          try {
         *              if (__tryCallNo >= 0) goto tryCallSw;                // maybe go do restore
         *              <try body using __tryCallNo>                         // execute script-defined code
         *                                                                   // ...stack capture WILL run catch/finally { } subblock
         *              leave tryEnd;                                        // executes finally { } subblock and exits
         *            tryThrow:<tryCallLabel>:
         *              throw new ScriptRestoreCatchException(__catThrown);  // catch { } was running, jump to its beginning
         *            tryCallSw:                                             // restoring...
         *              switch (__tryCallNo) back up into <try body>         // jump back inside try, ...
         *                                                                   // ... maybe to a leave if we were doing finally { } subblock
         *          } catch (Exception exc) {                                // in case we're getting to finally { } via a thrown exception:
         *              exc = ScriptRestoreCatchException.Unwrap(exc);       // unwrap possible ScriptRestoreCatchException
         *              if (callMode == CallMode_SAVE) goto catchRetro;      // don't touch anything if capturing stack
         *              __catThrown = exc;                                   // save exception so try { } can throw it on restore
         *              __tryCallNo = tryThrow:<tryCallLabel>;               // tell try { } to throw it on restore
         *            catchRetro:
         *              rethrow;                                             // in any case, go on to finally { } subblock now
         *          }
         *      } finally {
         *          if (callMode == CallMode_SAVE) goto finEnd;              // don't touch anything if capturing stack
         *          if (__finCallNo >= 0) goto finCallSw;                    // maybe go do restore
         *          <finally body using __finCallNo>                         // normal, execute script-defined code
         *        finEnd:
         *          endfinally                                               // jump to leave/throw target or next outer finally { }
         *        finCallSw:
         *          switch (__finCallNo) back up into <finally body>         // restoring, restart finally { } code wherever it was
         *      }
         *    tryEnd:
         */
        private void GenerateStmtTryFinally(TokenStmtTry tryStmt)
        {
            CompValuTemp finCallNo = new CompValuTemp(tokenTypeInt, this);
            CompValuTemp tryCallNo = new CompValuTemp(tokenTypeInt, this);
            CompValuTemp catThrown = new CompValuTemp(tokenTypeExc, this);

            ScriptMyLabel tryCallSw = ilGen.DefineLabel("__tryCallSw_" + tryStmt.Unique);
            ScriptMyLabel catchRetro = ilGen.DefineLabel("__catchRetro_" + tryStmt.Unique);
            ScriptMyLabel finCallSw = ilGen.DefineLabel("__finCallSw_" + tryStmt.Unique);
            BreakContTarg finEnd = new BreakContTarg(this, "__finEnd_" + tryStmt.Unique);
            ScriptMyLabel tryEnd = ilGen.DefineLabel("__tryEnd_" + tryStmt.Unique);

            SetCallNo(tryStmt, finCallNo, -1);
            SetCallNo(tryStmt, tryCallNo, -1);
            ilGen.Emit(tryStmt, OpCodes.Ldnull);
            catThrown.Pop(this, tryStmt);

            new CallLabel(this, tryStmt);              //   <oldcalllabel>:
            ilGen.BeginExceptionBlock();               //     try {
            ilGen.BeginExceptionBlock();               //     try {
            openCallLabel = null;

            GetCallNo(tryStmt, tryCallNo);             //         if (__tryCallNo >= 0) goto tryCallSw;
            ilGen.Emit(tryStmt, OpCodes.Ldc_I4_0);
            ilGen.Emit(tryStmt, OpCodes.Bge, tryCallSw);

            actCallNo = tryCallNo.localBuilder;                     // set up __tryCallNo for call labels
            actCallLabels = new LinkedList<CallLabel>();

            GenerateStmtBlock(tryStmt.tryStmt);            // output the try block statement subblock

            if(mightGetHere)
            {
                new CallLabel(this, tryStmt);          //       <newCallLabel>:
                ilGen.Emit(tryStmt, OpCodes.Leave, tryEnd);    //         leave tryEnd;
                openCallLabel = null;
            }

            foreach(IntermediateLeave iLeave in tryStmt.iLeaves.Values)
            {
                ilGen.MarkLabel(iLeave.jumpIntoLabel);     //       intr2_exit:
                new CallLabel(this, tryStmt);          //         tryCallNo = n;
                ilGen.Emit(tryStmt, OpCodes.Leave, iLeave.jumpAwayLabel);  //    __callNo_n_: leave int1_exit;
                openCallLabel = null;
            }

            CallLabel tryThrow = new CallLabel(this, tryStmt); //       tryThrow:<tryCallLabel>:
            catThrown.PushVal(this, tryStmt);          //         throw new ScriptRestoreCatchException (__catThrown);
            ilGen.Emit(tryStmt, OpCodes.Newobj, scriptRestoreCatchExceptionConstructorInfo);
            ilGen.Emit(tryStmt, OpCodes.Throw);
            openCallLabel = null;

            ilGen.MarkLabel(tryCallSw);                //       tryCallSw:
            OutputCallNoSwitchStmt();              //         switch (tryCallNo) ...
                                                   //     }

            ilGen.BeginCatchBlock(typeof(Exception));     // start of the catch block that can catch any exception
            ilGen.Emit(tryStmt, OpCodes.Call, scriptRestoreCatchExceptionUnwrap);  // exc = ScriptRestoreCatchException.Unwrap (exc);
            PushXMRInst();                     // if (callMode == CallMode_SAVE) goto catchRetro;
            ilGen.Emit(tryStmt, OpCodes.Ldfld, callModeFieldInfo);
            ilGen.Emit(tryStmt, OpCodes.Ldc_I4, XMRInstAbstract.CallMode_SAVE);
            ilGen.Emit(tryStmt, OpCodes.Beq, catchRetro);

            catThrown.Pop(this, tryStmt);              // __catThrown = exc;
            SetCallNo(tryStmt, tryCallNo, tryThrow.index);     // __tryCallNo = tryThrow:<tryCallLabel>;
            ilGen.Emit(tryStmt, OpCodes.Rethrow);

            ilGen.MarkLabel(catchRetro);               // catchRetro:
            ilGen.Emit(tryStmt, OpCodes.Pop);
            ilGen.Emit(tryStmt, OpCodes.Rethrow);          //    rethrow;

            ilGen.EndExceptionBlock();             // }

            ilGen.BeginFinallyBlock();             // start of the finally block

            PushXMRInst();                     // if (callMode == CallMode_SAVE) goto finEnd;
            ilGen.Emit(tryStmt, OpCodes.Ldfld, callModeFieldInfo);
            ilGen.Emit(tryStmt, OpCodes.Ldc_I4, XMRInstAbstract.CallMode_SAVE);
            ilGen.Emit(tryStmt, OpCodes.Beq, finEnd.label);

            GetCallNo(tryStmt, finCallNo);             // if (__finCallNo >= 0) goto finCallSw;
            ilGen.Emit(tryStmt, OpCodes.Ldc_I4_0);
            ilGen.Emit(tryStmt, OpCodes.Bge, finCallSw);

            actCallNo = finCallNo.localBuilder;                 // set up __finCallNo for call labels
            actCallLabels.Clear();
            mightGetHere = true;                    // if we can get to the 'try' assume we can get to the 'finally'
            GenerateStmtBlock(tryStmt.finallyStmt);        // output finally clause statement subblock

            ilGen.MarkLabel(finEnd.label);             // finEnd:
            ilGen.Emit(tryStmt, OpCodes.Endfinally);       //    return out to next finally { } or catch { } or leave target

            ilGen.MarkLabel(finCallSw);                // restore mode, switch (finCallNo) ...
            OutputCallNoSwitchStmt();

            ilGen.EndExceptionBlock();
            ilGen.MarkLabel(tryEnd);

            mightGetHere |= finEnd.used;                // get here if finally body falls through or has a break statement
        }

        /**
         * @brief Generate code to initialize a variable to its default value.
         */
        private void GenerateStmtVarIniDef(TokenStmtVarIniDef varIniDefStmt)
        {
            if(!mightGetHere)
                return;

            CompValu left = GenerateFromLVal(varIniDefStmt.var);
            left.PopPre(this, varIniDefStmt);
            PushDefaultValue(left.type);
            left.PopPost(this, varIniDefStmt);
        }

        /**
         * @brief generate code for a 'while' statement including the loop body.
         */
        private void GenerateStmtWhile(TokenStmtWhile whileStmt)
        {
            if(!mightGetHere)
                return;

            BreakContTarg oldBreakTarg = curBreakTarg;
            BreakContTarg oldContTarg = curContTarg;
            ScriptMyLabel loopLabel = ilGen.DefineLabel("whileloop_" + whileStmt.Unique);

            curBreakTarg = new BreakContTarg(this, "whilebreak_" + whileStmt.Unique);
            curContTarg = new BreakContTarg(this, "whilecont_" + whileStmt.Unique);

            ilGen.MarkLabel(loopLabel);                                          // loop:
            CompValu testRVal = GenerateFromRVal(whileStmt.testRVal);            //   testRVal = while test expression
            if(!IsConstBoolExprTrue(testRVal))
            {
                testRVal.PushVal(this, whileStmt.testRVal, tokenTypeBool);   //   if (!testRVal)
                ilGen.Emit(whileStmt, OpCodes.Brfalse, curBreakTarg.label);  //      goto break
                curBreakTarg.used = true;
            }
            GenerateStmt(whileStmt.bodyStmt);                                    //   while body statement
            if(curContTarg.used)
            {
                ilGen.MarkLabel(curContTarg.label);                          // cont:
                mightGetHere = true;
            }
            if(mightGetHere)
            {
                EmitCallCheckRun(whileStmt, false);                          //   __sw.CheckRun()
                ilGen.Emit(whileStmt, OpCodes.Br, loopLabel);                //   goto loop
            }
            mightGetHere = curBreakTarg.used;
            if(mightGetHere)
            {
                ilGen.MarkLabel(curBreakTarg.label);                         // done:
            }

            curBreakTarg = oldBreakTarg;
            curContTarg = oldContTarg;
        }

        /**
         * @brief process a local variable declaration statement, possibly with initialization expression.
         *        Note that the function header processing allocated stack space (CompValuTemp) for the
         *        variable and now all we do is write its initialization value.
         */
        private void GenerateDeclVar(TokenDeclVar declVar)
        {
             // Script gave us an initialization value, so just store init value in var like an assignment statement.
             // If no init given, set it to its default value.
            CompValu local = declVar.location;
            if(declVar.init != null)
            {
                CompValu rVal = GenerateFromRVal(declVar.init, local.GetArgTypes());
                local.PopPre(this, declVar);
                rVal.PushVal(this, declVar.init, declVar.type);
                local.PopPost(this, declVar);
            }
            else
            {
                local.PopPre(this, declVar);
                PushDefaultValue(declVar.type);
                local.PopPost(this, declVar);
            }
        }

        /**
         * @brief Get the type and location of an L-value (eg, variable)
         * @param lVal    = L-value expression to evaluate
         * @param argsig  = null: it's a field/property
         *                  else: select overload method that fits these arg types
         */
        private CompValu GenerateFromLVal(TokenLVal lVal)
        {
            return GenerateFromLVal(lVal, null);
        }
        private CompValu GenerateFromLVal(TokenLVal lVal, TokenType[] argsig)
        {
            if(lVal is TokenLValArEle)
                return GenerateFromLValArEle((TokenLValArEle)lVal);
            if(lVal is TokenLValBaseField)
                return GenerateFromLValBaseField((TokenLValBaseField)lVal, argsig);
            if(lVal is TokenLValIField)
                return GenerateFromLValIField((TokenLValIField)lVal, argsig);
            if(lVal is TokenLValName)
                return GenerateFromLValName((TokenLValName)lVal, argsig);
            if(lVal is TokenLValSField)
                return GenerateFromLValSField((TokenLValSField)lVal, argsig);
            throw new Exception("bad lval class");
        }

        /**
         * @brief we have an L-value token that is an element within an array.
         * @returns a CompValu giving the type and location of the element of the array.
         */
        private CompValu GenerateFromLValArEle(TokenLValArEle lVal)
        {
            CompValu subCompValu;

             // Compute location of array itself.
            CompValu baseCompValu = GenerateFromRVal(lVal.baseRVal);

             // Maybe it is a fixed array access.
            string basetypestring = baseCompValu.type.ToString();
            if(basetypestring.EndsWith("]"))
            {
                TokenRVal subRVal = lVal.subRVal;
                int nSubs = 1;
                if(subRVal is TokenRValList)
                {
                    nSubs = ((TokenRValList)subRVal).nItems;
                    subRVal = ((TokenRValList)subRVal).rVal;
                }

                int rank = basetypestring.IndexOf(']') - basetypestring.IndexOf('[');
                if(nSubs != rank)
                {
                    ErrorMsg(lVal.baseRVal, "expect " + rank + " subscript" + ((rank == 1) ? "" : "s") + " but have " + nSubs);
                }
                CompValu[] subCompValus = new CompValu[rank];
                int i;
                for(i = 0; (subRVal != null) && (i < rank); i++)
                {
                    subCompValus[i] = GenerateFromRVal(subRVal);
                    subRVal = (TokenRVal)subRVal.nextToken;
                }
                while(i < rank)
                    subCompValus[i++] = new CompValuInteger(new TokenTypeInt(lVal.subRVal), 0);
                return new CompValuFixArEl(this, baseCompValu, subCompValus);
            }

             // Maybe it is accessing the $idxprop property of a script-defined class.
            if(baseCompValu.type is TokenTypeSDTypeClass)
            {
                TokenName name = new TokenName(lVal, "$idxprop");
                TokenTypeSDTypeClass sdtType = (TokenTypeSDTypeClass)baseCompValu.type;
                TokenDeclSDTypeClass sdtDecl = sdtType.decl;
                TokenDeclVar idxProp = FindThisMember(sdtDecl, name, null);
                if(idxProp == null)
                {
                    ErrorMsg(lVal, "no index property in class " + sdtDecl.longName.val);
                    return new CompValuVoid(lVal);
                }
                if((idxProp.sdtFlags & ScriptReduce.SDT_STATIC) != 0)
                {
                    ErrorMsg(lVal, "non-static reference to static member " + idxProp.name.val);
                    return new CompValuVoid(idxProp);
                }
                CheckAccess(idxProp, name);

                TokenType[] argTypes = IdxPropArgTypes(idxProp);
                CompValu[] compValus = IdxPropCompValus(lVal, argTypes.Length);
                return new CompValuIdxProp(idxProp, baseCompValu, argTypes, compValus);

            }

             // Maybe they are accessing $idxprop property of a script-defined interface.
            if(baseCompValu.type is TokenTypeSDTypeInterface)
            {
                TokenName name = new TokenName(lVal, "$idxprop");
                TokenTypeSDTypeInterface sdtType = (TokenTypeSDTypeInterface)baseCompValu.type;
                TokenDeclVar idxProp = FindInterfaceMember(sdtType, name, null, ref baseCompValu);
                if(idxProp == null)
                {
                    ErrorMsg(lVal, "no index property defined for interface " + sdtType.decl.longName.val);
                    return baseCompValu;
                }

                TokenType[] argTypes = IdxPropArgTypes(idxProp);
                CompValu[] compValus = IdxPropCompValus(lVal, argTypes.Length);
                return new CompValuIdxProp(idxProp, baseCompValu, argTypes, compValus);
            }

             // Maybe it is extracting a character from a string.
            if((baseCompValu.type is TokenTypeKey) || (baseCompValu.type is TokenTypeStr))
            {
                subCompValu = GenerateFromRVal(lVal.subRVal);
                return new CompValuStrChr(new TokenTypeChar(lVal), baseCompValu, subCompValu);
            }

             // Maybe it is extracting an element from a list.
            if(baseCompValu.type is TokenTypeList)
            {
                subCompValu = GenerateFromRVal(lVal.subRVal);
                return new CompValuListEl(new TokenTypeObject(lVal), baseCompValu, subCompValu);
            }

             // Access should be to XMR_Array otherwise.
            if(!(baseCompValu.type is TokenTypeArray))
            {
                ErrorMsg(lVal, "taking subscript of non-array");
                return baseCompValu;
            }
            subCompValu = GenerateFromRVal(lVal.subRVal);
            return new CompValuArEle(new TokenTypeObject(lVal), baseCompValu, subCompValu);
        }

        /**
         * @brief Get number and type of arguments required by an index property.
         */
        private static TokenType[] IdxPropArgTypes(TokenDeclVar idxProp)
        {
            TokenType[] argTypes;
            if(idxProp.getProp != null)
            {
                int nArgs = idxProp.getProp.argDecl.varDict.Count;
                argTypes = new TokenType[nArgs];
                foreach(TokenDeclVar var in idxProp.getProp.argDecl.varDict)
                {
                    argTypes[var.vTableIndex] = var.type;
                }
            }
            else
            {
                int nArgs = idxProp.setProp.argDecl.varDict.Count - 1;
                argTypes = new TokenType[nArgs];
                foreach(TokenDeclVar var in idxProp.setProp.argDecl.varDict)
                {
                    if(var.vTableIndex < nArgs)
                    {
                        argTypes[var.vTableIndex] = var.type;
                    }
                }
            }
            return argTypes;
        }

        /**
         * @brief Get number and computed value of index property arguments.
         * @param lVal = list of arguments
         * @param nArgs = number of arguments required
         * @returns null: argument count mismatch
         *          else: array of index property argument values
         */
        private CompValu[] IdxPropCompValus(TokenLValArEle lVal, int nArgs)
        {
            TokenRVal subRVal = lVal.subRVal;
            int nSubs = 1;
            if(subRVal is TokenRValList)
            {
                nSubs = ((TokenRValList)subRVal).nItems;
                subRVal = ((TokenRValList)subRVal).rVal;
            }

            if(nSubs != nArgs)
            {
                ErrorMsg(lVal, "index property requires " + nArgs + " subscript(s)");
                return null;
            }

            CompValu[] subCompValus = new CompValu[nArgs];
            for(int i = 0; i < nArgs; i++)
            {
                subCompValus[i] = GenerateFromRVal(subRVal);
                subRVal = (TokenRVal)subRVal.nextToken;
            }
            return subCompValus;
        }

        /**
         * @brief using 'base' within a script-defined instance method to refer to an instance field/method 
         *        of the class being extended.
         */
        private CompValu GenerateFromLValBaseField(TokenLValBaseField baseField, TokenType[] argsig)
        {
            string fieldName = baseField.fieldName.val;

            TokenDeclSDType sdtDecl = curDeclFunc.sdtClass;
            if((sdtDecl == null) || ((curDeclFunc.sdtFlags & ScriptReduce.SDT_STATIC) != 0))
            {
                ErrorMsg(baseField, "cannot use 'base' outside instance method body");
                return new CompValuVoid(baseField);
            }
            if(!IsSDTInstMethod())
            {
                ErrorMsg(baseField, "cannot access instance member of base class from static method");
                return new CompValuVoid(baseField);
            }

            TokenDeclVar declVar = FindThisMember(sdtDecl.extends, baseField.fieldName, argsig);
            if(declVar != null)
            {
                CheckAccess(declVar, baseField.fieldName);
                TokenType baseType = declVar.sdtClass.MakeRefToken(baseField);
                CompValu basePtr = new CompValuArg(baseType, 0);
                return AccessInstanceMember(declVar, basePtr, baseField, true);
            }

            ErrorMsg(baseField, "no member " + fieldName + ArgSigString(argsig) + " rootward of " + sdtDecl.longName.val);
            return new CompValuVoid(baseField);
        }

        /**
         * @brief We have an L-value token that is an instance field/method within a struct.
         * @returns a CompValu giving the type and location of the field/method in the struct.
         */
        private CompValu GenerateFromLValIField(TokenLValIField lVal, TokenType[] argsig)
        {
            CompValu baseRVal = GenerateFromRVal(lVal.baseRVal);
            string fieldName = lVal.fieldName.val + ArgSigString(argsig);

             // Maybe they are accessing an instance field, method or property of a script-defined class.
            if(baseRVal.type is TokenTypeSDTypeClass)
            {
                TokenTypeSDTypeClass sdtType = (TokenTypeSDTypeClass)baseRVal.type;
                TokenDeclSDTypeClass sdtDecl = sdtType.decl;
                TokenDeclVar declVar = FindThisMember(sdtDecl, lVal.fieldName, argsig);
                if(declVar != null)
                {
                    CheckAccess(declVar, lVal.fieldName);
                    return AccessInstanceMember(declVar, baseRVal, lVal, false);
                }
                ErrorMsg(lVal.fieldName, "no member " + fieldName + " in class " + sdtDecl.longName.val);
                return new CompValuVoid(lVal.fieldName);
            }

             // Maybe they are accessing a method or property of a script-defined interface.
            if(baseRVal.type is TokenTypeSDTypeInterface)
            {
                TokenTypeSDTypeInterface sdtType = (TokenTypeSDTypeInterface)baseRVal.type;
                TokenDeclVar declVar = FindInterfaceMember(sdtType, lVal.fieldName, argsig, ref baseRVal);
                if(declVar != null)
                {
                    return new CompValuIntfMember(declVar, baseRVal);
                }
                ErrorMsg(lVal.fieldName, "no member " + fieldName + " in interface " + sdtType.decl.longName.val);
                return new CompValuVoid(lVal.fieldName);
            }

             // Since we only have a few built-in types with fields, just pound them out.
            if(baseRVal.type is TokenTypeArray)
            {

                // no arguments, no parentheses, just the field name, returning integer
                // but internally, it is a call to a method()
                if(fieldName == "count")
                {
                    return new CompValuIntInstROProp(tokenTypeInt, baseRVal, arrayCountMethodInfo);
                }

                // no arguments but with the parentheses, returning void
                if(fieldName == "clear()")
                {
                    return new CompValuIntInstMeth(XMR_Array.clearDelegate, baseRVal, arrayClearMethodInfo);
                }

                // single integer argument, returning an object
                if(fieldName == "index(integer)")
                {
                    return new CompValuIntInstMeth(XMR_Array.indexDelegate, baseRVal, arrayIndexMethodInfo);
                }
                if(fieldName == "value(integer)")
                {
                    return new CompValuIntInstMeth(XMR_Array.valueDelegate, baseRVal, arrayValueMethodInfo);
                }
            }
            if(baseRVal.type is TokenTypeRot)
            {
                FieldInfo fi = null;
                if(fieldName == "x")
                    fi = rotationXFieldInfo;
                if(fieldName == "y")
                    fi = rotationYFieldInfo;
                if(fieldName == "z")
                    fi = rotationZFieldInfo;
                if(fieldName == "s")
                    fi = rotationSFieldInfo;
                if(fi != null)
                {
                    return new CompValuField(new TokenTypeFloat(lVal), baseRVal, fi);
                }
            }
            if(baseRVal.type is TokenTypeVec)
            {
                FieldInfo fi = null;
                if(fieldName == "x")
                    fi = vectorXFieldInfo;
                if(fieldName == "y")
                    fi = vectorYFieldInfo;
                if(fieldName == "z")
                    fi = vectorZFieldInfo;
                if(fi != null)
                {
                    return new CompValuField(new TokenTypeFloat(lVal), baseRVal, fi);
                }
            }

            ErrorMsg(lVal, "type " + baseRVal.type.ToString() + " does not define member " + fieldName);
            return baseRVal;
        }

        /**
         * @brief We have an L-value token that is a function, method or variable name.
         * @param lVal = name we are looking for
         * @param argsig = null: just look for name as a variable
         *                 else: look for name as a function/method being called with the given argument types
         *                       eg, "(string,integer,list)"
         * @returns a CompValu giving the type and location of the function, method or variable.
         */
        private CompValu GenerateFromLValName(TokenLValName lVal, TokenType[] argsig)
        {
             // Look in variable stack then look for built-in constants and functions.
            TokenDeclVar var = FindNamedVar(lVal, argsig);
            if(var == null)
            {
                ErrorMsg(lVal, "undefined constant/function/variable " + lVal.name.val + ArgSigString(argsig));
                return new CompValuVoid(lVal);
            }

             // Maybe it has an implied 'this.' on the front.
            if((var.sdtClass != null) && ((var.sdtFlags & ScriptReduce.SDT_STATIC) == 0))
            {

                if(!IsSDTInstMethod())
                {
                    ErrorMsg(lVal, "cannot access instance member of class from static method");
                    return new CompValuVoid(lVal);
                }

                 // Don't allow something such as:
                 //
                 //    class A {
                 //        integer I;
                 //        class B {
                 //            Print ()
                 //            {
                 //                llOwnerSay ("I=" + (string)I); <- access to I not allowed inside class B.
                 //                                                  explicit reference required as we don't
                 //                                                  have a valid reference to class A.
                 //            }
                 //        }
                 //    }
                 //
                 // But do allow something such as:
                 //
                 //    class A {
                 //        integer I;
                 //    }
                 //    class B : A {
                 //        Print ()
                 //        {
                 //            llOwnerSay ("I=" + (string)I);
                 //        }
                 //    }
                for(TokenDeclSDType c = curDeclFunc.sdtClass; c != var.sdtClass; c = c.extends)
                {
                    if(c == null)
                    {
                        // our arg0 points to an instance of curDeclFunc.sdtClass, not var.sdtClass
                        ErrorMsg(lVal, "cannot access instance member of outer class with implied 'this'");
                        break;
                    }
                }

                CompValu thisCompValu = new CompValuArg(var.sdtClass.MakeRefToken(lVal), 0);
                return AccessInstanceMember(var, thisCompValu, lVal, false);
            }

             // It's a local variable, static field, global, constant, etc.
            return var.location;
        }

        /**
         * @brief Access a script-defined type's instance member
         * @param declVar = which member (field,method,property) to access
         * @param basePtr = points to particular object instance
         * @param ignoreVirt = true: access declVar's method directly; else: maybe use vTable
         * @returns where the field/method/property is located
         */
        private CompValu AccessInstanceMember(TokenDeclVar declVar, CompValu basePtr, Token errorAt, bool ignoreVirt)
        {
            if((declVar.sdtFlags & ScriptReduce.SDT_STATIC) != 0)
            {
                ErrorMsg(errorAt, "non-static reference to static member " + declVar.name.val);
                return new CompValuVoid(declVar);
            }
            return new CompValuInstMember(declVar, basePtr, ignoreVirt);
        }

        /**
         * @brief we have an L-value token that is a static member within a struct.
         * @returns a CompValu giving the type and location of the member in the struct.
         */
        private CompValu GenerateFromLValSField(TokenLValSField lVal, TokenType[] argsig)
        {
            TokenType stType = lVal.baseType;
            string fieldName = lVal.fieldName.val + ArgSigString(argsig);

             // Maybe they are accessing a static member of a script-defined class.
            if(stType is TokenTypeSDTypeClass)
            {
                TokenTypeSDTypeClass sdtType = (TokenTypeSDTypeClass)stType;
                TokenDeclVar declVar = FindThisMember(sdtType.decl, lVal.fieldName, argsig);
                if(declVar != null)
                {
                    CheckAccess(declVar, lVal.fieldName);
                    if((declVar.sdtFlags & ScriptReduce.SDT_STATIC) == 0)
                    {
                        ErrorMsg(lVal.fieldName, "static reference to non-static member " + fieldName);
                        return new CompValuVoid(lVal.fieldName);
                    }
                    return declVar.location;
                }
            }

            ErrorMsg(lVal.fieldName, "no member " + fieldName + " in " + stType.ToString());
            return new CompValuVoid(lVal.fieldName);
        }

        /**
         * @brief generate code from an RVal expression and return its type and where the result is stored.
         * For anything that has side-effects, statements are generated that perform the computation then
         * the result it put in a temp var and the temp var name is returned.
         * For anything without side-effects, they are returned as an equivalent sequence of Emits.
         * @param rVal = rVal token to be evaluated
         * @param argsig = null: not being used in an function/method context
         *                 else: string giving argument types, eg, "(string,integer,list,vector)"
         *                       that can be used to select among overloaded methods
         * @returns resultant type and location
         */
        private CompValu GenerateFromRVal(TokenRVal rVal)
        {
            return GenerateFromRVal(rVal, null);
        }
        private CompValu GenerateFromRVal(TokenRVal rVal, TokenType[] argsig)
        {
            errorMessageToken = rVal;

             // Maybe the expression can be converted to a constant.
            bool didOne;
            try
            {
                do
                {
                    didOne = false;
                    rVal = rVal.TryComputeConstant(LookupBodyConstants, ref didOne);
                } while(didOne);
            }
            catch(Exception ex)
            {
                ErrorMsg(errorMessageToken, ex.Message);
                throw;
            }

             // Generate code for the computation and return resulting type and location.
            CompValu cVal = null;
            if(rVal is TokenRValAsnPost)
                cVal = GenerateFromRValAsnPost((TokenRValAsnPost)rVal);
            if(rVal is TokenRValAsnPre)
                cVal = GenerateFromRValAsnPre((TokenRValAsnPre)rVal);
            if(rVal is TokenRValCall)
                cVal = GenerateFromRValCall((TokenRValCall)rVal);
            if(rVal is TokenRValCast)
                cVal = GenerateFromRValCast((TokenRValCast)rVal);
            if(rVal is TokenRValCondExpr)
                cVal = GenerateFromRValCondExpr((TokenRValCondExpr)rVal);
            if(rVal is TokenRValConst)
                cVal = GenerateFromRValConst((TokenRValConst)rVal);
            if(rVal is TokenRValInitDef)
                cVal = GenerateFromRValInitDef((TokenRValInitDef)rVal);
            if(rVal is TokenRValIsType)
                cVal = GenerateFromRValIsType((TokenRValIsType)rVal);
            if(rVal is TokenRValList)
                cVal = GenerateFromRValList((TokenRValList)rVal);
            if(rVal is TokenRValNewArIni)
                cVal = GenerateFromRValNewArIni((TokenRValNewArIni)rVal);
            if(rVal is TokenRValOpBin)
                cVal = GenerateFromRValOpBin((TokenRValOpBin)rVal);
            if(rVal is TokenRValOpUn)
                cVal = GenerateFromRValOpUn((TokenRValOpUn)rVal);
            if(rVal is TokenRValParen)
                cVal = GenerateFromRValParen((TokenRValParen)rVal);
            if(rVal is TokenRValRot)
                cVal = GenerateFromRValRot((TokenRValRot)rVal);
            if(rVal is TokenRValThis)
                cVal = GenerateFromRValThis((TokenRValThis)rVal);
            if(rVal is TokenRValUndef)
                cVal = GenerateFromRValUndef((TokenRValUndef)rVal);
            if(rVal is TokenRValVec)
                cVal = GenerateFromRValVec((TokenRValVec)rVal);
            if(rVal is TokenLVal)
                cVal = GenerateFromLVal((TokenLVal)rVal, argsig);

            if(cVal == null)
                throw new Exception("bad rval class " + rVal.GetType().ToString());

             // Sanity check.
            if(!youveAnError)
            {
                if(cVal.type == null)
                    throw new Exception("cVal has no type " + cVal.GetType());
                string cValType = cVal.type.ToString();
                string rValType = rVal.GetRValType(this, argsig).ToString();
                if(cValType == "bool")
                    cValType = "integer";
                if(rValType == "bool")
                    rValType = "integer";
                if(cValType != rValType)
                {
                    throw new Exception("cVal.type " + cValType + " != rVal.type " + rValType +
                                         "  (" + rVal.GetType().Name + " " + rVal.SrcLoc + ")");
                }
            }

            return cVal;
        }

        /**
         * @brief compute the result of a binary operator (eg, add, subtract, multiply, lessthan)
         * @param token = binary operator token, includes the left and right operands
         * @returns where the resultant R-value is as something that doesn't have side effects
         */
        private CompValu GenerateFromRValOpBin(TokenRValOpBin token)
        {
            CompValu left, right;
            string opcodeIndex = token.opcode.ToString();

             // Comma operators are special, as they say to compute the left-hand value and 
             // discard it, then compute the right-hand argument and that is the result.
            if(opcodeIndex == ",")
            {
                 // Compute left-hand operand but throw away result.
                GenerateFromRVal(token.rValLeft);

                 // Compute right-hand operand and that is the value of the expression.
                return GenerateFromRVal(token.rValRight);
            }

             // Simple overwriting assignments are their own special case,
             // as we want to cast the R-value to the type of the L-value.
             // And in the case of delegates, we want to use the arg signature
             // of the delegate to select which overloaded method to use.
            if(opcodeIndex == "=")
            {
                if(!(token.rValLeft is TokenLVal))
                {
                    ErrorMsg(token, "invalid L-value for =");
                    return GenerateFromRVal(token.rValLeft);
                }
                left = GenerateFromLVal((TokenLVal)token.rValLeft);
                right = Trivialize(GenerateFromRVal(token.rValRight, left.GetArgTypes()), token.rValRight);
                left.PopPre(this, token.rValLeft);
                right.PushVal(this, token.rValRight, left.type);  // push (left.type)right
                left.PopPost(this, token.rValLeft);               // pop to left
                return left;
            }

             // There are String.Concat() methods available for 2, 3 and 4 operands.
             // So see if we have a string concat op and optimize if so.
            if((opcodeIndex == "+") ||
                ((opcodeIndex == "+=") &&
                 (token.rValLeft is TokenLVal) &&
                 (token.rValLeft.GetRValType(this, null) is TokenTypeStr)))
            {

                 // We are adding something.  Maybe it's a bunch of strings together.
                List<TokenRVal> scorvs = new List<TokenRVal>();
                if(StringConcatOperands(token.rValLeft, token.rValRight, scorvs, token.opcode))
                {
                     // Evaluate all the operands, right-to-left on purpose per LSL scripting.
                    int i;
                    int n = scorvs.Count;
                    CompValu[] scocvs = new CompValu[n];
                    for(i = n; --i >= 0;)
                    {
                        scocvs[i] = GenerateFromRVal(scorvs[i]);
                        if(i > 0)
                            scocvs[i] = Trivialize(scocvs[i], scorvs[i]);
                    }

                    /*
                     * Figure out where to put the result.
                     * A temp if '+', or back in original L-value if '+='.
                     */
                    CompValu retcv;
                    if(opcodeIndex == "+")
                    {
                        retcv = new CompValuTemp(new TokenTypeStr(token.opcode), this);
                    }
                    else
                    {
                        retcv = GenerateFromLVal((TokenLVal)token.rValLeft);
                    }
                    retcv.PopPre(this, token);

                     // Call the String.Concat() methods, passing operands in left-to-right order.
                     // Force a cast to string (retcv.type) for each operand.
                    ++i;
                    scocvs[i].PushVal(this, scorvs[i], retcv.type);
                    while(i + 3 < n)
                    {
                        ++i;
                        scocvs[i].PushVal(this, scorvs[i], retcv.type);
                        ++i;
                        scocvs[i].PushVal(this, scorvs[i], retcv.type);
                        ++i;
                        scocvs[i].PushVal(this, scorvs[i], retcv.type);
                        ilGen.Emit(scorvs[i], OpCodes.Call, stringConcat4MethodInfo);
                    }
                    if(i + 2 < n)
                    {
                        ++i;
                        scocvs[i].PushVal(this, scorvs[i], retcv.type);
                        ++i;
                        scocvs[i].PushVal(this, scorvs[i], retcv.type);
                        ilGen.Emit(scorvs[i], OpCodes.Call, stringConcat3MethodInfo);
                    }
                    if(i + 1 < n)
                    {
                        ++i;
                        scocvs[i].PushVal(this, scorvs[i], retcv.type);
                        ilGen.Emit(scorvs[i], OpCodes.Call, stringConcat2MethodInfo);
                    }

                     // Put the result where we want it and return where we put it.
                    retcv.PopPost(this, token);
                    return retcv;
                }
            }

             // If "&&&", it is a short-circuiting AND.
             // Compute left-hand operand and if true, compute right-hand operand.
            if(opcodeIndex == "&&&")
            {
                bool leftVal, rightVal;
                left = GenerateFromRVal(token.rValLeft);
                if(!IsConstBoolExpr(left, out leftVal))
                {
                    ScriptMyLabel falseLabel = ilGen.DefineLabel("ssandfalse");
                    left.PushVal(this, tokenTypeBool);
                    ilGen.Emit(token, OpCodes.Brfalse, falseLabel);
                    right = GenerateFromRVal(token.rValRight);
                    if(!IsConstBoolExpr(right, out rightVal))
                    {
                        right.PushVal(this, tokenTypeBool);
                        goto donessand;
                    }
                    if(!rightVal)
                    {
                        ilGen.MarkLabel(falseLabel);
                        return new CompValuInteger(new TokenTypeInt(token.rValLeft), 0);
                    }
                    ilGen.Emit(token, OpCodes.Ldc_I4_1);
                    donessand:
                    ScriptMyLabel doneLabel = ilGen.DefineLabel("ssanddone");
                    ilGen.Emit(token, OpCodes.Br, doneLabel);
                    ilGen.MarkLabel(falseLabel);
                    ilGen.Emit(token, OpCodes.Ldc_I4_0);
                    ilGen.MarkLabel(doneLabel);
                    CompValuTemp retRVal = new CompValuTemp(new TokenTypeInt(token), this);
                    retRVal.Pop(this, token);
                    return retRVal;
                }

                if(!leftVal)
                {
                    return new CompValuInteger(new TokenTypeInt(token.rValLeft), 0);
                }

                right = GenerateFromRVal(token.rValRight);
                if(!IsConstBoolExpr(right, out rightVal))
                {
                    right.PushVal(this, tokenTypeBool);
                    CompValuTemp retRVal = new CompValuTemp(new TokenTypeInt(token), this);
                    retRVal.Pop(this, token);
                    return retRVal;
                }
                return new CompValuInteger(new TokenTypeInt(token), rightVal ? 1 : 0);
            }

             // If "|||", it is a short-circuiting OR.
             // Compute left-hand operand and if false, compute right-hand operand.
            if(opcodeIndex == "|||")
            {
                bool leftVal, rightVal;
                left = GenerateFromRVal(token.rValLeft);
                if(!IsConstBoolExpr(left, out leftVal))
                {
                    ScriptMyLabel trueLabel = ilGen.DefineLabel("ssortrue");
                    left.PushVal(this, tokenTypeBool);
                    ilGen.Emit(token, OpCodes.Brtrue, trueLabel);
                    right = GenerateFromRVal(token.rValRight);
                    if(!IsConstBoolExpr(right, out rightVal))
                    {
                        right.PushVal(this, tokenTypeBool);
                        goto donessor;
                    }
                    if(rightVal)
                    {
                        ilGen.MarkLabel(trueLabel);
                        return new CompValuInteger(new TokenTypeInt(token.rValLeft), 1);
                    }
                    ilGen.Emit(token, OpCodes.Ldc_I4_0);
                    donessor:
                    ScriptMyLabel doneLabel = ilGen.DefineLabel("ssanddone");
                    ilGen.Emit(token, OpCodes.Br, doneLabel);
                    ilGen.MarkLabel(trueLabel);
                    ilGen.Emit(token, OpCodes.Ldc_I4_1);
                    ilGen.MarkLabel(doneLabel);
                    CompValuTemp retRVal = new CompValuTemp(new TokenTypeInt(token), this);
                    retRVal.Pop(this, token);
                    return retRVal;
                }

                if(leftVal)
                {
                    return new CompValuInteger(new TokenTypeInt(token.rValLeft), 1);
                }

                right = GenerateFromRVal(token.rValRight);
                if(!IsConstBoolExpr(right, out rightVal))
                {
                    right.PushVal(this, tokenTypeBool);
                    CompValuTemp retRVal = new CompValuTemp(new TokenTypeInt(token), this);
                    retRVal.Pop(this, token);
                    return retRVal;
                }
                return new CompValuInteger(new TokenTypeInt(token), rightVal ? 1 : 0);
            }

             // Computation of some sort, compute right-hand operand value then left-hand value
             // because LSL is supposed to be right-to-left evaluation.
            right = Trivialize(GenerateFromRVal(token.rValRight), token.rValRight);

             // If left is a script-defined class and there is a method with the operator's name,
             // convert this to a call to that method with the right value as its single parameter.
             // Except don't if the right value is 'undef' so they can always compare to undef.
            TokenType leftType = token.rValLeft.GetRValType(this, null);
            if((leftType is TokenTypeSDTypeClass) && !(right.type is TokenTypeUndef))
            {
                TokenTypeSDTypeClass sdtType = (TokenTypeSDTypeClass)leftType;
                TokenDeclSDTypeClass sdtDecl = sdtType.decl;
                TokenType[] argsig = new TokenType[] { right.type };
                TokenName funcName = new TokenName(token.opcode, "$op" + opcodeIndex);
                TokenDeclVar declFunc = FindThisMember(sdtDecl, funcName, argsig);
                if(declFunc != null)
                {
                    CheckAccess(declFunc, funcName);
                    left = GenerateFromRVal(token.rValLeft);
                    CompValu method = AccessInstanceMember(declFunc, left, token, false);
                    CompValu[] argRVals = new CompValu[] { right };
                    return GenerateACall(method, argRVals, token);
                }
            }

             // Formulate key string for binOpStrings = (lefttype)(operator)(righttype)
            string leftIndex = leftType.ToString();
            string rightIndex = right.type.ToString();
            string key = leftIndex + opcodeIndex + rightIndex;

             // If that key exists in table, then the operation is defined between those types
             // ... and it produces an R-value of type as given in the table.
            BinOpStr binOpStr;
            if(BinOpStr.defined.TryGetValue(key, out binOpStr))
            {
                 // If table contained an explicit assignment type like +=, output the statement without
                 // casting the L-value, then return the L-value as the resultant value.
                 //
                 // Make sure we don't include comparisons (such as ==, >=, etc).
                 // Nothing like +=, -=, %=, etc, generate a boolean, only the comparisons.
                if((binOpStr.outtype != typeof(bool)) && opcodeIndex.EndsWith("=") && (opcodeIndex != "!="))
                {
                    if(!(token.rValLeft is TokenLVal))
                    {
                        ErrorMsg(token.rValLeft, "invalid L-value");
                        return GenerateFromRVal(token.rValLeft);
                    }
                    left = GenerateFromLVal((TokenLVal)token.rValLeft);
                    binOpStr.emitBO(this, token, left, right, left);
                    return left;
                }

                 // It's of the form left binop right.
                 // Compute left, perform operation then put result in a temp.
                left = GenerateFromRVal(token.rValLeft);
                CompValu retRVal = new CompValuTemp(TokenType.FromSysType(token.opcode, binOpStr.outtype), this);
                binOpStr.emitBO(this, token, left, right, retRVal);
                return retRVal;
            }

             // Nothing in the table, check for comparing object pointers because of the myriad of types possible.
             // This will compare list pointers, null pointers, script-defined type pointers, array pointers, etc.
             // It will show equal iff the memory addresses are equal and that is good enough.
            if(!leftType.ToSysType().IsValueType && !right.type.ToSysType().IsValueType && ((opcodeIndex == "==") || (opcodeIndex == "!=")))
            {
                CompValuTemp retRVal = new CompValuTemp(new TokenTypeInt(token), this);
                left = GenerateFromRVal(token.rValLeft);
                left.PushVal(this, token.rValLeft);
                right.PushVal(this, token.rValRight);
                ilGen.Emit(token, OpCodes.Ceq);
                if(opcodeIndex == "!=")
                {
                    ilGen.Emit(token, OpCodes.Ldc_I4_1);
                    ilGen.Emit(token, OpCodes.Xor);
                }
                retRVal.Pop(this, token);
                return retRVal;
            }

             // If the opcode ends with "=", it may be something like "+=".
             // So look up the key as if we didn't have the "=" to tell us if the operation is legal.
             // Also, the binary operation's output type must be the same as the L-value type.
             // Likewise, integer += float not allowed because result is float, but float += integer is ok.
            if(opcodeIndex.EndsWith("="))
            {
                key = leftIndex + opcodeIndex.Substring(0, opcodeIndex.Length - 1) + rightIndex;
                if(BinOpStr.defined.TryGetValue(key, out binOpStr))
                {
                    if(!(token.rValLeft is TokenLVal))
                    {
                        ErrorMsg(token, "invalid L-value for <op>=");
                        return GenerateFromRVal(token.rValLeft);
                    }
                    if(!binOpStr.rmwOK)
                    {
                        ErrorMsg(token, "<op>= not allowed: " + leftIndex + " " + opcodeIndex + " " + rightIndex);
                        return new CompValuVoid(token);
                    }

                     // Now we know for something like %= that left%right is legal for the types given.
                    left = GenerateFromLVal((TokenLVal)token.rValLeft);
                    if(binOpStr.outtype == leftType.ToSysType())
                    {
                        binOpStr.emitBO(this, token, left, right, left);
                    }
                    else
                    {
                        CompValu temp = new CompValuTemp(TokenType.FromSysType(token, binOpStr.outtype), this);
                        binOpStr.emitBO(this, token, left, right, temp);
                        left.PopPre(this, token);
                        temp.PushVal(this, token, leftType);
                        left.PopPost(this, token);
                    }
                    return left;
                }
            }

             // Can't find it, oh well.
            ErrorMsg(token, "op not defined: " + leftIndex + " " + opcodeIndex + " " + rightIndex);
            return new CompValuVoid(token);
        }

        /**
         * @brief Queue the given operands to the end of the scos list.
         *        If it can be broken down into more string concat operands, do so.
         *        Otherwise, just push it as one operand.
         * @param leftRVal  = left-hand operand of a '+' operation
         * @param rightRVal = right-hand operand of a '+' operation
         * @param scos      = left-to-right list of operands for the string concat so far
         * @param addop     = the add operator token (either '+' or '+=')
         * @returns false: neither operand is a string, nothing added to scos
         *           true: scos = updated with leftRVal then rightRVal added onto the end, possibly broken down further
         */
        private bool StringConcatOperands(TokenRVal leftRVal, TokenRVal rightRVal, List<TokenRVal> scos, TokenKw addop)
        {
            /*
             * If neither operand is a string (eg, float+integer), then the result isn't going to be a string.
             */
            TokenType leftType = leftRVal.GetRValType(this, null);
            TokenType rightType = rightRVal.GetRValType(this, null);
            if(!(leftType is TokenTypeStr) && !(rightType is TokenTypeStr))
                return false;

             // Also, list+string => list so reject that too.
             // Also, string+list => list so reject that too.
            if(leftType is TokenTypeList)
                return false;
            if(rightType is TokenTypeList)
                return false;

             // Append values to the end of the list in left-to-right order.
             // If value is formed from a something+something => string, 
             // push them as separate values, otherwise push as one value.
            StringConcatOperand(leftType, leftRVal, scos);
            StringConcatOperand(rightType, rightRVal, scos);

             // Maybe constant strings can be concatted.
            try
            {
                int len;
                while(((len = scos.Count) >= 2) &&
                       ((leftRVal = scos[len - 2]) is TokenRValConst) &&
                       ((rightRVal = scos[len - 1]) is TokenRValConst))
                {
                    object sum = addop.binOpConst(((TokenRValConst)leftRVal).val,
                                                   ((TokenRValConst)rightRVal).val);
                    scos[len - 2] = new TokenRValConst(addop, sum);
                    scos.RemoveAt(len - 1);
                }
            }
            catch
            {
            }

             // We pushed some string stuff.
            return true;
        }

        /**
         * @brief Queue the given operand to the end of the scos list.
         *        If it can be broken down into more string concat operands, do so.
         *        Otherwise, just push it as one operand.
         * @param type = rVal's resultant type
         * @param rVal = operand to examine
         * @param scos = left-to-right list of operands for the string concat so far
         * @returns with scos = updated with rVal added onto the end, possibly broken down further
         */
        private void StringConcatOperand(TokenType type, TokenRVal rVal, List<TokenRVal> scos)
        {
            bool didOne;
            do
            {
                didOne = false;
                rVal = rVal.TryComputeConstant(LookupBodyConstants, ref didOne);
            } while(didOne);

            if(!(type is TokenTypeStr))
                goto pushasis;
            if(!(rVal is TokenRValOpBin))
                goto pushasis;
            TokenRValOpBin rValOpBin = (TokenRValOpBin)rVal;
            if(!(rValOpBin.opcode is TokenKwAdd))
                goto pushasis;
            if(StringConcatOperands(rValOpBin.rValLeft, rValOpBin.rValRight, scos, rValOpBin.opcode))
                return;
            pushasis:
            scos.Add(rVal);
        }

        /**
         * @brief compute the result of an unary operator
         * @param token = unary operator token, includes the operand
         * @returns where the resultant R-value is
         */
        private CompValu GenerateFromRValOpUn(TokenRValOpUn token)
        {
            CompValu inRVal = GenerateFromRVal(token.rVal);

             // Script-defined types can define their own methods to handle unary operators.
            if(inRVal.type is TokenTypeSDTypeClass)
            {
                TokenTypeSDTypeClass sdtType = (TokenTypeSDTypeClass)inRVal.type;
                TokenDeclSDTypeClass sdtDecl = sdtType.decl;
                TokenName funcName = new TokenName(token.opcode, "$op" + token.opcode.ToString());
                TokenDeclVar declFunc = FindThisMember(sdtDecl, funcName, zeroArgs);
                if(declFunc != null)
                {
                    CheckAccess(declFunc, funcName);
                    CompValu method = AccessInstanceMember(declFunc, inRVal, token, false);
                    return GenerateACall(method, zeroCompValus, token);
                }
            }

             // Otherwise use the default.
            return UnOpGenerate(inRVal, token.opcode);
        }

        /**
         * @brief postfix operator -- this returns the type and location of the resultant value
         */
        private CompValu GenerateFromRValAsnPost(TokenRValAsnPost asnPost)
        {
            CompValu lVal = GenerateFromLVal(asnPost.lVal);

             // Make up a temp to save original value in.
            CompValuTemp result = new CompValuTemp(lVal.type, this);

             // Prepare to pop incremented value back into variable being incremented.
            lVal.PopPre(this, asnPost.lVal);

             // Copy original value to temp and leave value on stack.
            lVal.PushVal(this, asnPost.lVal);
            ilGen.Emit(asnPost.lVal, OpCodes.Dup);
            result.Pop(this, asnPost.lVal);

             // Perform the ++/--.
            if((lVal.type is TokenTypeChar) || (lVal.type is TokenTypeInt))
            {
                ilGen.Emit(asnPost, OpCodes.Ldc_I4_1);
            }
            else if(lVal.type is TokenTypeFloat)
            {
                ilGen.Emit(asnPost, OpCodes.Ldc_R4, 1.0f);
            }
            else
            {
                lVal.PopPost(this, asnPost.lVal);
                ErrorMsg(asnPost, "invalid type for " + asnPost.postfix.ToString());
                return lVal;
            }
            switch(asnPost.postfix.ToString())
            {
                case "++":
                    {
                        ilGen.Emit(asnPost, OpCodes.Add);
                        break;
                    }
                case "--":
                    {
                        ilGen.Emit(asnPost, OpCodes.Sub);
                        break;
                    }
                default:
                    throw new Exception("unknown asnPost op");
            }

             // Store new value in original variable.
            lVal.PopPost(this, asnPost.lVal);

            return result;
        }

        /**
         * @brief prefix operator -- this returns the type and location of the resultant value
         */
        private CompValu GenerateFromRValAsnPre(TokenRValAsnPre asnPre)
        {
            CompValu lVal = GenerateFromLVal(asnPre.lVal);

             // Make up a temp to put result in.
            CompValuTemp result = new CompValuTemp(lVal.type, this);

             // Prepare to pop incremented value back into variable being incremented.
            lVal.PopPre(this, asnPre.lVal);

             // Push original value.
            lVal.PushVal(this, asnPre.lVal);

             // Perform the ++/--.
            if((lVal.type is TokenTypeChar) || (lVal.type is TokenTypeInt))
            {
                ilGen.Emit(asnPre, OpCodes.Ldc_I4_1);
            }
            else if(lVal.type is TokenTypeFloat)
            {
                ilGen.Emit(asnPre, OpCodes.Ldc_R4, 1.0f);
            }
            else
            {
                lVal.PopPost(this, asnPre.lVal);
                ErrorMsg(asnPre, "invalid type for " + asnPre.prefix.ToString());
                return lVal;
            }
            switch(asnPre.prefix.ToString())
            {
                case "++":
                    {
                        ilGen.Emit(asnPre, OpCodes.Add);
                        break;
                    }
                case "--":
                    {
                        ilGen.Emit(asnPre, OpCodes.Sub);
                        break;
                    }
                default:
                    throw new Exception("unknown asnPre op");
            }

             // Store new value in temp variable, keeping new value on stack.
            ilGen.Emit(asnPre.lVal, OpCodes.Dup);
            result.Pop(this, asnPre.lVal);

             // Store new value in original variable.
            lVal.PopPost(this, asnPre.lVal);

            return result;
        }

        /**
         * @brief Generate code that calls a function or object's method.
         * @returns where the call's return value is stored (a TokenTypeVoid if void)
         */
        private CompValu GenerateFromRValCall(TokenRValCall call)
        {
            CompValu method;
            CompValu[] argRVals;
            int i, nargs;
            TokenRVal arg;
            TokenType[] argTypes;

             // Compute the values of all the function's call arguments.
             // Save where the computation results are in the argRVals[] array.
             // Might as well build the argument signature from the argument types, too.
            nargs = call.nArgs;
            argRVals = new CompValu[nargs];
            argTypes = new TokenType[nargs];
            if(nargs > 0)
            {
                i = 0;
                for(arg = call.args; arg != null; arg = (TokenRVal)arg.nextToken)
                {
                    argRVals[i] = GenerateFromRVal(arg);
                    argTypes[i] = argRVals[i].type;
                    i++;
                }
            }

             // Get function/method's entrypoint that matches the call argument types.
            method = GenerateFromRVal(call.meth, argTypes);
            if(method == null)
                return null;

            return GenerateACall(method, argRVals, call);
        }

        /**
         * @brief Generate call to a function/method.
         * @param method = function/method being called
         * @param argVRVals = its call parameters (zero length if none)
         * @param call = where in source code call is being made from (for error messages)
         * @returns type and location of return value (CompValuVoid if none)
         */
        private CompValu GenerateACall(CompValu method, CompValu[] argRVals, Token call)
        {
            CompValuTemp result;
            int i, nArgs;
            TokenType retType;
            TokenType[] argTypes;

             // Must be some kind of callable.
            retType = method.GetRetType();  // TokenTypeVoid if void; null means a variable
            if(retType == null)
            {
                ErrorMsg(call, "must be a delegate, function or method");
                return new CompValuVoid(call);
            }

             // Get a location for return value.
            if(retType is TokenTypeVoid)
            {
                result = new CompValuVoid(call);
            }
            else
            {
                result = new CompValuTemp(retType, this);
            }

             // Make sure all arguments are trivial, ie, don't involve their own call labels.
             // For any that aren't, output code to calculate the arg and put in a temporary.
            nArgs = argRVals.Length;
            for(i = 0; i < nArgs; i++)
            {
                if(!argRVals[i].IsReadTrivial(this, call))
                {
                    argRVals[i] = Trivialize(argRVals[i], call);
                }
            }

             // Inline functions know how to generate their own call.
            if(method is CompValuInline)
            {
                CompValuInline inline = (CompValuInline)method;
                inline.declInline.CodeGen(this, call, result, argRVals);
                return result;
            }

             // Push whatever the function/method needs as a this argument, if anything.
            method.CallPre(this, call);

             // Push the script-visible args, left-to-right.
            argTypes = method.GetArgTypes();
            for(i = 0; i < nArgs; i++)
            {
                if(argTypes == null)
                {
                    argRVals[i].PushVal(this, call);
                }
                else
                {
                    argRVals[i].PushVal(this, call, argTypes[i]);
                }
            }

             // Now output call instruction.
            method.CallPost(this, call);

             // Deal with the return value (if any), by putting it in 'result'.
            result.Pop(this, call, retType);
            return result;
        }

        /**
         * @brief This is needed to avoid nesting call labels around non-trivial properties.
         *        It should be used for the second (and later) operands.
         *        Note that a 'call' is considered an operator, so all arguments of a call
         *        should be trivialized, but the method itself does not need to be.
         */
        public CompValu Trivialize(CompValu operand, Token errorAt)
        {
            if(operand.IsReadTrivial(this, errorAt))
                return operand;
            CompValuTemp temp = new CompValuTemp(operand.type, this);
            operand.PushVal(this, errorAt);
            temp.Pop(this, errorAt);
            return temp;
        }

        /**
         * @brief Generate code that casts a value to a particular type.
         * @returns where the result of the conversion is stored.
         */
        private CompValu GenerateFromRValCast(TokenRValCast cast)
        {
             // If casting to a delegate type, use the argment signature 
             // of the delegate to help select the function/method, eg, 
             //    '(delegate string(integer))ToString'
             // will select 'string ToString(integer x)'
             // instaead of 'string ToString(float x)' or anything else
            TokenType[] argsig = null;
            TokenType outType = cast.castTo;
            if(outType is TokenTypeSDTypeDelegate)
            {
                argsig = ((TokenTypeSDTypeDelegate)outType).decl.GetArgTypes();
            }

             // Generate the value that is being cast.
             // If the value is already the requested type, just use it as is.
            CompValu inRVal = GenerateFromRVal(cast.rVal, argsig);
            if(inRVal.type == outType)
                return inRVal;

             // Different type, generate casting code, putting the result in a temp of the output type.
            CompValu outRVal = new CompValuTemp(outType, this);
            outRVal.PopPre(this, cast);
            inRVal.PushVal(this, cast, outType, true);
            outRVal.PopPost(this, cast);
            return outRVal;
        }

        /**
         * @brief Compute conditional expression value.
         * @returns type and location of computed value.
         */
        private CompValu GenerateFromRValCondExpr(TokenRValCondExpr rValCondExpr)
        {
            bool condVal;
            CompValu condValu = GenerateFromRVal(rValCondExpr.condExpr);
            if(IsConstBoolExpr(condValu, out condVal))
            {
                return GenerateFromRVal(condVal ? rValCondExpr.trueExpr : rValCondExpr.falseExpr);
            }

            ScriptMyLabel falseLabel = ilGen.DefineLabel("condexfalse");
            ScriptMyLabel doneLabel = ilGen.DefineLabel("condexdone");

            condValu.PushVal(this, rValCondExpr.condExpr, tokenTypeBool);
            ilGen.Emit(rValCondExpr, OpCodes.Brfalse, falseLabel);

            CompValu trueValu = GenerateFromRVal(rValCondExpr.trueExpr);
            trueValu.PushVal(this, rValCondExpr.trueExpr);
            ilGen.Emit(rValCondExpr, OpCodes.Br, doneLabel);

            ilGen.MarkLabel(falseLabel);
            CompValu falseValu = GenerateFromRVal(rValCondExpr.falseExpr);
            falseValu.PushVal(this, rValCondExpr.falseExpr);

            if(trueValu.type.GetType() != falseValu.type.GetType())
            {
                ErrorMsg(rValCondExpr, "? operands " + trueValu.type.ToString() + " : " +
                              falseValu.type.ToString() + " must be of same type");
            }

            ilGen.MarkLabel(doneLabel);
            CompValuTemp retRVal = new CompValuTemp(trueValu.type, this);
            retRVal.Pop(this, rValCondExpr);
            return retRVal;
        }

        /**
         * @brief Constant in the script somewhere
         * @returns where the constants value is stored
         */
        private CompValu GenerateFromRValConst(TokenRValConst rValConst)
        {
            switch(rValConst.type)
            {
                case TokenRValConstType.CHAR:
                    {
                        return new CompValuChar(new TokenTypeChar(rValConst), (char)(rValConst.val));
                    }
                case TokenRValConstType.FLOAT:
                    {
                        return new CompValuFloat(new TokenTypeFloat(rValConst), (double)(rValConst.val));
                    }
                case TokenRValConstType.INT:
                    {
                        return new CompValuInteger(new TokenTypeInt(rValConst), (int)(rValConst.val));
                    }
                case TokenRValConstType.KEY:
                    {
                        return new CompValuString(new TokenTypeKey(rValConst), (string)(rValConst.val));
                    }
                case TokenRValConstType.STRING:
                    {
                        return new CompValuString(new TokenTypeStr(rValConst), (string)(rValConst.val));
                    }
            }
            throw new Exception("unknown constant type " + rValConst.val.GetType());
        }

        /**
         * @brief generate a new list object
         * @param rValList = an rVal to create it from
         */
        private CompValu GenerateFromRValList(TokenRValList rValList)
        {
             // Compute all element values and remember where we put them.
             // Do it right-to-left as customary for LSL scripts.
            int i = 0;
            TokenRVal lastRVal = null;
            for(TokenRVal val = rValList.rVal; val != null; val = (TokenRVal)val.nextToken)
            {
                i++;
                val.prevToken = lastRVal;
                lastRVal = val;
            }
            CompValu[] vals = new CompValu[i];
            for(TokenRVal val = lastRVal; val != null; val = (TokenRVal)val.prevToken)
            {
                vals[--i] = GenerateFromRVal(val);
            }

             // This is the temp that will hold the created list.
            CompValuTemp newList = new CompValuTemp(new TokenTypeList(rValList.rVal), this);

             // Create a temp object[] array to hold all the initial values.
            ilGen.Emit(rValList, OpCodes.Ldc_I4, rValList.nItems);
            ilGen.Emit(rValList, OpCodes.Newarr, typeof(object));

             // Populate the array.
            i = 0;
            for(TokenRVal val = rValList.rVal; val != null; val = (TokenRVal)val.nextToken)
            {

                 // Get pointer to temp array object.
                ilGen.Emit(rValList, OpCodes.Dup);

                 // Get index in that array.
                ilGen.Emit(rValList, OpCodes.Ldc_I4, i);

                 // Store initialization value in array location.
                 // However, floats and ints need to be converted to LSL_Float and LSL_Integer,
                 // or things like llSetPayPrice() will puque when they try to cast the elements
                 // to LSL_Float or LSL_Integer.  Likewise with string/LSL_String.
                 //
                 // Maybe it's already LSL-boxed so we don't do anything with it except make sure
                 // it is an object, not a struct.
                CompValu eRVal = vals[i++];
                eRVal.PushVal(this, val);
                if(eRVal.type.ToLSLWrapType() == null)
                {
                    if(eRVal.type is TokenTypeFloat)
                    {
                        ilGen.Emit(val, OpCodes.Newobj, lslFloatConstructorInfo);
                        ilGen.Emit(val, OpCodes.Box, typeof(LSL_Float));
                    }
                    else if(eRVal.type is TokenTypeInt)
                    {
                        ilGen.Emit(val, OpCodes.Newobj, lslIntegerConstructorInfo);
                        ilGen.Emit(val, OpCodes.Box, typeof(LSL_Integer));
                    }
                    else if((eRVal.type is TokenTypeKey) || (eRVal.type is TokenTypeStr))
                    {
                        ilGen.Emit(val, OpCodes.Newobj, lslStringConstructorInfo);
                        ilGen.Emit(val, OpCodes.Box, typeof(LSL_String));
                    }
                    else if(eRVal.type.ToSysType().IsValueType)
                    {
                        ilGen.Emit(val, OpCodes.Box, eRVal.type.ToSysType());
                    }
                }
                else if(eRVal.type.ToLSLWrapType().IsValueType)
                {

                    // Convert the LSL value structs to an object of the LSL-boxed type
                    ilGen.Emit(val, OpCodes.Box, eRVal.type.ToLSLWrapType());
                }
                ilGen.Emit(val, OpCodes.Stelem, typeof(object));
            }

             // Create new list object from temp initial value array (whose ref is still on the stack).
            ilGen.Emit(rValList, OpCodes.Newobj, lslListConstructorInfo);
            newList.Pop(this, rValList);
            return newList;
        }

        /**
         * @brief New array allocation with initializer expressions.
         */
        private CompValu GenerateFromRValNewArIni(TokenRValNewArIni rValNewArIni)
        {
            return MallocAndInitArray(rValNewArIni.arrayType, rValNewArIni.valueList);
        }

        /**
         * @brief Mallocate and initialize an array from its initialization list.
         * @param arrayType = type of the array to be allocated and initialized
         * @param values    = initialization value list used to size and initialize the array.
         * @returns memory location of the resultant initialized array.
         */
        private CompValu MallocAndInitArray(TokenType arrayType, TokenList values)
        {
            TokenDeclSDTypeClass arrayDecl = ((TokenTypeSDTypeClass)arrayType).decl;
            TokenType eleType = arrayDecl.arrayOfType;
            int rank = arrayDecl.arrayOfRank;

            // Get size of each of the dimensions by scanning the initialization value list
            int[] dimSizes = new int[rank];
            FillInDimSizes(dimSizes, 0, rank, values);

            // Figure out where the array's $new() method is
            TokenType[] newargsig = new TokenType[rank];
            for(int k = 0; k < rank; k++)
            {
                newargsig[k] = tokenTypeInt;
            }
            TokenDeclVar newMeth = FindThisMember(arrayDecl, new TokenName(null, "$new"), newargsig);

            // Output a call to malloc the array with all default values
            //    array = ArrayType.$new (dimSizes[0], dimSizes[1], ...)
            CompValuTemp array = new CompValuTemp(arrayType, this);
            PushXMRInst();
            for(int k = 0; k < rank; k++)
            {
                ilGen.Emit(values, OpCodes.Ldc_I4, dimSizes[k]);
            }
            ilGen.Emit(values, OpCodes.Call, newMeth.ilGen);
            array.Pop(this, arrayType);

            // Figure out where the array's Set() method is
            TokenType[] setargsig = new TokenType[rank + 1];
            for(int k = 0; k < rank; k++)
            {
                setargsig[k] = tokenTypeInt;
            }
            setargsig[rank] = eleType;
            TokenDeclVar setMeth = FindThisMember(arrayDecl, new TokenName(null, "Set"), setargsig);

            // Fill in the array with the initializer values
            FillInInitVals(array, setMeth, dimSizes, 0, rank, values, eleType);

            // The array is our resultant value
            return array;
        }

        /**
         * @brief Compute an array's dimensions given its initialization value list
         * @param dimSizes = filled in with array's dimensions
         * @param dimNo    = what dimension the 'values' list applies to
         * @param rank     = total number of dimensions of the array
         * @param values   = list of values to initialize the array's 'dimNo' dimension with
         * @returns with dimSizes[dimNo..rank-1] filled in
         */
        private static void FillInDimSizes(int[] dimSizes, int dimNo, int rank, TokenList values)
        {
            // the size of a dimension is the largest number of initializer elements at this level
            // for dimNo 0, this is the number of elements in the top-level list
            if(dimSizes[dimNo] < values.tl.Count)
                dimSizes[dimNo] = values.tl.Count;

            // see if there is another dimension to calculate
            if(++dimNo < rank)
            {

                // its size is the size of the largest initializer list at the next inner level
                foreach(Token val in values.tl)
                {
                    if(val is TokenList)
                    {
                        TokenList subvals = (TokenList)val;
                        FillInDimSizes(dimSizes, dimNo, rank, subvals);
                    }
                }
            }
        }

        /**
         * @brief Output code to fill in array's initialization values
         * @param array      = array to be filled in
         * @param setMeth    = the array's Set() method
         * @param subscripts = holds subscripts being built
         * @param dimNo      = which dimension the 'values' are for
         * @param values     = list of initialization values for dimension 'dimNo'
         * @param rank       = number of dimensions of 'array'
         * @param values     = list of values to initialize the array's 'dimNo' dimension with
         * @param eleType    = the element's type
         * @returns with code emitted to initialize array's [subscripts[0], ..., subscripts[dimNo-1], *, *, ...]
         *                                                          dimNo and up completely filled ---^
         */
        private void FillInInitVals(CompValu array, TokenDeclVar setMeth, int[] subscripts, int dimNo, int rank, TokenList values, TokenType eleType)
        {
            subscripts[dimNo] = 0;
            foreach(Token val in values.tl)
            {
                CompValu initValue = null;

                 // If it is a sublist, process it.
                 //    If we don't have enough subscripts yet, hopefully that sublist will have enough.
                 //    If we already have enough subscripts, then that sublist can be for an element of this supposedly jagged array.
                if(val is TokenList)
                {
                    TokenList sublist = (TokenList)val;
                    if(dimNo + 1 < rank)
                    {
                         // We don't have enough subscripts yet, hopefully the sublist has the rest.
                        FillInInitVals(array, setMeth, subscripts, dimNo + 1, rank, sublist, eleType);
                    }
                    else if((eleType is TokenTypeSDTypeClass) && (((TokenTypeSDTypeClass)eleType).decl.arrayOfType == null))
                    {
                         // If we aren't a jagged array either, we can't do anything with the sublist.
                        ErrorMsg(val, "too many brace levels");
                    }
                    else
                    {
                         // We are a jagged array, so malloc a subarray and initialize it with the sublist.
                         // Then we can use that subarray to fill this array's element.
                        initValue = MallocAndInitArray(eleType, sublist);
                    }
                }

                 // If it is a value expression, then output code to compute the value.
                if(val is TokenRVal)
                {
                    if(dimNo + 1 < rank)
                    {
                        ErrorMsg((Token)val, "not enough brace levels");
                    }
                    else
                    {
                        initValue = GenerateFromRVal((TokenRVal)val);
                    }
                }

                 // If there is an initValue, output "array.Set (subscript[0], subscript[1], ..., initValue)"
                if(initValue != null)
                {
                    array.PushVal(this, val);
                    for(int i = 0; i <= dimNo; i++)
                    {
                        ilGen.Emit(val, OpCodes.Ldc_I4, subscripts[i]);
                    }
                    initValue.PushVal(this, val, eleType);
                    ilGen.Emit(val, OpCodes.Call, setMeth.ilGen);
                }

                 // That subscript is processed one way or another, on to the next.
                subscripts[dimNo]++;
            }
        }

        /**
         * @brief parenthesized expression
         * @returns type and location of the result of the computation.
         */
        private CompValu GenerateFromRValParen(TokenRValParen rValParen)
        {
            return GenerateFromRVal(rValParen.rVal);
        }

        /**
         * @brief create a rotation object from the x,y,z,w value expressions.
         */
        private CompValu GenerateFromRValRot(TokenRValRot rValRot)
        {
            CompValu xRVal, yRVal, zRVal, wRVal;

            xRVal = Trivialize(GenerateFromRVal(rValRot.xRVal), rValRot);
            yRVal = Trivialize(GenerateFromRVal(rValRot.yRVal), rValRot);
            zRVal = Trivialize(GenerateFromRVal(rValRot.zRVal), rValRot);
            wRVal = Trivialize(GenerateFromRVal(rValRot.wRVal), rValRot);
            return new CompValuRot(new TokenTypeRot(rValRot), xRVal, yRVal, zRVal, wRVal);
        }

        /**
         * @brief Using 'this' as a pointer to the current script-defined instance object.
         *        The value is located in arg #0 of the current instance method.
         */
        private CompValu GenerateFromRValThis(TokenRValThis zhis)
        {
            if(!IsSDTInstMethod())
            {
                ErrorMsg(zhis, "cannot access instance member of class from static method");
                return new CompValuVoid(zhis);
            }
            return new CompValuArg(curDeclFunc.sdtClass.MakeRefToken(zhis), 0);
        }

        /**
         * @brief 'undefined' constant.
         *        If this constant gets written to an array element, it will delete that element from the array.
         *        If the script retrieves an element by key that is not defined, it will get this value.
         *        This value can be stored in and retrieved from variables of type 'object' or script-defined classes.
         *        It is a runtime error to cast this value to any other type, eg, 
         *        we don't allow list or string variables to be null pointers.
         */
        private CompValu GenerateFromRValUndef(TokenRValUndef rValUndef)
        {
            return new CompValuNull(new TokenTypeUndef(rValUndef));
        }

        /**
         * @brief create a vector object from the x,y,z value expressions.
         */
        private CompValu GenerateFromRValVec(TokenRValVec rValVec)
        {
            CompValu xRVal, yRVal, zRVal;

            xRVal = Trivialize(GenerateFromRVal(rValVec.xRVal), rValVec);
            yRVal = Trivialize(GenerateFromRVal(rValVec.yRVal), rValVec);
            zRVal = Trivialize(GenerateFromRVal(rValVec.zRVal), rValVec);
            return new CompValuVec(new TokenTypeVec(rValVec), xRVal, yRVal, zRVal);
        }

        /**
         * @brief Generate code to get the default initialization value for a variable.
         */
        private CompValu GenerateFromRValInitDef(TokenRValInitDef rValInitDef)
        {
            TokenType type = rValInitDef.type;

            if(type is TokenTypeChar)
            {
                return new CompValuChar(type, (char)0);
            }
            if(type is TokenTypeRot)
            {
                CompValuFloat x = new CompValuFloat(type, ScriptBaseClass.ZERO_ROTATION.x);
                CompValuFloat y = new CompValuFloat(type, ScriptBaseClass.ZERO_ROTATION.y);
                CompValuFloat z = new CompValuFloat(type, ScriptBaseClass.ZERO_ROTATION.z);
                CompValuFloat s = new CompValuFloat(type, ScriptBaseClass.ZERO_ROTATION.s);
                return new CompValuRot(type, x, y, z, s);
            }
            if((type is TokenTypeKey) || (type is TokenTypeStr))
            {
                return new CompValuString(type, "");
            }
            if(type is TokenTypeVec)
            {
                CompValuFloat x = new CompValuFloat(type, ScriptBaseClass.ZERO_VECTOR.x);
                CompValuFloat y = new CompValuFloat(type, ScriptBaseClass.ZERO_VECTOR.y);
                CompValuFloat z = new CompValuFloat(type, ScriptBaseClass.ZERO_VECTOR.z);
                return new CompValuVec(type, x, y, z);
            }
            if(type is TokenTypeInt)
            {
                return new CompValuInteger(type, 0);
            }
            if(type is TokenTypeFloat)
            {
                return new CompValuFloat(type, 0);
            }
            if(type is TokenTypeVoid)
            {
                return new CompValuVoid(type);
            }

             // Default for 'object' type is 'undef'.
             // Likewise for script-defined classes and interfaces.
            if((type is TokenTypeObject) || (type is TokenTypeSDTypeClass) || (type is TokenTypeSDTypeDelegate) ||
                (type is TokenTypeSDTypeInterface) || (type is TokenTypeExc))
            {
                return new CompValuNull(type);
            }

             // array and list
            CompValuTemp temp = new CompValuTemp(type, this);
            PushDefaultValue(type);
            temp.Pop(this, rValInitDef, type);
            return temp;
        }

        /**
         * @brief Generate code to process an <rVal> is <type> expression, and produce a boolean value.
         */
        private CompValu GenerateFromRValIsType(TokenRValIsType rValIsType)
        {
             // Expression we want to know the type of.
            CompValu val = GenerateFromRVal(rValIsType.rValExp);

             // Pass it in to top-level type expression decoder.
            return GenerateFromTypeExp(val, rValIsType.typeExp);
        }

        /**
         * @brief See if the type of the given value matches the type expression.
         * @param val = where the value to be evaluated is stored
         * @param typeExp = script tokens representing type expression
         * @returns location where the boolean result is stored
         */
        private CompValu GenerateFromTypeExp(CompValu val, TokenTypeExp typeExp)
        {
            if(typeExp is TokenTypeExpBinOp)
            {
                CompValu left = GenerateFromTypeExp(val, ((TokenTypeExpBinOp)typeExp).leftOp);
                CompValu right = GenerateFromTypeExp(val, ((TokenTypeExpBinOp)typeExp).rightOp);
                CompValuTemp result = new CompValuTemp(tokenTypeBool, this);
                Token op = ((TokenTypeExpBinOp)typeExp).binOp;
                left.PushVal(this, ((TokenTypeExpBinOp)typeExp).leftOp);
                right.PushVal(this, ((TokenTypeExpBinOp)typeExp).rightOp);
                if(op is TokenKwAnd)
                {
                    ilGen.Emit(typeExp, OpCodes.And);
                }
                else if(op is TokenKwOr)
                {
                    ilGen.Emit(typeExp, OpCodes.Or);
                }
                else
                {
                    throw new Exception("unknown TokenTypeExpBinOp " + op.GetType());
                }
                result.Pop(this, typeExp);
                return result;
            }
            if(typeExp is TokenTypeExpNot)
            {
                CompValu interm = GenerateFromTypeExp(val, ((TokenTypeExpNot)typeExp).typeExp);
                CompValuTemp result = new CompValuTemp(tokenTypeBool, this);
                interm.PushVal(this, ((TokenTypeExpNot)typeExp).typeExp, tokenTypeBool);
                ilGen.Emit(typeExp, OpCodes.Ldc_I4_1);
                ilGen.Emit(typeExp, OpCodes.Xor);
                result.Pop(this, typeExp);
                return result;
            }
            if(typeExp is TokenTypeExpPar)
            {
                return GenerateFromTypeExp(val, ((TokenTypeExpPar)typeExp).typeExp);
            }
            if(typeExp is TokenTypeExpType)
            {
                CompValuTemp result = new CompValuTemp(tokenTypeBool, this);
                val.PushVal(this, typeExp);
                ilGen.Emit(typeExp, OpCodes.Isinst, ((TokenTypeExpType)typeExp).typeToken.ToSysType());
                ilGen.Emit(typeExp, OpCodes.Ldnull);
                ilGen.Emit(typeExp, OpCodes.Ceq);
                ilGen.Emit(typeExp, OpCodes.Ldc_I4_1);
                ilGen.Emit(typeExp, OpCodes.Xor);
                result.Pop(this, typeExp);
                return result;
            }
            if(typeExp is TokenTypeExpUndef)
            {
                CompValuTemp result = new CompValuTemp(tokenTypeBool, this);
                val.PushVal(this, typeExp);
                ilGen.Emit(typeExp, OpCodes.Ldnull);
                ilGen.Emit(typeExp, OpCodes.Ceq);
                result.Pop(this, typeExp);
                return result;
            }
            throw new Exception("unknown TokenTypeExp type " + typeExp.GetType());
        }

        /**
         * @brief Push the default (null) value for a particular variable
         * @param var = variable to get the default value for
         * @returns with value pushed on stack
         */
        public void PushVarDefaultValue(TokenDeclVar var)
        {
            PushDefaultValue(var.type);
        }
        public void PushDefaultValue(TokenType type)
        {
            if(type is TokenTypeArray)
            {
                PushXMRInst();                // instance
                ilGen.Emit(type, OpCodes.Newobj, xmrArrayConstructorInfo);
                return;
            }
            if(type is TokenTypeChar)
            {
                ilGen.Emit(type, OpCodes.Ldc_I4_0);
                return;
            }
            if(type is TokenTypeList)
            {
                ilGen.Emit(type, OpCodes.Ldc_I4_0);
                ilGen.Emit(type, OpCodes.Newarr, typeof(object));
                ilGen.Emit(type, OpCodes.Newobj, lslListConstructorInfo);
                return;
            }
            if(type is TokenTypeRot)
            {
                // Mono is tOO stOOpid to allow: ilGen.Emit (OpCodes.Ldsfld, zeroRotationFieldInfo);
                ilGen.Emit(type, OpCodes.Ldc_R8, ScriptBaseClass.ZERO_ROTATION.x);
                ilGen.Emit(type, OpCodes.Ldc_R8, ScriptBaseClass.ZERO_ROTATION.y);
                ilGen.Emit(type, OpCodes.Ldc_R8, ScriptBaseClass.ZERO_ROTATION.z);
                ilGen.Emit(type, OpCodes.Ldc_R8, ScriptBaseClass.ZERO_ROTATION.s);
                ilGen.Emit(type, OpCodes.Newobj, lslRotationConstructorInfo);
                return;
            }
            if((type is TokenTypeKey) || (type is TokenTypeStr))
            {
                ilGen.Emit(type, OpCodes.Ldstr, "");
                return;
            }
            if(type is TokenTypeVec)
            {
                // Mono is tOO stOOpid to allow: ilGen.Emit (OpCodes.Ldsfld, zeroVectorFieldInfo);
                ilGen.Emit(type, OpCodes.Ldc_R8, ScriptBaseClass.ZERO_VECTOR.x);
                ilGen.Emit(type, OpCodes.Ldc_R8, ScriptBaseClass.ZERO_VECTOR.y);
                ilGen.Emit(type, OpCodes.Ldc_R8, ScriptBaseClass.ZERO_VECTOR.z);
                ilGen.Emit(type, OpCodes.Newobj, lslVectorConstructorInfo);
                return;
            }
            if(type is TokenTypeInt)
            {
                ilGen.Emit(type, OpCodes.Ldc_I4_0);
                return;
            }
            if(type is TokenTypeFloat)
            {
                ilGen.Emit(type, OpCodes.Ldc_R4, 0.0f);
                return;
            }

             // Default for 'object' type is 'undef'.
             // Likewise for script-defined classes and interfaces.
            if((type is TokenTypeObject) || (type is TokenTypeSDTypeClass) || (type is TokenTypeSDTypeInterface) || (type is TokenTypeExc))
            {
                ilGen.Emit(type, OpCodes.Ldnull);
                return;
            }

             // Void is pushed as the default return value of a void function.
             // So just push nothing as expected of void functions.
            if(type is TokenTypeVoid)
            {
                return;
            }

             // Default for 'delegate' type is 'undef'.
            if(type is TokenTypeSDTypeDelegate)
            {
                ilGen.Emit(type, OpCodes.Ldnull);
                return;
            }

            throw new Exception("unknown type " + type.GetType().ToString());
        }

        /**
         * @brief Determine if the expression has a constant boolean value
         *        and if so, if the value is true or false.
         * @param expr = expression to evaluate
         * @returns true: expression is contant and has boolean value true
         *         false: otherwise
         */
        private bool IsConstBoolExprTrue(CompValu expr)
        {
            bool constVal;
            return IsConstBoolExpr(expr, out constVal) && constVal;
        }

        private bool IsConstBoolExpr(CompValu expr, out bool constVal)
        {
            if(expr is CompValuChar)
            {
                constVal = ((CompValuChar)expr).x != 0;
                return true;
            }
            if(expr is CompValuFloat)
            {
                constVal = ((CompValuFloat)expr).x != (double)0;
                return true;
            }
            if(expr is CompValuInteger)
            {
                constVal = ((CompValuInteger)expr).x != 0;
                return true;
            }
            if(expr is CompValuString)
            {
                string s = ((CompValuString)expr).x;
                constVal = s != "";
                if(constVal && (expr.type is TokenTypeKey))
                {
                    constVal = s != ScriptBaseClass.NULL_KEY;
                }
                return true;
            }

            constVal = false;
            return false;
        }

        /**
         * @brief Determine if the expression has a constant integer value
         *        and if so, return the integer value.
         * @param expr = expression to evaluate
         * @returns true: expression is contant and has integer value
         *         false: otherwise
         */
        private bool IsConstIntExpr(CompValu expr, out int constVal)
        {
            if(expr is CompValuChar)
            {
                constVal = (int)((CompValuChar)expr).x;
                return true;
            }
            if(expr is CompValuInteger)
            {
                constVal = ((CompValuInteger)expr).x;
                return true;
            }

            constVal = 0;
            return false;
        }

        /**
         * @brief Determine if the expression has a constant string value
         *        and if so, return the string value.
         * @param expr = expression to evaluate
         * @returns true: expression is contant and has string value
         *         false: otherwise
         */
        private bool IsConstStrExpr(CompValu expr, out string constVal)
        {
            if(expr is CompValuString)
            {
                constVal = ((CompValuString)expr).x;
                return true;
            }
            constVal = "";
            return false;
        }

        /**
         * @brief create table of legal event handler prototypes.
         *        This is used to make sure script's event handler declrations are valid.
         */
        private static VarDict CreateLegalEventHandlers()
        {
             // Get handler prototypes with full argument lists.
            VarDict leh = new InternalFuncDict(typeof(IEventHandlers), false);

             // We want the scripts to be able to declare their handlers with
             // fewer arguments than the full argument lists.  So define additional 
             // prototypes with fewer arguments.
            TokenDeclVar[] fullArgProtos = new TokenDeclVar[leh.Count];
            int i = 0;
            foreach(TokenDeclVar fap in leh)
                fullArgProtos[i++] = fap;

            foreach(TokenDeclVar fap in fullArgProtos)
            {
                TokenArgDecl fal = fap.argDecl;
                int fullArgCount = fal.vars.Length;
                for(i = 0; i < fullArgCount; i++)
                {
                    TokenArgDecl shortArgList = new TokenArgDecl(null);
                    for(int j = 0; j < i; j++)
                    {
                        TokenDeclVar var = fal.vars[j];
                        shortArgList.AddArg(var.type, var.name);
                    }
                    TokenDeclVar shortArgProto = new TokenDeclVar(null, null, null);
                    shortArgProto.name = new TokenName(null, fap.GetSimpleName());
                    shortArgProto.retType = fap.retType;
                    shortArgProto.argDecl = shortArgList;
                    leh.AddEntry(shortArgProto);
                }
            }

            return leh;
        }

        /**
         * @brief Emit a call to CheckRun(), (voluntary multitasking switch)
         */
        public void EmitCallCheckRun(Token errorAt, bool stack)
        {
            if(curDeclFunc.IsFuncTrivial(this))
                throw new Exception(curDeclFunc.fullName + " is supposed to be trivial");
            new CallLabel(this, errorAt);                               // jump here when stack restored
            PushXMRInst();                                              // instance
            ilGen.Emit(errorAt, OpCodes.Call, stack ? checkRunStackMethInfo : checkRunQuickMethInfo);
            openCallLabel = null;
        }

        /**
         * @brief Emit code to push a callNo var on the stack.
         */
        public void GetCallNo(Token errorAt, ScriptMyLocal callNoVar)
        {
            ilGen.Emit(errorAt, OpCodes.Ldloc, callNoVar);
            //ilGen.Emit (errorAt, OpCodes.Ldloca, callNoVar);
            //ilGen.Emit (errorAt, OpCodes.Volatile);
            //ilGen.Emit (errorAt, OpCodes.Ldind_I4);
        }
        public void GetCallNo(Token errorAt, CompValu callNoVar)
        {
            callNoVar.PushVal(this, errorAt);
            //callNoVar.PushRef (this, errorAt);
            //ilGen.Emit (errorAt, OpCodes.Volatile);
            //ilGen.Emit (errorAt, OpCodes.Ldind_I4);
        }

        /**
         * @brief Emit code to set a callNo var to a given constant.
         */
        public void SetCallNo(Token errorAt, ScriptMyLocal callNoVar, int val)
        {
            ilGen.Emit(errorAt, OpCodes.Ldc_I4, val);
            ilGen.Emit(errorAt, OpCodes.Stloc, callNoVar);
            //ilGen.Emit (errorAt, OpCodes.Ldloca, callNoVar);
            //ilGen.Emit (errorAt, OpCodes.Ldc_I4, val);
            //ilGen.Emit (errorAt, OpCodes.Volatile);
            //ilGen.Emit (errorAt, OpCodes.Stind_I4);
        }
        public void SetCallNo(Token errorAt, CompValu callNoVar, int val)
        {
            callNoVar.PopPre(this, errorAt);
            ilGen.Emit(errorAt, OpCodes.Ldc_I4, val);
            callNoVar.PopPost(this, errorAt);
            //callNoVar.PushRef (this, errorAt);
            //ilGen.Emit (errorAt, OpCodes.Ldc_I4, val);
            //ilGen.Emit (errorAt, OpCodes.Volatile);
            //ilGen.Emit (errorAt, OpCodes.Stind_I4);
        }

        /**
         * @brief handle a unary operator, such as -x.
         */
        private CompValu UnOpGenerate(CompValu inRVal, Token opcode)
        {
             // - Negate
            if(opcode is TokenKwSub)
            {
                if(inRVal.type is TokenTypeFloat)
                {
                    CompValuTemp outRVal = new CompValuTemp(new TokenTypeFloat(opcode), this);
                    inRVal.PushVal(this, opcode, outRVal.type);  // push value to negate, make sure not LSL-boxed
                    ilGen.Emit(opcode, OpCodes.Neg);     // compute the negative
                    outRVal.Pop(this, opcode);           // pop into result
                    return outRVal;                       // tell caller where we put it
                }
                if(inRVal.type is TokenTypeInt)
                {
                    CompValuTemp outRVal = new CompValuTemp(new TokenTypeInt(opcode), this);
                    inRVal.PushVal(this, opcode, outRVal.type);  // push value to negate, make sure not LSL-boxed
                    ilGen.Emit(opcode, OpCodes.Neg);     // compute the negative
                    outRVal.Pop(this, opcode);           // pop into result
                    return outRVal;                       // tell caller where we put it
                }
                if(inRVal.type is TokenTypeRot)
                {
                    CompValuTemp outRVal = new CompValuTemp(inRVal.type, this);
                    inRVal.PushVal(this, opcode);        // push rotation, then call negate routine
                    ilGen.Emit(opcode, OpCodes.Call, lslRotationNegateMethodInfo);
                    outRVal.Pop(this, opcode);           // pop into result
                    return outRVal;                       // tell caller where we put it
                }
                if(inRVal.type is TokenTypeVec)
                {
                    CompValuTemp outRVal = new CompValuTemp(inRVal.type, this);
                    inRVal.PushVal(this, opcode);        // push vector, then call negate routine
                    ilGen.Emit(opcode, OpCodes.Call, lslVectorNegateMethodInfo);
                    outRVal.Pop(this, opcode);           // pop into result
                    return outRVal;                       // tell caller where we put it
                }
                ErrorMsg(opcode, "can't negate a " + inRVal.type.ToString());
                return inRVal;
            }

             // ~ Complement (bitwise integer)
            if(opcode is TokenKwTilde)
            {
                if(inRVal.type is TokenTypeInt)
                {
                    CompValuTemp outRVal = new CompValuTemp(new TokenTypeInt(opcode), this);
                    inRVal.PushVal(this, opcode, outRVal.type);  // push value to negate, make sure not LSL-boxed
                    ilGen.Emit(opcode, OpCodes.Not);     // compute the complement
                    outRVal.Pop(this, opcode);           // pop into result
                    return outRVal;                       // tell caller where we put it
                }
                ErrorMsg(opcode, "can't complement a " + inRVal.type.ToString());
                return inRVal;
            }

             // ! Not (boolean)
             //
             // We stuff the 0/1 result in an int because I've seen x+!y in scripts
             // and we don't want to have to create tables to handle int+bool and
             // everything like that.
            if(opcode is TokenKwExclam)
            {
                CompValuTemp outRVal = new CompValuTemp(new TokenTypeInt(opcode), this);
                inRVal.PushVal(this, opcode, tokenTypeBool);  // anything converts to boolean
                ilGen.Emit(opcode, OpCodes.Ldc_I4_1);         // then XOR with 1 to flip it
                ilGen.Emit(opcode, OpCodes.Xor);
                outRVal.Pop(this, opcode);                    // pop into result
                return outRVal;                                // tell caller where we put it
            }

            throw new Exception("unhandled opcode " + opcode.ToString());
        }

        /**
         * @brief This is called while trying to compute the value of constant initializers.
         *        It is passed a name and that name is looked up in the constant tables.
         */
        private TokenRVal LookupInitConstants(TokenRVal rVal, ref bool didOne)
        {
             // If it is a static field of a script-defined type, look it up and hopefully we find a constant there.
            TokenDeclVar gblVar;
            if(rVal is TokenLValSField)
            {
                TokenLValSField lvsf = (TokenLValSField)rVal;
                if(lvsf.baseType is TokenTypeSDTypeClass)
                {
                    TokenDeclSDTypeClass sdtClass = ((TokenTypeSDTypeClass)lvsf.baseType).decl;
                    gblVar = sdtClass.members.FindExact(lvsf.fieldName.val, null);
                    if(gblVar != null)
                    {
                        if(gblVar.constant && (gblVar.init is TokenRValConst))
                        {
                            didOne = true;
                            return gblVar.init;
                        }
                    }
                }
                return rVal;
            }

             // Only other thing we handle is stand-alone names.
            if(!(rVal is TokenLValName))
                return rVal;
            string name = ((TokenLValName)rVal).name.val;

             // If we are doing the initializations for a script-defined type,
             // look for the constant among the fields for that type.
            if(currentSDTClass != null)
            {
                gblVar = currentSDTClass.members.FindExact(name, null);
                if(gblVar != null)
                {
                    if(gblVar.constant && (gblVar.init is TokenRValConst))
                    {
                        didOne = true;
                        return gblVar.init;
                    }
                    return rVal;
                }
            }

             // Look it up as a script-defined global variable.
             // Then if the variable is defined as a constant and has a constant value,
             // we are successful.  If it is defined as something else, return failure.
            gblVar = tokenScript.variablesStack.FindExact(name, null);
            if(gblVar != null)
            {
                if(gblVar.constant && (gblVar.init is TokenRValConst))
                {
                    didOne = true;
                    return gblVar.init;
                }
                return rVal;
            }

             // Maybe it is a built-in symbolic constant.
            ScriptConst scriptConst = ScriptConst.Lookup(name);
            if(scriptConst != null)
            {
                rVal = CompValuConst2RValConst(scriptConst.rVal, rVal);
                if(rVal is TokenRValConst)
                {
                    didOne = true;
                    return rVal;
                }
            }

             // Don't know what it is, return failure.
            return rVal;
        }

        /**
         * @brief This is called while trying to compute the value of constant expressions.
         *        It is passed a name and that name is looked up in the constant tables.
         */
        private TokenRVal LookupBodyConstants(TokenRVal rVal, ref bool didOne)
        {
             // If it is a static field of a script-defined type, look it up and hopefully we find a constant there.
            TokenDeclVar gblVar;
            if(rVal is TokenLValSField)
            {
                TokenLValSField lvsf = (TokenLValSField)rVal;
                if(lvsf.baseType is TokenTypeSDTypeClass)
                {
                    TokenDeclSDTypeClass sdtClass = ((TokenTypeSDTypeClass)lvsf.baseType).decl;
                    gblVar = sdtClass.members.FindExact(lvsf.fieldName.val, null);
                    if((gblVar != null) && gblVar.constant && (gblVar.init is TokenRValConst))
                    {
                        didOne = true;
                        return gblVar.init;
                    }
                }
                return rVal;
            }

             // Only other thing we handle is stand-alone names.
            if(!(rVal is TokenLValName))
                return rVal;
            string name = ((TokenLValName)rVal).name.val;

             // Scan through the variable stack and hopefully we find a constant there.
             // But we stop as soon as we get a match because that's what the script is referring to.
            CompValu val;
            for(VarDict vars = ((TokenLValName)rVal).stack; vars != null; vars = vars.outerVarDict)
            {
                TokenDeclVar var = vars.FindExact(name, null);
                if(var != null)
                {
                    val = var.location;
                    goto foundit;
                }

                TokenDeclSDTypeClass baseClass = vars.thisClass;
                if(baseClass != null)
                {
                    while((baseClass = baseClass.extends) != null)
                    {
                        var = baseClass.members.FindExact(name, null);
                        if(var != null)
                        {
                            val = var.location;
                            goto foundit;
                        }
                    }
                }
            }

             // Maybe it is a built-in symbolic constant.
            ScriptConst scriptConst = ScriptConst.Lookup(name);
            if(scriptConst != null)
            {
                val = scriptConst.rVal;
                goto foundit;
            }

             // Don't know what it is, return failure.
            return rVal;

             // Found a CompValu.  If it's a simple constant, then use it.
             // Otherwise tell caller we failed to simplify.
            foundit:
            rVal = CompValuConst2RValConst(val, rVal);
            if(rVal is TokenRValConst)
            {
                didOne = true;
            }
            return rVal;
        }

        private static TokenRVal CompValuConst2RValConst(CompValu val, TokenRVal rVal)
        {
            if(val is CompValuChar)
                rVal = new TokenRValConst(rVal, ((CompValuChar)val).x);
            if(val is CompValuFloat)
                rVal = new TokenRValConst(rVal, ((CompValuFloat)val).x);
            if(val is CompValuInteger)
                rVal = new TokenRValConst(rVal, ((CompValuInteger)val).x);
            if(val is CompValuString)
                rVal = new TokenRValConst(rVal, ((CompValuString)val).x);
            return rVal;
        }

        /**
         * @brief Generate code to push XMRInstanceSuperType pointer on stack.
         */
        public void PushXMRInst()
        {
            if(instancePointer == null)
            {
                ilGen.Emit(null, OpCodes.Ldarg_0);
            }
            else
            {
                ilGen.Emit(null, OpCodes.Ldloc, instancePointer);
            }
        }

        /**
         * @returns true: Ldarg_0 gives XMRSDTypeClObj pointer
         *                - this is the case for instance methods
         *         false: Ldarg_0 gives XMR_Instance pointer
         *                - this is the case for both global functions and static methods
         */
        public bool IsSDTInstMethod()
        {
            return (curDeclFunc.sdtClass != null) &&
                   ((curDeclFunc.sdtFlags & ScriptReduce.SDT_STATIC) == 0);
        }

        /**
         * @brief Look for a simply named function or variable (not a field or method)
         */
        public TokenDeclVar FindNamedVar(TokenLValName lValName, TokenType[] argsig)
        {
             // Look in variable stack for the given name.
            for(VarDict vars = lValName.stack; vars != null; vars = vars.outerVarDict)
            {

                // first look for it possibly with an argument signature
                // so we pick the correct overloaded method
                TokenDeclVar var = FindSingleMember(vars, lValName.name, argsig);
                if(var != null)
                    return var;

                // if that fails, try it without the argument signature.
                // delegates get entered like any other variable, ie, 
                // no signature on their name.
                if(argsig != null)
                {
                    var = FindSingleMember(vars, lValName.name, null);
                    if(var != null)
                        return var;
                }

                // if this is the frame for some class members, try searching base class members too
                TokenDeclSDTypeClass baseClass = vars.thisClass;
                if(baseClass != null)
                {
                    while((baseClass = baseClass.extends) != null)
                    {
                        var = FindSingleMember(baseClass.members, lValName.name, argsig);
                        if(var != null)
                            return var;
                        if(argsig != null)
                        {
                            var = FindSingleMember(baseClass.members, lValName.name, null);
                            if(var != null)
                                return var;
                        }
                    }
                }
            }

             // If not found, try one of the built-in constants or functions.
            if(argsig == null)
            {
                ScriptConst scriptConst = ScriptConst.Lookup(lValName.name.val);
                if(scriptConst != null)
                {
                    TokenDeclVar var = new TokenDeclVar(lValName.name, null, tokenScript);
                    var.name = lValName.name;
                    var.type = scriptConst.rVal.type;
                    var.location = scriptConst.rVal;
                    return var;
                }
            }
            else
            {
                TokenDeclVar inline = FindSingleMember(TokenDeclInline.inlineFunctions, lValName.name, argsig);
                if(inline != null)
                    return inline;
            }

            return null;
        }


        /**
         * @brief Find a member of an interface.
         * @param sdType = interface type
         * @param name = name of member to find
         * @param argsig = null: field/property; else: script-visible method argument types
         * @param baseRVal = pointer to interface object
         * @returns null: no such member
         *          else: pointer to member
         *                baseRVal = possibly modified to point to type-casted interface object
         */
        private TokenDeclVar FindInterfaceMember(TokenTypeSDTypeInterface sdtType, TokenName name, TokenType[] argsig, ref CompValu baseRVal)
        {
            TokenDeclSDTypeInterface sdtDecl = sdtType.decl;
            TokenDeclSDTypeInterface impl;
            TokenDeclVar declVar = sdtDecl.FindIFaceMember(this, name, argsig, out impl);
            if((declVar != null) && (impl != sdtDecl))
            {
                 // Accessing a method or propterty of another interface that the primary interface says it implements.
                 // In this case, we have to cast from the primary interface to that secondary interface.
                 //
                 // interface IEnumerable {
                 //     IEnumerator GetEnumerator ();
                 // }
                 // interface ICountable : IEnumerable {
                 //     integer GetCount ();
                 // }
                 // class List : ICountable {
                 //     public GetCount () : ICountable { ... }
                 //     public GetEnumerator () : IEnumerable { ... }
                 // }
                 //
                 //     ICountable aList = new List ();
                 //     IEnumerator anEnumer = aList.GetEnumerator ();   << we are here
                 //                                                      << baseRVal = aList
                 //                                                      << sdtDecl = ICountable
                 //                                                      << impl = IEnumerable
                 //                                                      << name = GetEnumerator
                 //                                                      << argsig = ()
                 // So we have to cast aList from ICountable to IEnumerable.

                // make type token for the secondary interface type
                TokenType subIntfType = impl.MakeRefToken(name);

                // make a temp variable of the secondary interface type
                CompValuTemp castBase = new CompValuTemp(subIntfType, this);

                // output code to cast from the primary interface to the secondary interface
                // this is 2 basic steps:
                // 1) cast from primary interface object -> class object
                //    ...gets it from interfaceObject.delegateArray[0].Target
                // 2) cast from class object -> secondary interface object
                //    ...gets it from classObject.sdtcITable[interfaceIndex]
                baseRVal.PushVal(this, name, subIntfType);

                // save result of casting in temp
                castBase.Pop(this, name);

                // return temp reference
                baseRVal = castBase;
            }

            return declVar;
        }

        /**
         * @brief Find a member of a script-defined type class.
         * @param sdtType = reference to class declaration
         * @param name = name of member to find
         * @param argsig = argument signature used to select among overloaded members
         * @returns null: no such member found
         *          else: the member found
         */
        public TokenDeclVar FindThisMember(TokenTypeSDTypeClass sdtType, TokenName name, TokenType[] argsig)
        {
            return FindThisMember(sdtType.decl, name, argsig);
        }
        public TokenDeclVar FindThisMember(TokenDeclSDTypeClass sdtDecl, TokenName name, TokenType[] argsig)
        {
            for(TokenDeclSDTypeClass sdtd = sdtDecl; sdtd != null; sdtd = sdtd.extends)
            {
                TokenDeclVar declVar = FindSingleMember(sdtd.members, name, argsig);
                if(declVar != null)
                    return declVar;
            }
            return null;
        }

        /**
         * @brief Look for a single member that matches the given name and argument signature
         * @param where = which dictionary to look in
         * @param name = basic name of the field or method, eg, "Printable"
         * @param argsig = argument types the method is being called with, eg, "(string)"
         *                 or null to find a field
         * @returns null: no member found
         *          else: the member found
         */
        public TokenDeclVar FindSingleMember(VarDict where, TokenName name, TokenType[] argsig)
        {
            TokenDeclVar[] members = where.FindCallables(name.val, argsig);
            if(members == null)
                return null;
            if(members.Length > 1)
            {
                ErrorMsg(name, "more than one matching member");
                for(int i = 0; i < members.Length; i++)
                {
                    ErrorMsg(members[i], "  " + members[i].argDecl.GetArgSig());
                }
            }
            return members[0];
        }

        /**
         * @brief Find an exact function name and argument signature match.
         *        Also verify that the return value type is an exact match.
         * @param where = which method dictionary to look in
         * @param name = basic name of the method, eg, "Printable"
         * @param ret = expected return value type
         * @param argsig = argument types the method is being called with, eg, "(string)"
         * @returns null: no exact match found
         *          else: the matching function
         */
        private TokenDeclVar FindExactWithRet(VarDict where, TokenName name, TokenType ret, TokenType[] argsig)
        {
            TokenDeclVar func = where.FindExact(name.val, argsig);
            if((func != null) && (func.retType.ToString() != ret.ToString()))
            {
                ErrorMsg(name, "return type mismatch, have " + func.retType.ToString() + ", expect " + ret.ToString());
            }
            if(func != null)
                CheckAccess(func, name);
            return func;
        }

        /**
         * @brief Check the private/protected/public access flags of a member.
         */
        private void CheckAccess(TokenDeclVar var, Token errorAt)
        {
            TokenDeclSDType nested;
            TokenDeclSDType definedBy = var.sdtClass;
            TokenDeclSDType accessedBy = curDeclFunc.sdtClass;

            //*******************************
            //  Check member-level access
            //*******************************

             // Note that if accessedBy is null, ie, accessing from global function (or event handlers),
             // anything tagged as SDT_PRIVATE or SDT_PROTECTED will fail.

             // Private means accessed by the class that defined the member or accessed by a nested class
             // of the class that defined the member.
            if((var.sdtFlags & ScriptReduce.SDT_PRIVATE) != 0)
            {
                for(nested = accessedBy; nested != null; nested = nested.outerSDType)
                {
                    if(nested == definedBy)
                        goto acc1ok;
                }
                ErrorMsg(errorAt, "private member " + var.fullName + " cannot be accessed by " + curDeclFunc.fullName);
                return;
            }

             // Protected means:
             //   If being accessed by an inner class, the inner class has access to it if the inner class derives 
             //   from the declaring class.  It also has access to it if an outer class derives from the declaring 
             //   class.
            if((var.sdtFlags & ScriptReduce.SDT_PROTECTED) != 0)
            {
                for(nested = accessedBy; nested != null; nested = nested.outerSDType)
                {
                    for(TokenDeclSDType rootward = nested; rootward != null; rootward = rootward.extends)
                    {
                        if(rootward == definedBy)
                            goto acc1ok;
                    }
                }
                ErrorMsg(errorAt, "protected member " + var.fullName + " cannot be accessed by " + curDeclFunc.fullName);
                return;
            }
            acc1ok:

             //******************************
             //  Check class-level access
             //******************************

             // If being accessed by same or inner class than where defined, it is ok.
             //
             //      class DefiningClass {
             //          varBeingAccessed;
             //                         .
             //                         .
             //                         .
             //                  class AccessingClass {
             //                      functionDoingAccess() { }
             //                  }
             //                         .
             //                         .
             //                         .
             //      }
            nested = accessedBy;
            while(true)
            {
                if(nested == definedBy)
                    return;
                if(nested == null)
                    break;
                nested = (TokenDeclSDTypeClass)nested.outerSDType;
            }

             // It is being accessed by an outer class than where defined, 
             // check for a 'private' or 'protected' class tag that blocks.
            do
            {
                 // If the field's class is defined directly inside the accessing class,
                 // access is allowed regardless of class-level private or protected tags.
                 //
                 //      class AccessingClass {
                 //          functionDoingAccess() { }
                 //          class DefiningClass {
                 //              varBeingAccessed;
                 //          }
                 //      }
                if(definedBy.outerSDType == accessedBy)
                    return;

                 // If the field's class is defined two or more levels inside the accessing class, 
                 // access is denied if the defining class is tagged private.
                 //
                 //      class AccessingClass {
                 //          functionDoingAccess() { }
                 //                         .
                 //                         .
                 //                         .
                 //                  class IntermediateClass {
                 //                      private class DefiningClass {
                 //                          varBeingAccessed;
                 //                      }
                 //                  }
                 //                         .
                 //                         .
                 //                         .
                 //      }
                if((definedBy.accessLevel & ScriptReduce.SDT_PRIVATE) != 0)
                {
                    ErrorMsg(errorAt, "member " + var.fullName + " cannot be accessed by " + curDeclFunc.fullName +
                                       " because of private class " + definedBy.longName.val);
                    return;
                }

                 // Likewise, if DefiningClass is tagged protected, the AccessingClass must derive from the
                 // IntermediateClass or access is denied.
                if((definedBy.accessLevel & ScriptReduce.SDT_PROTECTED) != 0)
                {
                    for(TokenDeclSDType extends = accessedBy; extends != definedBy.outerSDType; extends = extends.extends)
                    {
                        if(extends == null)
                        {
                            ErrorMsg(errorAt, "member " + var.fullName + " cannot be accessed by " + curDeclFunc.fullName +
                                               " because of protected class " + definedBy.longName.val);
                            return;
                        }
                    }
                }

                 // Check next outer level.
                definedBy = definedBy.outerSDType;
            } while(definedBy != null);
        }

        /**
         * @brief Convert a list of argument types to printable string, eg, "(list,string,float,integer)"
         *        If given a null, return "" indicating it is a field not a method
         */
        public static string ArgSigString(TokenType[] argsig)
        {
            if(argsig == null)
                return "";
            StringBuilder sb = new StringBuilder("(");
            for(int i = 0; i < argsig.Length; i++)
            {
                if(i > 0)
                    sb.Append(",");
                sb.Append(argsig[i].ToString());
            }
            sb.Append(")");
            return sb.ToString();
        }

        /**
         * @brief output error message and remember that we did
         */
        public void ErrorMsg(Token token, string message)
        {
            if((token == null) || (token.emsg == null))
                token = errorMessageToken;
            if(!youveAnError || (token.file != lastErrorFile) || (token.line > lastErrorLine))
            {
                token.ErrorMsg(message);
                youveAnError = true;
                lastErrorFile = token.file;
                lastErrorLine = token.line;
            }
        }

        /**
         * @brief Find a private static method.
         * @param owner = class the method is part of
         * @param name = name of method to find
         * @param args = array of argument types
         * @returns pointer to method
         */
        public static MethodInfo GetStaticMethod(Type owner, string name, Type[] args)
        {
            MethodInfo mi = owner.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, args, null);
            if(mi == null)
            {
                throw new Exception("undefined method " + owner.ToString() + "." + name);
            }
            return mi;
        }

        // http://wiki.secondlife.com/wiki/Rotation 'negate a rotation' says just negate .s component
        // but http://wiki.secondlife.com/wiki/LSL_Language_Test (lslangtest1.lsl) says negate all 4 values
        public static LSL_Rotation LSLRotationNegate(LSL_Rotation r)
        {
            return new LSL_Rotation(-r.x, -r.y, -r.z, -r.s);
        }
        public static LSL_Vector LSLVectorNegate(LSL_Vector v)
        {
            return -v;
        }
        public static string CatchExcToStr(Exception exc)
        {
            return exc.ToString();
        }
        //public static void       ConsoleWrite      (string str)     { Console.Write(str); }

        /**
         * @brief Defines an internal label that is used as a target for 'break' and 'continue' statements.
         */
        private class BreakContTarg
        {
            public bool used;
            public ScriptMyLabel label;
            public TokenStmtBlock block;

            public BreakContTarg(ScriptCodeGen scg, string name)
            {
                used = false;                         // assume it isn't referenced at all
                label = scg.ilGen.DefineLabel(name);  // label that the break/continue jumps to
                block = scg.curStmtBlock;              // { ... } that the break/continue label is in
            }
        }
    }

    /**
     * @brief Marker interface indicates an exception that can't be caught by a script-level try/catch.
     */
    public interface IXMRUncatchable
    {
    }

    /**
     * @brief Thrown by a script when it attempts to change to an undefined state.
     * These can be detected at compile time but the moron XEngine compiles
     * such things, so we compile them as runtime errors.
     */
    [SerializableAttribute]
    public class ScriptUndefinedStateException: Exception, ISerializable
    {
        public string stateName;
        public ScriptUndefinedStateException(string stateName) : base("undefined state " + stateName)
        {
            this.stateName = stateName;
        }
        protected ScriptUndefinedStateException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /**
     * @brief Created by a throw statement.
     */
    [SerializableAttribute]
    public class ScriptThrownException: Exception, ISerializable
    {
        public object thrown;

        /**
         * @brief Called by a throw statement to wrap the object in a unique
         *        tag that capable of capturing a stack trace.  Script can 
         *        unwrap it by calling xmrExceptionThrownValue().
         */
        public static Exception Wrap(object thrown)
        {
            return new ScriptThrownException(thrown);
        }
        private ScriptThrownException(object thrown) : base(thrown.ToString())
        {
            this.thrown = thrown;
        }

        /**
         * @brief Used by serialization/deserialization.
         */
        protected ScriptThrownException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /**
     * @brief Thrown by a script when it attempts to change to a defined state.
     */
    [SerializableAttribute]
    public class ScriptChangeStateException: Exception, ISerializable, IXMRUncatchable
    {
        public int newState;
        public ScriptChangeStateException(int newState)
        {
            this.newState = newState;
        }
        protected ScriptChangeStateException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /**
     * @brief We are restoring to the body of a catch { } so we need to 
     *        wrap the original exception in an outer exception, so the 
     *        system won't try to refill the stack trace.
     *
     *        We don't mark this one serializable as it should never get 
     *        serialized out.  It only lives from the throw to the very 
     *        beginning of the catch handler where it is promptly unwrapped.
     *        No CheckRun() call can possibly intervene.
     */
    public class ScriptRestoreCatchException: Exception
    {

        // old code uses these
        private object e;
        public ScriptRestoreCatchException(object e)
        {
            this.e = e;
        }
        public static object Unwrap(object o)
        {
            if(o is IXMRUncatchable)
                return null;
            if(o is ScriptRestoreCatchException)
                return ((ScriptRestoreCatchException)o).e;
            return o;
        }

        // new code uses these
        private Exception ee;
        public ScriptRestoreCatchException(Exception ee)
        {
            this.ee = ee;
        }
        public static Exception Unwrap(Exception oo)
        {
            if(oo is IXMRUncatchable)
                return null;
            if(oo is ScriptRestoreCatchException)
                return ((ScriptRestoreCatchException)oo).ee;
            return oo;
        }
    }

    [SerializableAttribute]
    public class ScriptBadCallNoException: Exception
    {
        public ScriptBadCallNoException(int callNo) : base("bad callNo " + callNo) { }
        protected ScriptBadCallNoException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    public class CVVMismatchException: Exception
    {
        public CVVMismatchException(string msg) : base(msg)
        {
        }
    }
}
