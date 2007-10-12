using libsecondlife;
using OpenSim.Framework.Interfaces;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Scenes
{
    /// <summary>
    /// A class for triggering remote scene events.
    /// </summary>
    public class EventManager
    {
        public delegate void OnFrameDelegate();

        public event OnFrameDelegate OnFrame;

        public delegate void OnBackupDelegate(IRegionDataStore datastore);

        public event OnBackupDelegate OnBackup;

        public delegate void OnNewClientDelegate(IClientAPI client);

        public event OnNewClientDelegate OnNewClient;

        public delegate void OnNewPresenceDelegate(ScenePresence presence);

        public event OnNewPresenceDelegate OnNewPresence;

        public delegate void OnRemovePresenceDelegate(LLUUID uuid);

        public event OnRemovePresenceDelegate OnRemovePresence;

        public delegate void OnParcelPrimCountUpdateDelegate();

        public event OnParcelPrimCountUpdateDelegate OnParcelPrimCountUpdate;

        public delegate void OnParcelPrimCountAddDelegate(SceneObjectGroup obj);

        public event OnParcelPrimCountAddDelegate OnParcelPrimCountAdd;

        public delegate void OnPluginConsoleDelegate(string[] args);

        public event OnPluginConsoleDelegate OnPluginConsole;

        public delegate void OnShutdownDelegate();

        public event OnShutdownDelegate OnShutdown;

        public delegate void ObjectGrabDelegate(uint localID, LLVector3 offsetPos, IClientAPI remoteClient);

        public delegate void OnPermissionErrorDelegate(LLUUID user, string reason);

        public event ObjectGrabDelegate OnObjectGrab;
        public event OnPermissionErrorDelegate OnPermissionError;

        public delegate void NewRezScript(uint localID, LLUUID itemID, string script);

        public event NewRezScript OnRezScript;

        public delegate void RemoveScript(uint localID, LLUUID itemID);

        public event RemoveScript OnRemoveScript;

        public delegate void SceneGroupMoved(LLUUID groupID, LLVector3 delta);

        public event SceneGroupMoved OnSceneGroupMove;

        public delegate void SceneGroupGrabed(LLUUID groupID, LLVector3 offset);

        public event SceneGroupGrabed OnSceneGroupGrab;

        public void TriggerPermissionError(LLUUID user, string reason)
        {
            if (OnPermissionError != null)
                OnPermissionError(user, reason);
        }

        public void TriggerOnPluginConsole(string[] args)
        {
            if (OnPluginConsole != null)
                OnPluginConsole(args);
        }

        public void TriggerOnFrame()
        {
            if (OnFrame != null)
            {
                OnFrame();
            }
        }

        public void TriggerOnNewClient(IClientAPI client)
        {
            if (OnNewClient != null)
                OnNewClient(client);
        }

        public void TriggerOnNewPresence(ScenePresence presence)
        {
            if (OnNewPresence != null)
                OnNewPresence(presence);
        }

        public void TriggerOnRemovePresence(LLUUID uuid)
        {
            if (OnRemovePresence != null)
            {
                OnRemovePresence(uuid);
            }
        }

        public void TriggerOnBackup(IRegionDataStore dstore)
        {
            if (OnBackup != null)
            {
                OnBackup(dstore);
            }
        }

        public void TriggerParcelPrimCountUpdate()
        {
            if (OnParcelPrimCountUpdate != null)
            {
                OnParcelPrimCountUpdate();
            }
        }

        public void TriggerParcelPrimCountAdd(SceneObjectGroup obj)
        {
            if (OnParcelPrimCountAdd != null)
            {
                OnParcelPrimCountAdd(obj);
            }
        }

        public void TriggerShutdown()
        {
            if (OnShutdown != null)
                OnShutdown();
        }

        public void TriggerObjectGrab(uint localID, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            if (OnObjectGrab != null)
            {
                OnObjectGrab(localID, offsetPos, remoteClient);
            }
        }

        public void TriggerRezScript(uint localID, LLUUID itemID, string script)
        {
            if (OnRezScript != null)
            {
                OnRezScript(localID, itemID, script);
            }
        }

        public void TriggerRemoveScript(uint localID, LLUUID itemID)
        {
            if (OnRemoveScript != null)
            {
                OnRemoveScript(localID, itemID);
            }
        }

        public void TriggerGroupMove(LLUUID groupID, LLVector3 delta)
        {
            if (OnSceneGroupMove != null)
            {
                OnSceneGroupMove(groupID, delta);
            }
        }

        public void TriggerGroupGrab(LLUUID groupID, LLVector3 offset)
        {
            if (OnSceneGroupGrab != null)
            {
                OnSceneGroupGrab(groupID, offset);
            }
        }
    }
}