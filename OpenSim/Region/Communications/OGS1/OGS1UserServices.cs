using System;
using System.Collections;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1UserServices :IUserService
    {
        CommunicationsOGS1 m_parent;
        public OGS1UserServices(CommunicationsOGS1 parent)
        {
            m_parent = parent;
        }

        public UserProfileData ConvertXMLRPCDataToUserProfile(Hashtable data)
        {
            if (data.Contains("error_type"))
            {
                Console.WriteLine("Error sent by user server when trying to get user profile: (" + data["error_type"] + "): " + data["error_desc"]);
                return null;
            }

            UserProfileData userData = new UserProfileData();
            userData.Firstname = (string)data["firstname"];
            userData.Lastname = (string)data["lastname"];
            userData.UUID = new LLUUID((string)data["uuid"]);
            userData.UserInventoryUri = (string)data["server_inventory"];
            userData.UserAssetUri = (string)data["server_asset"];
            userData.ProfileFirstText = (string)data["profile_firstlife_about"];
            userData.ProfileFirstImage = new LLUUID((string)data["profile_firstlife_image"]);
            userData.ProfileCanDoMask = Convert.ToUInt32((string)data["profile_can_do"]);
            userData.ProfileWantDoMask = Convert.ToUInt32(data["profile_want_do"]);
            userData.ProfileImage = new LLUUID((string)data["profile_image"]);
            userData.LastLogin = Convert.ToInt32((string)data["profile_lastlogin"]);
            userData.HomeRegion = Convert.ToUInt64((string)data["home_region"]);
            userData.HomeLocation = new LLVector3((float)Convert.ToDecimal((string)data["home_coordinates_x"]), (float)Convert.ToDecimal((string)data["home_coordinates_y"]), (float)Convert.ToDecimal((string)data["home_coordinates_z"]));
            userData.HomeLookAt = new LLVector3((float)Convert.ToDecimal((string)data["home_look_x"]), (float)Convert.ToDecimal((string)data["home_look_y"]), (float)Convert.ToDecimal((string)data["home_look_z"]));

            return userData;
        }
        public UserProfileData GetUserProfile(string firstName, string lastName)
        {
            return GetUserProfile(firstName, lastName);
        }
        public UserProfileData GetUserProfile(string name)
        {
            try
            {
                Hashtable param = new Hashtable();
                param["avatar_name"] = name;
                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_user_by_name", parameters);
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable)resp.Value;

                return ConvertXMLRPCDataToUserProfile(respData);
            }
            catch (System.Net.WebException e)
            {
                OpenSim.Framework.Console.MainLog.Instance.Warn("Error when trying to fetch profile data by name from remote user server: " + e.Message);
            }
            return null;
        }
        public UserProfileData GetUserProfile(LLUUID avatarID)
        {
            try
            {
                Hashtable param = new Hashtable();
                param["avatar_uuid"] = avatarID.ToString();
                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_user_by_uuid", parameters);
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable)resp.Value;

                return ConvertXMLRPCDataToUserProfile(respData);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error when trying to fetch profile data by uuid from remote user server: " + e.Message);
            }
            return null;
        }

        public void clearUserAgent(LLUUID avatarID) 
        {
            // TODO: implement
        }

        public UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            return SetupMasterUser(firstName, lastName, "");
        }

        public UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            UserProfileData profile = GetUserProfile(firstName, lastName);
            return profile;
        }

        public void AddUserProfile(string firstName, string lastName, string pass, uint regX, uint regY)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
