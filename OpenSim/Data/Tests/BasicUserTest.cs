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

// TODO: Money Transfer, Inventory Transfer and UpdateUserRegion once they exist

using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;
using OpenMetaverse;

namespace OpenSim.Data.Tests
{
    public class BasicUserTest
    {
        public IUserDataPlugin db;
        public UUID user1;
        public UUID user2;
        public UUID user3;
        public UUID user4;
        public UUID webkey;
        public UUID zero = UUID.Zero;
        public Random random;

        public UUID agent1;
        public UUID agent2;
        public UUID agent3;
        public UUID agent4;
        
        public UUID region1;
        
        public string fname0;
        public string lname0;
        public string fname1;
        public string lname1;
        public string fname2;
        public string lname2;
        public string fname3;
        public string lname3;

        public void SuperInit()
        {
            try
            {
                log4net.Config.XmlConfigurator.Configure();
            }
            catch (Exception)
            {
                // I don't care, just leave log4net off
            }
            random = new Random();
            user1 = UUID.Random();
            user2 = UUID.Random();
            user3 = UUID.Random();
            user4 = UUID.Random();
            agent1 = UUID.Random();
            agent2 = UUID.Random();
            agent3 = UUID.Random();
            agent4 = UUID.Random();
            webkey = UUID.Random();
            region1 = UUID.Random();
            fname0 = RandomName(random);
            lname0 = RandomName(random);
            fname1 = RandomName(random);
            lname1 = RandomName(random);
            fname2 = RandomName(random);
            lname2 = RandomName(random);
            fname3 = RandomName(random);
            lname3 = RandomName(random);
        }

        [Test]
        public void T001_LoadEmpty()
        {
            Assert.That(db.GetUserByUUID(zero), Is.Null);
            Assert.That(db.GetUserByUUID(user1), Is.Null);
            Assert.That(db.GetUserByUUID(user2), Is.Null);
            Assert.That(db.GetUserByUUID(user3), Is.Null);
            Assert.That(db.GetUserByUUID(UUID.Random()), Is.Null);

            Assert.That(db.GetAgentByUUID(zero), Is.Null);
            Assert.That(db.GetAgentByUUID(agent1), Is.Null);
            Assert.That(db.GetAgentByUUID(agent2), Is.Null);
            Assert.That(db.GetAgentByUUID(agent3), Is.Null);
            Assert.That(db.GetAgentByUUID(UUID.Random()), Is.Null);
        }

        [Test]
        public void T999_StillNull()
        {
            Assert.That(db.GetUserByUUID(zero), Is.Null);
            Assert.That(db.GetAgentByUUID(zero), Is.Null);
        }

        [Test]
        public void T010_CreateUser()
        {
            UserProfileData u1 = NewUser(user1,fname1,lname1);
            UserProfileData u2 = NewUser(user2,fname2,lname2);
            UserProfileData u3 = NewUser(user3,fname3,lname3);

            db.AddNewUserProfile(u1);
            db.AddNewUserProfile(u2);
            db.AddNewUserProfile(u3);
            UserProfileData u1a = db.GetUserByUUID(user1);
            UserProfileData u2a = db.GetUserByUUID(user2);
            UserProfileData u3a = db.GetUserByUUID(user3);
            Assert.That(user1,Is.EqualTo(u1a.ID));
            Assert.That(user2,Is.EqualTo(u2a.ID));
            Assert.That(user3,Is.EqualTo(u3a.ID));
        }
        
        [Test]
        public void T011_FetchUserByName()
        {
            UserProfileData u1 = db.GetUserByName(fname1,lname1);
            UserProfileData u2 = db.GetUserByName(fname2,lname2);
            UserProfileData u3 = db.GetUserByName(fname3,lname3);
            Assert.That(user1,Is.EqualTo(u1.ID));
            Assert.That(user2,Is.EqualTo(u2.ID));
            Assert.That(user3,Is.EqualTo(u3.ID));
        }

        [Test]
        public void T012_UpdateUserProfile()
        {
            UserProfileData u1 = db.GetUserByUUID(user1);
            Assert.That(fname1,Is.EqualTo(u1.FirstName));
            u1.FirstName = "Ugly";
            
            db.UpdateUserProfile(u1);
            Assert.That("Ugly",Is.EqualTo(u1.FirstName));                   
        }

        [Test]
        public void T013_StoreUserWebKey()
        {
            UserProfileData u1 = db.GetUserByUUID(user1);
            Assert.That(u1.WebLoginKey,Is.EqualTo(zero));
            db.StoreWebLoginKey(user1, webkey);
            u1 = db.GetUserByUUID(user1);
            Assert.That(u1.WebLoginKey,Is.EqualTo(webkey));
        }
        
        [Test]
        public void T014_ExpectedNullReferenceReturns()
        {
            UserProfileData u0 = NewUser(zero,fname0,lname0); 
            UserProfileData u4 = NewUser(user4,fname2,lname2);
            db.AddNewUserProfile(u0);
            db.AddNewUserProfile(u4);
            Assert.That(db.GetUserByUUID(zero),Is.Null);
            Assert.That(db.GetUserByUUID(user4),Is.Null);
        }        

        [Test]
        public void T020_CreateAgent()
        {
            UserAgentData a1 = NewAgent(user1,agent1);
            UserAgentData a2 = NewAgent(user2,agent2);
            UserAgentData a3 = NewAgent(user3,agent3);
            db.AddNewUserAgent(a1);
            db.AddNewUserAgent(a2);
            db.AddNewUserAgent(a3);
            UserAgentData a1a = db.GetAgentByUUID(user1);
            UserAgentData a2a = db.GetAgentByUUID(user2);
            UserAgentData a3a = db.GetAgentByUUID(user3);
            Assert.That(agent1,Is.EqualTo(a1a.SessionID));
            Assert.That(user1,Is.EqualTo(a1a.ProfileID));
            Assert.That(agent2,Is.EqualTo(a2a.SessionID));
            Assert.That(user2,Is.EqualTo(a2a.ProfileID));
            Assert.That(agent3,Is.EqualTo(a3a.SessionID));
            Assert.That(user3,Is.EqualTo(a3a.ProfileID));
        }
        
