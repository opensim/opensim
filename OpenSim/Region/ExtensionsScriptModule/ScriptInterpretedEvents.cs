using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Environment.Scenes;
using libsecondlife;
using Key = libsecondlife.LLUUID;

namespace OpenSim.Region.ExtensionsScriptModule
{

    public class ScriptInterpretedEvents
    {
        public delegate void OnTouchStartDelegate(Key user);
        public event OnTouchStartDelegate OnTouchStart;


        public void TriggerTouchStart(Key user)
        {
            if (OnTouchStart != null)
                OnTouchStart(user);
        }
    }
}
