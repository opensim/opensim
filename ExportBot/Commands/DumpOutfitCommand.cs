using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Utilities.Assets;
using libsecondlife.Utilities.Appearance;

namespace libsecondlife.TestClient
{
    public class DumpOutfitCommand : Command
    {
        libsecondlife.Utilities.Assets.AssetManager Assets;
        List<LLUUID> OutfitAssets = new List<LLUUID>();

        public DumpOutfitCommand(TestClient testClient)
        {
            Name = "dumpoutfit";
            Description = "Dumps all of the textures from an avatars outfit to the hard drive. Usage: dumpoutfit [avatar-uuid]";

            Assets = new AssetManager(testClient);
            Assets.OnImageReceived += new AssetManager.ImageReceivedCallback(Assets_OnImageReceived);
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            if (args.Length != 1)
                return "Usage: dumpoutfit [avatar-uuid]";

            LLUUID target;

            if (!LLUUID.TryParse(args[0], out target))
                return "Usage: dumpoutfit [avatar-uuid]";

            lock (Client.AvatarList)
            {
                foreach (Avatar avatar in Client.AvatarList.Values)
                {
                    if (avatar.ID == target)
                    {
                        StringBuilder output = new StringBuilder("Downloading ");

                        lock (OutfitAssets) OutfitAssets.Clear();

                        foreach (KeyValuePair<uint, LLObject.TextureEntryFace> face in avatar.Textures.FaceTextures)
                        {
                            ImageType type = ImageType.Normal;

                            switch ((AppearanceManager.TextureIndex)face.Key)
                            {
                                case AppearanceManager.TextureIndex.HeadBaked:
                                case AppearanceManager.TextureIndex.EyesBaked:
                                case AppearanceManager.TextureIndex.UpperBaked:
                                case AppearanceManager.TextureIndex.LowerBaked:
                                case AppearanceManager.TextureIndex.SkirtBaked:
                                    type = ImageType.Baked;
                                    break;
                            }

                            Assets.RequestImage(face.Value.TextureID, type, 100000.0f, 0);

                            output.Append(((AppearanceManager.TextureIndex)face.Key).ToString());
                            output.Append(" ");
                        }

                        return output.ToString();
                    }
                }
            }

            return "Couldn't find avatar " + target.ToStringHyphenated();
        }

        private void Assets_OnImageReceived(ImageDownload image)
        {
            if (image.Success)
            {
                try
                {
                    File.WriteAllBytes(image.ID.ToStringHyphenated() + ".jp2", image.AssetData);
                    Console.WriteLine("Wrote JPEG2000 image " + image.ID.ToStringHyphenated() + ".jp2");

                    byte[] tgaFile = OpenJPEGNet.OpenJPEG.DecodeToTGA(image.AssetData);
                    File.WriteAllBytes(image.ID.ToStringHyphenated() + ".tga", tgaFile);
                    Console.WriteLine("Wrote TGA image " + image.ID.ToStringHyphenated() + ".tga");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
            else
            {
                Console.WriteLine("Failed to download image " + image.ID.ToStringHyphenated());
            }
        }
    }
}
