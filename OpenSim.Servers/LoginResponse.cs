/*
* Copyright (c) OpenSim project, http://sim.opensecondlife.org/
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using Nwc.XmlRpc;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections;
using System.Security.Cryptography;
using System.Xml;
using libsecondlife;
using OpenSim;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Grid;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.User;
using OpenSim.Framework.Utilities;

namespace OpenSim.UserServer
{
    /// <summary>
    /// A temp class to handle login response.
    /// Should make use of UserProfileManager where possible.
    /// </summary>

    public class LoginResponse
    {
        private Hashtable loginFlagsHash;
        private Hashtable globalTexturesHash;
        private Hashtable loginError;

        private ArrayList loginFlags;
        private ArrayList globalTextures;

        // Login Flags
        private string dst;
        private string stipendSinceLogin;
        private string gendered;
        private string everLoggedIn;
        private string login;
        private string simPort;
        private string simAddress;
        private string agentID;
        private string sessionID;
        private string secureSessionID;
        private Int32 circuitCode;

        // Global Textures
        private string sunTexture;
        private string cloudTexture;
        private string moonTexture;

        // Error Flags
        private string errorReason;
        private string errorMessage;

        // Response
        private XmlRpcResponse xmlRpcResponse;
        private XmlRpcResponse defaultXmlRpcResponse;
        private string defaultTextResponse;

        public LoginResponse()
        {
            this.loginFlags = new ArrayList();
            this.globalTextures = new ArrayList();
            this.SetDefaultValues();
        } // LoginServer

        // This will go away as we replace new-login.dat:
        private void GetDefaultResponse()
        {
            try
            {
                // read in default response string
                StreamReader SR;
                string lines;
                SR = File.OpenText("new-login.dat");

                this.defaultTextResponse = "";
                while (!SR.EndOfStream)
                {
                    lines = SR.ReadLine();
                    this.defaultTextResponse += lines;
                }
                SR.Close();
                this.defaultXmlRpcResponse = (XmlRpcResponse)(new XmlRpcResponseDeserializer()).Deserialize(this.defaultTextResponse);
            }
            catch (Exception E)
            {
                Console.WriteLine(E.ToString());
            }
        } // GetDefaultResponse

        public void SetDefaultValues()
        {
            this.GetDefaultResponse();

            this.DST                    = "N";
            this.StipendSinceLogin      = "N";
            this.Gendered               = "Y";
            this.EverLoggedIn           = "Y";
            this.login                  = "false";

            this.SunTexture             = "cce0f112-878f-4586-a2e2-a8f104bba271";
            this.CloudTexture           = "fc4b9f0b-d008-45c6-96a4-01dd947ac621";
            this.MoonTexture            = "fc4b9f0b-d008-45c6-96a4-01dd947ac621";

            this.ErrorMessage           = "You have entered an invalid name/password combination.  Check Caps/lock.";
            this.ErrorReason            = "key";

        } // SetDefaultValues

        private XmlRpcResponse GenerateResponse(string reason, string message, string login)
        {
            // Overwrite any default values;
            this.xmlRpcResponse = new XmlRpcResponse();

            // Ensure Login Failed message/reason;
            this.ErrorMessage = message;
            this.ErrorReason = reason;

            this.loginError = new Hashtable();
            this.loginError["reason"] = this.ErrorReason;
            this.loginError["message"] = this.ErrorMessage;
            this.loginError["login"] = login;
            this.xmlRpcResponse.Value = this.loginError;
            return (this.xmlRpcResponse);
        } // GenerateResponse

        public XmlRpcResponse LoginFailedResponse()
        {
            return (this.GenerateResponse("key", "You have entered an invalid name/password combination.  Check Caps/lock.", "false"));
        } // LoginFailedResponse

        public XmlRpcResponse ConnectionFailedResponse()
        {
            return (this.LoginFailedResponse());
        } // CreateErrorConnectingToGridResponse()

        public XmlRpcResponse CreateAlreadyLoggedInResponse()
        {
            return(this.GenerateResponse("presence", "You appear to be already logged in, if this is not the case please wait for your session to timeout, if this takes longer than a few minutes please contact the grid owner", "false"));
        } // CreateAlreadyLoggedInResponse()

        public XmlRpcResponse ToXmlRpcResponse()
        {
            this.xmlRpcResponse = this.defaultXmlRpcResponse;
            Hashtable responseData = (Hashtable)this.xmlRpcResponse.Value;

            this.loginFlagsHash = new Hashtable();
            this.loginFlagsHash["daylight_savings"]         = this.DST;
            this.loginFlagsHash["stipend_since_login"]      = this.StipendSinceLogin;
            this.loginFlagsHash["gendered"]                 = this.Gendered;
            this.loginFlagsHash["ever_logged_in"]           = this.EverLoggedIn;
            this.loginFlags.Add(this.loginFlagsHash);

            this.globalTexturesHash = new Hashtable();
            this.globalTexturesHash["sun_texture_id"]       = this.SunTexture;
            this.globalTexturesHash["cloud_texture_id"]     = this.CloudTexture;
            this.globalTexturesHash["moon_texture_id"]      = this.MoonTexture;
            this.globalTextures.Add(this.globalTexturesHash);

            responseData["sim_port"]                 = this.SimPort;
            responseData["sim_ip"]                   = this.SimAddress;
            responseData["agent_id"]                 = this.AgentID;
            responseData["session_id"]               = this.SessionID;
            responseData["secure_session_id"]        = this.SecureSessionID;
            responseData["circuit_code"]             = this.CircuitCode;
            responseData["seconds_since_epoch"]      = (Int32)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            responseData["login-flags"]              = this.loginFlags;
            responseData["global-textures"]          = this.globalTextures;

            return (this.xmlRpcResponse);

        } // ToXmlRpcResponse

        public string Login
        {
            get
            {
                return this.login;
            }
            set
            {
                this.login = value;
            }
        } // Login

        public string DST
        {
            get
            {
                return this.dst;
            }
            set
            {
                this.dst = value;
            }
        } // DST

        public string StipendSinceLogin
        {
            get
            {
                return this.stipendSinceLogin;
            }
            set
            {
                this.stipendSinceLogin = value;
            }
        } // StipendSinceLogin

        public string Gendered
        {
            get
            {
                return this.gendered;
            }
            set
            {
                this.gendered = value;
            }
        } // Gendered

        public string EverLoggedIn
        {
            get
            {
                return this.everLoggedIn;
            }
            set
            {
                this.everLoggedIn = value;
            }
        } // EverLoggedIn

        public string SimPort
        {
            get
            {
                return this.simPort;
            }
            set
            {
                this.simPort = value;
            }
        } // SimPort

        public string SimAddress
        {
            get
            {
                return this.simAddress;
            }
            set
            {
                this.simAddress = value;
            }
        } // SimAddress

        public string AgentID
        {
            get
            {
                return this.agentID;
            }
            set
            {
                this.agentID = value;
            }
        } // AgentID

        public string SessionID
        {
            get
            {
                return this.sessionID;
            }
            set
            {
                this.sessionID = value;
            }
        } // SessionID

        public string SecureSessionID
        {
            get
            {
                return this.secureSessionID;
            }
            set
            {
                this.secureSessionID = value;
            }
        } // SecureSessionID

        public Int32 CircuitCode
        {
            get
            {
                return this.circuitCode;
            }
            set
            {
                this.circuitCode = value;
            }
        } // CircuitCode

        public string SunTexture
        {
            get
            {
                return this.sunTexture;
            }
            set
            {
                this.sunTexture = value;
            }
        } // SunTexture

        public string CloudTexture
        {
            get
            {
                return this.cloudTexture;
            }
            set
            {
                this.cloudTexture = value;
            }
        } // CloudTexture

        public string MoonTexture
        {
            get
            {
                return this.moonTexture;
            }
            set
            {
                this.moonTexture = value;
            }
        } // MoonTexture

        public string ErrorReason
        {
            get
            {
                return this.errorReason;
            }
            set
            {
                this.errorReason = value;
            }
        } // ErrorReason

        public string ErrorMessage
        {
            get
            {
                return this.errorMessage;
            }
            set
            {
                this.errorMessage = value;
            }
        } // ErrorMessage

    } // LoginResponse
} // namespace OpenSim.UserServer