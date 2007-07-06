using System;
using System.Collections;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Data;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1UserServices :IUserServices
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
            userData.username = (string)data["firstname"];
            userData.surname = (string)data["lastname"];
            userData.UUID = new LLUUID((string)data["uuid"]);
            userData.userInventoryURI = (string)data["server_inventory"];
            userData.userAssetURI = (string)data["server_asset"];
            userData.profileFirstText = (string)data["profile_firstlife_about"];
            userData.profileFirstImage = new LLUUID((string)data["profile_firstlife_image"]);
            userData.profileCanDoMask = Convert.ToUInt32((string)data["profile_can_do"]);
            userData.profileWantDoMask = Convert.ToUInt32(data["profile_want_do"]);
            userData.profileImage = new LLUUID((string)data["profile_image"]);
            userData.lastLogin = Convert.ToInt32((string)data["profile_lastlogin"]);
            userData.homeRegion = Convert.ToUInt64((string)data["home_region"]);
            userData.homeLocation = new LLVector3((float)Convert.ToDecimal((string)data["home_coordinates_x"]), (float)Convert.ToDecimal((string)data["home_coordinates_y"]), (float)Convert.ToDecimal((string)data["home_coordinates_z"]));
            userData.homeLookAt = new LLVector3((float)Convert.ToDecimal((string)data["home_look_x"]), (float)Convert.ToDecimal((string)data["home_look_y"]), (float)Convert.ToDecimal((string)data["home_look_z"]));

            return userData;
        }
        public UserProfileData GetUserProfile(string firstName, string lastName)
        {
            return GetUserProfile(firstName + " " + lastName);
        }
        public UserProfileData GetUserProfile(string name)
        {

            //try
            //{
                Hashtable param = new Hashtable();
                param["avatar_name"] = name;
                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_user_by_name", parameters);
                XmlRpcResponse resp = req.Send(m_parent.ServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable)resp.Value;

                return ConvertXMLRPCDataToUserProfile(respData);
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine("Error when trying to fetch profile data by name from remote user server: " + e.Message);
            //}
            //return null;
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
                XmlRpcResponse resp = req.Send(m_parent.ServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable)resp.Value;

                return ConvertXMLRPCDataToUserProfile(respData);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error when trying to fetch profile data by uuid from remote user server: " + e.Message);
            }
            return null;
        }

        public UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            return SetupMasterUser(firstName, lastName, "");
        }

        public UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            UserProfileData profile = GetUserProfile(firstName, lastName);
            if (profile == null)
            {
                Console.WriteLine("Unknown Master User. Grid Mode: No clue what I should do. Probably would choose the grid owner UUID when that is implemented");
            }
            return null;
        }
    }
}
