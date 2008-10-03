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
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Lifetime;
using OpenMetaverse;
using Nini.Config;
using OpenSim;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.ScriptEngine.Shared; 
using OpenSim.Region.ScriptEngine.Shared.Api.Plugins; 
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;
using TPFlags = OpenSim.Framework.Constants.TeleportFlags;


namespace OpenSim.Region.ScriptEngine.Shared.Api
{
    //////////////////////////////////////////////////////////////
    //
    // Level description
    //
    // None     - Function is no threat at all. It doesn't constitute
    //            an threat to either users or the system and has no
    //            known side effects
    //
    // Nuisance - Abuse of this command can cause a nuisance to the
    //            region operator, such as log message spew
    //
    // VeryLow  - Extreme levels ob abuse of this function can cause
    //            impaired functioning of the region, or very gullible
    //            users can be tricked into experiencing harmless effects
    //
    // Low      - Intentional abuse can cause crashes or malfunction
    //            under certain circumstances, which can easily be rectified,
    //            or certain users can be tricked into certain situations
    //            in an avoidable manner.
    //
    // Moderate - Intentional abuse can cause denial of service and crashes
    //            with potential of data or state loss, or trusting users
    //            can be tricked into embarrassing or uncomfortable
    //            situationsa.
    //
    // High     - Casual abuse can cause impaired functionality or temporary
    //            denial of service conditions. Intentional abuse can easily
    //            cause crashes with potential data loss, or can be used to
    //            trick experienced and cautious users into unwanted situations,
    //            or changes global data permanently and without undo ability
    //
    // VeryHigh - Even normal use may, depending on the number of instances,
    //            or frequency of use, result in severe service impairment
    //            or crash with loss of data, or can be used to cause
    //            unwanted or harmful effects on users without giving the
    //            user a means to avoid it.
    //
    // Severe   - Even casual use is a danger to region stability, or function
    //            allows console or OS command execution, or function allows
    //            taking money without consent, or allows deletion or
    //            modification of user data, or allows the compromise of
    //            sensitive data by design.

    public enum ThreatLevel
    {
        None = 0,
        Nuisance = 1,
        VeryLow = 2,
        Low = 3,
        Moderate = 4,
        High = 5,
        VeryHigh = 6,
        Severe = 7
    };
        
    [Serializable]
    public class OSSL_Api : MarshalByRefObject, IOSSL_Api, IScriptApi
    {
        internal IEventReceiver m_ScriptEngine;
        internal SceneObjectPart m_host;
        internal uint m_localID;
        internal UUID m_itemID;
        internal bool m_OSFunctionsEnabled = false;
        internal ThreatLevel m_MaxThreatLevel = ThreatLevel.VeryLow;
        internal float m_ScriptDelayFactor = 1.0f;
        internal float m_ScriptDistanceFactor = 1.0f;
        internal Dictionary<string, List<UUID> > m_FunctionPerms = new Dictionary<string, List<UUID> >();

        public void Initialize(IEventReceiver ScriptEngine, SceneObjectPart host, uint localID, UUID itemID)
        {
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;

            if (m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
                m_OSFunctionsEnabled = true;

            m_ScriptDelayFactor =
                    m_ScriptEngine.Config.GetFloat("ScriptDelayFactor", 1.0f);
            m_ScriptDistanceFactor =
                    m_ScriptEngine.Config.GetFloat("ScriptDistanceLimitFactor", 1.0f);

            string risk = m_ScriptEngine.Config.GetString("OSFunctionThreatLevel", "VeryLow");
            switch (risk)
            {
            case "None":
                m_MaxThreatLevel = ThreatLevel.None;
                break;
            case "VeryLow":
                m_MaxThreatLevel = ThreatLevel.VeryLow;
                break;
            case "Low":
                m_MaxThreatLevel = ThreatLevel.Low;
                break;
            case "Moderate":
                m_MaxThreatLevel = ThreatLevel.Moderate;
                break;
            case "High":
                m_MaxThreatLevel = ThreatLevel.High;
                break;
            case "VeryHigh":
                m_MaxThreatLevel = ThreatLevel.VeryHigh;
                break;
            case "Severe":
                m_MaxThreatLevel = ThreatLevel.Severe;
                break;
            default:
                break;
            }
        }

        //
        // Never expire this object
        //
        public override Object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.Zero;
            }
            return lease;
        }

