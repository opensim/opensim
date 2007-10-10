using libsecondlife;
using OpenSim.Framework.Interfaces;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class AvatarProfilesModule : IRegionModule
    {
        private Scene m_scene;

        public AvatarProfilesModule()
        {
        }

        public void Initialise(Scene scene)
        {
            m_scene = scene;
            m_scene.EventManager.OnNewClient += NewClient;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "AvatarProfilesModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        public void NewClient(IClientAPI client)
        {
            client.OnRequestAvatarProperties += RequestAvatarProperty;
        }

        public void RemoveClient(IClientAPI client)
        {
            client.OnRequestAvatarProperties -= RequestAvatarProperty;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="avatarID"></param>
        public void RequestAvatarProperty(IClientAPI remoteClient, LLUUID avatarID)
        {
            string about = "OpenSim crash test dummy";
            string bornOn = "Before now";
            string flAbout = "First life? What is one of those? OpenSim is my life!";
            LLUUID partner = new LLUUID("11111111-1111-0000-0000-000100bba000");
            remoteClient.SendAvatarProperties(avatarID, about, bornOn, "", flAbout, 0, LLUUID.Zero, LLUUID.Zero, "",
                                              partner);
        }
    }
}