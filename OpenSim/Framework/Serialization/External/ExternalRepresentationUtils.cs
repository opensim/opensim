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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Services.Interfaces;

namespace OpenSim.Framework.Serialization.External
{
    /// <summary>
    /// Utilities for manipulating external representations of data structures in OpenSim
    /// </summary>
    public class ExternalRepresentationUtils
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Populate a node with data read from xml using a dictinoary of processors
        /// </summary>
        /// <param name="nodeToFill"></param>
        /// <param name="processors"></param>
        /// <param name="xtr"></param>
        /// <returns>true on successful, false if there were any processing failures</returns>
        public static bool ExecuteReadProcessors<NodeType>(
            NodeType nodeToFill, Dictionary<string, Action<NodeType, XmlReader>> processors, XmlReader xtr)
        {
            return ExecuteReadProcessors(
                nodeToFill,
                processors,
                xtr,
                (o, nodeName, e) => {
                    m_log.Debug(string.Format("[ExternalRepresentationUtils]: Error while parsing element {0} ",
                        nodeName), e);
                });
        }

        /// <summary>
        /// Populate a node with data read from xml using a dictinoary of processors
        /// </summary>
        /// <param name="nodeToFill"></param>
        /// <param name="processors"></param>
        /// <param name="xtr"></param>
        /// <param name="parseExceptionAction">
        /// Action to take if there is a parsing problem.  This will usually just be to log the exception
        /// </param>
        /// <returns>true on successful, false if there were any processing failures</returns>
        public static bool ExecuteReadProcessors<NodeType>(
            NodeType nodeToFill,
            Dictionary<string, Action<NodeType, XmlReader>> processors,
            XmlReader xtr,
            Action<NodeType, string, Exception> parseExceptionAction)
        {
            bool errors = false;
            int numErrors = 0;

            Stopwatch timer = new Stopwatch();
            timer.Start();

            string nodeName = string.Empty;
            while (xtr.NodeType != XmlNodeType.EndElement)
            {
                nodeName = xtr.Name;

                // m_log.DebugFormat("[ExternalRepresentationUtils]: Processing node: {0}", nodeName);

                Action<NodeType, XmlReader> p = null;
                if (processors.TryGetValue(xtr.Name, out p))
                {
                    // m_log.DebugFormat("[ExternalRepresentationUtils]: Found processor for {0}", nodeName);

                    try
                    {
                        p(nodeToFill, xtr);
                    }
                    catch (Exception e)
                    {
                        errors = true;
                        parseExceptionAction(nodeToFill, nodeName, e);
                        
                        if (xtr.EOF)
                        {
                            m_log.Debug("[ExternalRepresentationUtils]: Aborting ExecuteReadProcessors due to unexpected end of XML");
                            break;
                        }
                        
                        if (++numErrors == 10)
                        {
                            m_log.Debug("[ExternalRepresentationUtils]: Aborting ExecuteReadProcessors due to too many parsing errors");
                            break;
                        }

                        if (xtr.NodeType == XmlNodeType.EndElement)
                            xtr.Read();
                    }
                }
                else
                {
                    // m_log.DebugFormat("[ExternalRepresentationUtils]: found unknown element \"{0}\"", nodeName);
                    xtr.ReadOuterXml(); // ignore
                }

                if (timer.Elapsed.TotalSeconds >= 60)
                {
                    m_log.Debug("[ExternalRepresentationUtils]: Aborting ExecuteReadProcessors due to timeout");
                    errors = true;
                    break;
                }
            }

            return errors;
        }

