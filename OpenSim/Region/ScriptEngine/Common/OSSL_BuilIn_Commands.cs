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
using OpenMetaverse;
using Nini.Config;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.ScriptEngine.Common.ScriptEngineBase;
using TPFlags = OpenSim.Framework.Constants.TeleportFlags;
//using OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL;

namespace OpenSim.Region.ScriptEngine.Common
{
    public class OSSL_BuilIn_Commands : LSL_BuiltIn_Commands, OSSL_BuilIn_Commands_Interface
    {
        public OSSL_BuilIn_Commands(ScriptEngineBase.ScriptEngine scriptEngine, SceneObjectPart host, uint localID,
                                    UUID itemID)
            : base(scriptEngine, host, localID, itemID)
        {
            Prim = new OSSLPrim(this);
        }

        public OSSLPrim Prim;

        [Serializable]
        public class OSSLPrim
        {
            internal OSSL_BuilIn_Commands OSSL;
            public OSSLPrim(OSSL_BuilIn_Commands bc)
            {
                OSSL = bc;
                Position = new OSSLPrim_Position(this);
                Rotation = new OSSLPrim_Rotation(this);
            }

            public OSSLPrim_Position Position;
            public OSSLPrim_Rotation Rotation;
            //public LSL_Types.Vector3 Position
            //{
            //    get { return OSSL.llGetPos(); }
            //    set { OSSL.llSetPos(value); }
            //}
            //public LSL_Types.Quaternion Rotation
            //{
            //    get { return OSSL.llGetRot(); }
            //    set { OSSL.llSetRot(value); }
            //}
            private TextStruct _text;
            public TextStruct Text
            {
                get { return _text; }
                set
                {
                    _text = value;
                    OSSL.llSetText(_text.Text, _text.color, _text.alpha);
                }
            }

            [Serializable]
            public struct TextStruct
            {
                public string Text;
                public LSL_Types.Vector3 color;
                public double alpha;
            }
        }

        [Serializable]
        public class OSSLPrim_Position
        {
            private OSSLPrim prim;
            private LSL_Types.Vector3 Position;
            public OSSLPrim_Position(OSSLPrim _prim)
            {
                prim = _prim;
            }
            private void Load()
            {
                Position = prim.OSSL.llGetPos();
            }
            private void Save()
            {
                if (Position.x > 255)
                    Position.x = 255;
                if (Position.x < 0)
                    Position.x = 0;
                if (Position.y > 255)
                    Position.y = 255;
                if (Position.y < 0)
                    Position.y = 0;
                if (Position.z > 768)
                    Position.z = 768;
                if (Position.z < 0)
                    Position.z = 0;
                prim.OSSL.llSetPos(Position);
            }

            public double x
            {
                get
                {
                    Load();
                    return Position.x;
                }
                set
                {
                    Load();
                    Position.x = value;
                    Save();
                }
            }
            public double y
            {
                get
                {
                    Load();
                    return Position.y;
                }
                set
                {
                    Load();
                    Position.y = value;
                    Save();
                }
            }
            public double z
            {
                get
                {
                    Load();
                    return Position.z;
                }
                set
                {
                    Load();
                    Position.z = value;
                    Save();
                }
            }
        }

        [Serializable]
        public class OSSLPrim_Rotation
        {
            private OSSLPrim prim;
            private LSL_Types.Quaternion Rotation;
            public OSSLPrim_Rotation(OSSLPrim _prim)
            {
                prim = _prim;
            }
            private void Load()
            {
                Rotation = prim.OSSL.llGetRot();
            }
            private void Save()
            {
                prim.OSSL.llSetRot(Rotation);
            }

            public double x
            {
                get
                {
                    Load();
                    return Rotation.x;
                }
                set
                {
                    Load();
                    Rotation.x = value;
                    Save();
                }
            }
            public double y
            {
                get
                {
                    Load();
                    return Rotation.y;
                }
                set
                {
                    Load();
                    Rotation.y = value;
                    Save();
                }
            }
            public double z
            {
                get
                {
                    Load();
                    return Rotation.z;
                }
                set
                {
                    Load();
                    Rotation.z = value;
                    Save();
                }
            }
            public double s
            {
                get
                {
                    Load();
                    return Rotation.s;
                }
                set
                {
                    Load();
                    Rotation.s = value;
                    Save();
                }
            }
        }
        //public struct OSSLPrim_Rotation
        //{
        //    public double X;
        //    public double Y;
        //    public double Z;
        //    public double R;
        //}


                //
        // OpenSim functions
        //

        public int osTerrainSetHeight(int x, int y, double val)
        {
            m_host.AddScriptLPS(1);
            if (x > 255 || x < 0 || y > 255 || y < 0)
                LSLError("osTerrainSetHeight: Coordinate out of bounds");

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
            m_host.AddScriptLPS(1);
            if (x > 255 || x < 0 || y > 255 || y < 0)
                LSLError("osTerrainGetHeight: Coordinate out of bounds");

            return World.Heightmap[x, y];
        }

        public int osRegionRestart(double seconds)
        {
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
            m_host.AddScriptLPS(1);
            World.SendGeneralAlert(msg);
        }

        public void osSetRot(UUID target, Quaternion rotation)
        {
            m_host.AddScriptLPS(1);
            if (World.Entities.ContainsKey(target))
            {
                World.Entities[target].Rotation = rotation;
            }
            else
            {
                LSLError("osSetRot: Invalid target");
            }
        }

