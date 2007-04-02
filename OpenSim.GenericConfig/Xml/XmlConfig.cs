using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using OpenSim.Framework.Interfaces;

namespace OpenSim.GenericConfig
{
    public class XmlConfig : IGenericConfig
    {
        private XmlDocument doc;
        private XmlNode rootNode;
        private XmlNode configNode;
        private string fileName;

        public XmlConfig(string filename)
        {
            fileName = filename;
        }

        public void LoadData()
        {
            doc = new XmlDocument();
            try
            {
                XmlTextReader reader = new XmlTextReader(fileName);
                reader.WhitespaceHandling = WhitespaceHandling.None;
                doc.Load(reader);
                reader.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }
            try
            {
                rootNode = doc.FirstChild;
                if (rootNode.Name != "Root")
                    throw new Exception("Error: Invalid .xml File. Missing <Root>");

                configNode = rootNode.FirstChild;
                if (configNode.Name != "Config")
                    throw new Exception("Error: Invalid .xml File. <Root> first child should be <Config>");
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public string GetAttribute(string attributeName)
        {
            string result = "";
            if (configNode.Attributes[attributeName] != null)
            {
                result = ((XmlAttribute)configNode.Attributes.GetNamedItem(attributeName)).Value;
            }
            return result;
        }

        public bool SetAttribute(string attributeName, string attributeValue)
        {
            if (configNode.Attributes[attributeName] != null)
            {
                configNode.Attributes.GetNamedItem(attributeName).Value = attributeValue;
            }
            else
            {
                XmlAttribute attri;
                attri = doc.CreateAttribute(attributeName);
                attri.Value = attributeValue;
                configNode.Attributes.Append(attri);
            }
            return true;
        }

        public void Commit()
        {
            doc.Save(fileName);
        }

        public void Close()
        {
            configNode = null;
            rootNode = null;
            doc = null;
        }
                             
    }
}
