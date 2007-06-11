/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
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
using OpenSim.Framework.User;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Interfaces;

// ?
using OpenSim.Framework.Grid;

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
        private Hashtable eventCategoriesHash;
        private Hashtable uiConfigHash;
        private Hashtable classifiedCategoriesHash;

        private ArrayList loginFlags;
        private ArrayList globalTextures;
        private ArrayList eventCategories;
        private ArrayList uiConfig;
        private ArrayList classifiedCategories;
        private ArrayList inventoryRoot;
        private ArrayList initialOutfit;
        private ArrayList agentInventory;

        private UserProfile userProfile;

        private LLUUID agentID;
        private LLUUID sessionID;
        private LLUUID secureSessionID;
        private LLUUID baseFolderID;
        private LLUUID inventoryFolderID;

        // Login Flags
        private string dst;
        private string stipendSinceLogin;
        private string gendered;
        private string everLoggedIn;
        private string login;
        private string simPort;
        private string simAddress;
        private string agentAccess;
        private Int32 circuitCode;
        private uint regionX;
        private uint regionY;

        // Login
        private string firstname;
        private string lastname;

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

        private string welcomeMessage;
        private string startLocation;
        private string allowFirstLife;
        private string home;
        private string seedCapability;
        private string lookAt;

        public LoginResponse()
        {
            this.loginFlags = new ArrayList();
            this.globalTextures = new ArrayList();
            this.eventCategories = new ArrayList();
            this.uiConfig = new ArrayList();
            this.classifiedCategories = new ArrayList();

            this.loginError = new Hashtable();
            this.eventCategoriesHash = new Hashtable();
            this.classifiedCategoriesHash = new Hashtable();
            this.uiConfigHash = new Hashtable();

            this.defaultXmlRpcResponse = new XmlRpcResponse();
            this.userProfile = new UserProfile();
            this.inventoryRoot = new ArrayList();
            this.initialOutfit = new ArrayList();
            this.agentInventory = new ArrayList();

            this.xmlRpcResponse = new XmlRpcResponse();
            this.defaultXmlRpcResponse = new XmlRpcResponse();

            this.SetDefaultValues();
        } // LoginServer

        public void SetDefaultValues()
        {
            try
            {
                this.DST = "N";
                this.StipendSinceLogin = "N";
                this.Gendered = "Y";
                this.EverLoggedIn = "Y";
                this.login = "false";
                this.firstname = "Test";
                this.lastname = "User";
                this.agentAccess = "M";
                this.startLocation = "last";
                this.allowFirstLife = "Y";

                this.SunTexture = "cce0f112-878f-4586-a2e2-a8f104bba271";
                this.CloudTexture = "fc4b9f0b-d008-45c6-96a4-01dd947ac621";
                this.MoonTexture = "fc4b9f0b-d008-45c6-96a4-01dd947ac621";

                this.ErrorMessage = "You have entered an invalid name/password combination.  Check Caps/lock.";
                this.ErrorReason = "key";
                this.welcomeMessage = "Welcome to OpenSim!";
                this.seedCapability = "";
                this.home = "{'region_handle':[r" + (997 * 256).ToString() + ",r" + (996 * 256).ToString() + "], 'position':[r" + this.userProfile.homepos.X.ToString() + ",r" + this.userProfile.homepos.Y.ToString() + ",r" + this.userProfile.homepos.Z.ToString() + "], 'look_at':[r" + this.userProfile.homelookat.X.ToString() + ",r" + this.userProfile.homelookat.Y.ToString() + ",r" + this.userProfile.homelookat.Z.ToString() + "]}";
                this.lookAt = "[r0.99949799999999999756,r0.03166859999999999814,r0]";
                this.RegionX = (uint)255232;
                this.RegionY = (uint)254976;

                // Classifieds;
                this.AddClassifiedCategory((Int32)1, "Shopping");
                this.AddClassifiedCategory((Int32)2, "Land Rental");
                this.AddClassifiedCategory((Int32)3, "Property Rental");
                this.AddClassifiedCategory((Int32)4, "Special Attraction");
                this.AddClassifiedCategory((Int32)5, "New Products");
                this.AddClassifiedCategory((Int32)6, "Employment");
                this.AddClassifiedCategory((Int32)7, "Wanted");
                this.AddClassifiedCategory((Int32)8, "Service");
                this.AddClassifiedCategory((Int32)9, "Personal");

                int SessionRand = Util.RandomClass.Next(1, 999);
                this.SessionID = new LLUUID("aaaabbbb-0200-" + SessionRand.ToString("0000") + "-8664-58f53e442797");
                this.SecureSessionID = LLUUID.Random();

                this.userProfile.Inventory.CreateRootFolder(this.userProfile.UUID, true);
                this.baseFolderID = this.userProfile.Inventory.GetFolderID("Textures");
                this.inventoryFolderID = this.userProfile.Inventory.GetFolderID("My Inventory-");
                Hashtable InventoryRootHash = new Hashtable();
                InventoryRootHash["folder_id"] = this.userProfile.Inventory.InventoryRoot.FolderID.ToStringHyphenated();
                this.inventoryRoot.Add(InventoryRootHash);

                Hashtable TempHash;
                foreach (InventoryFolder InvFolder in this.userProfile.Inventory.InventoryFolders.Values)
                {
                    TempHash = new Hashtable();
                    TempHash["name"] = InvFolder.FolderName;
                    TempHash["parent_id"] = InvFolder.ParentID.ToStringHyphenated();
                    TempHash["version"] = (Int32)InvFolder.Version;
                    TempHash["type_default"] = (Int32)InvFolder.DefaultType;
                    TempHash["folder_id"] = InvFolder.FolderID.ToStringHyphenated();
                    this.agentInventory.Add(TempHash);
                }

                Hashtable InitialOutfitHash = new Hashtable();
                InitialOutfitHash["folder_name"] = "Nightclub Female";
                InitialOutfitHash["gender"] = "female";
                this.initialOutfit.Add(InitialOutfitHash);
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(
                    OpenSim.Framework.Console.LogPriority.LOW, 
                    "LoginResponse: Unable to set default values: " + e.Message
                );
            }

        } // SetDefaultValues

        protected virtual LLUUID GetAgentId()
        {
            // todo
            LLUUID Agent;
            int AgentRand = Util.RandomClass.Next(1, 9999);
            Agent = new LLUUID("99998888-0100-" + AgentRand.ToString("0000") + "-8ec1-0b1d5cd6aead");
            return Agent;
        } // GetAgentId

        private XmlRpcResponse GenerateFailureResponse(string reason, string message, string login)
        {
            // Overwrite any default values;
            this.xmlRpcResponse = new XmlRpcResponse();

            // Ensure Login Failed message/reason;
            this.ErrorMessage = message;
            this.ErrorReason = reason;

            this.loginError["reason"] = this.ErrorReason;
            this.loginError["message"] = this.ErrorMessage;
            this.loginError["login"] = login;
            this.xmlRpcResponse.Value = this.loginError;
            return (this.xmlRpcResponse);
        } // GenerateResponse

        public XmlRpcResponse LoginFailedResponse()
        {
            return (this.GenerateFailureResponse("key", "You have entered an invalid name/password combination.  Check Caps/lock.", "false"));
        } // LoginFailedResponse

        public XmlRpcResponse ConnectionFailedResponse()
        {
            return (this.LoginFailedResponse());
        } // CreateErrorConnectingToGridResponse()

        public XmlRpcResponse CreateAlreadyLoggedInResponse()
        {
            return (this.GenerateFailureResponse("presence", "You appear to be already logged in, if this is not the case please wait for your session to timeout, if this takes longer than a few minutes please contact the grid owner", "false"));
        } // CreateAlreadyLoggedInResponse()

        public XmlRpcResponse ToXmlRpcResponse()
        {
            try
            {

                Hashtable responseData = new Hashtable();

                this.loginFlagsHash = new Hashtable();
                this.loginFlagsHash["daylight_savings"] = this.DST;
                this.loginFlagsHash["stipend_since_login"] = this.StipendSinceLogin;
                this.loginFlagsHash["gendered"] = this.Gendered;
                this.loginFlagsHash["ever_logged_in"] = this.EverLoggedIn;
                this.loginFlags.Add(this.loginFlagsHash);

                responseData["first_name"] = this.Firstname;
                responseData["last_name"] = this.Lastname;
                responseData["agent_access"] = this.agentAccess;

                this.globalTexturesHash = new Hashtable();
                this.globalTexturesHash["sun_texture_id"] = this.SunTexture;
                this.globalTexturesHash["cloud_texture_id"] = this.CloudTexture;
                this.globalTexturesHash["moon_texture_id"] = this.MoonTexture;
                this.globalTextures.Add(this.globalTexturesHash);
                this.eventCategories.Add(this.eventCategoriesHash);

                this.AddToUIConfig("allow_first_life", this.allowFirstLife);
                this.uiConfig.Add(this.uiConfigHash);

                // Create a agent and session LLUUID
                this.agentID = this.GetAgentId();

                responseData["sim_port"] = this.SimPort;
                responseData["sim_ip"] = this.SimAddress;
                responseData["agent_id"] = this.AgentID.ToStringHyphenated();
                responseData["session_id"] = this.SessionID.ToStringHyphenated();
                responseData["secure_session_id"] = this.SecureSessionID.ToStringHyphenated();
                responseData["circuit_code"] = this.CircuitCode;
                responseData["seconds_since_epoch"] = (Int32)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                responseData["login-flags"] = this.loginFlags;
                responseData["global-textures"] = this.globalTextures;
                responseData["seed_capability"] = this.seedCapability;

                responseData["event_categories"] = this.eventCategories;
                responseData["event_notifications"] = new ArrayList(); // todo
                responseData["classified_categories"] = this.classifiedCategories;
                responseData["ui-config"] = this.uiConfig;

                responseData["inventory-skeleton"] = this.agentInventory;
                responseData["inventory-skel-lib"] = new ArrayList(); // todo
                responseData["inventory-root"] = this.inventoryRoot;
                responseData["gestures"] = new ArrayList(); // todo
                responseData["inventory-lib-owner"] = new ArrayList(); // todo
                responseData["initial-outfit"] = this.initialOutfit;
                responseData["start_location"] = this.startLocation;
                responseData["seed_capability"] = this.seedCapability;
                responseData["home"] = this.home;
                responseData["look_at"] = this.lookAt;
                responseData["message"] = this.welcomeMessage;
                responseData["region_x"] = (Int32)this.RegionX * 256;
                responseData["region_y"] = (Int32)this.RegionY * 256;

                responseData["login"] = "true";
                this.xmlRpcResponse.Value = responseData;

                return (this.xmlRpcResponse);
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(
                    OpenSim.Framework.Console.LogPriority.LOW,
                    "LoginResponse: Error creating XML-RPC Response: " + e.Message
                );
                return (this.GenerateFailureResponse("Internal Error", "Error generating Login Response", "false"));

            }

        } // ToXmlRpcResponse

        public void SetEventCategories(string category, string value)
        {
            this.eventCategoriesHash[category] = value;
        } // SetEventCategories

        public void AddToUIConfig(string itemName, string item)
        {
            this.uiConfigHash[itemName] = item;
        } // SetUIConfig

        public void AddClassifiedCategory(Int32 ID, string categoryName)
        {
            this.classifiedCategoriesHash["category_name"] = categoryName;
            this.classifiedCategoriesHash["category_id"] = ID;
            this.classifiedCategories.Add(this.classifiedCategoriesHash);
            // this.classifiedCategoriesHash.Clear();
        } // SetClassifiedCategory

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

        public LLUUID AgentID
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

        public LLUUID SessionID
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

        public LLUUID SecureSessionID
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

        public LLUUID BaseFolderID
        {
            get
            {
                return this.baseFolderID;
            }
            set
            {
                this.baseFolderID = value;
            }
        } // BaseFolderID

        public LLUUID InventoryFolderID
        {
            get
            {
                return this.inventoryFolderID;
            }
            set
            {
                this.inventoryFolderID = value;
            }
        } // InventoryFolderID

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

        public uint RegionX
        {
            get
            {
                return this.regionX;
            }
            set
            {
                this.regionX = value;
            }
        } // RegionX

        public uint RegionY
        {
            get
            {
                return this.regionY;
            }
            set
            {
                this.regionY = value;
            }
        } // RegionY

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

        public string Firstname
        {
            get
            {
                return this.firstname;
            }
            set
            {
                this.firstname = value;
            }
        } // Firstname

        public string Lastname
        {
            get
            {
                return this.lastname;
            }
            set
            {
                this.lastname = value;
            }
        } // Lastname

        public string AgentAccess
        {
            get
            {
                return this.agentAccess;
            }
            set
            {
                this.agentAccess = value;
            }
        }

        public string StartLocation
        {
            get
            {
                return this.startLocation;
            }
            set
            {
                this.startLocation = value;
            }
        } // StartLocation

        public string LookAt
        {
            get
            {
                return this.lookAt;
            }
            set
            {
                this.lookAt = value;
            }
        }

        public string SeedCapability
        {
            get
            {
                return this.seedCapability;
            }
            set
            {
                this.seedCapability = value;
            }
        } // SeedCapability

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