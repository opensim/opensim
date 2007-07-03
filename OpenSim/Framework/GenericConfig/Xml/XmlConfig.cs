/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.IO;
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
        private bool createdFile = false;

        public XmlConfig(string filename)
        {
            fileName = filename;
        }

        public void LoadData()
        {
            doc = new XmlDocument();

            if (File.Exists(fileName))
            {
                XmlTextReader reader = new XmlTextReader(fileName);
                reader.WhitespaceHandling = WhitespaceHandling.None;
                doc.Load(reader);
                reader.Close();
            }
            else
            {
                createdFile = true;
                rootNode = doc.CreateNode(XmlNodeType.Element, "Root", "");
                doc.AppendChild(rootNode);
                configNode = doc.CreateNode(XmlNodeType.Element, "Config", "");
                rootNode.AppendChild(configNode);
            }


            rootNode = doc.FirstChild;
            if (rootNode.Name != "Root")
                throw new Exception("Error: Invalid .xml File. Missing <Root>");

            configNode = rootNode.FirstChild;
            if (configNode.Name != "Config")
                throw new Exception("Error: Invalid .xml File. <Root> first child should be <Config>");

            if (createdFile)
            {
                this.Commit();
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
                ((XmlAttribute)configNode.Attributes.GetNamedItem(attributeName)).Value = attributeValue;
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
