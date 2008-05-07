/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework;

namespace OpenSim.Region.Environment.Scenes
{
    public class SceneExternalChecks
    {
        private Scene m_scene;

        public SceneExternalChecks(Scene scene)
        {
            m_scene = scene;
        }

        #region REZ OBJECT
        public delegate bool CanRezObject(int objectCount, LLUUID owner, IScene scene, LLVector3 objectPosition);
        private List<CanRezObject> CanRezObjectCheckFunctions = new List<CanRezObject>();

        public void addCheckRezObject(CanRezObject delegateFunc)
        {
            if(!CanRezObjectCheckFunctions.Contains(delegateFunc))
                CanRezObjectCheckFunctions.Add(delegateFunc);
        }
        public void removeCheckRezObject(CanRezObject delegateFunc)
        {
            if (CanRezObjectCheckFunctions.Contains(delegateFunc))
                CanRezObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanRezObject(int objectCount, LLUUID owner, LLVector3 objectPosition)
        {
            foreach (CanRezObject check in CanRezObjectCheckFunctions)
            {
                if (check(objectCount, owner, m_scene, objectPosition) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region RUN SCRIPT
        public delegate bool CanRunScript(LLUUID script, LLUUID owner, IScene scene);
        private List<CanRunScript> CanRunScriptCheckFunctions = new List<CanRunScript>();

        public void addCheckRunScript(CanRunScript delegateFunc)
        {
            if (!CanRunScriptCheckFunctions.Contains(delegateFunc))
                CanRunScriptCheckFunctions.Add(delegateFunc);
        }
        public void removeCheckRunScript(CanRunScript delegateFunc)
        {
            if (CanRunScriptCheckFunctions.Contains(delegateFunc))
                CanRunScriptCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanRunScript(LLUUID script, LLUUID owner)
        {
            foreach (CanRunScript check in CanRunScriptCheckFunctions)
            {
                if (check(script,owner,m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

    }
}
