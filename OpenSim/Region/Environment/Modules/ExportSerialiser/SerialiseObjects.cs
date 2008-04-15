using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.ExportSerialiser
{
    class SerialiseObjects : IFileSerialiser
    {
        #region IFileSerialiser Members

        public string WriteToFile(Scene scene, string dir)
        {
            string targetFileName = dir + "objects.xml";

            scene.SavePrimsToXml2(targetFileName);

            return "objects.xml";
        }

        #endregion
    }
}