        public Scene World
        {
            get { return m_ScriptEngine.World; }
        }

        internal void OSSLError(string msg)
        {
            throw new Exception("OSSL Runtime Error: " + msg);
        }

        protected void CheckThreatLevel(ThreatLevel level, string function)
        {
            if (!m_OSFunctionsEnabled)
                OSSLError(function+": permission denied"); // throws

            if (!m_FunctionPerms.ContainsKey(function))
            {
                string perm = m_ScriptEngine.Config.GetString("Allow_"+function, "true");
                bool allowed;

                if (bool.TryParse(perm, out allowed))
                {
                    // Boolean given
                    if (allowed)
                        m_FunctionPerms[function] = null; // a null value is all
                    else
                        m_FunctionPerms[function] = new List<UUID>(); // Empty list = none
                }
                else
                {
                    m_FunctionPerms[function] = new List<UUID>();

                    string[] ids = perm.Split(new char[] {','});
                    foreach (string id in ids)
                    {
                        string current = id.Trim();
                        UUID uuid;

                        if (UUID.TryParse(current, out uuid))
                        {
                            m_FunctionPerms[function].Add(uuid);
                        }
                    }
                }
            }

            // If the list is null, then the value was true / undefined
            // Threat level governs permissions in this case
            //
            // If the list is non-null, then it is a list of UUIDs allowed
            // to use that particular function. False causes an empty
            // list and therefore means "no one"
            //
            if (m_FunctionPerms[function] == null) // No list = true
            {
                if (level > m_MaxThreatLevel)
                    throw new Exception("Threat level too high - "+function);
            }
            else
            {
                if (!m_FunctionPerms[function].Contains(m_host.OwnerID))
                    throw new Exception("Threat level too high - "+function);
            }
        }

        protected void ScriptSleep(int delay)
        {
            delay = (int)((float)delay * m_ScriptDelayFactor);
            if (delay == 0)
                return;
            System.Threading.Thread.Sleep(delay);
        }

        //
        // OpenSim functions
        //