        public string osSetDynamicTextureURL(string dynamicID, string contentType, string url, string extraParams,
                                             int timer)
        {
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
            m_host.AddScriptLPS(1);
            IConfigSource config = new IniConfigSource(Application.iniFilePath);
            if (config.Configs["LL-Functions"] == null)
                config.AddConfig("LL-Functions");

            if (config.Configs["LL-Functions"].GetBoolean("AllowosConsoleCommand", false))
            {
                if (World.ExternalChecks.ExternalChecksCanRunConsoleCommand(m_host.OwnerID))
                {
                    MainConsole.Instance.RunCommand(command);
                    return true;
                }
                return false;
            }
            return false;
        }
        public void osSetPrimFloatOnWater(int floatYN)
        {
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
                        // ScriptSleep(5000);

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
            m_host.AddScriptLPS(1);
            if (World.Entities.ContainsKey(avatar) && World.Entities[avatar] is ScenePresence)
            {
                ScenePresence target = (ScenePresence)World.Entities[avatar];
                target.AddAnimation(avatar);
            }
        }

        public void osAvatarStopAnimation(string avatar, string animation)
        {
            m_host.AddScriptLPS(1);
            if (World.Entities.ContainsKey(avatar) && World.Entities[avatar] is ScenePresence)
            {
                ScenePresence target = (ScenePresence)World.Entities[avatar];
                target.RemoveAnimation(animation);
            }
        }

        //Texture draw functions
        public string osMovePen(string drawList, int x, int y)
        {
            m_host.AddScriptLPS(1);
            drawList += "MoveTo " + x + "," + y + ";";
            return drawList;
        }

        public string osDrawLine(string drawList, int startX, int startY, int endX, int endY)
        {
            m_host.AddScriptLPS(1);
            drawList += "MoveTo "+ startX+","+ startY +"; LineTo "+endX +","+endY +"; ";
            return drawList;
        }

        public string osDrawLine(string drawList, int endX, int endY)
        {
            m_host.AddScriptLPS(1);
            drawList += "LineTo " + endX + "," + endY + "; ";
            return drawList;
        }

        public string osDrawText(string drawList, string text)
        {
            m_host.AddScriptLPS(1);
            drawList += "Text " + text + "; ";
            return drawList;
        }

        public string osDrawEllipse(string drawList, int width, int height)
        {
            m_host.AddScriptLPS(1);
            drawList += "Ellipse " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawRectangle(string drawList, int width, int height)
        {
            m_host.AddScriptLPS(1);
            drawList += "Rectangle " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawFilledRectangle(string drawList, int width, int height)
        {
            m_host.AddScriptLPS(1);
            drawList += "FillRectangle " + width + "," + height + "; ";
            return drawList;
        }

        public string osSetFontSize(string drawList, int fontSize)
        {
            m_host.AddScriptLPS(1);
            drawList += "FontSize "+ fontSize +"; ";
            return drawList;
        }

        public string osSetPenSize(string drawList, int penSize)
        {
            m_host.AddScriptLPS(1);
            drawList += "PenSize " + penSize + "; ";
            return drawList;
        }

        public string osSetPenColour(string drawList, string colour)
        {
            m_host.AddScriptLPS(1);
            drawList += "PenColour " + colour + "; ";
            return drawList;
        }

        public string osDrawImage(string drawList, int width, int height, string imageUrl)
        {
           m_host.AddScriptLPS(1);
           drawList +="Image " +width + "," + height+ ","+ imageUrl +"; " ;
           return drawList;
        }

        public void osSetStateEvents(int events)
        {
            m_host.SetScriptEvents(m_itemID, events);
        }

        public void osOpenRemoteDataChannel(string channel)
        {
            m_host.AddScriptLPS(1);
            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            if (xmlrpcMod.IsEnabled())
            {
                UUID channelID = xmlrpcMod.OpenXMLRPCChannel(m_localID, m_itemID, new UUID(channel));
                object[] resobj = new object[] { new LSL_Types.LSLInteger(1), new LSL_Types.LSLString(channelID.ToString()), new LSL_Types.LSLString(UUID.Zero.ToString()), new LSL_Types.LSLString(String.Empty), new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(String.Empty) };
                m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(m_localID, m_itemID, "remote_data", EventQueueManager.llDetectNull, resobj);
            }
        }

        public string osGetScriptEngineName()
        {
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

        //for testing purposes only
        public void osSetParcelMediaTime(double time)
        {
            World.ParcelMediaSetTime((float)time);
        }
        
        public Hashtable osParseJSON(string JSON)
        {
            m_host.AddScriptLPS(1);

            // see http://www.json.org/ for more details on JSON
            
            string currentKey=null;
            Stack objectStack = new Stack(); // objects in JSON can be nested so we need to keep a track of this
            Hashtable jsondata = new Hashtable(); // the hashtable to be returned
            
            try
            {
                
                // iterate through the serialised stream of tokens and store at the right depth in the hashtable
                // the top level hashtable may contain more nested hashtables within it each containing an objects representation
                for (int i=0;i<JSON.Length; i++)
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
                            
                            // just loop through until the next quote mark storing the string
                            while (JSON[i]!='"')
                            {
                                tokenValue+=JSON[i++];
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
                            }
                            //advance the counter to the letter 'e'
                            i = i+4;
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
                LSLError("osParseJSON: The JSON string is not valid " + JSON);
            }
            
            return jsondata;                        
        }     
    }
}
