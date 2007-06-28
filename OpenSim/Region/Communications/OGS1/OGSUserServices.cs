using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Types;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Data;
using libsecondlife;

using Nwc.XmlRpc;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGSUserServices :IUserServices
    {
        GridCommsManager m_parent;
        public OGSUserServices(GridCommsManager parent)
        {
            m_parent = parent;
        }

        public UserProfileData ConvertXMLRPCDataToUserProfile(Hashtable data)
        {
            UserProfileData userData = new UserProfileData();
            userData.username = (string)data["firstname"];
            userData.surname = (string)data["lastname"];
            userData.UUID = new LLUUID((string)data["uuid"]);
            userData.userInventoryURI = (string)data["server_inventory"];
            userData.userAssetURI = (string)data["server_asset"];
            userData.profileFirstText = (string)data["profile_firstlife_about"];
            userData.profileFirstImage = new LLUUID((string)data["profile_firstlife_image"]);
            userData.profileCanDoMask = (uint)data["profile_can_do"];
            userData.profileWantDoMask = (uint)data["profile_want_do"];
            userData.profileImage = new LLUUID((string)data["profile_image"]);
            userData.lastLogin = (int)data["profile_lastlogin"];
            userData.homeLocation = new LLVector3();
            userData.homeLookAt = new LLVector3();

            return userData;
        }
        public UserProfileData GetUserProfile(string firstName, string lastName)
        {
            return GetUserProfile(firstName + " " + lastName);
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
                XmlRpcResponse resp = req.Send(m_parent.ServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable)resp.Value;

                return ConvertXMLRPCDataToUserProfile(respData);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error when trying to fetch profile data by name from remote user server: " + e.Message);
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
