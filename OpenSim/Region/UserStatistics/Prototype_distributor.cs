using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework;

namespace OpenSim.Region.UserStatistics
{
    public class Prototype_distributor : IStatsController
    {
        private string prototypejs=string.Empty;


        public Hashtable ProcessModel(Hashtable pParams)
        {
            Hashtable pResult = new Hashtable();
            if (prototypejs.Length == 0)
            {
                StreamReader fs = new StreamReader(new FileStream(Util.dataDir() + "/data/prototype.js", FileMode.Open));
                prototypejs = fs.ReadToEnd();
            }
            pResult["js"] = prototypejs;
            return pResult;
        }

        public string RenderView(Hashtable pModelResult)
        {
            return pModelResult["js"].ToString();
        }

    }
}
