using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework;

namespace OpenSim.Region.UserStatistics
{
    public class Updater_distributor : IStatsController
    {
        private string updaterjs = string.Empty;


        public Hashtable ProcessModel(Hashtable pParams)
        {
            Hashtable pResult = new Hashtable();
            if (updaterjs.Length == 0)
            {
                StreamReader fs = new StreamReader(new FileStream(Util.dataDir() + "/data/updater.js", FileMode.Open));
                updaterjs = fs.ReadToEnd();
                fs.Close();
                fs.Dispose();
            }
            pResult["js"] = updaterjs;
            return pResult;
        }

        public string RenderView(Hashtable pModelResult)
        {
            return pModelResult["js"].ToString();
        }

    }
}