        /// <summary>
        /// Takes a XML representation of a SceneObjectPart and returns another XML representation
        /// with creator data added to it.
        /// </summary>
        /// <param name="xml">The SceneObjectPart represented in XML2</param>
        /// <param name="homeURL">The URL of the user agents service (home) for the creator</param>
        /// <param name="userService">The service for retrieving user account information</param>
        /// <param name="scopeID">The scope of the user account information (Grid ID)</param>
        /// <returns>The SceneObjectPart represented in XML2</returns>
        [Obsolete("This method is deprecated. Use RewriteSOP instead.")]
        public static string RewriteSOP_Old(string xml, string homeURL, IUserAccountService userService, UUID scopeID)
        {
            if (xml == string.Empty || homeURL == string.Empty || userService == null)
                return xml;

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNodeList sops = doc.GetElementsByTagName("SceneObjectPart");

            foreach (XmlNode sop in sops)
            {
                UserAccount creator = null;
                bool hasCreatorData = false;
                XmlNodeList nodes = sop.ChildNodes;
                foreach (XmlNode node in nodes)
                {
                    if (node.Name == "CreatorID")
                    {
                        UUID uuid = UUID.Zero;
                        UUID.TryParse(node.InnerText, out uuid);
                        creator = userService.GetUserAccount(scopeID, uuid);
                    }

                    if (node.Name == "CreatorData" && node.InnerText != null && node.InnerText != string.Empty)
                        hasCreatorData = true;

                    //if (node.Name == "OwnerID")
                    //{
                    //    UserAccount owner = GetUser(node.InnerText);
                    //    if (owner != null)
                    //        node.InnerText = m_ProfileServiceURL + "/" + node.InnerText + "/" + owner.FirstName + " " + owner.LastName;
                    //}
                }
                if (!hasCreatorData && creator != null)
                {
                    XmlElement creatorData = doc.CreateElement("CreatorData");
                    creatorData.InnerText = CalcCreatorData(homeURL, creator.FirstName + " " + creator.LastName);
                    sop.AppendChild(creatorData);
                }
            }

            using (StringWriter wr = new StringWriter())
            {
                doc.Save(wr);
                return wr.ToString();
            }
        }

        /// <summary>
        /// Takes a XML representation of a SceneObjectPart and returns another XML representation
        /// with creator data added to it.
        /// </summary>
        /// <param name="xml">The SceneObjectPart represented in XML2</param>
        /// <param name="sceneName">An identifier for the component that's calling this function</param>
        /// <param name="homeURL">The URL of the user agents service (home) for the creator</param>
        /// <param name="userService">The service for retrieving user account information</param>
        /// <param name="scopeID">The scope of the user account information (Grid ID)</param>
        /// <returns>The SceneObjectPart represented in XML2</returns>
        public static string RewriteSOP(string xmlData, string sceneName, string homeURL, IUserAccountService userService, UUID scopeID)
        {
            //            Console.WriteLine("Input XML [{0}]", xmlData);
            if (xmlData == string.Empty || homeURL == string.Empty || userService == null)
                return xmlData;

            // Deal with bug
            xmlData = ExternalRepresentationUtils.SanitizeXml(xmlData);

            using (StringWriter sw = new StringWriter())
            using (XmlTextWriter writer = new XmlTextWriter(sw))
            using (XmlTextReader wrappedReader = new XmlTextReader(xmlData, XmlNodeType.Element, null))
            using (XmlReader reader = XmlReader.Create(wrappedReader, new XmlReaderSettings() { IgnoreWhitespace = true, ConformanceLevel = ConformanceLevel.Fragment }))
            {
                TransformXml(reader, writer, sceneName, homeURL, userService, scopeID);

                //                Console.WriteLine("Output: [{0}]", sw.ToString());

                return sw.ToString();
            }
        }

