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

// TODO: Money Transfer, Inventory Transfer and UpdateUserRegion once they exist

using System;
using System.Collections.Generic;
using System.Text;
using log4net.Config;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using log4net;
using System.Reflection;

namespace OpenSim.Data.Tests
{
    public class BasicUserTest
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public IUserDataPlugin db;
        public UUID user1;
        public UUID user2;
        public UUID user3;
        public UUID user4;
        public UUID user5;
        public UUID webkey;
        public UUID zero = UUID.Zero;
        public static Random random;

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
            OpenSim.Tests.Common.TestLogging.LogToConsole();
            random = new Random();
            user1 = UUID.Random();
            user2 = UUID.Random();
            user3 = UUID.Random();
            user4 = UUID.Random();
            user5 = UUID.Random();
            agent1 = UUID.Random();
            agent2 = UUID.Random();
            agent3 = UUID.Random();
            agent4 = UUID.Random();
            webkey = UUID.Random();
            region1 = UUID.Random();
            fname0 = RandomName();
            lname0 = RandomName();
            fname1 = RandomName();
            lname1 = RandomName();
            fname2 = RandomName();
            lname2 = RandomName();
            fname3 = RandomName();
            lname3 = RandomName();
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
        public void T010_CreateUser()
        {
            UserProfileData u1 = NewUser(user1,fname1,lname1);
            UserProfileData u2 = NewUser(user2,fname2,lname2);
            UserProfileData u3 = NewUser(user3,fname3,lname3);
            // this is used to check whether null works here
            u3.Email = null;

            db.AddNewUserProfile(u1);
            db.AddNewUserProfile(u2);
            db.AddNewUserProfile(u3);
            UserProfileData u1a = db.GetUserByUUID(user1);
            UserProfileData u2a = db.GetUserByUUID(user2);
            UserProfileData u3a = db.GetUserByUUID(user3);
            Assert.That(user1,Is.EqualTo(u1a.ID), "Assert.That(user1,Is.EqualTo(u1a.ID))");
            Assert.That(user2,Is.EqualTo(u2a.ID), "Assert.That(user2,Is.EqualTo(u2a.ID))");
            Assert.That(user3,Is.EqualTo(u3a.ID), "Assert.That(user3,Is.EqualTo(u3a.ID))");

            // and one email test
            Assert.That(u3.Email, Is.Null);
        }
        
        [Test]
        public void T011_FetchUserByName()
        {
            UserProfileData u1 = db.GetUserByName(fname1,lname1);
            UserProfileData u2 = db.GetUserByName(fname2,lname2);
            UserProfileData u3 = db.GetUserByName(fname3,lname3);
            Assert.That(user1,Is.EqualTo(u1.ID), "Assert.That(user1,Is.EqualTo(u1.ID))");
            Assert.That(user2,Is.EqualTo(u2.ID), "Assert.That(user2,Is.EqualTo(u2.ID))");
            Assert.That(user3,Is.EqualTo(u3.ID), "Assert.That(user3,Is.EqualTo(u3.ID))");
        }

        [Test]
        public void T012_UpdateUserProfile()
        {
            UserProfileData u1 = db.GetUserByUUID(user1);
            Assert.That(fname1,Is.EqualTo(u1.FirstName), "Assert.That(fname1,Is.EqualTo(u1.FirstName))");
            u1.FirstName = "Ugly";
            
            db.UpdateUserProfile(u1);
            Assert.That("Ugly",Is.EqualTo(u1.FirstName), "Assert.That(\"Ugly\",Is.EqualTo(u1.FirstName))");
        }

