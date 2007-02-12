/*
Copyright (c) OpenSim project, http://sim.opensecondlife.org/

* Copyright (c) <year>, <copyright holder>
* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using libsecondlife;

namespace OpenSim
{
	/// <summary>
	/// Description of Globals.
	/// </summary>
	public sealed class Globals
	{
		private static Globals instance = new Globals();
		
		public static Globals Instance 
		{
			get 
			{
				return instance;
			}
		}
		
		private Globals()
		{
		}
		
		public string RegionName = "Test Sandbox\0";
		public ulong RegionHandle = 1096213093147648;
		public int IpPort = 1000;
		
		public bool LoginSever = true;
		public ushort LoginServerPort = 8080;

        //There's probably a better way to do this (i.e put this somewhere more appropiate), but it'll do for now
        public LLUUID ANIM_AGENT_AFRAID = new LLUUID("6b61c8e8-4747-0d75-12d7-e49ff207a4ca");
        public LLUUID ANIM_AGENT_AIM_BAZOOKA_R = new LLUUID("b5b4a67d-0aee-30d2-72cd-77b333e932ef");
        public LLUUID ANIM_AGENT_AIM_BOW_L = new LLUUID("46bb4359-de38-4ed8-6a22-f1f52fe8f506");
        public LLUUID ANIM_AGENT_AIM_HANDGUN_R = new LLUUID("3147d815-6338-b932-f011-16b56d9ac18b");
        public LLUUID ANIM_AGENT_AIM_RIFLE_R = new LLUUID("ea633413-8006-180a-c3ba-96dd1d756720");
        public LLUUID ANIM_AGENT_ANGRY = new LLUUID("5747a48e-073e-c331-f6f3-7c2149613d3e");
        public LLUUID ANIM_AGENT_AWAY = new LLUUID("fd037134-85d4-f241-72c6-4f42164fedee");
        public LLUUID ANIM_AGENT_BACKFLIP = new LLUUID("c4ca6188-9127-4f31-0158-23c4e2f93304");
        public LLUUID ANIM_AGENT_BELLY_LAUGH = new LLUUID("18b3a4b5-b463-bd48-e4b6-71eaac76c515");
        public LLUUID ANIM_AGENT_BLOW_KISS = new LLUUID("db84829b-462c-ee83-1e27-9bbee66bd624");
        public LLUUID ANIM_AGENT_BORED = new LLUUID("b906c4ba-703b-1940-32a3-0c7f7d791510");
        public LLUUID ANIM_AGENT_BOW = new LLUUID("82e99230-c906-1403-4d9c-3889dd98daba");
        public LLUUID ANIM_AGENT_BRUSH = new LLUUID("349a3801-54f9-bf2c-3bd0-1ac89772af01");
        public LLUUID ANIM_AGENT_BUSY = new LLUUID("efcf670c-2d18-8128-973a-034ebc806b67");
        public LLUUID ANIM_AGENT_CLAP = new LLUUID("9b0c1c4e-8ac7-7969-1494-28c874c4f668");
        public LLUUID ANIM_AGENT_COURTBOW = new LLUUID("9ba1c942-08be-e43a-fb29-16ad440efc50");
        public LLUUID ANIM_AGENT_CROUCH = new LLUUID("201f3fdf-cb1f-dbec-201f-7333e328ae7c");
        public LLUUID ANIM_AGENT_CROUCHWALK = new LLUUID("47f5f6fb-22e5-ae44-f871-73aaaf4a6022");
        public LLUUID ANIM_AGENT_CRY = new LLUUID("92624d3e-1068-f1aa-a5ec-8244585193ed");
        public LLUUID ANIM_AGENT_CUSTOMIZE = new LLUUID("038fcec9-5ebd-8a8e-0e2e-6e71a0a1ac53");
        public LLUUID ANIM_AGENT_CUSTOMIZE_DONE = new LLUUID("6883a61a-b27b-5914-a61e-dda118a9ee2c");
        public LLUUID ANIM_AGENT_DANCE1 = new LLUUID("b68a3d7c-de9e-fc87-eec8-543d787e5b0d");
        public LLUUID ANIM_AGENT_DANCE2 = new LLUUID("928cae18-e31d-76fd-9cc9-2f55160ff818");
        public LLUUID ANIM_AGENT_DANCE3 = new LLUUID("30047778-10ea-1af7-6881-4db7a3a5a114");
        public LLUUID ANIM_AGENT_DANCE4 = new LLUUID("951469f4-c7b2-c818-9dee-ad7eea8c30b7");
        public LLUUID ANIM_AGENT_DANCE5 = new LLUUID("4bd69a1d-1114-a0b4-625f-84e0a5237155");
        public LLUUID ANIM_AGENT_DANCE6 = new LLUUID("cd28b69b-9c95-bb78-3f94-8d605ff1bb12");
        public LLUUID ANIM_AGENT_DANCE7 = new LLUUID("a54d8ee2-28bb-80a9-7f0c-7afbbe24a5d6");
        public LLUUID ANIM_AGENT_DANCE8 = new LLUUID("b0dc417c-1f11-af36-2e80-7e7489fa7cdc");
        public LLUUID ANIM_AGENT_DEAD = new LLUUID("57abaae6-1d17-7b1b-5f98-6d11a6411276");
        public LLUUID ANIM_AGENT_DRINK = new LLUUID("0f86e355-dd31-a61c-fdb0-3a96b9aad05f");
        public LLUUID ANIM_AGENT_EMBARRASSED = new LLUUID("514af488-9051-044a-b3fc-d4dbf76377c6");
        public LLUUID ANIM_AGENT_EXPRESS_AFRAID = new LLUUID("aa2df84d-cf8f-7218-527b-424a52de766e");
        public LLUUID ANIM_AGENT_EXPRESS_ANGER = new LLUUID("1a03b575-9634-b62a-5767-3a679e81f4de");
        public LLUUID ANIM_AGENT_EXPRESS_BORED = new LLUUID("214aa6c1-ba6a-4578-f27c-ce7688f61d0d");
        public LLUUID ANIM_AGENT_EXPRESS_CRY = new LLUUID("d535471b-85bf-3b4d-a542-93bea4f59d33");
        public LLUUID ANIM_AGENT_EXPRESS_DISDAIN = new LLUUID("d4416ff1-09d3-300f-4183-1b68a19b9fc1");
        public LLUUID ANIM_AGENT_EXPRESS_EMBARRASSED = new LLUUID("0b8c8211-d78c-33e8-fa28-c51a9594e424");
        public LLUUID ANIM_AGENT_EXPRESS_FROWN = new LLUUID("fee3df48-fa3d-1015-1e26-a205810e3001");
        public LLUUID ANIM_AGENT_EXPRESS_KISS = new LLUUID("1e8d90cc-a84e-e135-884c-7c82c8b03a14");
        public LLUUID ANIM_AGENT_EXPRESS_LAUGH = new LLUUID("62570842-0950-96f8-341c-809e65110823");
        public LLUUID ANIM_AGENT_EXPRESS_OPEN_MOUTH = new LLUUID("d63bc1f9-fc81-9625-a0c6-007176d82eb7");
        public LLUUID ANIM_AGENT_EXPRESS_REPULSED = new LLUUID("f76cda94-41d4-a229-2872-e0296e58afe1");
        public LLUUID ANIM_AGENT_EXPRESS_SAD = new LLUUID("eb6ebfb2-a4b3-a19c-d388-4dd5c03823f7");
        public LLUUID ANIM_AGENT_EXPRESS_SHRUG = new LLUUID("a351b1bc-cc94-aac2-7bea-a7e6ebad15ef");
        public LLUUID ANIM_AGENT_EXPRESS_SMILE = new LLUUID("b7c7c833-e3d3-c4e3-9fc0-131237446312");
        public LLUUID ANIM_AGENT_EXPRESS_SURPRISE = new LLUUID("728646d9-cc79-08b2-32d6-937f0a835c24");
        public LLUUID ANIM_AGENT_EXPRESS_TONGUE_OUT = new LLUUID("835965c6-7f2f-bda2-5deb-2478737f91bf");
        public LLUUID ANIM_AGENT_EXPRESS_TOOTHSMILE = new LLUUID("b92ec1a5-e7ce-a76b-2b05-bcdb9311417e");
        public LLUUID ANIM_AGENT_EXPRESS_WINK = new LLUUID("da020525-4d94-59d6-23d7-81fdebf33148");
        public LLUUID ANIM_AGENT_EXPRESS_WORRY = new LLUUID("9c05e5c7-6f07-6ca4-ed5a-b230390c3950");
        public LLUUID ANIM_AGENT_FALLDOWN = new LLUUID("666307d9-a860-572d-6fd4-c3ab8865c094");
        public LLUUID ANIM_AGENT_FEMALE_WALK = new LLUUID("f5fc7433-043d-e819-8298-f519a119b688");
        public LLUUID ANIM_AGENT_FINGER_WAG = new LLUUID("c1bc7f36-3ba0-d844-f93c-93be945d644f");
        public LLUUID ANIM_AGENT_FIST_PUMP = new LLUUID("7db00ccd-f380-f3ee-439d-61968ec69c8a");
        public LLUUID ANIM_AGENT_FLY = new LLUUID("aec4610c-757f-bc4e-c092-c6e9caf18daf");
        public LLUUID ANIM_AGENT_FLYSLOW = new LLUUID("2b5a38b2-5e00-3a97-a495-4c826bc443e6");
        public LLUUID ANIM_AGENT_HELLO = new LLUUID("9b29cd61-c45b-5689-ded2-91756b8d76a9");
        public LLUUID ANIM_AGENT_HOLD_BAZOOKA_R = new LLUUID("ef62d355-c815-4816-2474-b1acc21094a6");
        public LLUUID ANIM_AGENT_HOLD_BOW_L = new LLUUID("8b102617-bcba-037b-86c1-b76219f90c88");
        public LLUUID ANIM_AGENT_HOLD_HANDGUN_R = new LLUUID("efdc1727-8b8a-c800-4077-975fc27ee2f2");
        public LLUUID ANIM_AGENT_HOLD_RIFLE_R = new LLUUID("3d94bad0-c55b-7dcc-8763-033c59405d33");
        public LLUUID ANIM_AGENT_HOLD_THROW_R = new LLUUID("7570c7b5-1f22-56dd-56ef-a9168241bbb6");
        public LLUUID ANIM_AGENT_HOVER = new LLUUID("4ae8016b-31b9-03bb-c401-b1ea941db41d");
        public LLUUID ANIM_AGENT_HOVER_DOWN = new LLUUID("20f063ea-8306-2562-0b07-5c853b37b31e");
        public LLUUID ANIM_AGENT_HOVER_UP = new LLUUID("62c5de58-cb33-5743-3d07-9e4cd4352864");
        public LLUUID ANIM_AGENT_IMPATIENT = new LLUUID("5ea3991f-c293-392e-6860-91dfa01278a3");
        public LLUUID ANIM_AGENT_JUMP = new LLUUID("2305bd75-1ca9-b03b-1faa-b176b8a8c49e");
        public LLUUID ANIM_AGENT_JUMP_FOR_JOY = new LLUUID("709ea28e-1573-c023-8bf8-520c8bc637fa");
        public LLUUID ANIM_AGENT_KISS_MY_BUTT = new LLUUID("19999406-3a3a-d58c-a2ac-d72e555dcf51");
        public LLUUID ANIM_AGENT_LAND = new LLUUID("7a17b059-12b2-41b1-570a-186368b6aa6f");
        public LLUUID ANIM_AGENT_LAUGH_SHORT = new LLUUID("ca5b3f14-3194-7a2b-c894-aa699b718d1f");
        public LLUUID ANIM_AGENT_MEDIUM_LAND = new LLUUID("f4f00d6e-b9fe-9292-f4cb-0ae06ea58d57");
        public LLUUID ANIM_AGENT_MOTORCYCLE_SIT = new LLUUID("08464f78-3a8e-2944-cba5-0c94aff3af29");
        public LLUUID ANIM_AGENT_MUSCLE_BEACH = new LLUUID("315c3a41-a5f3-0ba4-27da-f893f769e69b");
        public LLUUID ANIM_AGENT_NO = new LLUUID("5a977ed9-7f72-44e9-4c4c-6e913df8ae74");
        public LLUUID ANIM_AGENT_NO_UNHAPPY = new LLUUID("d83fa0e5-97ed-7eb2-e798-7bd006215cb4");
        public LLUUID ANIM_AGENT_NYAH_NYAH = new LLUUID("f061723d-0a18-754f-66ee-29a44795a32f");
        public LLUUID ANIM_AGENT_ONETWO_PUNCH = new LLUUID("eefc79be-daae-a239-8c04-890f5d23654a");
        public LLUUID ANIM_AGENT_PEACE = new LLUUID("b312b10e-65ab-a0a4-8b3c-1326ea8e3ed9");
        public LLUUID ANIM_AGENT_POINT_ME = new LLUUID("17c024cc-eef2-f6a0-3527-9869876d7752");
        public LLUUID ANIM_AGENT_POINT_YOU = new LLUUID("ec952cca-61ef-aa3b-2789-4d1344f016de");
        public LLUUID ANIM_AGENT_PRE_JUMP = new LLUUID("7a4e87fe-de39-6fcb-6223-024b00893244");
        public LLUUID ANIM_AGENT_PUNCH_LEFT = new LLUUID("f3300ad9-3462-1d07-2044-0fef80062da0");
        public LLUUID ANIM_AGENT_PUNCH_RIGHT = new LLUUID("c8e42d32-7310-6906-c903-cab5d4a34656");
        public LLUUID ANIM_AGENT_REPULSED = new LLUUID("36f81a92-f076-5893-dc4b-7c3795e487cf");
        public LLUUID ANIM_AGENT_ROUNDHOUSE_KICK = new LLUUID("49aea43b-5ac3-8a44-b595-96100af0beda");
        public LLUUID ANIM_AGENT_RPS_COUNTDOWN = new LLUUID("35db4f7e-28c2-6679-cea9-3ee108f7fc7f");
        public LLUUID ANIM_AGENT_RPS_PAPER = new LLUUID("0836b67f-7f7b-f37b-c00a-460dc1521f5a");
        public LLUUID ANIM_AGENT_RPS_ROCK = new LLUUID("42dd95d5-0bc6-6392-f650-777304946c0f");
        public LLUUID ANIM_AGENT_RPS_SCISSORS = new LLUUID("16803a9f-5140-e042-4d7b-d28ba247c325");
        public LLUUID ANIM_AGENT_RUN = new LLUUID("05ddbff8-aaa9-92a1-2b74-8fe77a29b445");
        public LLUUID ANIM_AGENT_SAD = new LLUUID("0eb702e2-cc5a-9a88-56a5-661a55c0676a");
        public LLUUID ANIM_AGENT_SALUTE = new LLUUID("cd7668a6-7011-d7e2-ead8-fc69eff1a104");
        public LLUUID ANIM_AGENT_SHOOT_BOW_L = new LLUUID("e04d450d-fdb5-0432-fd68-818aaf5935f8");
        public LLUUID ANIM_AGENT_SHOUT = new LLUUID("6bd01860-4ebd-127a-bb3d-d1427e8e0c42");
        public LLUUID ANIM_AGENT_SHRUG = new LLUUID("70ea714f-3a97-d742-1b01-590a8fcd1db5");
        public LLUUID ANIM_AGENT_SIT = new LLUUID("1a5fe8ac-a804-8a5d-7cbd-56bd83184568");
        public LLUUID ANIM_AGENT_SIT_FEMALE = new LLUUID("b1709c8d-ecd3-54a1-4f28-d55ac0840782");
        public LLUUID ANIM_AGENT_SIT_GENERIC = new LLUUID("245f3c54-f1c0-bf2e-811f-46d8eeb386e7");
        public LLUUID ANIM_AGENT_SIT_GROUND = new LLUUID("1c7600d6-661f-b87b-efe2-d7421eb93c86");
        public LLUUID ANIM_AGENT_SIT_GROUND_CONSTRAINED = new LLUUID("1a2bd58e-87ff-0df8-0b4c-53e047b0bb6e");
        public LLUUID ANIM_AGENT_SIT_TO_STAND = new LLUUID("a8dee56f-2eae-9e7a-05a2-6fb92b97e21e");
        public LLUUID ANIM_AGENT_SLEEP = new LLUUID("f2bed5f9-9d44-39af-b0cd-257b2a17fe40");
        public LLUUID ANIM_AGENT_SMOKE_IDLE = new LLUUID("d2f2ee58-8ad1-06c9-d8d3-3827ba31567a");
        public LLUUID ANIM_AGENT_SMOKE_INHALE = new LLUUID("6802d553-49da-0778-9f85-1599a2266526");
        public LLUUID ANIM_AGENT_SMOKE_THROW_DOWN = new LLUUID("0a9fb970-8b44-9114-d3a9-bf69cfe804d6");
        public LLUUID ANIM_AGENT_SNAPSHOT = new LLUUID("eae8905b-271a-99e2-4c0e-31106afd100c");
        public LLUUID ANIM_AGENT_STAND = new LLUUID("2408fe9e-df1d-1d7d-f4ff-1384fa7b350f");
        public LLUUID ANIM_AGENT_STANDUP = new LLUUID("3da1d753-028a-5446-24f3-9c9b856d9422");
        public LLUUID ANIM_AGENT_STAND_1 = new LLUUID("15468e00-3400-bb66-cecc-646d7c14458e");
        public LLUUID ANIM_AGENT_STAND_2 = new LLUUID("370f3a20-6ca6-9971-848c-9a01bc42ae3c");
        public LLUUID ANIM_AGENT_STAND_3 = new LLUUID("42b46214-4b44-79ae-deb8-0df61424ff4b");
        public LLUUID ANIM_AGENT_STAND_4 = new LLUUID("f22fed8b-a5ed-2c93-64d5-bdd8b93c889f");
        public LLUUID ANIM_AGENT_STRETCH = new LLUUID("80700431-74ec-a008-14f8-77575e73693f");
        public LLUUID ANIM_AGENT_STRIDE = new LLUUID("1cb562b0-ba21-2202-efb3-30f82cdf9595");
        public LLUUID ANIM_AGENT_SURF = new LLUUID("41426836-7437-7e89-025d-0aa4d10f1d69");
        public LLUUID ANIM_AGENT_SURPRISE = new LLUUID("313b9881-4302-73c0-c7d0-0e7a36b6c224");
        public LLUUID ANIM_AGENT_SWORD_STRIKE = new LLUUID("85428680-6bf9-3e64-b489-6f81087c24bd");
        public LLUUID ANIM_AGENT_TALK = new LLUUID("5c682a95-6da4-a463-0bf6-0f5b7be129d1");
        public LLUUID ANIM_AGENT_TANTRUM = new LLUUID("11000694-3f41-adc2-606b-eee1d66f3724");
        public LLUUID ANIM_AGENT_THROW_R = new LLUUID("aa134404-7dac-7aca-2cba-435f9db875ca");
        public LLUUID ANIM_AGENT_TRYON_SHIRT = new LLUUID("83ff59fe-2346-f236-9009-4e3608af64c1");
        public LLUUID ANIM_AGENT_TURNLEFT = new LLUUID("56e0ba0d-4a9f-7f27-6117-32f2ebbf6135");
        public LLUUID ANIM_AGENT_TURNRIGHT = new LLUUID("2d6daa51-3192-6794-8e2e-a15f8338ec30");
        public LLUUID ANIM_AGENT_TYPE = new LLUUID("c541c47f-e0c0-058b-ad1a-d6ae3a4584d9");
        public LLUUID ANIM_AGENT_WALK = new LLUUID("6ed24bd8-91aa-4b12-ccc7-c97c857ab4e0");
        public LLUUID ANIM_AGENT_WHISPER = new LLUUID("7693f268-06c7-ea71-fa21-2b30d6533f8f");
        public LLUUID ANIM_AGENT_WHISTLE = new LLUUID("b1ed7982-c68e-a982-7561-52a88a5298c0");
        public LLUUID ANIM_AGENT_WINK = new LLUUID("869ecdad-a44b-671e-3266-56aef2e3ac2e");
        public LLUUID ANIM_AGENT_WINK_HOLLYWOOD = new LLUUID("c0c4030f-c02b-49de-24ba-2331f43fe41c");
        public LLUUID ANIM_AGENT_WORRY = new LLUUID("9f496bd2-589a-709f-16cc-69bf7df1d36c");
        public LLUUID ANIM_AGENT_YES = new LLUUID("15dd911d-be82-2856-26db-27659b142875");
        public LLUUID ANIM_AGENT_YES_HAPPY = new LLUUID("b8c8b2a3-9008-1771-3bfc-90924955ab2d");
        public LLUUID ANIM_AGENT_YOGA_FLOAT = new LLUUID("42ecd00b-9947-a97c-400a-bbc9174c7aeb");
	}
}
