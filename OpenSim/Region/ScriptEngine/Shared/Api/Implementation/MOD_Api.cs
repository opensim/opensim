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
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Lifetime;
using OpenMetaverse;
using Nini.Config;
using OpenSim;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api.Plugins;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Shared.Api
{
    [Serializable]
    public class MOD_Api : MarshalByRefObject, IMOD_Api, IScriptApi
    {
        internal IScriptEngine m_ScriptEngine;
        internal SceneObjectPart m_host;
        internal TaskInventoryItem m_item;
        internal bool m_MODFunctionsEnabled = false;
        internal IScriptModuleComms m_comms = null;

        public void Initialize(IScriptEngine ScriptEngine, SceneObjectPart host, TaskInventoryItem item)
        {
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_item = item;

            if (m_ScriptEngine.Config.GetBoolean("AllowMODFunctions", false))
                m_MODFunctionsEnabled = true;

            m_comms = m_ScriptEngine.World.RequestModuleInterface<IScriptModuleComms>();
            if (m_comms == null)
                m_MODFunctionsEnabled = false;
        }

        public override Object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.FromMinutes(0);
//                lease.RenewOnCallTime = TimeSpan.FromSeconds(10.0);
//                lease.SponsorshipTimeout = TimeSpan.FromMinutes(1.0);
            }
            return lease;
        }

        public Scene World
        {
            get { return m_ScriptEngine.World; }
        }

        internal void MODError(string msg)
        {
            throw new ScriptException("MOD Runtime Error: " + msg);
        }

        /// <summary>
        /// Dumps an error message on the debug console.
        /// </summary>
        /// <param name='message'></param>
        internal void MODShoutError(string message) 
        {
            if (message.Length > 1023)
                message = message.Substring(0, 1023);

            World.SimChat(Utils.StringToBytes(message),
                          ChatTypeEnum.Shout, ScriptBaseClass.DEBUG_CHANNEL, m_host.ParentGroup.RootPart.AbsolutePosition, m_host.Name, m_host.UUID, true);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm.DeliverMessage(ChatTypeEnum.Shout, ScriptBaseClass.DEBUG_CHANNEL, m_host.Name, m_host.UUID, message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fname">The name of the function to invoke</param>
        /// <param name="parms">List of parameters</param>
        /// <returns>string result of the invocation</returns>
        public void modInvokeN(string fname, params object[] parms)
        {
            Type returntype = m_comms.LookupReturnType(fname);
            if (returntype != typeof(string))
                MODError(String.Format("return type mismatch for {0}",fname));

            modInvoke(fname,parms);
        }

        public LSL_String modInvokeS(string fname, params object[] parms)
        {
            Type returntype = m_comms.LookupReturnType(fname);
            if (returntype != typeof(string))
                MODError(String.Format("return type mismatch for {0}",fname));

            string result = (string)modInvoke(fname,parms);
            return new LSL_String(result);
        }

        public LSL_Integer modInvokeI(string fname, params object[] parms)
        {
            Type returntype = m_comms.LookupReturnType(fname);
            if (returntype != typeof(int))
                MODError(String.Format("return type mismatch for {0}",fname));

            int result = (int)modInvoke(fname,parms);
            return new LSL_Integer(result);
        }
        
        public LSL_Float modInvokeF(string fname, params object[] parms)
        {
            Type returntype = m_comms.LookupReturnType(fname);
            if (returntype != typeof(float))
                MODError(String.Format("return type mismatch for {0}",fname));

            float result = (float)modInvoke(fname,parms);
            return new LSL_Float(result);
        }

        public LSL_Key modInvokeK(string fname, params object[] parms)
        {
            Type returntype = m_comms.LookupReturnType(fname);
            if (returntype != typeof(UUID))
                MODError(String.Format("return type mismatch for {0}",fname));

            UUID result = (UUID)modInvoke(fname,parms);
            return new LSL_Key(result.ToString());
        }

        public LSL_Vector modInvokeV(string fname, params object[] parms)
        {
            Type returntype = m_comms.LookupReturnType(fname);
            if (returntype != typeof(OpenMetaverse.Vector3))
                MODError(String.Format("return type mismatch for {0}",fname));

            OpenMetaverse.Vector3 result = (OpenMetaverse.Vector3)modInvoke(fname,parms);
            return new LSL_Vector(result.X,result.Y,result.Z);
        }

        public LSL_Rotation modInvokeR(string fname, params object[] parms)
        {
            Type returntype = m_comms.LookupReturnType(fname);
            if (returntype != typeof(OpenMetaverse.Quaternion))
                MODError(String.Format("return type mismatch for {0}",fname));

            OpenMetaverse.Quaternion result = (OpenMetaverse.Quaternion)modInvoke(fname,parms);
            return new LSL_Rotation(result.X,result.Y,result.Z,result.W);
        }

        public LSL_List modInvokeL(string fname, params object[] parms)
        {
            Type returntype = m_comms.LookupReturnType(fname);
            if (returntype != typeof(object[]))
                MODError(String.Format("return type mismatch for {0}",fname));

            object[] result = (object[])modInvoke(fname,parms);
            object[] llist = new object[result.Length];
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] is string)
                {
                    llist[i] = new LSL_String((string)result[i]);
                }
                else if (result[i] is int)
                {
                    llist[i] = new LSL_Integer((int)result[i]);
                }
                else if (result[i] is float)
                {
                    llist[i] = new LSL_Float((float)result[i]);
                }
                else if (result[i] is UUID)
                {
                    llist[i] = new LSL_Key(result[i].ToString());
                }
                else if (result[i] is OpenMetaverse.Vector3)
                {
                    OpenMetaverse.Vector3 vresult = (OpenMetaverse.Vector3)result[i];
                    llist[i] = new LSL_Vector(vresult.X, vresult.Y, vresult.Z);
                }
                else if (result[i] is OpenMetaverse.Quaternion)
                {
                    OpenMetaverse.Quaternion qresult = (OpenMetaverse.Quaternion)result[i];
                    llist[i] = new LSL_Rotation(qresult.X, qresult.Y, qresult.Z, qresult.W);
                }
                else
                {
                    MODError(String.Format("unknown list element {1} returned by {0}", fname, result[i].GetType().Name));
                }
            }

            return new LSL_List(llist);
        }

        /// <summary>
        /// Invokes a preregistered function through the ScriptModuleComms class
        /// </summary>
        /// <param name="fname">The name of the function to invoke</param>
        /// <param name="fname">List of parameters</param>
        /// <returns>string result of the invocation</returns>
        protected object modInvoke(string fname, params object[] parms)
        {
            if (!m_MODFunctionsEnabled)
            {
                MODShoutError("Module command functions not enabled");
                return "";
            }

            Type[] signature = m_comms.LookupTypeSignature(fname);
            if (signature.Length != parms.Length)
                MODError(String.Format("wrong number of parameters to function {0}",fname));
            
            object[] convertedParms = new object[parms.Length];
            for (int i = 0; i < parms.Length; i++)
                convertedParms[i] = ConvertFromLSL(parms[i],signature[i], fname);

            // now call the function, the contract with the function is that it will always return
            // non-null but don't trust it completely
            try 
            {
                object result = m_comms.InvokeOperation(m_host.UUID, m_item.ItemID, fname, convertedParms);
                if (result != null)
                    return result;

                MODError(String.Format("Invocation of {0} failed; null return value",fname));
            }
            catch (Exception e)
            {
                MODError(String.Format("Invocation of {0} failed; {1}",fname,e.Message));
            }

            return null;
        }
        
        /// <summary>
        /// Send a command to functions registered on an event
        /// </summary>
        public string modSendCommand(string module, string command, string k)
        {
            if (!m_MODFunctionsEnabled)
            {
                MODShoutError("Module command functions not enabled");
                return UUID.Zero.ToString();;
            }

            UUID req = UUID.Random();

            m_comms.RaiseEvent(m_item.ItemID, req.ToString(), module, command, k);

            return req.ToString();
        }

        /// <summary>
        /// </summary>
        protected object ConvertFromLSL(object lslparm, Type type, string fname)
        {
            // ---------- String ----------
            if (lslparm is LSL_String)
            {
                if (type == typeof(string))
                    return (string)(LSL_String)lslparm;

                // Need to check for UUID since keys are often treated as strings
                if (type == typeof(UUID))
                    return new UUID((string)(LSL_String)lslparm);
            }

            // ---------- Integer ----------
            else if (lslparm is LSL_Integer)
            {
                if (type == typeof(int) || type == typeof(float))
                    return (int)(LSL_Integer)lslparm;
            }

            // ---------- Float ----------
            else if (lslparm is LSL_Float)
            {
                if (type == typeof(float))
                    return (float)(LSL_Float)lslparm;
            }

            // ---------- Key ----------
            else if (lslparm is LSL_Key)
            {
                if (type == typeof(UUID))
                    return new UUID((LSL_Key)lslparm);
            }

            // ---------- Rotation ----------
            else if (lslparm is LSL_Rotation)
            {
                if (type == typeof(OpenMetaverse.Quaternion))
                {
                    return (OpenMetaverse.Quaternion)((LSL_Rotation)lslparm);
                }
            }

            // ---------- Vector ----------
            else if (lslparm is LSL_Vector)
            {
                if (type == typeof(OpenMetaverse.Vector3))
                {
                    return (OpenMetaverse.Vector3)((LSL_Vector)lslparm);
                }
            }

            // ---------- List ----------
            else if (lslparm is LSL_List)
            {
                if (type == typeof(object[]))
                {
                    object[] plist = (lslparm as LSL_List).Data;
                    object[] result = new object[plist.Length];
                    for (int i = 0; i < plist.Length; i++)
                    {
                        if (plist[i] is LSL_String)
                            result[i] = (string)(LSL_String)plist[i];                            
                        else if (plist[i] is LSL_Integer)
                            result[i] = (int)(LSL_Integer)plist[i];
                        else if (plist[i] is int)
                            result[i] = plist[i];
                        else if (plist[i] is LSL_Float)
                            result[i] = (float)(LSL_Float)plist[i];
                        else if (plist[i] is LSL_Key)
                            result[i] = new UUID((LSL_Key)plist[i]);
                        else if (plist[i] is LSL_Rotation)
                            result[i] = (Quaternion)((LSL_Rotation)plist[i]);
                        else if (plist[i] is LSL_Vector)
                            result[i] = (Vector3)((LSL_Vector)plist[i]);
                        else
                            MODError(String.Format("{0}: unknown LSL list element type", fname));
                    }

                    return result;
                }
            }
            
            MODError(String.Format("{1}: parameter type mismatch; expecting {0}",type.Name, fname));
            return null;
        }

    }
}