        public int osTerrainSetHeight(int x, int y, double val)
        {
            CheckThreatLevel(ThreatLevel.High, "osTerrainSetHeight");

            m_host.AddScriptLPS(1);
            if (x > 255 || x < 0 || y > 255 || y < 0)
                OSSLError("osTerrainSetHeight: Coordinate out of bounds");

            if (World.ExternalChecks.ExternalChecksCanTerraformLand(m_host.OwnerID, new Vector3(x, y, 0)))
            {
                World.Heightmap[x, y] = val;
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public double osTerrainGetHeight(int x, int y)
        {
            CheckThreatLevel(ThreatLevel.None, "osTerrainGetHeight");

            m_host.AddScriptLPS(1);
            if (x > 255 || x < 0 || y > 255 || y < 0)
                OSSLError("osTerrainGetHeight: Coordinate out of bounds");

            return World.Heightmap[x, y];
        }

        public int osRegionRestart(double seconds)
        {
            // This is High here because region restart is not reliable
            // it may result in the region staying down or becoming
            // unstable. This should be changed to Low or VeryLow once
            // The underlying functionality is fixed, since the security
            // as such is sound
            //
            CheckThreatLevel(ThreatLevel.High, "osRegionRestart");

            m_host.AddScriptLPS(1);
            if (World.ExternalChecks.ExternalChecksCanIssueEstateCommand(m_host.OwnerID, false))
            {
                World.Restart((float)seconds);
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public void osRegionNotice(string msg)
        {
            // This implementation provides absolutely no security
            // It's high griefing potential makes this classification
            // necessary
            //
            CheckThreatLevel(ThreatLevel.VeryHigh, "osRegionNotice");

            m_host.AddScriptLPS(1);
            World.SendGeneralAlert(msg);
        }

        public void osSetRot(UUID target, Quaternion rotation)
        {
            // This function has no security. It can be used to destroy
            // arbitrary builds the user would normally have no rights to
            //
            CheckThreatLevel(ThreatLevel.VeryHigh, "osSetRot");

            m_host.AddScriptLPS(1);
            if (World.Entities.ContainsKey(target))
            {
                World.Entities[target].Rotation = rotation;
            }
            else
            {
                OSSLError("osSetRot: Invalid target");
            }
        }

        public string osSetDynamicTextureURL(string dynamicID, string contentType, string url, string extraParams,
                                             int timer)
        {
            // This may be upgraded depending on the griefing or DOS
            // potential, or guarded with a delay
            //
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureURL");

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                UUID createdTexture =
                    textureManager.AddDynamicTextureURL(World.RegionInfo.RegionID, m_host.UUID, contentType, url,
                                                        extraParams, timer);
                return createdTexture.ToString();
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return UUID.Zero.ToString();
        }

        public string osSetDynamicTextureURLBlend(string dynamicID, string contentType, string url, string extraParams,
                                             int timer, int alpha)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureURLBlend");

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                UUID createdTexture =
                    textureManager.AddDynamicTextureURL(World.RegionInfo.RegionID, m_host.UUID, contentType, url,
                                                        extraParams, timer, true, (byte) alpha);
                return createdTexture.ToString();
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return UUID.Zero.ToString();
        }

        public string osSetDynamicTextureData(string dynamicID, string contentType, string data, string extraParams,
                                           int timer)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureData");

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                if (textureManager != null)
                {
                    if (extraParams == String.Empty)
                    {
                        extraParams = "256";
                    }
                    UUID createdTexture =
                        textureManager.AddDynamicTextureData(World.RegionInfo.RegionID, m_host.UUID, contentType, data,
                                                            extraParams, timer);
                    return createdTexture.ToString();
                }
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return UUID.Zero.ToString();
        }

        public string osSetDynamicTextureDataBlend(string dynamicID, string contentType, string data, string extraParams,
                                          int timer, int alpha)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureDataBlend");

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                if (textureManager != null)
                {
                    if (extraParams == String.Empty)
                    {
                        extraParams = "256";
                    }
                    UUID createdTexture =
                        textureManager.AddDynamicTextureData(World.RegionInfo.RegionID, m_host.UUID, contentType, data,
                                                            extraParams, timer, true, (byte) alpha);
                    return createdTexture.ToString();
                }
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return UUID.Zero.ToString();
        }

        public bool osConsoleCommand(string command)
        {
            CheckThreatLevel(ThreatLevel.Severe, "osConsoleCommand");

            m_host.AddScriptLPS(1);

            if (World.ExternalChecks.ExternalChecksCanRunConsoleCommand(m_host.OwnerID))
            {
                MainConsole.Instance.RunCommand(command);
                return true;
            }
            return false;
        }

        public void osSetPrimFloatOnWater(int floatYN)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetPrimFloatOnWater");

            m_host.AddScriptLPS(1);
            if (m_host.ParentGroup != null)
            {
                if (m_host.ParentGroup.RootPart != null)
                {
                    m_host.ParentGroup.RootPart.SetFloatOnWater(floatYN);
                }
            }
        }

        // Teleport functions
        public void osTeleportAgent(string agent, string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            // High because there is no security check. High griefer potential
            //
            CheckThreatLevel(ThreatLevel.High, "osTeleportAgent");

            m_host.AddScriptLPS(1);
            UUID agentId = new UUID();
            if (UUID.TryParse(agent, out agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence != null)
                {
                    // agent must be over owners land to avoid abuse
                    if (m_host.OwnerID == World.GetLandOwner(presence.AbsolutePosition.X, presence.AbsolutePosition.Y))
                    {
                        World.RequestTeleportLocation(presence.ControllingClient, regionName,
                            new Vector3((float)position.x, (float)position.y, (float)position.z),
                            new Vector3((float)lookat.x, (float)lookat.y, (float)lookat.z), (uint)TPFlags.ViaLocation);
                        ScriptSleep(5000);
                    }
                }
            }
        }

        public void osTeleportAgent(string agent, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            osTeleportAgent(agent, World.RegionInfo.RegionName, position, lookat);
        }

        // Adam's super super custom animation functions
        public void osAvatarPlayAnimation(string avatar, string animation)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osAvatarPlayAnimation");

            UUID avatarID = (UUID)avatar;

