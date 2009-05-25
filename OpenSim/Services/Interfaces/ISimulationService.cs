using System;
using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Services.Interfaces
{
    public interface ISimulationService
    {
        #region Agents

        bool CreateAgent(ulong regionHandle, AgentCircuitData aCircuit, out string reason);

        /// <summary>
        /// Full child agent update.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        bool UpdateAgent(ulong regionHandle, AgentData data);

        /// <summary>
        /// Short child agent update, mostly for position.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        bool UpdateAgent(ulong regionHandle, AgentPosition data);

        bool RetrieveAgent(ulong regionHandle, UUID id, out IAgentData agent);

        /// <summary>
        /// Message from receiving region to departing region, telling it got contacted by the client.
        /// When sent over REST, it invokes the opaque uri.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="id"></param>
        /// <param name="uri"></param>
        /// <returns></returns>
        bool ReleaseAgent(ulong regionHandle, UUID id, string uri);

        /// <summary>
        /// Close agent.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        bool CloseAgent(ulong regionHandle, UUID id);

        #endregion Agents

        #region Objects

        /// <summary>
        /// Create an object in the destination region. This message is used primarily for prim crossing.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="sog"></param>
        /// <param name="isLocalCall"></param>
        /// <returns></returns>
        bool CreateObject(ulong regionHandle, ISceneObject sog, bool isLocalCall);

        /// <summary>
        /// Create an object from the user's inventory in the destination region. 
        /// This message is used primarily by clients.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="userID"></param>
        /// <param name="itemID"></param>
        /// <returns></returns>
        bool CreateObject(ulong regionHandle, UUID userID, UUID itemID);

        #endregion Objects

        #region Regions

        bool HelloNeighbour(ulong regionHandle, RegionInfo thisRegion);

        #endregion Regions

    }
}
