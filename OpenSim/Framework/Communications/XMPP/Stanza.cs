using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace OpenSim.Framework.Communications.XMPP
{
    public class Stanza
    {

        public string localName = String.Empty;
        public JId to;
        public JId from;
        public string id;
        public string lang;
        public string nodeName;

        public Stanza(XmlNode node, Object defaults, bool hasID)
        {

        }
        //public virtual XmlElement getNode()
        //{
            //return new XmlElement();
        //}
        public virtual string generateId()
        {
            return "";
        }
    }
}
