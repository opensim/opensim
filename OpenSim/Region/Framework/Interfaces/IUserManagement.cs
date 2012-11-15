using System;
using System.Collections.Generic;

using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    /// <summary>
    /// This maintains the relationship between a UUID and a user name.
    /// </summary>
    public interface IUserManagement
    {
        string GetUserName(UUID uuid);
        string GetUserHomeURL(UUID uuid);
        string GetUserUUI(UUID uuid);
        string GetUserServerURL(UUID uuid, string serverType);

        /// <summary>
        /// Get user ID by the given name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>UUID.Zero if no user with that name is found or if the name is "Unknown User"</returns>
        UUID GetUserIdByName(string name);

        /// <summary>
        /// Get user ID by the given name.
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <returns>UUID.Zero if no user with that name is found or if the name is "Unknown User"</returns>
        UUID GetUserIdByName(string firstName, string lastName);

        /// <summary>
        /// Add a user.
        /// </summary>
        /// <remarks>
        /// If an account is found for the UUID, then the names in this will be used rather than any information
        /// extracted from creatorData.
        /// </remarks>
        /// <param name="uuid"></param>
        /// <param name="creatorData">The creator data for this user.</param>
        void AddUser(UUID uuid, string creatorData);

        /// <summary>
        /// Add a user.
        /// </summary>
        /// <remarks>
        /// The UUID is related to the name without any other checks being performed, such as user account presence.
        /// </remarks>
        /// <param name="uuid"></param>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        void AddUser(UUID uuid, string firstName, string lastName);

        /// <summary>
        /// Add a user.
        /// </summary>
        /// <remarks>
        /// The arguments apart from uuid are formed into a creatorData string and processing proceeds as for the
        /// AddUser(UUID uuid, string creatorData) method.
        /// </remarks>
        /// <param name="uuid"></param>
        /// <param name="firstName"></param>
        /// <param name="profileURL"></param>
        void AddUser(UUID uuid, string firstName, string lastName, string homeURL);

        bool IsLocalGridUser(UUID uuid);
    }
}
