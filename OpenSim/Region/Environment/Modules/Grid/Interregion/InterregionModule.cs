using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Grid.Interregion
{
    public class InterregionModule : IInterregionModule, IRegionModule
    {
        #region Direction enum

        public enum Direction
        {
            North,
            NorthEast,
            East,
            SouthEast,
            South,
            SouthWest,
            West,
            NorthWest
        }

        #endregion

        private readonly Dictionary<Type, Object> m_interfaces = new Dictionary<Type, object>();
        private readonly Object m_lockObject = new object();
        private readonly List<Location> m_myLocations = new List<Location>();

        private readonly Dictionary<Location, string[]> m_neighbourInterfaces = new Dictionary<Location, string[]>();
        private readonly Dictionary<Location, RemotingObject> m_neighbourRemote = new Dictionary<Location, RemotingObject>();
        private IConfigSource m_config;
        private const bool m_enabled = false;

        private RemotingObject m_myRemote;
        private TcpChannel m_tcpChannel;
        private int m_tcpPort = 10101;

        #region IInterregionModule Members

        public void internal_CreateRemotingObjects()
        {
            lock (m_lockObject)
            {
                if (m_tcpChannel == null)
                {
                    m_myRemote = new RemotingObject(m_interfaces, m_myLocations.ToArray());
                    m_tcpChannel = new TcpChannel(m_tcpPort);

                    ChannelServices.RegisterChannel(m_tcpChannel, false);
                    RemotingServices.Marshal(m_myRemote, "OpenSimRemote2", typeof (RemotingObject));
                }
            }
        }

        public void RegisterMethod<T>(T e)
        {
            m_interfaces[typeof (T)] = e;
        }

        public bool HasInterface<T>(Location loc)
        {
            foreach (string val in m_neighbourInterfaces[loc])
            {
                if (val == typeof (T).FullName)
                {
                    return true;
                }
            }
            return false;
        }

        public T RequestInterface<T>(Location loc)
        {
            if (m_neighbourRemote.ContainsKey(loc))
            {
                return m_neighbourRemote[loc].RequestInterface<T>();
            }
            throw new IndexOutOfRangeException("No neighbour availible at that location");
        }

        public T[] RequestInterface<T>()
        {
            List<T> m_t = new List<T>();
            foreach (RemotingObject remote in m_neighbourRemote.Values)
            {
                try
                {
                    m_t.Add(remote.RequestInterface<T>());
                }
                catch (NotSupportedException)
                {
                }
            }
            return m_t.ToArray();
        }

        public Location GetLocationByDirection(Scene scene, Direction dir)
        {
            return new Location(0, 0);
        }

        public void RegisterRemoteRegion(string uri)
        {
            RegisterRemotingInterface((RemotingObject) Activator.GetObject(typeof (RemotingObject), uri));
        }

        #endregion

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_myLocations.Add(new Location((int) scene.RegionInfo.RegionLocX,
                                           (int) scene.RegionInfo.RegionLocY));
            m_config = source;

            scene.RegisterModuleInterface<IInterregionModule>(this);
        }

        public void PostInitialise()
        {
            if (m_enabled)
            {
                try
                {
                    m_tcpPort = m_config.Configs["Comms"].GetInt("remoting_port", m_tcpPort);
                }
                catch
                {
                }

                internal_CreateRemotingObjects();
            }
        }

        public void Close()
        {
            ChannelServices.UnregisterChannel(m_tcpChannel);
        }

        public string Name
        {
            get { return "InterregionModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        private void RegisterRemotingInterface(RemotingObject remote)
        {
            Location[] locs = remote.GetLocations();
            string[] interfaces = remote.GetInterfaces();
            foreach (Location loc in locs)
            {
                m_neighbourInterfaces[loc] = interfaces;
                m_neighbourRemote[loc] = remote;
            }
        }
    }
}