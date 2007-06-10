using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Region.Scripting
{
    public class FollowRandomAvatar : Script
    {
        public FollowRandomAvatar()
            : base(LLUUID.Random())
        {
            OnFrame += MyOnFrame;
        }

        private void MyOnFrame(IScriptContext context)
        {
            LLVector3 pos = context.Entity.Pos;

            IScriptReadonlyEntity avatar;

            if (context.TryGetRandomAvatar(out avatar))
            {
                LLVector3 avatarPos = avatar.Pos;

                float x = pos.X + ((float)avatarPos.X.CompareTo(pos.X)) / 2;
                float y = pos.Y + ((float)avatarPos.Y.CompareTo(pos.Y)) / 2;

                LLVector3 newPos = new LLVector3(x, y, pos.Z);

                context.Entity.Pos = newPos;
            }
        }
    }


}