            m_host.AddScriptLPS(1);
            if (World.Entities.ContainsKey((UUID)avatar) && World.Entities[avatarID] is ScenePresence)
            {
                ScenePresence target = (ScenePresence)World.Entities[avatarID];
                target.AddAnimation(animation);
            }
        }

        public void osAvatarStopAnimation(string avatar, string animation)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osAvatarStopAnimation");

            UUID avatarID = (UUID)avatar;

            m_host.AddScriptLPS(1);
            if (World.Entities.ContainsKey(avatarID) && World.Entities[avatarID] is ScenePresence)
            {
                ScenePresence target = (ScenePresence)World.Entities[avatarID];
                target.RemoveAnimation(animation);
            }
        }

        //Texture draw functions
        public string osMovePen(string drawList, int x, int y)
        {
            CheckThreatLevel(ThreatLevel.None, "osMovePen");

            m_host.AddScriptLPS(1);
            drawList += "MoveTo " + x + "," + y + ";";
            return drawList;
        }

        public string osDrawLine(string drawList, int startX, int startY, int endX, int endY)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawLine");

            m_host.AddScriptLPS(1);
            drawList += "MoveTo "+ startX+","+ startY +"; LineTo "+endX +","+endY +"; ";
            return drawList;
        }

        public string osDrawLine(string drawList, int endX, int endY)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawLine");

            m_host.AddScriptLPS(1);
            drawList += "LineTo " + endX + "," + endY + "; ";
            return drawList;
        }

        public string osDrawText(string drawList, string text)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawText");

            m_host.AddScriptLPS(1);
            drawList += "Text " + text + "; ";
            return drawList;
        }

        public string osDrawEllipse(string drawList, int width, int height)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawEllipse");

            m_host.AddScriptLPS(1);
            drawList += "Ellipse " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawRectangle(string drawList, int width, int height)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawRectangle");

            m_host.AddScriptLPS(1);
            drawList += "Rectangle " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawFilledRectangle(string drawList, int width, int height)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawFilledRectangle");

            m_host.AddScriptLPS(1);
            drawList += "FillRectangle " + width + "," + height + "; ";
            return drawList;
        }

        public string osSetFontSize(string drawList, int fontSize)
        {
            CheckThreatLevel(ThreatLevel.None, "osSetFontSize");

            m_host.AddScriptLPS(1);
            drawList += "FontSize "+ fontSize +"; ";
            return drawList;
        }

        public string osSetPenSize(string drawList, int penSize)
        {
            CheckThreatLevel(ThreatLevel.None, "osSetPenSize");

            m_host.AddScriptLPS(1);
            drawList += "PenSize " + penSize + "; ";
            return drawList;
        }

        public string osSetPenColour(string drawList, string colour)
        {
            CheckThreatLevel(ThreatLevel.None, "osSetPenColour");

            m_host.AddScriptLPS(1);
            drawList += "PenColour " + colour + "; ";
            return drawList;
        }

        public string osDrawImage(string drawList, int width, int height, string imageUrl)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawImage");

            m_host.AddScriptLPS(1);
            drawList +="Image " +width + "," + height+ ","+ imageUrl +"; " ;
            return drawList;
        }

        public void osSetStateEvents(int events)
        {
            // This function is a hack. There is no reason for it's existence
            // anymore, since state events now work properly.
            // It was probably added as a crutch or debugging aid, and
            // should be removed
            //
            CheckThreatLevel(ThreatLevel.High, "osSetStateEvents");

            m_host.SetScriptEvents(m_itemID, events);
        }

        public void osSetRegionWaterHeight(double height)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetRegionWaterHeight");

            m_host.AddScriptLPS(1);
            //Check to make sure that the script's owner is the estate manager/master
            //World.Permissions.GenericEstatePermission(
            if (World.ExternalChecks.ExternalChecksCanBeGodLike(m_host.OwnerID))
            {
                World.EventManager.TriggerRequestChangeWaterHeight((float)height);
            }
        }

        public double osList2Double(LSL_Types.list src, int index)
        {
            // There is really no double type in OSSL. C# and other
            // have one, but the current implementation of LSL_Types.list
            // is not allowed to contain any.
            // This really should be removed.
            //
            CheckThreatLevel(ThreatLevel.None, "osList2Double");

            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length)
            {
                return 0.0;
            }
            return Convert.ToDouble(src.Data[index]);
        }

        public void osSetParcelMediaURL(string url)
        {
            // What actually is the difference to the LL function?
            //
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetParcelMediaURL");

            m_host.AddScriptLPS(1);
            UUID landowner = World.GetLandOwner(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y);

            if (landowner == UUID.Zero)
            {
                return;
            }

            if (landowner != m_host.ObjectOwner)
            {
                return;
            }

            World.SetLandMediaURL(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y, url);
        }

        public string osGetScriptEngineName()
        {
            // This gets a "high" because knowing the engine may be used
            // to exploit engine-specific bugs or induce usage patterns
            // that trigger engine-specific failures.
            // Besides, public grid users aren't supposed to know.
            //
            CheckThreatLevel(ThreatLevel.High, "osGetScriptEngineName");

            m_host.AddScriptLPS(1);

            int scriptEngineNameIndex = 0;

            if (!String.IsNullOrEmpty(m_ScriptEngine.ScriptEngineName))
            {
                // parse off the "ScriptEngine."
                scriptEngineNameIndex = m_ScriptEngine.ScriptEngineName.IndexOf(".", scriptEngineNameIndex);
                scriptEngineNameIndex++; // get past delimiter

                int scriptEngineNameLength = m_ScriptEngine.ScriptEngineName.Length - scriptEngineNameIndex;

                // create char array then a string that is only the script engine name
                Char[] scriptEngineNameCharArray = m_ScriptEngine.ScriptEngineName.ToCharArray(scriptEngineNameIndex, scriptEngineNameLength);
                String scriptEngineName = new String(scriptEngineNameCharArray);

                return scriptEngineName;
            }
            else
            {
                return String.Empty;
            }
        }
		
		public string osGetSimulatorVersion()
		{
            // High because it can be used to target attacks to known weaknesses
            // This would allow a new class of griefer scripts that don't even
            // require their user to know what they are doing (see script
            // kiddie)
            //
		    CheckThreatLevel(ThreatLevel.High,"osGetSimulatorVersion");
		    m_host.AddScriptLPS(1);
		    return m_ScriptEngine.World.GetSimulatorVersion();
		}


        //for testing purposes only
        public void osSetParcelMediaTime(double time)
        {
            // This gets very high because I have no idea what it does.
            // If someone knows, please adjust. If it;s no longer needed,
            // please remove.
            //
            CheckThreatLevel(ThreatLevel.VeryHigh, "osSetParcelMediaTime");

            m_host.AddScriptLPS(1);

            World.ParcelMediaSetTime((float)time);
        }
        
        
            
        public Hashtable osParseJSON(string JSON)
        {
            CheckThreatLevel(ThreatLevel.None, "osParseJSON");
            
            m_host.AddScriptLPS(1);

            // see http://www.json.org/ for more details on JSON
            
            string currentKey=null;
            Stack objectStack = new Stack(); // objects in JSON can be nested so we need to keep a track of this
            Hashtable jsondata = new Hashtable(); // the hashtable to be returned
            int i=0;
            try
            {
                
                // iterate through the serialised stream of tokens and store at the right depth in the hashtable
                // the top level hashtable may contain more nested hashtables within it each containing an objects representation
                for (i=0;i<JSON.Length; i++)
                {
                    
                    // Console.WriteLine(""+JSON[i]); 
                    switch (JSON[i])
                    {
                        case '{':
                            // create hashtable and add it to the stack or array if we are populating one, we can have a lot of nested objects in JSON
                            
                            Hashtable currentObject = new Hashtable();  
                            if (objectStack.Count==0) // the stack should only be empty for the first outer object
                            {
                                
                                objectStack.Push(jsondata);
                            }
                            else if (objectStack.Peek().ToString()=="System.Collections.ArrayList")
                            {
                                // add it to the parent array
                                ((ArrayList)objectStack.Peek()).Add(currentObject);
                                objectStack.Push(currentObject);
                            }
                            else
                            { 
                                // add it to the parent hashtable
                                ((Hashtable)objectStack.Peek()).Add(currentKey,currentObject);
                                objectStack.Push(currentObject);
                            }  
                            
                            // clear the key
                            currentKey=null;
                        break;
                        case '}':
                            // pop the hashtable off the stack
                            objectStack.Pop();
                        break;
                        case '"':// string boundary
                            
                            string tokenValue="";
                            i++; // move to next char
                            
                            // just loop through until the next quote mark storing the string, ignore quotes with pre-ceding \
                            while (JSON[i]!='"')
                            {
                                tokenValue+=JSON[i];
                                
                                // handle escaped double quotes \"
                                if (JSON[i]=='\\' && JSON[i+1]=='"')
                                {    
                                    tokenValue+=JSON[i+1];
                                    i++;   
                                }    
                                i++;
                                    
                            }
                            
                            // ok we've got a string, if we've got an array on the top of the stack then we store it
                            if (objectStack.Peek().ToString()=="System.Collections.ArrayList")
                            {
                                ((ArrayList)objectStack.Peek()).Add(tokenValue);
                            }
                            else if (currentKey==null)   // no key stored and its not an array this must be a key so store it
                            {
                                currentKey = tokenValue;
                            }
                            else   
                            {
                                // we have a key so lets store this value
                                ((Hashtable)objectStack.Peek()).Add(currentKey,tokenValue);
                                // now lets clear the key, we're done with it and moving on
                                currentKey=null;
                            }
                        
                        break;
                        case ':':// key : value separator
                        // just ignore
                        break;
                        case ' ':// spaces
                        // just ignore
                        break;
                        case '[': // array start
                            ArrayList currentArray = new ArrayList(); 
                            
                            if (objectStack.Peek().ToString()=="System.Collections.ArrayList")
                            {   
                                ((ArrayList)objectStack.Peek()).Add(currentArray);
                            }
                            else   
                            {  
                                ((Hashtable)objectStack.Peek()).Add(currentKey,currentArray);
                                // clear the key
                                currentKey=null;
                            }
                            objectStack.Push(currentArray);
                        
                        break;
                        case ',':// seperator
                            // just ignore
                        break;
                        case ']'://Array end
                            // pop the array off the stack
                            objectStack.Pop();
                        break;
                        case 't': // we've found a character start not in quotes, it must be a boolean true
                        
                            if (objectStack.Peek().ToString()=="System.Collections.ArrayList")
                            {
                                ((ArrayList)objectStack.Peek()).Add(true);
                            }
                            else
                            { 
                                ((Hashtable)objectStack.Peek()).Add(currentKey,true);
                                currentKey=null;
                            }
                            
                            //advance the counter to the letter 'e'
                            i = i+3;
                        break;
                        case 'f': // we've found a character start not in quotes, it must be a boolean false
                            
                            if (objectStack.Peek().ToString()=="System.Collections.ArrayList")
                            { 
                                ((ArrayList)objectStack.Peek()).Add(false);
                            }
                            else
                            {
                                ((Hashtable)objectStack.Peek()).Add(currentKey,false);
                                currentKey=null;
                            }
                            //advance the counter to the letter 'e'
                            i = i+4;
                        break;
                        case '\n':// carriage return
                            // just ignore
                        break;
                        case '\r':// carriage return
                            // just ignore
                        break;
                        default:
                            // ok here we're catching all numeric types int,double,long we might want to spit these up mr accurately
                            // but for now we'll just do them as strings
                            
                            string numberValue="";
                            
                            // just loop through until the next known marker quote mark storing the string
                            while (JSON[i] != '"' && JSON[i] != ',' && JSON[i] != ']' && JSON[i] != '}' && JSON[i] != ' ')
                            {
                                numberValue+=""+JSON[i++];
                            }
                            
                            i--; // we want to process this caracter that marked the end of this string in the main loop
                            
                            // ok we've got a string, if we've got an array on the top of the stack then we store it
                            if (objectStack.Peek().ToString()=="System.Collections.ArrayList")
                            {
                                ((ArrayList)objectStack.Peek()).Add(numberValue);
                            }
                            else   
                            {
                                // we have a key so lets store this value
                                ((Hashtable)objectStack.Peek()).Add(currentKey,numberValue);
                                // now lets clear the key, we're done with it and moving on
                                currentKey=null;
                            }
                                                    
                        break;
                    }                                                                              
                }                
            }
            catch(Exception)
            {
                OSSLError("osParseJSON: The JSON string is not valid " + JSON) ;
            }
            
            return jsondata;                        
        }     
    }
}
