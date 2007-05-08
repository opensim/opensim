using System;
using System.Collections.Generic;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class CloneProfileCommand : Command
    {
        Avatar.AvatarProperties Properties;
        Avatar.Interests Interests;
        List<LLUUID> Groups = new List<LLUUID>();
        bool ReceivedProperties = false;
        bool ReceivedInterests = false;
        bool ReceivedGroups = false;
        ManualResetEvent ReceivedProfileEvent = new ManualResetEvent(false);

        public CloneProfileCommand(TestClient testClient)
        {
            testClient.Avatars.OnAvatarInterests += new AvatarManager.AvatarInterestsCallback(Avatars_OnAvatarInterests);
            testClient.Avatars.OnAvatarProperties += new AvatarManager.AvatarPropertiesCallback(Avatars_OnAvatarProperties);
            testClient.Avatars.OnAvatarGroups += new AvatarManager.AvatarGroupsCallback(Avatars_OnAvatarGroups);
            testClient.Self.OnJoinGroup += new MainAvatar.JoinGroupCallback(Self_OnJoinGroup);

            Name = "cloneprofile";
            Description = "Clones another avatars profile as closely as possible. WARNING: This command will " +
                "destroy your existing profile! Usage: cloneprofile [targetuuid]";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            if (args.Length != 1)
                return Description;

            LLUUID targetID;
            ReceivedProperties = false;
            ReceivedInterests = false;
            ReceivedGroups = false;

            try
            {
                targetID = new LLUUID(args[0]);
            }
            catch (Exception)
            {
                return Description;
            }

            // Request all of the packets that make up an avatar profile
            Client.Avatars.RequestAvatarProperties(targetID);

            // Wait for all the packets to arrive
            ReceivedProfileEvent.Reset();
            ReceivedProfileEvent.WaitOne(5000, false);

            // Check if everything showed up
            if (!ReceivedInterests || !ReceivedProperties || !ReceivedGroups)
                return "Failed to retrieve a complete profile for that UUID";

            // Synchronize our profile
            Client.Self.ProfileInterests = Interests;
            Client.Self.ProfileProperties = Properties;
            Client.Self.SetAvatarInformation();

            // TODO: Leave all the groups we're currently a member of? This could
            // break TestClient connectivity that might be relying on group authentication

            // Attempt to join all the groups
            foreach (LLUUID groupID in Groups)
            {
                Client.Self.RequestJoinGroup(groupID);
            }

            return "Synchronized our profile to the profile of " + targetID.ToStringHyphenated();
        }

        void Avatars_OnAvatarProperties(LLUUID avatarID, Avatar.AvatarProperties properties)
        {
            lock (ReceivedProfileEvent)
            {
                Properties = properties;
                ReceivedProperties = true;

                if (ReceivedInterests && ReceivedProperties && ReceivedGroups)
                    ReceivedProfileEvent.Set();
            }
        }

        void Avatars_OnAvatarInterests(LLUUID avatarID, Avatar.Interests interests)
        {
            lock (ReceivedProfileEvent)
            {
                Interests = interests;
                ReceivedInterests = true;

                if (ReceivedInterests && ReceivedProperties && ReceivedGroups)
                    ReceivedProfileEvent.Set();
            }
        }

        void Avatars_OnAvatarGroups(LLUUID avatarID, AvatarGroupsReplyPacket.GroupDataBlock[] groups)
        {
            lock (ReceivedProfileEvent)
            {
                foreach (AvatarGroupsReplyPacket.GroupDataBlock block in groups)
                {
                    Groups.Add(block.GroupID);
                }

                ReceivedGroups = true;

                if (ReceivedInterests && ReceivedProperties && ReceivedGroups)
                    ReceivedProfileEvent.Set();
            }
        }

        void Self_OnJoinGroup(LLUUID groupID, bool success)
        {
            Console.WriteLine(Client.ToString() + (success ? " joined " : " failed to join ") + 
                groupID.ToStringHyphenated());

            if (success)
            {
                Console.WriteLine(Client.ToString() + " setting " + groupID.ToStringHyphenated() + 
                    " as the active group");
                Client.Self.ActivateGroup(groupID);
            }
        }
    }
}