        [Test]
        public void T013_StoreUserWebKey()
        {
            UserProfileData u1 = db.GetUserByUUID(user1);
            Assert.That(u1.WebLoginKey,Is.EqualTo(zero), "Assert.That(u1.WebLoginKey,Is.EqualTo(zero))");
            db.StoreWebLoginKey(user1, webkey);
            u1 = db.GetUserByUUID(user1);
            Assert.That(u1.WebLoginKey,Is.EqualTo(webkey), "Assert.That(u1.WebLoginKey,Is.EqualTo(webkey))");
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
        public void T015_UserPersistency()
        {
            UserProfileData u = new UserProfileData();
            UUID id = user5;
            string fname = RandomName();
            string lname = RandomName();
            string email = RandomName();
            string passhash =  RandomName();
            string passsalt =  RandomName();
            UUID homeregion = UUID.Random();
            UUID webloginkey = UUID.Random();
            uint homeregx = (uint) random.Next();
            uint homeregy = (uint) random.Next();
            Vector3 homeloc 
                = new Vector3(
                    (float)Math.Round(random.NextDouble(), 5),
                    (float)Math.Round(random.NextDouble(), 5),
                    (float)Math.Round(random.NextDouble(), 5));
            Vector3 homelookat 
                = new Vector3(
                    (float)Math.Round(random.NextDouble(), 5),
                    (float)Math.Round(random.NextDouble(), 5),
                    (float)Math.Round(random.NextDouble(), 5));
            int created = random.Next();
            int lastlogin = random.Next();
            string userinvuri = RandomName();
            string userasseturi = RandomName();
            uint candomask = (uint) random.Next();
            uint wantdomask = (uint) random.Next();
            string abouttext = RandomName();
            string flabouttext = RandomName();
            UUID image = UUID.Random();
            UUID firstimage = UUID.Random();
            UserAgentData agent = NewAgent(id,UUID.Random());
            int userflags = random.Next();
            int godlevel = random.Next();
            string customtype = RandomName();
            UUID partner = UUID.Random();
            
            //HomeRegionX and HomeRegionY must only use 24 bits
            homeregx = ((homeregx << 8) >> 8);
            homeregy = ((homeregy << 8) >> 8);

            u.ID = id;
            u.WebLoginKey = webloginkey;
            u.HomeRegionID = homeregion;
            u.FirstName = fname;
            u.SurName = lname;
            u.Email = email;
            u.PasswordHash = passhash;
            u.PasswordSalt = passsalt;
            u.HomeRegionX = homeregx;
            u.HomeRegionY = homeregy;
            ulong homereg = u.HomeRegion;
            u.HomeLocation = homeloc;
            u.HomeLookAt = homelookat;
            u.Created = created;
            u.LastLogin = lastlogin;
            u.UserInventoryURI = userinvuri;
            u.UserAssetURI = userasseturi;
            u.CanDoMask = candomask;
            u.WantDoMask = wantdomask;
            u.AboutText = abouttext;
            u.FirstLifeAboutText = flabouttext;
            u.Image = image;
            u.FirstLifeImage = firstimage;
            u.CurrentAgent = agent;
            u.UserFlags = userflags;
            u.GodLevel = godlevel;
            u.CustomType = customtype;
            u.Partner = partner;
            
            db.AddNewUserProfile(u);
            UserProfileData u1a = db.GetUserByUUID(id);
            Assert.That(u1a,Is.Not.Null);
            Assert.That(id,Is.EqualTo(u1a.ID), "Assert.That(id,Is.EqualTo(u1a.ID))");
            Assert.That(homeregion,Is.EqualTo(u1a.HomeRegionID), "Assert.That(homeregion,Is.EqualTo(u1a.HomeRegionID))");
            Assert.That(webloginkey,Is.EqualTo(u1a.WebLoginKey), "Assert.That(webloginkey,Is.EqualTo(u1a.WebLoginKey))");
            Assert.That(fname,Is.EqualTo(u1a.FirstName), "Assert.That(fname,Is.EqualTo(u1a.FirstName))");
            Assert.That(lname,Is.EqualTo(u1a.SurName), "Assert.That(lname,Is.EqualTo(u1a.SurName))");
            Assert.That(email,Is.EqualTo(u1a.Email), "Assert.That(email,Is.EqualTo(u1a.Email))");
            Assert.That(passhash,Is.EqualTo(u1a.PasswordHash), "Assert.That(passhash,Is.EqualTo(u1a.PasswordHash))");
            Assert.That(passsalt,Is.EqualTo(u1a.PasswordSalt), "Assert.That(passsalt,Is.EqualTo(u1a.PasswordSalt))");
            Assert.That(homeregx,Is.EqualTo(u1a.HomeRegionX), "Assert.That(homeregx,Is.EqualTo(u1a.HomeRegionX))");
            Assert.That(homeregy,Is.EqualTo(u1a.HomeRegionY), "Assert.That(homeregy,Is.EqualTo(u1a.HomeRegionY))");
            Assert.That(homereg,Is.EqualTo(u1a.HomeRegion), "Assert.That(homereg,Is.EqualTo(u1a.HomeRegion))");
            Assert.That(homeloc,Is.EqualTo(u1a.HomeLocation), "Assert.That(homeloc,Is.EqualTo(u1a.HomeLocation))");
            Assert.That(homelookat,Is.EqualTo(u1a.HomeLookAt), "Assert.That(homelookat,Is.EqualTo(u1a.HomeLookAt))");
            Assert.That(created,Is.EqualTo(u1a.Created), "Assert.That(created,Is.EqualTo(u1a.Created))");
            Assert.That(lastlogin,Is.EqualTo(u1a.LastLogin), "Assert.That(lastlogin,Is.EqualTo(u1a.LastLogin))");
            // RootInventoryFolderID is not tested because it is saved in SQLite,
            // but not in MySQL
            Assert.That(userinvuri,Is.EqualTo(u1a.UserInventoryURI), "Assert.That(userinvuri,Is.EqualTo(u1a.UserInventoryURI))");
            Assert.That(userasseturi,Is.EqualTo(u1a.UserAssetURI), "Assert.That(userasseturi,Is.EqualTo(u1a.UserAssetURI))");
            Assert.That(candomask,Is.EqualTo(u1a.CanDoMask), "Assert.That(candomask,Is.EqualTo(u1a.CanDoMask))");
            Assert.That(wantdomask,Is.EqualTo(u1a.WantDoMask), "Assert.That(wantdomask,Is.EqualTo(u1a.WantDoMask))");
            Assert.That(abouttext,Is.EqualTo(u1a.AboutText), "Assert.That(abouttext,Is.EqualTo(u1a.AboutText))");
            Assert.That(flabouttext,Is.EqualTo(u1a.FirstLifeAboutText), "Assert.That(flabouttext,Is.EqualTo(u1a.FirstLifeAboutText))");
            Assert.That(image,Is.EqualTo(u1a.Image), "Assert.That(image,Is.EqualTo(u1a.Image))");
            Assert.That(firstimage,Is.EqualTo(u1a.FirstLifeImage), "Assert.That(firstimage,Is.EqualTo(u1a.FirstLifeImage))");
            Assert.That(u1a.CurrentAgent,Is.Null);
            Assert.That(userflags,Is.EqualTo(u1a.UserFlags), "Assert.That(userflags,Is.EqualTo(u1a.UserFlags))");
            Assert.That(godlevel,Is.EqualTo(u1a.GodLevel), "Assert.That(godlevel,Is.EqualTo(u1a.GodLevel))");
            Assert.That(customtype,Is.EqualTo(u1a.CustomType), "Assert.That(customtype,Is.EqualTo(u1a.CustomType))");
            Assert.That(partner,Is.EqualTo(u1a.Partner), "Assert.That(partner,Is.EqualTo(u1a.Partner))");
        }

        [Test]
        public void T016_UserUpdatePersistency()
        {
            UUID id = user5;
            UserProfileData u = db.GetUserByUUID(id);
            string fname = RandomName();
            string lname = RandomName();
            string email = RandomName();
            string passhash =  RandomName();
            string passsalt =  RandomName();
            UUID homeregionid = UUID.Random();
            UUID webloginkey = UUID.Random();
            uint homeregx = (uint) random.Next();
            uint homeregy = (uint) random.Next();
            Vector3 homeloc = new Vector3((float)Math.Round(random.NextDouble(),5),(float)Math.Round(random.NextDouble(),5),(float)Math.Round(random.NextDouble(),5));
            Vector3 homelookat = new Vector3((float)Math.Round(random.NextDouble(),5),(float)Math.Round(random.NextDouble(),5),(float)Math.Round(random.NextDouble(),5));
            int created = random.Next();
            int lastlogin = random.Next();
            string userinvuri = RandomName();
            string userasseturi = RandomName();
            uint candomask = (uint) random.Next();
            uint wantdomask = (uint) random.Next();
            string abouttext = RandomName();
            string flabouttext = RandomName();
            UUID image = UUID.Random();
            UUID firstimage = UUID.Random();
            UserAgentData agent = NewAgent(id,UUID.Random());
            int userflags = random.Next();
            int godlevel = random.Next();
            string customtype = RandomName();
            UUID partner = UUID.Random();
            
            //HomeRegionX and HomeRegionY must only use 24 bits
            homeregx = ((homeregx << 8) >> 8);
            homeregy = ((homeregy << 8) >> 8);
       
            u.WebLoginKey = webloginkey;
            u.HomeRegionID = homeregionid;
            u.FirstName = fname;
            u.SurName = lname;
            u.Email = email;
            u.PasswordHash = passhash;
            u.PasswordSalt = passsalt;
            u.HomeRegionX = homeregx;
            u.HomeRegionY = homeregy;
            ulong homereg = u.HomeRegion;
            u.HomeLocation = homeloc;
            u.HomeLookAt = homelookat;
            u.Created = created;
            u.LastLogin = lastlogin;
            u.UserInventoryURI = userinvuri;
            u.UserAssetURI = userasseturi;
            u.CanDoMask = candomask;
            u.WantDoMask = wantdomask;
            u.AboutText = abouttext;
            u.FirstLifeAboutText = flabouttext;
            u.Image = image;
            u.FirstLifeImage = firstimage;
            u.CurrentAgent = agent;
            u.UserFlags = userflags;
            u.GodLevel = godlevel;
            u.CustomType = customtype;
            u.Partner = partner;
            
            db.UpdateUserProfile(u);
            UserProfileData u1a = db.GetUserByUUID(id);
            Assert.That(u1a,Is.Not.Null);
            Assert.That(id,Is.EqualTo(u1a.ID), "Assert.That(id,Is.EqualTo(u1a.ID))");
            Assert.That(homeregionid,Is.EqualTo(u1a.HomeRegionID), "Assert.That(homeregionid,Is.EqualTo(u1a.HomeRegionID))");
            Assert.That(webloginkey,Is.EqualTo(u1a.WebLoginKey), "Assert.That(webloginkey,Is.EqualTo(u1a.WebLoginKey))");
            Assert.That(fname,Is.EqualTo(u1a.FirstName), "Assert.That(fname,Is.EqualTo(u1a.FirstName))");
            Assert.That(lname,Is.EqualTo(u1a.SurName), "Assert.That(lname,Is.EqualTo(u1a.SurName))");
            Assert.That(email,Is.EqualTo(u1a.Email), "Assert.That(email,Is.EqualTo(u1a.Email))");
            Assert.That(passhash,Is.EqualTo(u1a.PasswordHash), "Assert.That(passhash,Is.EqualTo(u1a.PasswordHash))");
            Assert.That(passsalt,Is.EqualTo(u1a.PasswordSalt), "Assert.That(passsalt,Is.EqualTo(u1a.PasswordSalt))");
            Assert.That(homereg,Is.EqualTo(u1a.HomeRegion), "Assert.That(homereg,Is.EqualTo(u1a.HomeRegion))");
            Assert.That(homeregx,Is.EqualTo(u1a.HomeRegionX), "Assert.That(homeregx,Is.EqualTo(u1a.HomeRegionX))");
            Assert.That(homeregy,Is.EqualTo(u1a.HomeRegionY), "Assert.That(homeregy,Is.EqualTo(u1a.HomeRegionY))");
            Assert.That(homereg,Is.EqualTo(u1a.HomeRegion), "Assert.That(homereg,Is.EqualTo(u1a.HomeRegion))");
            Assert.That(homeloc,Is.EqualTo(u1a.HomeLocation), "Assert.That(homeloc,Is.EqualTo(u1a.HomeLocation))");
            Assert.That(homelookat,Is.EqualTo(u1a.HomeLookAt), "Assert.That(homelookat,Is.EqualTo(u1a.HomeLookAt))");
            Assert.That(created,Is.EqualTo(u1a.Created), "Assert.That(created,Is.EqualTo(u1a.Created))");
            Assert.That(lastlogin,Is.EqualTo(u1a.LastLogin), "Assert.That(lastlogin,Is.EqualTo(u1a.LastLogin))");
            // RootInventoryFolderID is not tested because it is saved in SQLite,
            // but not in MySQL
            Assert.That(userasseturi,Is.EqualTo(u1a.UserAssetURI), "Assert.That(userasseturi,Is.EqualTo(u1a.UserAssetURI))");
            Assert.That(candomask,Is.EqualTo(u1a.CanDoMask), "Assert.That(candomask,Is.EqualTo(u1a.CanDoMask))");
            Assert.That(wantdomask,Is.EqualTo(u1a.WantDoMask), "Assert.That(wantdomask,Is.EqualTo(u1a.WantDoMask))");
            Assert.That(abouttext,Is.EqualTo(u1a.AboutText), "Assert.That(abouttext,Is.EqualTo(u1a.AboutText))");
            Assert.That(flabouttext,Is.EqualTo(u1a.FirstLifeAboutText), "Assert.That(flabouttext,Is.EqualTo(u1a.FirstLifeAboutText))");
            Assert.That(image,Is.EqualTo(u1a.Image), "Assert.That(image,Is.EqualTo(u1a.Image))");
            Assert.That(firstimage,Is.EqualTo(u1a.FirstLifeImage), "Assert.That(firstimage,Is.EqualTo(u1a.FirstLifeImage))");
            Assert.That(u1a.CurrentAgent,Is.Null);
            Assert.That(userflags,Is.EqualTo(u1a.UserFlags), "Assert.That(userflags,Is.EqualTo(u1a.UserFlags))");
            Assert.That(godlevel,Is.EqualTo(u1a.GodLevel), "Assert.That(godlevel,Is.EqualTo(u1a.GodLevel))");
            Assert.That(customtype,Is.EqualTo(u1a.CustomType), "Assert.That(customtype,Is.EqualTo(u1a.CustomType))");
            Assert.That(partner,Is.EqualTo(u1a.Partner), "Assert.That(partner,Is.EqualTo(u1a.Partner))");
        }

        [Test]
        public void T017_UserUpdateRandomPersistency()
        {
            UUID id = user5;
            UserProfileData u = db.GetUserByUUID(id);
            new PropertyScrambler<UserProfileData>().DontScramble(x=>x.ID).Scramble(u);
            
            db.UpdateUserProfile(u);
            UserProfileData u1a = db.GetUserByUUID(id);
            Assert.That(u1a, Constraints.PropertyCompareConstraint(u)
                .IgnoreProperty(x=>x.HomeRegionX)
                .IgnoreProperty(x=>x.HomeRegionY)
                .IgnoreProperty(x=>x.RootInventoryFolderID)
                );
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
            Assert.That(agent1,Is.EqualTo(a1a.SessionID), "Assert.That(agent1,Is.EqualTo(a1a.SessionID))");
            Assert.That(user1,Is.EqualTo(a1a.ProfileID), "Assert.That(user1,Is.EqualTo(a1a.ProfileID))");
            Assert.That(agent2,Is.EqualTo(a2a.SessionID), "Assert.That(agent2,Is.EqualTo(a2a.SessionID))");
            Assert.That(user2,Is.EqualTo(a2a.ProfileID), "Assert.That(user2,Is.EqualTo(a2a.ProfileID))");
            Assert.That(agent3,Is.EqualTo(a3a.SessionID), "Assert.That(agent3,Is.EqualTo(a3a.SessionID))");
            Assert.That(user3,Is.EqualTo(a3a.ProfileID), "Assert.That(user3,Is.EqualTo(a3a.ProfileID))");
        }
        
        [Test]
        public void T021_FetchAgentByName()
        {
            String name3 = fname3 + " " + lname3;
            UserAgentData a2 = db.GetAgentByName(fname2,lname2);
            UserAgentData a3 = db.GetAgentByName(name3);
            Assert.That(user2,Is.EqualTo(a2.ProfileID), "Assert.That(user2,Is.EqualTo(a2.ProfileID))");
            Assert.That(user3,Is.EqualTo(a3.ProfileID), "Assert.That(user3,Is.EqualTo(a3.ProfileID))");
        }
        
        [Test]
        public void T022_ExceptionalCases()
        {
            UserAgentData a0 = NewAgent(user4,zero);
            UserAgentData a4 = NewAgent(zero,agent4);
            db.AddNewUserAgent(a0);
            db.AddNewUserAgent(a4);

            Assert.That(db.GetAgentByUUID(user4),Is.Null);
            Assert.That(db.GetAgentByUUID(zero),Is.Null);
        }
        
        [Test]
        public void T023_AgentPersistency()
        {
            UUID user = user4;
            UUID agent = agent4;
            UUID secureagent = UUID.Random();
            string agentip = RandomName();
            uint agentport = (uint)random.Next();
            bool agentonline = (random.NextDouble() > 0.5);
            int logintime = random.Next();
            int logouttime = random.Next();
            UUID regionid = UUID.Random();
            ulong regionhandle = (ulong) random.Next();
            Vector3 currentpos = new Vector3((float)Math.Round(random.NextDouble(),5),(float)Math.Round(random.NextDouble(),5),(float)Math.Round(random.NextDouble(),5));
            Vector3 currentlookat = new Vector3((float)Math.Round(random.NextDouble(),5),(float)Math.Round(random.NextDouble(),5),(float)Math.Round(random.NextDouble(),5));
            UUID orgregionid = UUID.Random();
            
            UserAgentData a = new UserAgentData();
            a.ProfileID = user;
            a.SessionID = agent;
            a.SecureSessionID = secureagent;
            a.AgentIP = agentip;
            a.AgentPort = agentport;
            a.AgentOnline = agentonline;
            a.LoginTime = logintime;
            a.LogoutTime = logouttime;
            a.Region = regionid;
            a.Handle = regionhandle;
            a.Position = currentpos;
            a.LookAt = currentlookat;
            a.InitialRegion = orgregionid;

            db.AddNewUserAgent(a);

            UserAgentData a1 = db.GetAgentByUUID(user4);
            Assert.That(user,Is.EqualTo(a1.ProfileID), "Assert.That(user,Is.EqualTo(a1.ProfileID))");
            Assert.That(agent,Is.EqualTo(a1.SessionID), "Assert.That(agent,Is.EqualTo(a1.SessionID))");
            Assert.That(secureagent,Is.EqualTo(a1.SecureSessionID), "Assert.That(secureagent,Is.EqualTo(a1.SecureSessionID))");
            Assert.That(agentip,Is.EqualTo(a1.AgentIP), "Assert.That(agentip,Is.EqualTo(a1.AgentIP))");
            Assert.That(agentport,Is.EqualTo(a1.AgentPort), "Assert.That(agentport,Is.EqualTo(a1.AgentPort))");
            Assert.That(agentonline,Is.EqualTo(a1.AgentOnline), "Assert.That(agentonline,Is.EqualTo(a1.AgentOnline))");
            Assert.That(logintime,Is.EqualTo(a1.LoginTime), "Assert.That(logintime,Is.EqualTo(a1.LoginTime))");
            Assert.That(logouttime,Is.EqualTo(a1.LogoutTime), "Assert.That(logouttime,Is.EqualTo(a1.LogoutTime))");
            Assert.That(regionid,Is.EqualTo(a1.Region), "Assert.That(regionid,Is.EqualTo(a1.Region))");
            Assert.That(regionhandle,Is.EqualTo(a1.Handle), "Assert.That(regionhandle,Is.EqualTo(a1.Handle))");
            Assert.That(currentpos,Is.EqualTo(a1.Position), "Assert.That(currentpos,Is.EqualTo(a1.Position))");
            Assert.That(currentlookat,Is.EqualTo(a1.LookAt), "Assert.That(currentlookat,Is.EqualTo(a1.LookAt))");
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
            Assert.That(fl1.Count,Is.EqualTo(2), "Assert.That(fl1.Count,Is.EqualTo(2))");
            perms.Add(user2,1);
            perms.Add(user3,2);
            for (int i = 0; i < fl1.Count; i++)
            {
                Assert.That(user1,Is.EqualTo(fl1[i].FriendListOwner), "Assert.That(user1,Is.EqualTo(fl1[i].FriendListOwner))");
                friends.Add(fl1[i].Friend,1);
                temp = perms[fl1[i].Friend];
                Assert.That(temp,Is.EqualTo(fl1[i].FriendPerms), "Assert.That(temp,Is.EqualTo(fl1[i].FriendPerms))");
            }
            tempu1 = friends[user2];
            tempu2 = friends[user3];
            Assert.That(1,Is.EqualTo(tempu1) & Is.EqualTo(tempu2), "Assert.That(1,Is.EqualTo(tempu1) & Is.EqualTo(tempu2))");
        }
        
        [Test]
        public void T031_RemoveUserFriend()
        // user1 has 2 friends, user2 and user3.
        {
            List<FriendListItem> fl1 = db.GetUserFriendList(user1);
            List<FriendListItem> fl2 = db.GetUserFriendList(user2);

            Assert.That(fl1.Count,Is.EqualTo(2), "Assert.That(fl1.Count,Is.EqualTo(2))");
            Assert.That(fl1[0].Friend,Is.EqualTo(user2) | Is.EqualTo(user3), "Assert.That(fl1[0].Friend,Is.EqualTo(user2) | Is.EqualTo(user3))");
            Assert.That(fl2[0].Friend,Is.EqualTo(user1), "Assert.That(fl2[0].Friend,Is.EqualTo(user1))");
            db.RemoveUserFriend(user2, user1);
            
            fl1 = db.GetUserFriendList(user1);
            fl2 = db.GetUserFriendList(user2);
            Assert.That(fl1.Count,Is.EqualTo(1), "Assert.That(fl1.Count,Is.EqualTo(1))");
            Assert.That(fl1[0].Friend, Is.EqualTo(user3), "Assert.That(fl1[0].Friend, Is.EqualTo(user3))");
            Assert.That(fl2, Is.Empty);
        }
        
        [Test]
        public void T032_UpdateFriendPerms()
        // user1 has 1 friend, user3, who has permission 2 in T030.
        {
            List<FriendListItem> fl1 = db.GetUserFriendList(user1);
            Assert.That(fl1[0].FriendPerms,Is.EqualTo(2), "Assert.That(fl1[0].FriendPerms,Is.EqualTo(2))");
            db.UpdateUserFriendPerms(user1, user3, 4);
            
            fl1 = db.GetUserFriendList(user1);
            Assert.That(fl1[0].FriendPerms,Is.EqualTo(4), "Assert.That(fl1[0].FriendPerms,Is.EqualTo(4))");
        }
        
        [Test]
        public void T040_UserAppearance()
        {
            AvatarAppearance appear = new AvatarAppearance();
            appear.Owner = user1;
            db.UpdateUserAppearance(user1, appear);
            AvatarAppearance user1app = db.GetUserAppearance(user1);
            Assert.That(user1,Is.EqualTo(user1app.Owner), "Assert.That(user1,Is.EqualTo(user1app.Owner))");
        }
        
        [Test]
        public void T041_UserAppearancePersistency()
        {
            AvatarAppearance appear = new AvatarAppearance();
            UUID owner = UUID.Random();
            int serial = random.Next();
            byte[] visualp = new byte[218];
            random.NextBytes(visualp);
            UUID bodyitem = UUID.Random();
            UUID bodyasset = UUID.Random();
            UUID skinitem = UUID.Random();
            UUID skinasset = UUID.Random();
            UUID hairitem = UUID.Random();
            UUID hairasset = UUID.Random();
            UUID eyesitem = UUID.Random();
            UUID eyesasset = UUID.Random();
            UUID shirtitem = UUID.Random();
            UUID shirtasset = UUID.Random();
            UUID pantsitem = UUID.Random();
            UUID pantsasset = UUID.Random();
            UUID shoesitem = UUID.Random();
            UUID shoesasset = UUID.Random();
            UUID socksitem = UUID.Random();
            UUID socksasset = UUID.Random();
            UUID jacketitem = UUID.Random();
            UUID jacketasset = UUID.Random();
            UUID glovesitem = UUID.Random();
            UUID glovesasset = UUID.Random();
            UUID ushirtitem = UUID.Random();
            UUID ushirtasset = UUID.Random();
            UUID upantsitem = UUID.Random();
            UUID upantsasset = UUID.Random();
            UUID skirtitem = UUID.Random();
            UUID skirtasset = UUID.Random();
            Primitive.TextureEntry texture = AvatarAppearance.GetDefaultTexture();
            float avatarheight = (float) (Math.Round(random.NextDouble(),5));
            
            appear.Owner = owner;
            appear.Serial = serial;
            appear.VisualParams = visualp;
            appear.BodyItem = bodyitem;
            appear.BodyAsset = bodyasset;
            appear.SkinItem = skinitem;
            appear.SkinAsset = skinasset;
            appear.HairItem = hairitem;
            appear.HairAsset = hairasset;
            appear.EyesItem = eyesitem;
            appear.EyesAsset = eyesasset;
            appear.ShirtItem = shirtitem;
            appear.ShirtAsset = shirtasset;
            appear.PantsItem = pantsitem;
            appear.PantsAsset = pantsasset;
            appear.ShoesItem = shoesitem;
            appear.ShoesAsset = shoesasset;
            appear.SocksItem = socksitem;
            appear.SocksAsset = socksasset;
            appear.JacketItem = jacketitem;
            appear.JacketAsset = jacketasset;
            appear.GlovesItem = glovesitem;
            appear.GlovesAsset = glovesasset;
            appear.UnderShirtItem = ushirtitem;
            appear.UnderShirtAsset = ushirtasset;
            appear.UnderPantsItem = upantsitem;
            appear.UnderPantsAsset = upantsasset;
            appear.SkirtItem = skirtitem;
            appear.SkirtAsset = skirtasset;
            appear.Texture = texture;
            appear.AvatarHeight = avatarheight;
            
            db.UpdateUserAppearance(owner, appear);
            AvatarAppearance app = db.GetUserAppearance(owner);

            Assert.That(owner,Is.EqualTo(app.Owner), "Assert.That(owner,Is.EqualTo(app.Owner))");
            Assert.That(serial,Is.EqualTo(app.Serial), "Assert.That(serial,Is.EqualTo(app.Serial))");
            Assert.That(visualp,Is.EqualTo(app.VisualParams), "Assert.That(visualp,Is.EqualTo(app.VisualParams))");
            Assert.That(bodyitem,Is.EqualTo(app.BodyItem), "Assert.That(bodyitem,Is.EqualTo(app.BodyItem))");
            Assert.That(bodyasset,Is.EqualTo(app.BodyAsset), "Assert.That(bodyasset,Is.EqualTo(app.BodyAsset))");
            Assert.That(skinitem,Is.EqualTo(app.SkinItem), "Assert.That(skinitem,Is.EqualTo(app.SkinItem))");
            Assert.That(skinasset,Is.EqualTo(app.SkinAsset), "Assert.That(skinasset,Is.EqualTo(app.SkinAsset))");
            Assert.That(hairitem,Is.EqualTo(app.HairItem), "Assert.That(hairitem,Is.EqualTo(app.HairItem))");
            Assert.That(hairasset,Is.EqualTo(app.HairAsset), "Assert.That(hairasset,Is.EqualTo(app.HairAsset))");
            Assert.That(eyesitem,Is.EqualTo(app.EyesItem), "Assert.That(eyesitem,Is.EqualTo(app.EyesItem))");
            Assert.That(eyesasset,Is.EqualTo(app.EyesAsset), "Assert.That(eyesasset,Is.EqualTo(app.EyesAsset))");
            Assert.That(shirtitem,Is.EqualTo(app.ShirtItem), "Assert.That(shirtitem,Is.EqualTo(app.ShirtItem))");
            Assert.That(shirtasset,Is.EqualTo(app.ShirtAsset), "Assert.That(shirtasset,Is.EqualTo(app.ShirtAsset))");
            Assert.That(pantsitem,Is.EqualTo(app.PantsItem), "Assert.That(pantsitem,Is.EqualTo(app.PantsItem))");
            Assert.That(pantsasset,Is.EqualTo(app.PantsAsset), "Assert.That(pantsasset,Is.EqualTo(app.PantsAsset))");
            Assert.That(shoesitem,Is.EqualTo(app.ShoesItem), "Assert.That(shoesitem,Is.EqualTo(app.ShoesItem))");
            Assert.That(shoesasset,Is.EqualTo(app.ShoesAsset), "Assert.That(shoesasset,Is.EqualTo(app.ShoesAsset))");
            Assert.That(socksitem,Is.EqualTo(app.SocksItem), "Assert.That(socksitem,Is.EqualTo(app.SocksItem))");
            Assert.That(socksasset,Is.EqualTo(app.SocksAsset), "Assert.That(socksasset,Is.EqualTo(app.SocksAsset))");
            Assert.That(jacketitem,Is.EqualTo(app.JacketItem), "Assert.That(jacketitem,Is.EqualTo(app.JacketItem))");
            Assert.That(jacketasset,Is.EqualTo(app.JacketAsset), "Assert.That(jacketasset,Is.EqualTo(app.JacketAsset))");
            Assert.That(glovesitem,Is.EqualTo(app.GlovesItem), "Assert.That(glovesitem,Is.EqualTo(app.GlovesItem))");
            Assert.That(glovesasset,Is.EqualTo(app.GlovesAsset), "Assert.That(glovesasset,Is.EqualTo(app.GlovesAsset))");
            Assert.That(ushirtitem,Is.EqualTo(app.UnderShirtItem), "Assert.That(ushirtitem,Is.EqualTo(app.UnderShirtItem))");
            Assert.That(ushirtasset,Is.EqualTo(app.UnderShirtAsset), "Assert.That(ushirtasset,Is.EqualTo(app.UnderShirtAsset))");
            Assert.That(upantsitem,Is.EqualTo(app.UnderPantsItem), "Assert.That(upantsitem,Is.EqualTo(app.UnderPantsItem))");
            Assert.That(upantsasset,Is.EqualTo(app.UnderPantsAsset), "Assert.That(upantsasset,Is.EqualTo(app.UnderPantsAsset))");
            Assert.That(skirtitem,Is.EqualTo(app.SkirtItem), "Assert.That(skirtitem,Is.EqualTo(app.SkirtItem))");
            Assert.That(skirtasset,Is.EqualTo(app.SkirtAsset), "Assert.That(skirtasset,Is.EqualTo(app.SkirtAsset))");
            Assert.That(texture.ToString(),Is.EqualTo(app.Texture.ToString()), "Assert.That(texture.ToString(),Is.EqualTo(app.Texture.ToString()))");
            Assert.That(avatarheight,Is.EqualTo(app.AvatarHeight), "Assert.That(avatarheight,Is.EqualTo(app.AvatarHeight))");
        }

        [Test]
        public void T999_StillNull()
        {
            Assert.That(db.GetUserByUUID(zero), Is.Null);
            Assert.That(db.GetAgentByUUID(zero), Is.Null);
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
            a.AgentIP = RandomName();
            return a;
        }
        
        public static string RandomName()
        {
            StringBuilder name = new StringBuilder();
            int size = random.Next(5,12); 
            char ch ;
            for (int i=0; i<size; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65))) ;
                name.Append(ch);
            }
            return name.ToString();
        }
    }
}
