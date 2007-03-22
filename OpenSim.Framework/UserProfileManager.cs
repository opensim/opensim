using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework.Sims;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Utilities;

namespace OpenSim.Framework.User
{
    public class UserProfileManager : UserProfileManagerBase
    {
        public string GridURL;
        public string GridSendKey;
        public string GridRecvKey;
        public string DefaultStartupMsg;

        public UserProfileManager()
        {

        }

        public void SetKeys(string sendKey, string recvKey, string url, string message)
        {
            GridRecvKey = recvKey;
            GridSendKey = sendKey;
            GridURL = url;
            DefaultStartupMsg = message;
        }

        public virtual string ParseXMLRPC(string requestBody)
        {
            XmlRpcRequest request = (XmlRpcRequest)(new XmlRpcRequestDeserializer()).Deserialize(requestBody);

            Hashtable requestData = (Hashtable)request.Params[0];
            switch (request.MethodName)
            {
                case "login_to_simulator":
                    bool GoodXML = (requestData.Contains("first") && requestData.Contains("last") && requestData.Contains("passwd"));
                    bool GoodLogin = false;
                    string firstname = "";
                    string lastname = "";
                    string passwd = "";

                    if (GoodXML)
                    {
                        firstname = (string)requestData["first"];
                        lastname = (string)requestData["last"];
                        passwd = (string)requestData["passwd"];
                        GoodLogin = AuthenticateUser(firstname, lastname, passwd);
                    }


                    if (!(GoodXML && GoodLogin))
                    {
                        XmlRpcResponse LoginErrorResp = new XmlRpcResponse();
                        Hashtable ErrorRespData = new Hashtable();
                        ErrorRespData["reason"] = "key";
                        ErrorRespData["message"] = "Error connecting to grid. Please double check your login details and check with the grid owner if you are sure these are correct";
                        ErrorRespData["login"] = "false";
                        LoginErrorResp.Value = ErrorRespData;
                        return (Regex.Replace(XmlRpcResponseSerializer.Singleton.Serialize(LoginErrorResp), " encoding=\"utf-16\"", ""));
                    }

                    UserProfile TheUser = GetProfileByName(firstname, lastname);

                    if (!((TheUser.CurrentSessionID == null) && (TheUser.CurrentSecureSessionID == null)))
                    {
                        XmlRpcResponse PresenceErrorResp = new XmlRpcResponse();
                        Hashtable PresenceErrorRespData = new Hashtable();
                        PresenceErrorRespData["reason"] = "presence";
                        PresenceErrorRespData["message"] = "You appear to be already logged in, if this is not the case please wait for your session to timeout, if this takes longer than a few minutes please contact the grid owner";
                        PresenceErrorRespData["login"] = "false";
                        PresenceErrorResp.Value = PresenceErrorRespData;
                        return (Regex.Replace(XmlRpcResponseSerializer.Singleton.Serialize(PresenceErrorResp), " encoding=\"utf-16\"", ""));

                    }

                    try
                    {
                        LLUUID AgentID = TheUser.UUID;
                        TheUser.InitSessionData();
                       // SimProfile SimInfo = new SimProfile();
                       // SimInfo = SimInfo.LoadFromGrid(TheUser.homeregionhandle, GridURL, GridSendKey, GridRecvKey);

                        XmlRpcResponse LoginGoodResp = new XmlRpcResponse();
                        Hashtable LoginGoodData = new Hashtable();

                        Hashtable GlobalT = new Hashtable();
                        GlobalT["sun_texture_id"] = "cce0f112-878f-4586-a2e2-a8f104bba271";
                        GlobalT["cloud_texture_id"] = "fc4b9f0b-d008-45c6-96a4-01dd947ac621";
                        GlobalT["moon_texture_id"] = "fc4b9f0b-d008-45c6-96a4-01dd947ac621";
                        ArrayList GlobalTextures = new ArrayList();
                        GlobalTextures.Add(GlobalT);

                        Hashtable LoginFlagsHash = new Hashtable();
                        LoginFlagsHash["daylight_savings"] = "N";
                        LoginFlagsHash["stipend_since_login"] = "N";
                        LoginFlagsHash["gendered"] = "Y";
                        LoginFlagsHash["ever_logged_in"] = "Y";
                        ArrayList LoginFlags = new ArrayList();
                        LoginFlags.Add(LoginFlagsHash);

                        Hashtable uiconfig = new Hashtable();
                        uiconfig["allow_first_life"] = "Y";
                        ArrayList ui_config = new ArrayList();
                        ui_config.Add(uiconfig);

                        Hashtable ClassifiedCategoriesHash = new Hashtable();
                        ClassifiedCategoriesHash["category_name"] = "bla bla";
                        ClassifiedCategoriesHash["category_id"] = (Int32)1;
                        ArrayList ClassifiedCategories = new ArrayList();
                        ClassifiedCategories.Add(ClassifiedCategoriesHash);

                        ArrayList AgentInventory = new ArrayList();
                        foreach (InventoryFolder InvFolder in TheUser.Inventory.InventoryFolders.Values)
                        {
                            Hashtable TempHash = new Hashtable();
                            TempHash["name"] = InvFolder.FolderName;
                            TempHash["parent_id"] = InvFolder.ParentID.ToStringHyphenated();
                            TempHash["version"] = (Int32)InvFolder.Version;
                            TempHash["type_default"] = (Int32)InvFolder.DefaultType;
                            TempHash["folder_id"] = InvFolder.FolderID.ToStringHyphenated();
                            AgentInventory.Add(TempHash);
                        }

                        Hashtable InventoryRootHash = new Hashtable();
                        InventoryRootHash["folder_id"] = TheUser.Inventory.InventoryRoot.FolderID.ToStringHyphenated();
                        ArrayList InventoryRoot = new ArrayList();
                        InventoryRoot.Add(InventoryRootHash);

                        Hashtable InitialOutfitHash = new Hashtable();
                        InitialOutfitHash["folder_name"] = "Nightclub Female";
                        InitialOutfitHash["gender"] = "female";
                        ArrayList InitialOutfit = new ArrayList();
                        InitialOutfit.Add(InitialOutfitHash);

                        uint circode = (uint)(Util.RandomClass.Next());
                        //TheUser.AddSimCircuit(circode, SimInfo.UUID);

                        LoginGoodData["last_name"] = "\"" + TheUser.firstname + "\"";
                        LoginGoodData["ui-config"] = ui_config;
                        LoginGoodData["sim_ip"] = "127.0.0.1"; //SimInfo.sim_ip.ToString();
                        LoginGoodData["login-flags"] = LoginFlags;
                        LoginGoodData["global-textures"] = GlobalTextures;
                        LoginGoodData["classified_categories"] = ClassifiedCategories;
                        LoginGoodData["event_categories"] = new ArrayList();
                        LoginGoodData["inventory-skeleton"] = AgentInventory;
                        LoginGoodData["inventory-skel-lib"] = new ArrayList();
                        LoginGoodData["inventory-root"] = InventoryRoot;
                        LoginGoodData["event_notifications"] = new ArrayList();
                        LoginGoodData["gestures"] = new ArrayList();
                        LoginGoodData["inventory-lib-owner"] = new ArrayList();
                        LoginGoodData["initial-outfit"] = InitialOutfit;
                        LoginGoodData["seconds_since_epoch"] = (Int32)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                        LoginGoodData["start_location"] = "last";
                        LoginGoodData["home"] = "{'region_handle':[r" + (997 * 256).ToString() + ",r" + (996 * 256).ToString() + "], 'position':[r" + TheUser.homepos.X.ToString() + ",r" + TheUser.homepos.Y.ToString() + ",r" + TheUser.homepos.Z.ToString() + "], 'look_at':[r" + TheUser.homelookat.X.ToString() + ",r" + TheUser.homelookat.Y.ToString() + ",r" + TheUser.homelookat.Z.ToString() + "]}";
                        LoginGoodData["message"] = DefaultStartupMsg;
                        LoginGoodData["first_name"] = "\"" + firstname + "\"";
                        LoginGoodData["circuit_code"] = (Int32)circode;
                        LoginGoodData["sim_port"] = 9000; //(Int32)SimInfo.sim_port;
                        LoginGoodData["secure_session_id"] = TheUser.CurrentSecureSessionID.ToStringHyphenated();
                        LoginGoodData["look_at"] = "\n[r" + TheUser.homelookat.X.ToString() + ",r" + TheUser.homelookat.Y.ToString() + ",r" + TheUser.homelookat.Z.ToString() + "]\n";
                        LoginGoodData["agent_id"] = AgentID.ToStringHyphenated();
                        LoginGoodData["region_y"] = (Int32) 996 * 256; // (Int32)SimInfo.RegionLocY * 256;
                        LoginGoodData["region_x"] = (Int32) 997 * 256;  //SimInfo.RegionLocX * 256;
                        LoginGoodData["seed_capability"] = null;
                        LoginGoodData["agent_access"] = "M";
                        LoginGoodData["session_id"] = TheUser.CurrentSessionID.ToStringHyphenated();
                        LoginGoodData["login"] = "true";

                        this.CustomiseResponse(ref LoginGoodData, TheUser);
                        LoginGoodResp.Value = LoginGoodData;
                        //TheUser.SendDataToSim(SimInfo);
                        return (Regex.Replace(XmlRpcResponseSerializer.Singleton.Serialize(LoginGoodResp), "utf-16", "utf-8"));

                    }
                    catch (Exception E)
                    {
                        Console.WriteLine(E.ToString());
                    }

                    break;
            }

            return "";
        }