        protected static void TransformXml(XmlReader reader, XmlWriter writer, string sceneName, string homeURI, IUserAccountService userAccountService, UUID scopeID)
        {
            //            m_log.DebugFormat("[HG ASSET MAPPER]: Transforming XML");

            int sopDepth = -1;
            UserAccount creator = null;
            bool hasCreatorData = false;

            while (reader.Read())
            {
                //                Console.WriteLine("Depth: {0}, name {1}", reader.Depth, reader.Name);

                switch (reader.NodeType)
                {
                    case XmlNodeType.Attribute:
                        //                    Console.WriteLine("FOUND ATTRIBUTE {0}", reader.Name);
                        writer.WriteAttributeString(reader.Name, reader.Value);
                        break;

                    case XmlNodeType.CDATA:
                        writer.WriteCData(reader.Value);
                        break;

                    case XmlNodeType.Comment:
                        writer.WriteComment(reader.Value);
                        break;

                    case XmlNodeType.DocumentType:
                        writer.WriteDocType(reader.Name, reader.Value, null, null);
                        break;

                    case XmlNodeType.Element:
                        //                    m_log.DebugFormat("Depth {0} at element {1}", reader.Depth, reader.Name);

                        writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);

                        if (reader.HasAttributes)
                        {
                            while (reader.MoveToNextAttribute())
                                writer.WriteAttributeString(reader.Name, reader.Value);

                            reader.MoveToElement();
                        }

                        if (reader.LocalName == "SceneObjectPart")
                        {
                            if (sopDepth < 0)
                            {
                                sopDepth = reader.Depth;
                                //                            m_log.DebugFormat("[HG ASSET MAPPER]: Set sopDepth to {0}", sopDepth);
                            }
                        }
                        else
                        {
                            if (sopDepth >= 0 && reader.Depth == sopDepth + 1)
                            {
                                if (reader.Name == "CreatorID")
                                {
                                    reader.Read();
                                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "Guid" || reader.Name == "UUID")
                                    {
                                        reader.Read();

                                        if (reader.NodeType == XmlNodeType.Text)
                                        {
                                            UUID uuid = UUID.Zero;
                                            UUID.TryParse(reader.Value, out uuid);
                                            creator = userAccountService.GetUserAccount(scopeID, uuid);
                                            writer.WriteElementString("UUID", reader.Value);
                                            reader.Read();
                                        }
                                        else
                                        {
                                            // If we unexpected run across mixed content in this node, still carry on
                                            // transforming the subtree (this replicates earlier behaviour).
                                            TransformXml(reader, writer, sceneName, homeURI, userAccountService, scopeID);
                                        }
                                    }
                                    else
                                    {
                                        // If we unexpected run across mixed content in this node, still carry on
                                        // transforming the subtree (this replicates earlier behaviour).
                                        TransformXml(reader, writer, sceneName, homeURI, userAccountService, scopeID);
                                    }
                                }
                                else if (reader.Name == "CreatorData")
                                {
                                    reader.Read();
                                    if (reader.NodeType == XmlNodeType.Text)
                                    {
                                        hasCreatorData = true;
                                        writer.WriteString(reader.Value);
                                    }
                                    else
                                    {
                                        // If we unexpected run across mixed content in this node, still carry on
                                        // transforming the subtree (this replicates earlier behaviour).
                                        TransformXml(reader, writer, sceneName, homeURI, userAccountService, scopeID);
                                    }
                                }
                            }
                        }

                        if (reader.IsEmptyElement)
                        {
                            //                        m_log.DebugFormat("[HG ASSET MAPPER]: Writing end for empty element {0}", reader.Name);
                            writer.WriteEndElement();
                        }

                        break;

                    case XmlNodeType.EndElement:
                        //                    m_log.DebugFormat("Depth {0} at EndElement", reader.Depth);
                        if (sopDepth == reader.Depth)
                        {
                            if (!hasCreatorData && creator != null)
                                writer.WriteElementString(reader.Prefix, "CreatorData", reader.NamespaceURI, string.Format("{0};{1} {2}", homeURI, creator.FirstName, creator.LastName));

                            //                        m_log.DebugFormat("[HG ASSET MAPPER]: Reset sopDepth");
                            sopDepth = -1;
                            creator = null;
                            hasCreatorData = false;
                        }
                        writer.WriteEndElement();
                        break;

                    case XmlNodeType.EntityReference:
                        writer.WriteEntityRef(reader.Name);
                        break;

                    case XmlNodeType.ProcessingInstruction:
                        writer.WriteProcessingInstruction(reader.Name, reader.Value);
                        break;

                    case XmlNodeType.Text:
                        writer.WriteString(reader.Value);
                        break;

                    case XmlNodeType.XmlDeclaration:
                        // For various reasons, not all serializations have xml declarations (or consistent ones) 
                        // and as it's embedded inside a byte stream we don't need it anyway, so ignore.
                        break;

                    default:
                        m_log.WarnFormat(
                            "[HG ASSET MAPPER]: Unrecognized node {0} in asset XML transform in {1}",
                            reader.NodeType, sceneName);
                        break;
                }
            }
        }

        public static string CalcCreatorData(string homeURL, string name)
        {
            return homeURL + ";" + name;
        }

        internal static string CalcCreatorData(string homeURL, UUID uuid, string name)
        {
            return homeURL + "/" + uuid + ";" + name;
        }

        /// <summary>
        /// Sanitation for bug introduced in Oct. 20 (1eb3e6cc43e2a7b4053bc1185c7c88e22356c5e8)
        /// </summary>
        /// <param name="xmlData"></param>
        /// <returns></returns>
        public static string SanitizeXml(string xmlData)
        {
            string fixedData = xmlData;
            if (fixedData != null)
                // Loop, because it may contain multiple
                while (fixedData.Contains("xmlns:xmlns:"))
                    fixedData = fixedData.Replace("xmlns:xmlns:", "xmlns:");
            return fixedData;
        }

    }
}
