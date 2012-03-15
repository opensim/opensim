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
        internal uint m_localID;
        internal UUID m_itemID;
        internal bool m_MODFunctionsEnabled = false;
        internal IScriptModuleComms m_comms = null;

        public void Initialize(IScriptEngine ScriptEngine, SceneObjectPart host, uint localID, UUID itemID)
        {
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;

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
            throw new Exception("MOD Runtime Error: " + msg);
        }

        //
        //Dumps an error message on the debug console.
        //

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
        /// <param name="fname">List of parameters</param>
        /// <returns>string result of the invocation</returns>
        public string modInvokeS(string fname, params object[] parms)
        {
            Type returntype = m_comms.LookupReturnType(fname);
            if (returntype != typeof(string))
                MODError(String.Format("return type mismatch for {0}",fname));

            return (string)modInvoke(fname,parms);
        }

        public int modInvokeI(string fname, params object[] parms)
        {
            Type returntype = m_comms.LookupReturnType(fname);
            if (returntype != typeof(int))
                MODError(String.Format("return type mismatch for {0}",fname));

            return (int)modInvoke(fname,parms);
        }
        
        public float modInvokeF(string fname, params object[] parms)
        {
            Type returntype = m_comms.LookupReturnType(fname);
            if (returntype != typeof(float))
                MODError(String.Format("return type mismatch for {0}",fname));

            return (float)modInvoke(fname,parms);
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
            {
                if (parms[i] is LSL_String)
                {
                    if (signature[i] != typeof(string))
                        MODError(String.Format("parameter type mismatch in {0}; expecting {1}",fname,signature[i].Name));

                    convertedParms[i] = (string)(LSL_String)parms[i];
                }
                else if (parms[i] is LSL_Integer)
                {
                    if (signature[i] != typeof(int))
                        MODError(String.Format("parameter type mismatch in {0}; expecting {1}",fname,signature[i].Name));

                    convertedParms[i] = (int)(LSL_Integer)parms[i];
                }
                else if (parms[i] is LSL_Float)
                {
                    if (signature[i] != typeof(float))
                        MODError(String.Format("parameter type mismatch in {0}; expecting {1}",fname,signature[i].Name));

                    convertedParms[i] = (float)(LSL_Float)parms[i];
                }
                else if (parms[i] is LSL_Key)
                {
                    if (signature[i] != typeof(string))
                        MODError(String.Format("parameter type mismatch in {0}; expecting {1}",fname,signature[i].Name));

                    convertedParms[i] =  (string)(LSL_Key)parms[i];
                }
                else if (parms[i] is LSL_Rotation)
                {
                    if (signature[i] != typeof(string))
                        MODError(String.Format("parameter type mismatch in {0}; expecting {1}",fname,signature[i].Name));

                    convertedParms[i] =  (string)(LSL_Rotation)parms[i];
                }
                else if (parms[i] is LSL_Vector)
                {
                    if (signature[i] != typeof(string))
                        MODError(String.Format("parameter type mismatch in {0}; expecting {1}",fname,signature[i].Name));

                    convertedParms[i] =  (string)(LSL_Vector)parms[i];
                }
                else
                {
                    if (signature[i] != parms[i].GetType())
                        MODError(String.Format("parameter type mismatch in {0}; expecting {1}",fname,signature[i].Name));

                    convertedParms[i] = parms[i];
                }
            }

            return m_comms.InvokeOperation(m_itemID,fname,convertedParms);
        }
        
        public string modSendCommand(string module, string command, string k)
        {
            if (!m_MODFunctionsEnabled)
            {
                MODShoutError("Module command functions not enabled");
                return UUID.Zero.ToString();;
            }

            UUID req = UUID.Random();

            m_comms.RaiseEvent(m_itemID, req.ToString(), module, command, k);

            return req.ToString();
        }
    }
}