        public virtual void CustomiseResponse(ref Hashtable response, UserProfile theUser)
        {
            //default method set up to act as ogs user server
            SimProfile SimInfo = new SimProfile();
            //get siminfo from grid server
            SimInfo = SimInfo.LoadFromGrid(theUser.homeregionhandle, GridURL, GridSendKey, GridRecvKey);
            uint circode = (uint)response["circuit_code"];
            theUser.AddSimCircuit(circode, SimInfo.UUID);
            response["home"] = "{'region_handle':[r" + (SimInfo.RegionLocX * 256).ToString() + ",r" + (SimInfo.RegionLocY * 256).ToString() + "], 'position':[r" + theUser.homepos.X.ToString() + ",r" + theUser.homepos.Y.ToString() + ",r" + theUser.homepos.Z.ToString() + "], 'look_at':[r" + theUser.homelookat.X.ToString() + ",r" + theUser.homelookat.Y.ToString() + ",r" + theUser.homelookat.Z.ToString() + "]}";
            response["sim_ip"] = SimInfo.sim_ip.ToString();
            response["sim_port"] = (Int32)SimInfo.sim_port;
            response["region_y"] = (Int32) SimInfo.RegionLocY * 256;
            response["region_x"] = (Int32) SimInfo.RegionLocX * 256;

            //default is ogs user server, so let the sim know about the user via a XmlRpcRequest
            Console.WriteLine(SimInfo.caps_url);
            Hashtable SimParams = new Hashtable();
            SimParams["session_id"] = theUser.CurrentSessionID.ToString();
            SimParams["secure_session_id"] = theUser.CurrentSecureSessionID.ToString();
            SimParams["firstname"] = theUser.firstname;
            SimParams["lastname"] = theUser.lastname;
            SimParams["agent_id"] = theUser.UUID.ToString();
            SimParams["circuit_code"] = (Int32)theUser.Circuits[SimInfo.UUID];
            ArrayList SendParams = new ArrayList();
            SendParams.Add(SimParams);

            XmlRpcRequest GridReq = new XmlRpcRequest("expect_user", SendParams);
            XmlRpcResponse GridResp = GridReq.Send(SimInfo.caps_url, 3000);
        }
    }
}
