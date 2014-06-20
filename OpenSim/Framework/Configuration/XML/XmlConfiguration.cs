/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.IO;
using System.Xml;

namespace OpenSim.Framework.Configuration.XML
{
    public class XmlConfiguration : IGenericConfig
    {
        private XmlDocument doc;
        private XmlNode rootNode;
        private XmlNode configNode;
        private string fileName;
        private bool createdFile = false;

        public void SetFileName(string file)
        {
            fileName = file;
        }

        private void LoadDataToClass()
        {
            rootNode = doc.SelectSingleNode("Root");
            if (null == rootNode)
                throw new Exception("Error: Invalid .xml File. Missing <Root>");

            configNode = rootNode.SelectSingleNode("Config");
            if (null == configNode)
                throw new Exception("Error: Invalid .xml File. <Root> should contain a <Config>");
        }

        public void LoadData()
        {
            lock (this)
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
                    rootNode = doc.CreateNode(XmlNodeType.Element, "Root", String.Empty);
                    doc.AppendChild(rootNode);
                    configNode = doc.CreateNode(XmlNodeType.Element, "Config", String.Empty);
                    rootNode.AppendChild(configNode);
                }

                LoadDataToClass();

                if (createdFile)
                {
                    Commit();
                }
            }
        }

        public void LoadDataFromString(string data)
        {
            doc = new XmlDocument();
            doc.LoadXml(data);

            LoadDataToClass();
        }

        public string GetAttribute(string attributeName)
        {
            string result = null;
            if (configNode.Attributes[attributeName] != null)
            {
                result = ((XmlAttribute) configNode.Attributes.GetNamedItem(attributeName)).Value;
            }
            return result;
        }

        public bool SetAttribute(string attributeName, string attributeValue)
        {
            if (configNode.Attributes[attributeName] != null)
            {
                ((XmlAttribute) configNode.Attributes.GetNamedItem(attributeName)).Value = attributeValue;
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
            if (string.IsNullOrEmpty(fileName))
                return;

            if (!Directory.Exists(Util.configDir()))
            {
                Directory.CreateDirectory(Util.configDir());
            }
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
