using System;
using System.Collections.Generic;
using System.Text;
using System.Speech.Synthesis;
using libsecondlife;
using libsecondlife.Packets;
using libsecondlife.AssetSystem;


// Since this requires .Net 3.0 I've left it out of the project by default.
// To use this: include it in the project and add a reference to the System.Speech.dll

namespace libsecondlife.TestClient
{
    public class TtsCommand : Command
    {
		SpeechSynthesizer _speechSynthesizer;

		public TtsCommand(TestClient testClient)
        {
            Name = "tts";
            Description = "Text To Speech.  When activated, client will echo all recieved chat messages out thru the computer's speakers.";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
			if (!Active)
			{
				if (_speechSynthesizer == null)
					_speechSynthesizer = new SpeechSynthesizer();
				Active = true;
				Client.Self.OnChat += new MainAvatar.ChatCallback(Self_OnChat);
				return "TTS is now on.";
			}
			else
			{
				Active = false;
				Client.Self.OnChat -= new MainAvatar.ChatCallback(Self_OnChat);
				return "TTS is now off.";
			}
        }

		void Self_OnChat(string message, byte audible, byte type, byte sourcetype, string fromName, LLUUID id, LLUUID ownerid, LLVector3 position)
		{
			if (message.Length > 0)
			{
				_speechSynthesizer.SpeakAsync(message);
			}
		}
	}
}