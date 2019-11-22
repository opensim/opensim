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
using System.Reflection.Emit;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Yengine
{
    public class XMRSDTypeClObj
    {
        /*
         * Which script instance we are part of so we can access
         * the script's global variables and functions.
         */
        public XMRInstAbstract xmrInst;

        /*
         * What class we actually are in the hierarchy
         * used for casting.
         */
        public TokenDeclSDTypeClass sdtcClass;

        /*
         * Our VTable array, used for calling virtual functions.
         * And ITable array, used for calling our implementation of interface functions.
         */
        public Delegate[] sdtcVTable;
        public Delegate[][] sdtcITable;

        /*
         * These arrays hold the instance variable values.
         * The array lengths are determined by the script compilation,
         * and are found in TokenDeclSDTypeClass.instSizes.
         */
        public XMRInstArrays instVars;

        /**
         * @brief Called by script's $new() to initialize a new object.
         */
        public XMRSDTypeClObj(XMRInstAbstract inst, int classindex)
        {
            Construct(inst, classindex);
            instVars.AllocVarArrays(sdtcClass.instSizes);
        }

        /**
         * @brief Set up everything except the instVars arrays.
         * @param inst = script instance this object is part of
         * @param classindex = which script-defined type class this object is an onstance of
         * @returns with the vtables filled in
         */
        private void Construct(XMRInstAbstract inst, int classindex)
        {
            Delegate[] thisMid = null;
            TokenDeclSDTypeClass clas = (TokenDeclSDTypeClass)inst.m_ObjCode.sdObjTypesIndx[classindex];

            xmrInst = inst;
            sdtcClass = clas;
            instVars = new XMRInstArrays(inst);

            /*
             * VTable consists of delegates built from DynamicMethods and the 'this' pointer.
             * Yes, yes, lots of shitty little mallocs.
             */
            DynamicMethod[] vDynMeths = clas.vDynMeths;
            if(vDynMeths != null)
            {
                int n = vDynMeths.Length;
                Type[] vMethTypes = clas.vMethTypes;
                sdtcVTable = new Delegate[n];
                for(int i = 0; i < n; i++)
                {
                    sdtcVTable[i] = vDynMeths[i].CreateDelegate(vMethTypes[i], this);
                }
            }

            /*
             * Fill in interface vtables.
             * There is one array of delegates for each implemented interface.
             * The array of delegates IS the interface's object, ie, the 'this' value of the interface.
             * To cast from the class type to the interface type, just get the correct array from the table.
             * To cast from the interface type to the class type, just get Target of entry 0.
             *
             * So we end up with this:
             *    sdtcITable[interfacenumber][methodofintfnumber] = delegate of this.ourimplementationofinterfacesmethod
             */
            if(clas.iDynMeths != null)
            {
                int nIFaces = clas.iDynMeths.Length;
                sdtcITable = new Delegate[nIFaces][];
                for(int i = 0; i < nIFaces; i++)
                {

                    // get vector of entrypoints of our instance methods that implement that interface
                    DynamicMethod[] iDynMeths = clas.iDynMeths[i];
                    Type[] iMethTypes = clas.iMethTypes[i];

                    // allocate an array with a slot for each method the interface defines
                    int nMeths = iDynMeths.Length;
                    Delegate[] ivec;
                    if(nMeths > 0)
                    {
                        // fill in the array with delegates that reference back to this class instance
                        ivec = new Delegate[nMeths];
                        for(int j = 0; j < nMeths; j++)
                        {
                            ivec[j] = iDynMeths[j].CreateDelegate(iMethTypes[j], this);
                        }
                    }
                    else
                    {
                        // just a marker interface with no methods,
                        // allocate a one-element array and fill
                        // with a dummy entry.  this will allow casting
                        // back to the original class instance (this)
                        // by reading Target of entry 0.
                        if(thisMid == null)
                        {
                            thisMid = new Delegate[1];
                            thisMid[0] = markerInterfaceDummy.CreateDelegate(typeof(MarkerInterfaceDummy), this);
                        }
                        ivec = thisMid;
                    }

                    // save whatever we ended up allocating
                    sdtcITable[i] = ivec;
                }
            }
        }

        private delegate void MarkerInterfaceDummy();
        private static DynamicMethod markerInterfaceDummy = MakeMarkerInterfaceDummy();
        private static DynamicMethod MakeMarkerInterfaceDummy()
        {
            DynamicMethod dm = new DynamicMethod("XMRSDTypeClObj.MarkerInterfaceDummy", null, new Type[] { typeof(XMRSDTypeClObj) });
            ILGenerator ilGen = dm.GetILGenerator();
            ilGen.Emit(OpCodes.Ret);
            return dm;
        }

        /**
         * @brief Perform runtime casting of script-defined interface object to
         *        its corresponding script-defined class object.
         * @param da = interface object (array of delegates pointing to class's implementations of interface's methods)
         * @param classindex = what class those implementations are supposedly part of
         * @returns original script-defined class object
         */
        public static XMRSDTypeClObj CastIFace2Class(Delegate[] da, int classindex)
        {
            return CastClass2Class(da[0].Target, classindex);
        }

        /**
         * @brief Perform runtime casting of XMRSDTypeClObj's.
         * @param ob = XMRSDTypeClObj of unknown script-defined class to cast
         * @param classindex = script-defined class to cast it to
         * @returns ob is a valid instance of classindex; else exception thrown
         */
        public static XMRSDTypeClObj CastClass2Class(object ob, int classindex)
        {
            /*
             * Let mono check to see if we at least have an XMRSDTypeClObj.
             */
            XMRSDTypeClObj ci = (XMRSDTypeClObj)ob;
            if(ci != null)
            {

                /*
                 * This is the target class, ie, what we are hoping the object can cast to.
                 */
                TokenDeclSDTypeClass tc = (TokenDeclSDTypeClass)ci.xmrInst.m_ObjCode.sdObjTypesIndx[classindex];

                /*
                 * Step from the object's actual class rootward.
                 * If we find the target class along the way, the cast is valid.
                 * If we run off the end of the root, the cast is not valid.
                 */
                for(TokenDeclSDTypeClass ac = ci.sdtcClass; ac != tc; ac = ac.extends)
                {
                    if(ac == null)
                        throw new InvalidCastException("invalid cast from " + ci.sdtcClass.longName.val +
                                                                      " to " + tc.longName.val);
                }

                /*
                 * The target class is at or rootward of the actual class,
                 * so the cast is valid.
                 */
            }
            return ci;
        }

        /**
         * @brief Cast an arbitrary object to the given interface.
         * @param ob = object to be cast of unknown type
         * @returns ob cast to the interface type
         */
        public static Delegate[] CastObj2IFace(object ob, string ifacename)
        {
            if(ob == null)
                return null;

            /*
             * If it is already one of our interfaces, extract the script-defined class object from it.
             */
            if(ob is Delegate[])
            {
                Delegate[] da = (Delegate[])ob;
                ob = da[0].Target;
            }

            /*
             * Now that we have a presumed script-defined class object, cast that to the requested interface
             * by picking the array of delegates that corresponds to the requested interface.
             */
            XMRSDTypeClObj ci = (XMRSDTypeClObj)ob;
            int iFaceIndex = ci.sdtcClass.intfIndices[ifacename];
            return ci.sdtcITable[iFaceIndex];
        }

        /**
         * @brief Write the whole thing out to a stream.
         */
        public void Capture(XMRInstArrays.Sender sendValue)
        {
            sendValue(this.sdtcClass.sdTypeIndex);
            this.instVars.SendArrays(sendValue);
        }

        /**
         * @brief Read the whole thing in from a stream.
         */
        public XMRSDTypeClObj()
        {
        }
        public void Restore(XMRInstAbstract inst, XMRInstArrays.Recver recvValue)
        {
            int classindex = (int)recvValue();
            Construct(inst, classindex);
            this.instVars.RecvArrays(recvValue);
        }
    }
}
