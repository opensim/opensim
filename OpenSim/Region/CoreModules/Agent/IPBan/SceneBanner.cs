using System.Collections.Generic;
using System.Net;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Agent.IPBan
{
    internal class SceneBanner
    {
                private static readonly log4net.ILog m_log
                    = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private List<string> bans;
        private SceneBase m_scene;
        public SceneBanner(SceneBase scene, List<string> banList)
        {
            scene.EventManager.OnClientConnect += EventManager_OnClientConnect;

            bans = banList;
            m_scene = scene;
        }

        void EventManager_OnClientConnect(IClientCore client)
        {
            IClientIPEndpoint ipEndpoint;
            if(client.TryGet(out ipEndpoint))
            {
                IPAddress end = ipEndpoint.EndPoint;

                try
                {
                    IPHostEntry rDNS = Dns.GetHostEntry(end);
                    foreach (string ban in bans)
                    {
                        if (rDNS.HostName.Contains(ban) ||
                            end.ToString().StartsWith(ban))
                        {
                            client.Disconnect("Banned - network \"" + ban + "\" is not allowed to connect to this server.");
                            m_log.Warn("[IPBAN] Disconnected '" + end + "' due to '" + ban + "' ban.");
                            return;
                        }
                    }
                }
                catch (System.Net.Sockets.SocketException sex)
                {
                    m_log.WarnFormat("[IPBAN] IP address \"{0}\" cannot be resolved via DNS", end);
                }
                m_log.WarnFormat("[IPBAN] User \"{0}\" not in any ban lists. Allowing connection.", end);
            }
        }
    }
}
