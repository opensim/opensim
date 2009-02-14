using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Data.NHibernate
{
    public class EstateRegionLink
    {
        private UUID estateRegionLinkID;
        public UUID EstateRegionLinkID
        {
            get
            {
                return estateRegionLinkID;
            }
            set
            {
                estateRegionLinkID = value;
            }
        }

        private uint estateID;
        public uint EstateID
        {
            get
            {
                return estateID;
            }
            set
            {
                estateID = value;
            }
        }

        private UUID regionID;
        public UUID RegionID
        {
            get
            {
                return regionID;
            }
            set
            {
                regionID = value;
            }
        }
    }
}
