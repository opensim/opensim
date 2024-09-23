using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OpenSim.Framework;
using System.Net;

namespace OpenSim.Region.ClientStack.LindenCaps
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "DisplayNameModule")]
    public class DisplayNameModule : IDisplayNameModule, INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IEventQueue m_EventQueue = null;

        protected Scene m_Scene = null;

        private bool m_Enabled = false;

        #region ISharedRegionModule

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["ClientStack.LindenCaps"];
            if (config == null)
                return;

            string url = config.GetString("Cap_SetDisplayName", string.Empty);
            if (url == "localhost")
                m_Enabled = true;

            if (!m_Enabled)
                return;

            m_log.Info("[DISPLAY NAMES] Plugin enabled!");
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_Scene = scene;

            scene.RegisterModuleInterface<IDisplayNameModule>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_Scene = null;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_EventQueue = scene.RequestModuleInterface<IEventQueue>();
            if (m_EventQueue is null)
            {
                m_log.Info("[DISPLAY NAMES]: Module disabled becuase IEventQueue was not found!");
                return;
            }

            scene.EventManager.OnRegisterCaps += OnRegisterCaps;
        }

        public void PostInitialise() { }

        public void Close() { }

        public string Name { get { return "DisplayNamesModule"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        #region IDisplayNameModule

        public string GetDisplayName(UUID avatar)
        {
            var user = m_Scene.UserManagementModule.GetUserData(avatar);

            if (user is not null)
            {
                return user.ViewerDisplayName;
            }

            return string.Empty;
        }

        #endregion

        private void OnRegisterCaps(UUID agentID, Caps caps)
        {
            if (m_Scene.UserManagementModule.IsLocalGridUser(agentID))
            {
                caps.RegisterSimpleHandler("SetDisplayName", new SimpleStreamHandler($"/{UUID.Random()}", (req, resp) => SetDisplayName(agentID, req, resp)));
                return;
            }
        }

        private void SetDisplayName(UUID agent_id, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            if (httpRequest.HttpMethod != "POST")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            ScenePresence sp = m_Scene.GetScenePresence(agent_id);
            if (sp == null || sp.IsDeleted)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.Gone;
                return;
            }

            if (sp.IsInTransit && !sp.IsInLocalTransit)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                httpResponse.AddHeader("Retry-After", "30");
                return;
            }

            var userData = m_Scene.UserManagementModule.GetUserData(agent_id);

            if (userData.NameChanged.AddDays(7) > DateTime.UtcNow)
            {
                m_Scene.GetScenePresence(agent_id).ControllingClient.SendAlertMessage("You can only change your display name once a week!");
                return;
            }

            OSDMap req = (OSDMap)OSDParser.DeserializeLLSDXml(httpRequest.InputStream);
            if (req.ContainsKey("display_name"))
            {
                OSDArray name = req["display_name"] as OSDArray;

                string oldName = name[0].AsString();
                string newName = name[1].AsString();

                bool resetting = string.IsNullOrWhiteSpace(newName);
                if (resetting) newName = string.Empty;

                bool success = m_Scene.UserManagementModule.SetDisplayName(agent_id, newName);

                if (success)
                {
                    // Update the current object
                    userData.DisplayName = newName;
                    userData.NameChanged = DateTime.UtcNow;

                    if (resetting)
                        m_log.InfoFormat("[DISPLAY NAMES] {0} {1} reset their display name", userData.FirstName, userData.LastName);
                    else
                        m_log.InfoFormat("[DISPLAY NAMES] {0} {1} changed their display name to {2}", userData.FirstName, userData.LastName, userData.DisplayName);

                    DateTime next_update = DateTime.UtcNow.AddDays(7);

                    OSD update = FormatDisplayNameUpdate(oldName, userData, next_update);

                    m_Scene.ForEachClient(x => {
                        m_EventQueue.Enqueue(update, x.AgentId);
                    });

                    SendSetDisplayNameReply(newName, oldName, userData, next_update);
                }
                else
                {
                    m_Scene.GetScenePresence(agent_id).ControllingClient.SendAlertMessage("Failed to update display name.");
                    httpResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                    return;
                }
            }

            httpResponse.ContentType = "application/llsd+xml";
            httpResponse.RawBuffer = Utils.StringToBytes("<llsd><undef/></llsd>");
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }

        public OSD FormatDisplayNameUpdate(string oldName, UserData userData, DateTime nextUpdate)
        {
            var agentData = new OSDMap();
            agentData["display_name"] = OSD.FromString(userData.ViewerDisplayName);
            agentData["id"] = OSD.FromUUID(userData.Id);
            agentData["is_display_name_default"] = OSD.FromBoolean(userData.IsNameDefault);
            agentData["legacy_first_name"] = OSD.FromString(userData.FirstName);
            agentData["legacy_last_name"] = OSD.FromString(userData.LastName);
            agentData["username"] = OSD.FromString(userData.Username);
            agentData["display_name_next_update"] = OSD.FromDate(nextUpdate);

            var body = new OSDMap();
            body["agent"] = agentData;
            body["agent_id"] = OSD.FromString(userData.Id.ToString());
            body["old_display_name"] = OSD.FromString(oldName);

            var nameReply = new OSDMap();
            nameReply["body"] = body;
            nameReply["message"] = OSD.FromString("DisplayNameUpdate");
            return nameReply;
        }

        public void SendSetDisplayNameReply(string newDisplayName, string oldDisplayName, UserData nameInfo, DateTime nextUpdate)
        {
            var content = new OSDMap();
            content["display_name"] = OSD.FromString(nameInfo.ViewerDisplayName);
            content["display_name_next_update"] = OSD.FromDate(nextUpdate);
            content["id"] = OSD.FromUUID(nameInfo.Id);
            content["is_display_name_default"] = OSD.FromBoolean(nameInfo.IsNameDefault);
            content["legacy_first_name"] = OSD.FromString(nameInfo.FirstName);
            content["legacy_last_name"] = OSD.FromString(nameInfo.LastName);
            content["username"] = OSD.FromString(nameInfo.LowerUsername);

            var body = new OSDMap();
            body["content"] = content;
            body["reason"] = OSD.FromString("OK");
            body["status"] = OSD.FromInteger(200);

            var nameReply = new OSDMap();
            nameReply["body"] = body;
            nameReply["message"] = OSD.FromString("SetDisplayNameReply");

            m_EventQueue.Enqueue((OSD)nameReply, nameInfo.Id);
        }
    }
}