        [Test]
        public void T021_FetchAgentByName()
        {
            String name3 = fname3 + " " + lname3;
            UserAgentData a2 = db.GetAgentByName(fname2,lname2);
            UserAgentData a3 = db.GetAgentByName(name3);
            Assert.That(user2,Is.EqualTo(a2.ProfileID));
            Assert.That(user3,Is.EqualTo(a3.ProfileID));                    
        }
        
        [Test]
        public void T022_ExceptionalCases()
        {
            // This will follow User behavior, return Null, in the future
            UserAgentData a0 = NewAgent(user4,zero);
            UserAgentData a4 = NewAgent(zero,agent4);
            db.AddNewUserAgent(a0);
            db.AddNewUserAgent(a4);
            Assert.That(db.GetAgentByUUID(user4),Is.Null);
            Assert.That(db.GetAgentByUUID(zero),Is.Null);
        }
        
        [Test]
        public void T030_CreateFriendList()
        {
            Dictionary<UUID, uint> perms = new Dictionary<UUID,uint>();
            Dictionary<UUID, int> friends = new Dictionary<UUID,int>();
            uint temp;
            int tempu1, tempu2;
            db.AddNewUserFriend(user1,user2, 1);
            db.AddNewUserFriend(user1,user3, 2);
            db.AddNewUserFriend(user1,user2, 4); 
            List<FriendListItem> fl1 = db.GetUserFriendList(user1);
            Assert.That(fl1.Count,Is.EqualTo(2));                   
            perms.Add(user2,1);
            perms.Add(user3,2);
            for (int i = 0; i < fl1.Count; i++)
            {   
                Assert.That(user1,Is.EqualTo(fl1[i].FriendListOwner));
                friends.Add(fl1[i].Friend,1);
                temp = perms[fl1[i].Friend];
                Assert.That(temp,Is.EqualTo(fl1[i].FriendPerms));
            }
            tempu1 = friends[user2];
            tempu2 = friends[user3];
            Assert.That(1,Is.EqualTo(tempu1) & Is.EqualTo(tempu2));
        }
        
        [Test]
        public void T031_RemoveUserFriend()
        // user1 has 2 friends, user2 and user3.
        {
            List<FriendListItem> fl1 = db.GetUserFriendList(user1);
            List<FriendListItem> fl2 = db.GetUserFriendList(user2);

            Assert.That(fl1.Count,Is.EqualTo(2));
            Assert.That(fl1[0].Friend,Is.EqualTo(user2) | Is.EqualTo(user3));
            Assert.That(fl2[0].Friend,Is.EqualTo(user1));
            db.RemoveUserFriend(user2, user1);
            
            fl1 = db.GetUserFriendList(user1);
            fl2 = db.GetUserFriendList(user2);
            Assert.That(fl1.Count,Is.EqualTo(1));
            Assert.That(fl1[0].Friend, Is.EqualTo(user3));
            Assert.That(fl2, Is.Empty);
        }
        
        [Test]
        public void T032_UpdateFriendPerms()
        // user1 has 1 friend, user3, who has permission 2 in T030.
        {
            List<FriendListItem> fl1 = db.GetUserFriendList(user1);
            Assert.That(fl1[0].FriendPerms,Is.EqualTo(2));
            db.UpdateUserFriendPerms(user1, user3, 4);
            
            fl1 = db.GetUserFriendList(user1);
            Assert.That(fl1[0].FriendPerms,Is.EqualTo(4));                  
        }
        
        [Test]
        public void T040_UserAppearance()
        {
            AvatarAppearance appear = new AvatarAppearance();
            appear.Owner = user1;
            db.UpdateUserAppearance(user1, appear);
            AvatarAppearance user1app = db.GetUserAppearance(user1);
            Assert.That(user1,Is.EqualTo(user1app.Owner));
        }

        public UserProfileData NewUser(UUID id,string fname,string lname)
        {
            UserProfileData u = new UserProfileData();
            u.ID = id;
            u.FirstName = fname;
            u.SurName = lname;
            u.PasswordHash = "NOTAHASH";
            u.PasswordSalt = "NOTSALT";                     
            // MUST specify at least these 5 parameters or an exception is raised

            return u;
        }
        
        public UserAgentData NewAgent(UUID user_profile, UUID agent)
        {
            UserAgentData a = new UserAgentData();
            a.ProfileID = user_profile;
            a.SessionID = agent;
            a.SecureSessionID = UUID.Random();
            a.AgentIP = RandomName(random);
            return a;
        }
        
        public static string RandomName(Random random)
        {
            StringBuilder name = new StringBuilder();
            int size = random.Next(5,12); 
            char ch ;
            for(int i=0; i<size; i++)
            {       
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65))) ;
                name.Append(ch);
            }
            return name.ToString();
        }
        
        public void PrintFriendsList(List<FriendListItem> fl)
        {
            Console.WriteLine("Friends {0} and {1} and {2}", agent1, agent2, agent3);
            Console.WriteLine("List owner is {0}",fl[0].FriendListOwner);
            for (int i = 0; i < fl.Count; i++)
            {
                    Console.WriteLine("Friend {0}",fl[i].Friend);
            }

        }
    }
}
