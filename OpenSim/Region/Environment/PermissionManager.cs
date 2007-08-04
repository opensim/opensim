using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Framework.Types;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Region.Capabilities;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.LandManagement;

using libsecondlife;

namespace OpenSim.Region.Environment
{
    public class PermissionManager
    {
        protected Scene m_scene;

        public PermissionManager(Scene world)
        {
            m_scene = world;
        }

        public delegate void OnPermissionErrorDelegate(LLUUID user, string reason);
        public event OnPermissionErrorDelegate OnPermissionError;

        protected virtual void SendPermissionError(LLUUID user, string reason)
        {
            if (OnPermissionError != null)
                OnPermissionError(user, reason);
        }

        protected virtual bool IsAdministrator(LLUUID user)
        {
            return m_scene.RegionInfo.MasterAvatarAssignedUUID == user;
        }

        protected virtual bool IsEstateManager(LLUUID user)
        {
            return false;
        }

        public virtual bool CanRezObject(LLUUID user, LLVector3 position)
        {
            return true;
        }

        /// <summary>
        /// Permissions check - can user delete an object?
        /// </summary>
        /// <param name="user">User attempting the delete</param>
        /// <param name="obj">Target object</param>
        /// <returns>Has permission?</returns>
        public virtual bool CanDeRezObject(LLUUID user, LLUUID obj)
        {
            // Default: deny
            bool canDeRez = false;

            // If it's not an object, we cant derez it.
            if (!(m_scene.Entities[obj] is SceneObject))
                return false;

            SceneObject task = (SceneObject)m_scene.Entities[obj];
            LLUUID taskOwner = null; // Since we dont have a 'owner' property on task yet

            // Object owners should be able to delete their own content
            if (user == taskOwner)
                canDeRez = true;

            // Users should be able to delete what is over their land.
            if (m_scene.LandManager.getLandObject(task.Pos.X, task.Pos.Y).landData.ownerID == user)
                canDeRez = true;

            // Estate users should be able to delete anything in the sim
            if (IsEstateManager(user))
                canDeRez = true;

            // Admin objects should not be deletable by the above
            if (IsAdministrator(taskOwner))
                canDeRez = false;

            // Admin should be able to delete anything in the sim (including admin objects)
            if (IsAdministrator(user))
                canDeRez = true;

            return canDeRez;
        }

        public virtual bool CanEditObject(LLUUID user, LLUUID obj)
        {
            // Permissions for editing fall into the same category as deleting
            // May need to add check for "no-mod" items.
            return CanDeRezObject(user, obj);
        }

        public virtual bool CanEditScript(LLUUID user, LLUUID script)
        {
            return false;
        }

        public virtual bool CanRunScript(LLUUID user, LLUUID script)
        {
            return false;
        }

        public virtual bool CanReturnObject(LLUUID user, LLUUID obj)
        {
            // Same category as deleting, but eventually will need seperate check
            // as sometimes it's better to allow returning only.
            return CanDeRezObject(user, obj);
        }

        public virtual bool CanTerraform(LLUUID user, LLUUID position)
        {
            return false;
        }

        public virtual bool CanEditEstateSettings(LLUUID user)
        {
            // Default: deny
            bool canEdit = false;

            // Estate admins should be able to use estate tools
            if (IsEstateManager(user))
                canEdit = true;

            // Administrators always have permission
            if (IsAdministrator(user))
                canEdit = true;

            return canEdit;
        }

        public virtual bool CanEditParcel(LLUUID user, Land parcel)
        {
            return false;
        }

        public virtual bool CanSellParcel(LLUUID user, Land parcel)
        {
            return false;
        }

        public virtual bool CanAbandonParcel(LLUUID user, Land parcel)
        {
            return false;
        }
    }
}
