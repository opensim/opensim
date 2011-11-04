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
 *     * Neither the name of the OpenSimulator Project nor the
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
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.ApplicationPlugins.Rest.Inventory.Tests
{
    public class Remote : ITest
    {
        private static readonly int PARM_TESTID      = 0;
        private static readonly int PARM_COMMAND     = 1;

        private static readonly int PARM_MOVE_AVATAR = 2;
        private static readonly int PARM_MOVE_X      = 3;
        private static readonly int PARM_MOVE_Y      = 4;
        private static readonly int PARM_MOVE_Z      = 5;

        private bool    enabled = false;

        // No constructor code is required.

        public Remote()
        {
            Rest.Log.InfoFormat("{0} Remote services constructor", MsgId);
        }

        // Post-construction, pre-enabled initialization opportunity
        // Not currently exploited.

        public void Initialize()
        {
            enabled = true;
            Rest.Log.InfoFormat("{0} Remote services initialized", MsgId);
        }

        // Called by the plug-in to halt REST processing. Local processing is
        // disabled, and control blocks until all current processing has
        // completed. No new processing will be started

        public void Close()
        {
            enabled = false;
            Rest.Log.InfoFormat("{0} Remote services closing down", MsgId);
        }

        // Properties

        internal string MsgId
        {
            get { return Rest.MsgId; }
        }

        // Remote Handler
        // Key information of interest here is the Parameters array, each
        // entry represents an element of the URI, with element zero being
        // the

        public void Execute(RequestData rdata)
        {
            if (!enabled) return;

            // If we can't relate to what's there, leave it for others.

            if (rdata.Parameters.Length == 0 || rdata.Parameters[PARM_TESTID] != "remote")
                return;

            Rest.Log.DebugFormat("{0} REST Remote handler ENTRY", MsgId);

            // Remove the prefix and what's left are the parameters. If we don't have
            // the parameters we need, fail the request. Parameters do NOT include
            // any supplied query values.

            if (rdata.Parameters.Length > 1)
            {
                switch (rdata.Parameters[PARM_COMMAND].ToLower())
                {
                    case "move" :
                        DoMove(rdata);
                        break;
                    default :
                        DoHelp(rdata);
                        break;
                }
            }
            else
            {
                DoHelp(rdata);
            }
        }

        private void DoHelp(RequestData rdata)
        {
            rdata.body = Help;
            rdata.Complete();
            rdata.Respond("Help");
        }

        private void DoMove(RequestData rdata)
        {
            if (rdata.Parameters.Length < 6)
            {
                Rest.Log.WarnFormat("{0} Move: No movement information provided", MsgId);
                rdata.Fail(Rest.HttpStatusCodeBadRequest, "no movement information provided");
            }
            else
            {
                string[] names = rdata.Parameters[PARM_MOVE_AVATAR].Split(Rest.CA_SPACE);
                ScenePresence presence = null;
                Scene scene = null;

                if (names.Length != 2)
                {
                    rdata.Fail(Rest.HttpStatusCodeBadRequest,
                        String.Format("invalid avatar name: <{0}>",rdata.Parameters[PARM_MOVE_AVATAR]));
                }

                Rest.Log.WarnFormat("{0} '{1}' command received for {2} {3}",
                            MsgId, rdata.Parameters[0], names[0], names[1]);

                // The first parameter should be an avatar name, look for the
                // avatar in the known regions first.

                Rest.main.SceneManager.ForEachScene(delegate(Scene s)
                {
                    s.ForEachRootScenePresence(delegate(ScenePresence sp)
                    {
                        if (sp.Firstname == names[0] && sp.Lastname == names[1])
                        {
                            scene = s;
                            presence = sp;
                        }
                    });
                });

                if (presence != null)
                {
                    Rest.Log.DebugFormat("{0} Move : Avatar {1} located in region {2}",
                                MsgId, rdata.Parameters[PARM_MOVE_AVATAR], scene.RegionInfo.RegionName);

                    try
                    {
                        float x = Convert.ToSingle(rdata.Parameters[PARM_MOVE_X]);
                        float y = Convert.ToSingle(rdata.Parameters[PARM_MOVE_Y]);
                        float z = Convert.ToSingle(rdata.Parameters[PARM_MOVE_Z]);
                        Vector3 vector = new Vector3(x, y, z);
                        presence.MoveToTarget(vector, false, false);
                    }
                    catch (Exception e)
                    {
                        rdata.Fail(Rest.HttpStatusCodeBadRequest,
                                   String.Format("invalid parameters: {0}", e.Message));
                    }
                }
                else
                {
                    rdata.Fail(Rest.HttpStatusCodeBadRequest,
                            String.Format("avatar {0} not present", rdata.Parameters[PARM_MOVE_AVATAR]));
                }

                rdata.Complete();
                rdata.Respond("OK");
            }
        }

        private static readonly string Help =
                "<html>"
              + "<head><title>Remote Command Usage</title></head>"
              + "<body>"
              + "<p>Supported commands are:</p>"
              + "<dl>"
              + "<dt>move/avatar-name/x/y/z</dt>"
              + "<dd>moves the specified avatar to another location</dd>"
              + "</dl>"
              + "</body>"
              + "</html>"
        ;
    }
}
