/*
 * GloebitUser.cs is part of OpenSim-MoneyModule-Gloebit
 * Copyright (C) 2015 Gloebit LLC
 *
 * OpenSim-MoneyModule-Gloebit is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * OpenSim-MoneyModule-Gloebit is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with OpenSim-MoneyModule-Gloebit.  If not, see <https://www.gnu.org/licenses/>.
 */

/*
 * GloebitUser.cs
 * 
 * Object representation of an AppUser for use with the GloebitAPI
 * See GloebitUserData.cs for DB implementation
 * Do not confuse this with representing a single Gloebit account.
 * --- This represents a user within the app utilizing this API (referred to 
 * --- as an AppUser).  Multiple AppUsers can be connected to a single Gloebit
 * --- account.
 * Do not confuse this with representing a single local user, though this is
 * --- generally the case.  There can be multiple records for a sinlge local
 * --- account if this product has connected via multiple Gloebit Apps, such as
 * --- to our Sandbox environment and to our Production environment.
 * This stores a record per user per Gloebit App (per OAuth Key).
 * --- The primary function here is to handle storage and retrieval of the
 * --- authorization token and AppUserID for this AppUser necessary for most 
 * --- GloebitAPI calls.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenMetaverse;

namespace Gloebit.GloebitMoneyModule {

    // TODO: Should we consider renaming to GloebitAppUser?
    public class GloebitUser {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public string AppKey;
        public string PrincipalID;
        public string GloebitID;
        public string GloebitToken;
        public string LastSessionID;

        // TODO - update userMap to be a proper LRU Cache
        private static Dictionary<string, GloebitUser> s_userMap = new Dictionary<string, GloebitUser>();

        private object userLock = new object();

        public GloebitUser() {
        }

        private GloebitUser(string appKey, string principalID, string gloebitID, string token, string sessionID) {
            this.AppKey = appKey;
            this.PrincipalID = principalID;
            this.GloebitID = gloebitID;
            this.GloebitToken = token;
            this.LastSessionID = sessionID;
        }

        private GloebitUser(GloebitUser copyFrom) {
            this.AppKey = copyFrom.AppKey;
            this.PrincipalID = copyFrom.PrincipalID;
            this.GloebitID = copyFrom.GloebitID;
            this.GloebitToken = copyFrom.GloebitToken;
            this.LastSessionID = copyFrom.LastSessionID;
        }

        private void UpdateFrom(GloebitUser updateFrom) {
            this.GloebitID = updateFrom.GloebitID;
            this.GloebitToken = updateFrom.GloebitToken;
            this.LastSessionID = updateFrom.LastSessionID;
        }

        public static GloebitUser Get(UUID appKey, UUID agentID) {
            return(GloebitUser.Get(appKey.ToString(), agentID.ToString()));
        }

        public static GloebitUser Get(UUID appKey, string agentIdStr) {
            return(GloebitUser.Get(appKey.ToString(), agentIdStr));
        }

        public static GloebitUser Get(string appKeyStr, UUID agentID) {
            return(GloebitUser.Get(appKeyStr, agentID.ToString()));
        }
            
        public static GloebitUser Get(string appKeyStr, string agentIdStr) {
            m_log.Info("[GLOEBITMONEYMODULE] in GloebitUser.Get");

            GloebitUser u;
            lock(s_userMap) {
                s_userMap.TryGetValue(agentIdStr, out u);
            }

            if (u == null) {
                m_log.DebugFormat("[GLOEBITMONEYMODULE] Looking for prior user for {0}", agentIdStr);
                string[] keys = new string[2]{"AppKey", "PrincipalID"};
                string[] values = new string[2]{appKeyStr, agentIdStr};
                GloebitUser[] users;
                try {
                    users = GloebitUserData.Instance.Get(keys, values);
                } catch(Exception e) {
                    m_log.WarnFormat("[GLOEBITMONEYMODULE] failed GloebitUser.Get because {0}", e);
                    users = new GloebitUser[0];
                }

                switch(users.Length) {
                case 1:
                    u = users[0];
                    m_log.DebugFormat("[GLOEBITMONEYMODULE] FOUND USER TOKEN! {0} valid token? {1} --- SesionID{2}", u.PrincipalID, !String.IsNullOrEmpty(u.GloebitToken), u.LastSessionID);
                    break;
                case 0:
                    m_log.DebugFormat("[GLOEBITMONEYMODULE] CREATING NEW USER {0}", agentIdStr);
                    u = new GloebitUser(appKeyStr, agentIdStr, String.Empty, String.Empty, String.Empty);
                    break;
                default:
                    throw new Exception(String.Format("[GLOEBITMONEYMODULE] Failed to find exactly one prior token for {0}", agentIdStr));
                }

                // Store in map and return GloebitUser
                lock(s_userMap) {
                    // Make sure no one else has already loaded this user
                    GloebitUser alreadyLoadedUser;
                    s_userMap.TryGetValue(agentIdStr, out alreadyLoadedUser);
                    if (alreadyLoadedUser == null) {
                        s_userMap[agentIdStr] = u;
                    } else {
                        u = alreadyLoadedUser;
                    }
                }
            }

            // Create a thread local copy of the user to return.
            GloebitUser localUser;
            lock (u.userLock) {
                localUser = new GloebitUser(u);
            }

            return localUser;
        }

        public static void InvalidateCache(UUID agentID) {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] in GloebitUser.InvalidateCache");
            string agentIdStr = agentID.ToString();
            lock(s_userMap) {
                s_userMap.Remove(agentIdStr);
            }
        }

        public bool IsNewSession(UUID newSessionID) {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] in IsNewSession for last:{0} current:{1}", this.LastSessionID, newSessionID.ToString());
            string newSessionIDStr = newSessionID.ToString();
            if (this.LastSessionID == newSessionIDStr) {
                m_log.DebugFormat("[GLOEBITMONEYMODULE] User is not new session");
                return false;
            }
            // Before we return true, Ensure our cache is up to date
            GloebitUser.InvalidateCache(UUID.Parse(this.PrincipalID));
            GloebitUser u_from_db = GloebitUser.Get(this.AppKey, UUID.Parse(this.PrincipalID));
            if (u_from_db.LastSessionID == newSessionIDStr) {
                m_log.DebugFormat("[GLOEBITMONEYMODULE] User Cache was out of date.  Updated cache.  User is not new session");
                // cache was out of date.  update local user copy form db
                this.UpdateFrom(u_from_db);
                return false;
            } else {
                m_log.DebugFormat("[GLOEBITMONEYMODULE] User is New Session");
                // we have a new session.  Store it and return true.

                // Code to ensure we update user in cache
                GloebitUser u;
                lock (s_userMap) {
                    s_userMap.TryGetValue(this.PrincipalID, out u);
                }
                if (u == null) {
                    m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitUser.IsNewSession() Did not find User in s_userMap to update.  User logged out.");
                    u = u_from_db;  // User logged out.  Still want to store token.  Don't want to add back to map.
                }
                lock (u.userLock) {
                    u.LastSessionID = newSessionIDStr;
                    bool stored = GloebitUserData.Instance.Store(u);
                    if (!stored) {
                        throw new Exception(String.Format("[GLOEBITMONEYMODULE] GloebitUser.IsNewSession Failed to store user {0}", this.PrincipalID));
                    }
                    this.UpdateFrom(u);
                }

                return true;
            }
        }

        public bool IsAuthed() {
            return !String.IsNullOrEmpty(this.GloebitToken);
        }

        // TODO: Why is this static?
        public static GloebitUser Authorize(string appKeyStr, UUID agentId, string token, string gloebitID) {
            string agentIdStr = agentId.ToString();

            // TODO: I think there has to be a better way to do this, but I'm not finding it right now.
            // By calling Get, we make sure that the user is in the map and has any additional data users store.
            GloebitUser localUser = GloebitUser.Get(appKeyStr, agentId);
            GloebitUser u;
            lock (s_userMap) {
                s_userMap.TryGetValue(agentIdStr, out u);
            }
            if (u == null) {
                m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitUser.Authorize() Did not find User in s_userMap.  User logged out.");
                u = localUser;  // User logged out.  Still want to store token.  Don't want to add back to map.
            }
            lock (u.userLock) {
                u.GloebitToken = token;
                u.GloebitID = gloebitID;
                bool stored = GloebitUserData.Instance.Store(u);
                if (!stored) {
                    throw new Exception(String.Format("[GLOEBITMONEYMODULE] GloebitUser.Authorize Failed to store user {0}", agentIdStr));
                }
                localUser = new GloebitUser(u);
            }

            return localUser;
        }

        public void InvalidateToken() {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitUser.InvalidateToken() {0}, valid token? {1}", PrincipalID, !String.IsNullOrEmpty(GloebitToken));

            if(!String.IsNullOrEmpty(GloebitToken)) {
                GloebitUser u;
                lock (s_userMap) {
                    s_userMap.TryGetValue(PrincipalID, out u);
                }
                if (u == null) {
                    u = this;   // User logged out.  Still want to invalidate token.  Don't want to add back to map.
                }
                lock (u.userLock) {
                    if (GloebitToken != u.GloebitToken) {
                        // Someone else invalidated it already or authorized it.
                        // Don't overwrite
                        // TODO: should we set this equal to a copy of u before we return?
                        return;
                    } else {
                        u.GloebitToken = String.Empty;
                        bool stored = GloebitUserData.Instance.Store(u);
                        if (!stored) {
                            throw new Exception(String.Format("[GLOEBITMONEYMODULE] GloebitUser.InvalidateToken Failed to store user {0}", PrincipalID));
                        }
                        // TODO: should we set this equal to a copy of u before we return?
                    }
                }
            }
        }

        // TODO: do we need an Update function to update the local user from the one in the map?
        // Ideally, users are thrown away after use, but we should review.

        public static void Cleanup(UUID agentId) {
            InvalidateCache(agentId);
        }
    }
}
