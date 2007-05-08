using System;
using System.Collections.Generic;
using System.Threading;
using libsecondlife;
using libsecondlife.Utilities;

namespace libsecondlife.TestClient
{
    public class ParcelInfoCommand : Command
    {
        private ParcelDownloader ParcelDownloader;
        private ManualResetEvent ParcelsDownloaded = new ManualResetEvent(false);
        private int ParcelCount = 0;

        public ParcelInfoCommand(TestClient testClient)
		{
			Name = "parcelinfo";
			Description = "Prints out info about all the parcels in this simulator";

            ParcelDownloader = new ParcelDownloader(testClient);
            ParcelDownloader.OnParcelsDownloaded += new ParcelDownloader.ParcelsDownloadedCallback(Parcels_OnParcelsDownloaded);
            testClient.Network.OnDisconnected += new NetworkManager.DisconnectedCallback(Network_OnDisconnected);
		}

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            ParcelDownloader.DownloadSimParcels(Client.Network.CurrentSim);

            ParcelsDownloaded.Reset();
            ParcelsDownloaded.WaitOne(20000, false);

            if (Client.Network.CurrentSim != null)
                return "Downloaded information for " + ParcelCount + " parcels in " + Client.Network.CurrentSim.Name;
            else
                return String.Empty;
        }

        void Parcels_OnParcelsDownloaded(Simulator simulator, Dictionary<int, Parcel> Parcels, int[,] map)
        {
            foreach (KeyValuePair<int, Parcel> parcel in Parcels)
            {
                WaterType type = ParcelDownloader.GetWaterType(map, parcel.Value.LocalID);
                float delta = ParcelDownloader.GetHeightRange(map, parcel.Value.LocalID);
                int deviation = ParcelDownloader.GetRectangularDeviation(parcel.Value.AABBMin, parcel.Value.AABBMax, 
                    parcel.Value.Area);

                Console.WriteLine("Parcels[{0}]: Name: \"{1}\", Description: \"{2}\" ACL Count: {3}, " +
                    "Location: {4}, Height Range: {5}, Shape Deviation: {6}", parcel.Key, parcel.Value.Name, 
                    parcel.Value.Desc, parcel.Value.AccessList.Count, type.ToString(), delta, deviation);
            }

            ParcelCount = Parcels.Count;

            ParcelsDownloaded.Set();
        }

        void Network_OnDisconnected(NetworkManager.DisconnectType reason, string message)
        {
            ParcelsDownloaded.Set();
        }
    }
}
