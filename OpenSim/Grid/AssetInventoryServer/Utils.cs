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
using System.Globalization;
using System.Xml;
using System.Xml.Serialization;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Grid.AssetInventoryServer
{
    public static class Utils
    {
        public static UUID GetAuthToken(OSHttpRequest request)
        {
            UUID authToken = UUID.Zero;

            string[] authHeader = request.Headers.GetValues("Authorization");
            if (authHeader != null && authHeader.Length == 1)
            {
                // Example header:
                // Authorization: OpenGrid 65fda0b5-4446-42f5-b828-aaf644293646
                string[] authHeaderParts = authHeader[0].Split(' ');
                if (authHeaderParts.Length == 2 && authHeaderParts[0] == "OpenGrid")
                    UUID.TryParse(authHeaderParts[1], out authToken);
            }

            //if (authToken == UUID.Zero && request.Cookies != null)
            //{
            //    // Check for an authToken cookie to make logins browser-compatible
            //    RequestCookie authCookie = request.Cookies["authToken"];
            //    if (authCookie != null)
            //        UUID.TryParse(authCookie.Value, out authToken);
            //}

            return authToken;
        }

        public static Uri GetOpenSimUri(UUID avatarID)
        {
            return new Uri("http://opensim/" + avatarID.ToString());
        }

        public static bool TryGetOpenSimUUID(Uri avatarUri, out UUID avatarID)
        {
            string[] parts = avatarUri.Segments;
            return UUID.TryParse(parts[parts.Length - 1], out avatarID);
        }

        #region SL / file extension / content-type conversions

        public static string SLAssetTypeToContentType(int assetType)
        {
            switch (assetType)
            {
                case 0:
                    return "image/jp2";
                case 1:
                    return "application/ogg";
                case 2:
                    return "application/x-metaverse-callingcard";
                case 3:
                    return "application/x-metaverse-landmark";
                case 5:
                    return "application/x-metaverse-clothing";
                case 6:
                    return "application/x-metaverse-primitive";
                case 7:
                    return "application/x-metaverse-notecard";
                case 8:
                    return "application/x-metaverse-folder";
                case 10:
                    return "application/x-metaverse-lsl";
                case 11:
                    return "application/x-metaverse-lso";
                case 12:
                    return "image/tga";
                case 13:
                    return "application/x-metaverse-bodypart";
                case 17:
                    return "audio/x-wav";
                case 19:
                    return "image/jpeg";
                case 20:
                    return "application/x-metaverse-animation";
                case 21:
                    return "application/x-metaverse-gesture";
                case 22:
                    return "application/x-metaverse-simstate";
                default:
                    return "application/octet-stream";
            }
        }

        public static int ContentTypeToSLAssetType(string contentType)
        {
            switch (contentType)
            {
                case "image/jp2":
                    return 0;
                case "application/ogg":
                    return 1;
                case "application/x-metaverse-callingcard":
                    return 2;
                case "application/x-metaverse-landmark":
                    return 3;
                case "application/x-metaverse-clothing":
                    return 5;
                case "application/x-metaverse-primitive":
                    return 6;
                case "application/x-metaverse-notecard":
                    return 7;
                case "application/x-metaverse-lsl":
                    return 10;
                case "application/x-metaverse-lso":
                    return 11;
                case "image/tga":
                    return 12;
                case "application/x-metaverse-bodypart":
                    return 13;
                case "audio/x-wav":
                    return 17;
                case "image/jpeg":
                    return 19;
                case "application/x-metaverse-animation":
                    return 20;
                case "application/x-metaverse-gesture":
                    return 21;
                case "application/x-metaverse-simstate":
                    return 22;
                default:
                    return -1;
            }
        }

        public static string ContentTypeToExtension(string contentType)
        {
            switch (contentType)
            {
                case "image/jp2":
                    return "texture";
                case "application/ogg":
                    return "ogg";
                case "application/x-metaverse-callingcard":
                    return "callingcard";
                case "application/x-metaverse-landmark":
                    return "landmark";
                case "application/x-metaverse-clothing":
                    return "clothing";
                case "application/x-metaverse-primitive":
                    return "primitive";
                case "application/x-metaverse-notecard":
                    return "notecard";
                case "application/x-metaverse-lsl":
                    return "lsl";
                case "application/x-metaverse-lso":
                    return "lso";
                case "image/tga":
                    return "tga";
                case "application/x-metaverse-bodypart":
                    return "bodypart";
                case "audio/x-wav":
                    return "wav";
                case "image/jpeg":
                    return "jpg";
                case "application/x-metaverse-animation":
                    return "animation";
                case "application/x-metaverse-gesture":
                    return "gesture";
                case "application/x-metaverse-simstate":
                    return "simstate";
                default:
                    return "bin";
            }
        }

        public static string ExtensionToContentType(string extension)
        {
            switch (extension)
            {
                case "texture":
                case "jp2":
                case "j2c":
                    return "image/jp2";
                case "sound":
                case "ogg":
                    return "application/ogg";
                case "callingcard":
                    return "application/x-metaverse-callingcard";
                case "landmark":
                    return "application/x-metaverse-landmark";
                case "clothing":
                    return "application/x-metaverse-clothing";
                case "primitive":
                    return "application/x-metaverse-primitive";
                case "notecard":
                    return "application/x-metaverse-notecard";
                case "lsl":
                    return "application/x-metaverse-lsl";
                case "lso":
                    return "application/x-metaverse-lso";
                case "tga":
                    return "image/tga";
                case "bodypart":
                    return "application/x-metaverse-bodypart";
                case "wav":
                    return "audio/x-wav";
                case "jpg":
                case "jpeg":
                    return "image/jpeg";
                case "animation":
                    return "application/x-metaverse-animation";
                case "gesture":
                    return "application/x-metaverse-gesture";
                case "simstate":
                    return "application/x-metaverse-simstate";
                case "txt":
                    return "text/plain";
                case "xml":
                    return "application/xml";
                default:
                    return "application/octet-stream";
            }
        }

        #endregion SL / file extension / content-type conversions

        #region XML Serialization

        public class GeneratedReader : XmlSerializationReader
        {
            public object ReadRoot_InventoryFolderBase()
            {
                Reader.MoveToContent();
                if (Reader.LocalName != "InventoryFolderBase" || Reader.NamespaceURI != "")
                    throw CreateUnknownNodeException();
                return ReadObject_InventoryFolder(true, true);
            }

            public object ReadRoot_InventoryItemBase()
            {
                Reader.MoveToContent();
                if (Reader.LocalName != "InventoryItemBase" || Reader.NamespaceURI != "")
                    throw CreateUnknownNodeException();
                return ReadObject_InventoryItem(true, true);
            }

            public object ReadRoot_InventoryCollection()
            {
                Reader.MoveToContent();
                if (Reader.LocalName != "InventoryCollection" || Reader.NamespaceURI != "")
                    throw CreateUnknownNodeException();
                return ReadObject_InventoryCollection(true, true);
            }

            public InventoryFolderWithChildren ReadObject_InventoryFolder(bool isNullable, bool checkType)
            {
                InventoryFolderWithChildren ob = null;
                if (isNullable && ReadNull()) return null;

                if (checkType)
                {
                    System.Xml.XmlQualifiedName t = GetXsiType();
                    if (t == null)
                    { }
                    else if (t.Name != "InventoryFolderBase" || t.Namespace != "")
                        throw CreateUnknownTypeException(t);
                }

                ob = (InventoryFolderWithChildren)Activator.CreateInstance(typeof(InventoryFolderWithChildren), true);

                Reader.MoveToElement();

                while (Reader.MoveToNextAttribute())
                {
                    if (IsXmlnsAttribute(Reader.Name))
                    {
                    }
                    else
                    {
                        UnknownNode(ob);
                    }
                }

                Reader.MoveToElement();
                Reader.MoveToElement();
                if (Reader.IsEmptyElement)
                {
                    Reader.Skip();
                    return ob;
                }

                Reader.ReadStartElement();
                Reader.MoveToContent();

                bool b0 = false, b1 = false, b2 = false, b3 = false, b4 = false, b5 = false;

                while (Reader.NodeType != System.Xml.XmlNodeType.EndElement)
                {
                    if (Reader.NodeType == System.Xml.XmlNodeType.Element)
                    {
                        if (Reader.LocalName == "Owner" && Reader.NamespaceURI == "" && !b1)
                        {
                            b1 = true;
                            ob.@Owner = ReadObject_UUID(false, true);
                        }
                        else if (Reader.LocalName == "Version" && Reader.NamespaceURI == "" && !b5)
                        {
                            b5 = true;
                            string s6 = Reader.ReadElementString();
                            ob.@Version = UInt16.Parse(s6, CultureInfo.InvariantCulture);
                        }
                        else if (Reader.LocalName == "ID" && Reader.NamespaceURI == "" && !b3)
                        {
                            b3 = true;
                            ob.@ID = ReadObject_UUID(false, true);
                        }
                        else if (Reader.LocalName == "Type" && Reader.NamespaceURI == "" && !b4)
                        {
                            b4 = true;
                            string s7 = Reader.ReadElementString();
                            ob.@Type = Int16.Parse(s7, CultureInfo.InvariantCulture);
                        }
                        else if (Reader.LocalName == "Name" && Reader.NamespaceURI == "" && !b0)
                        {
                            b0 = true;
                            string s8 = Reader.ReadElementString();
                            ob.@Name = s8;
                        }
                        else if (Reader.LocalName == "ParentID" && Reader.NamespaceURI == "" && !b2)
                        {
                            b2 = true;
                            ob.@ParentID = ReadObject_UUID(false, true);
                        }
                        else
                        {
                            UnknownNode(ob);
                        }
                    }
                    else
                        UnknownNode(ob);

                    Reader.MoveToContent();
                }

                ReadEndElement();

                return ob;
            }

            public InventoryItemBase ReadObject_InventoryItem(bool isNullable, bool checkType)
            {
                InventoryItemBase ob = null;
                if (isNullable && ReadNull()) return null;

                if (checkType)
                {
                    System.Xml.XmlQualifiedName t = GetXsiType();
                    if (t == null)
                    { }
                    else if (t.Name != "InventoryItemBase" || t.Namespace != "")
                        throw CreateUnknownTypeException(t);
                }

                ob = (InventoryItemBase)Activator.CreateInstance(typeof(InventoryItemBase), true);

                Reader.MoveToElement();

                while (Reader.MoveToNextAttribute())
                {
                    if (IsXmlnsAttribute(Reader.Name))
                    {
                    }
                    else
                    {
                        UnknownNode(ob);
                    }
                }

                Reader.MoveToElement();
                Reader.MoveToElement();
                if (Reader.IsEmptyElement)
                {
                    Reader.Skip();
                    return ob;
                }

                Reader.ReadStartElement();
                Reader.MoveToContent();

                bool b9 = false, b10 = false, b11 = false, b12 = false, b13 = false, b14 = false, b15 = false, b16 = false, b17 = false, b18 = false, b19 = false, b20 = false, b21 = false, b22 = false, b23 = false, b24 = false, b25 = false, b26 = false, b27 = false, b28 = false;

                while (Reader.NodeType != System.Xml.XmlNodeType.EndElement)
                {
                    if (Reader.NodeType == System.Xml.XmlNodeType.Element)
                    {
                        if (Reader.LocalName == "GroupPermissions" && Reader.NamespaceURI == "" && !b20)
                        {
                            b20 = true;
                            string s29 = Reader.ReadElementString();
                            ob.@GroupPermissions = UInt32.Parse(s29, CultureInfo.InvariantCulture);
                        }
                        else if (Reader.LocalName == "AssetType" && Reader.NamespaceURI == "" && !b21)
                        {
                            b21 = true;
                            string s30 = Reader.ReadElementString();
                            ob.@AssetType = Int32.Parse(s30, CultureInfo.InvariantCulture);
                        }
                        else if (Reader.LocalName == "SalePrice" && Reader.NamespaceURI == "" && !b25)
                        {
                            b25 = true;
                            string s31 = Reader.ReadElementString();
                            ob.@SalePrice = Int32.Parse(s31, CultureInfo.InvariantCulture);
                        }
                        else if (Reader.LocalName == "AssetID" && Reader.NamespaceURI == "" && !b22)
                        {
                            b22 = true;
                            ob.@AssetID = ReadObject_UUID(false, true);
                        }
                        else if (Reader.LocalName == "Folder" && Reader.NamespaceURI == "" && !b11)
                        {
                            b11 = true;
                            ob.@Folder = ReadObject_UUID(false, true);
                        }
                        else if (Reader.LocalName == "Name" && Reader.NamespaceURI == "" && !b14)
                        {
                            b14 = true;
                            string s32 = Reader.ReadElementString();
                            ob.@Name = s32;
                        }
                        else if (Reader.LocalName == "NextPermissions" && Reader.NamespaceURI == "" && !b16)
                        {
                            b16 = true;
                            string s33 = Reader.ReadElementString();
                            ob.@NextPermissions = UInt32.Parse(s33, CultureInfo.InvariantCulture);
                        }
                        else if (Reader.LocalName == "BasePermissions" && Reader.NamespaceURI == "" && !b18)
                        {
                            b18 = true;
                            string s34 = Reader.ReadElementString();
                            ob.@BasePermissions = UInt32.Parse(s34, CultureInfo.InvariantCulture);
                        }
                        else if (Reader.LocalName == "ID" && Reader.NamespaceURI == "" && !b9)
                        {
                            b9 = true;
                            ob.@ID = ReadObject_UUID(false, true);
                        }
                        else if (Reader.LocalName == "Flags" && Reader.NamespaceURI == "" && !b27)
                        {
                            b27 = true;
                            string s35 = Reader.ReadElementString();
                            ob.@Flags = UInt32.Parse(s35, CultureInfo.InvariantCulture);
                        }
                        else if (Reader.LocalName == "GroupOwned" && Reader.NamespaceURI == "" && !b24)
                        {
                            b24 = true;
                            string s36 = Reader.ReadElementString();
                            ob.@GroupOwned = XmlConvert.ToBoolean(s36);
                        }
                        else if (Reader.LocalName == "InvType" && Reader.NamespaceURI == "" && !b10)
                        {
                            b10 = true;
                            string s37 = Reader.ReadElementString();
                            ob.@InvType = Int32.Parse(s37, CultureInfo.InvariantCulture);
                        }
                        else if (Reader.LocalName == "GroupID" && Reader.NamespaceURI == "" && !b23)
                        {
                            b23 = true;
                            ob.@GroupID = ReadObject_UUID(false, true);
                        }
                        else if (Reader.LocalName == "Description" && Reader.NamespaceURI == "" && !b15)
                        {
                            b15 = true;
                            string s38 = Reader.ReadElementString();
                            ob.@Description = s38;
                        }
                        else if (Reader.LocalName == "CreationDate" && Reader.NamespaceURI == "" && !b28)
                        {
                            b28 = true;
                            string s39 = Reader.ReadElementString();
                            ob.@CreationDate = Int32.Parse(s39, CultureInfo.InvariantCulture);
                        }
                        else if (Reader.LocalName == "EveryOnePermissions" && Reader.NamespaceURI == "" && !b19)
                        {
                            b19 = true;
                            string s40 = Reader.ReadElementString();
                            ob.@EveryOnePermissions = UInt32.Parse(s40, CultureInfo.InvariantCulture);
                        }
                        else if (Reader.LocalName == "Creator" && Reader.NamespaceURI == "" && !b13)
                        {
                            b13 = true;
                            ob.@CreatorId = Reader.ReadElementString();
                        }
                        else if (Reader.LocalName == "Owner" && Reader.NamespaceURI == "" && !b12)
                        {
                            b12 = true;
                            ob.@Owner = ReadObject_UUID(false, true);
                        }
                        else if (Reader.LocalName == "SaleType" && Reader.NamespaceURI == "" && !b26)
                        {
                            b26 = true;
                            string s41 = Reader.ReadElementString();
                            ob.@SaleType = byte.Parse(s41, CultureInfo.InvariantCulture);
                        }
                        else if (Reader.LocalName == "CurrentPermissions" && Reader.NamespaceURI == "" && !b17)
                        {
                            b17 = true;
                            string s42 = Reader.ReadElementString();
                            ob.@CurrentPermissions = UInt32.Parse(s42, CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            UnknownNode(ob);
                        }
                    }
                    else
                        UnknownNode(ob);

                    Reader.MoveToContent();
                }

                ReadEndElement();

                return ob;
            }

            public InventoryCollection ReadObject_InventoryCollection(bool isNullable, bool checkType)
            {
                InventoryCollection ob = null;
                if (isNullable && ReadNull()) return null;

                if (checkType)
                {
                    System.Xml.XmlQualifiedName t = GetXsiType();
                    if (t == null)
                    { }
                    else if (t.Name != "InventoryCollection" || t.Namespace != "")
                        throw CreateUnknownTypeException(t);
                }

                ob = (InventoryCollection)Activator.CreateInstance(typeof(InventoryCollection), true);

                Reader.MoveToElement();

                while (Reader.MoveToNextAttribute())
                {
                    if (IsXmlnsAttribute(Reader.Name))
                    {
                    }
                    else
                    {
                        UnknownNode(ob);
                    }
                }

                Reader.MoveToElement();
                Reader.MoveToElement();
                if (Reader.IsEmptyElement)
                {
                    Reader.Skip();
                    if (ob.@Folders == null)
                    {
                        ob.@Folders = new System.Collections.Generic.Dictionary<UUID, InventoryFolderWithChildren>();
                    }
                    if (ob.@Items == null)
                    {
                        ob.@Items = new System.Collections.Generic.Dictionary<UUID, InventoryItemBase>();
                    }
                    return ob;
                }

                Reader.ReadStartElement();
                Reader.MoveToContent();

                bool b43 = false, b44 = false, b45 = false;

                while (Reader.NodeType != System.Xml.XmlNodeType.EndElement)
                {
                    if (Reader.NodeType == System.Xml.XmlNodeType.Element)
                    {
                        if (Reader.LocalName == "UserID" && Reader.NamespaceURI == "" && !b45)
                        {
                            b45 = true;
                            ob.@UserID = ReadObject_UUID(false, true);
                        }
                        else if (Reader.LocalName == "Items" && Reader.NamespaceURI == "" && !b44)
                        {
                            System.Collections.Generic.Dictionary<UUID, InventoryItemBase> o46 = ob.@Items;
                            if (((object)o46) == null)
                            {
                                o46 = new System.Collections.Generic.Dictionary<UUID, InventoryItemBase>();
                                ob.@Items = o46;
                            }
                            if (Reader.IsEmptyElement)
                            {
                                Reader.Skip();
                            }
                            else
                            {
                                int n47 = 0;
                                Reader.ReadStartElement();
                                Reader.MoveToContent();

                                while (Reader.NodeType != System.Xml.XmlNodeType.EndElement)
                                {
                                    if (Reader.NodeType == System.Xml.XmlNodeType.Element)
                                    {
                                        if (Reader.LocalName == "InventoryItemBase" && Reader.NamespaceURI == "")
                                        {
                                            if (((object)o46) == null)
                                                throw CreateReadOnlyCollectionException("System.Collections.Generic.List<InventoryItemBase>");
                                            InventoryItemBase item = ReadObject_InventoryItem(true, true);
                                            o46.Add(item.ID, item);
                                            n47++;
                                        }
                                        else UnknownNode(null);
                                    }
                                    else UnknownNode(null);

                                    Reader.MoveToContent();
                                }
                                ReadEndElement();
                            }
                            b44 = true;
                        }
                        else if (Reader.LocalName == "Folders" && Reader.NamespaceURI == "" && !b43)
                        {
                            System.Collections.Generic.Dictionary<UUID, InventoryFolderWithChildren> o48 = ob.@Folders;
                            if (((object)o48) == null)
                            {
                                o48 = new System.Collections.Generic.Dictionary<UUID, InventoryFolderWithChildren>();
                                ob.@Folders = o48;
                            }
                            if (Reader.IsEmptyElement)
                            {
                                Reader.Skip();
                            }
                            else
                            {
                                int n49 = 0;
                                Reader.ReadStartElement();
                                Reader.MoveToContent();

                                while (Reader.NodeType != System.Xml.XmlNodeType.EndElement)
                                {
                                    if (Reader.NodeType == System.Xml.XmlNodeType.Element)
                                    {
                                        if (Reader.LocalName == "InventoryFolderBase" && Reader.NamespaceURI == "")
                                        {
                                            if (((object)o48) == null)
                                                throw CreateReadOnlyCollectionException("System.Collections.Generic.List<InventoryFolderBase>");
                                            InventoryFolderWithChildren folder = ReadObject_InventoryFolder(true, true);
                                            o48.Add(folder.ID, folder);
                                            n49++;
                                        }
                                        else UnknownNode(null);
                                    }
                                    else UnknownNode(null);

                                    Reader.MoveToContent();
                                }
                                ReadEndElement();
                            }
                            b43 = true;
                        }
                        else
                        {
                            UnknownNode(ob);
                        }
                    }
                    else
                        UnknownNode(ob);

                    Reader.MoveToContent();
                }
                if (ob.@Folders == null)
                {
                    ob.@Folders = new System.Collections.Generic.Dictionary<UUID, InventoryFolderWithChildren>();
                }
                if (ob.@Items == null)
                {
                    ob.@Items = new System.Collections.Generic.Dictionary<UUID, InventoryItemBase>();
                }

                ReadEndElement();

                return ob;
            }

            public OpenMetaverse.UUID ReadObject_UUID(bool isNullable, bool checkType)
            {
                OpenMetaverse.UUID ob = (OpenMetaverse.UUID)Activator.CreateInstance(typeof(OpenMetaverse.UUID), true);
                System.Xml.XmlQualifiedName t = GetXsiType();
                if (t == null)
                { }
                else if (t.Name != "UUID" || t.Namespace != "")
                    throw CreateUnknownTypeException(t);

                Reader.MoveToElement();

                while (Reader.MoveToNextAttribute())
                {
                    if (IsXmlnsAttribute(Reader.Name))
                    {
                    }
                    else
                    {
                        UnknownNode(ob);
                    }
                }

                Reader.MoveToElement();
                Reader.MoveToElement();
                if (Reader.IsEmptyElement)
                {
                    Reader.Skip();
                    return ob;
                }

                Reader.ReadStartElement();
                Reader.MoveToContent();

                bool b52 = false;

                while (Reader.NodeType != System.Xml.XmlNodeType.EndElement)
                {
                    if (Reader.NodeType == System.Xml.XmlNodeType.Element)
                    {
                        if (Reader.LocalName == "Guid" && Reader.NamespaceURI == "" && !b52)
                        {
                            b52 = true;
                            string s53 = Reader.ReadElementString();
                            ob.@Guid = XmlConvert.ToGuid(s53);
                        }
                        else
                        {
                            UnknownNode(ob);
                        }
                    }
                    else
                        UnknownNode(ob);

                    Reader.MoveToContent();
                }

                ReadEndElement();

                return ob;
            }

            protected override void InitCallbacks()
            {
            }

            protected override void InitIDs()
            {
            }
        }

        public class GeneratedWriter : XmlSerializationWriter
        {
            const string xmlNamespace = "http://www.w3.org/2000/xmlns/";
            //static readonly System.Reflection.MethodInfo toBinHexStringMethod = typeof(XmlConvert).GetMethod("ToBinHexString", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, null, new Type[] { typeof(byte[]) }, null);
            //static string ToBinHexString(byte[] input)
            //{
            //    return input == null ? null : (string)toBinHexStringMethod.Invoke(null, new object[] { input });
            //}
            public void WriteRoot_InventoryFolder(object o)
            {
                WriteStartDocument();
                InventoryFolderWithChildren ob = (InventoryFolderWithChildren)o;
                TopLevelElement();
                WriteObject_InventoryFolder(ob, "InventoryFolderBase", "", true, false, true);
            }

            public void WriteRoot_InventoryItem(object o)
            {
                WriteStartDocument();
                InventoryItemBase ob = (InventoryItemBase)o;
                TopLevelElement();
                WriteObject_InventoryItem(ob, "InventoryItemBase", "", true, false, true);
            }

            public void WriteRoot_InventoryCollection(object o)
            {
                WriteStartDocument();
                InventoryCollection ob = (InventoryCollection)o;
                TopLevelElement();
                WriteObject_InventoryCollection(ob, "InventoryCollection", "", true, false, true);
            }

            void WriteObject_InventoryFolder(InventoryFolderWithChildren ob, string element, string namesp, bool isNullable, bool needType, bool writeWrappingElem)
            {
                if (((object)ob) == null)
                {
                    if (isNullable)
                        WriteNullTagLiteral(element, namesp);
                    return;
                }

                System.Type type = ob.GetType();
                if (type == typeof(InventoryFolderWithChildren))
                { }
                else
                {
                    throw CreateUnknownTypeException(ob);
                }

                if (writeWrappingElem)
                {
                    WriteStartElement(element, namesp, ob);
                }

                if (needType) WriteXsiType("InventoryFolderBase", "");

                WriteElementString("Name", "", ob.@Name);
                WriteObject_UUID(ob.@Owner, "Owner", "", false, false, true);
                WriteObject_UUID(ob.@ParentID, "ParentID", "", false, false, true);
                WriteObject_UUID(ob.@ID, "ID", "", false, false, true);
                WriteElementString("Type", "", ob.@Type.ToString(CultureInfo.InvariantCulture));
                WriteElementString("Version", "", ob.@Version.ToString(CultureInfo.InvariantCulture));
                if (writeWrappingElem) WriteEndElement(ob);
            }

            void WriteObject_InventoryItem(InventoryItemBase ob, string element, string namesp, bool isNullable, bool needType, bool writeWrappingElem)
            {
                if (((object)ob) == null)
                {
                    if (isNullable)
                        WriteNullTagLiteral(element, namesp);
                    return;
                }

                System.Type type = ob.GetType();
                if (type == typeof(InventoryItemBase))
                { }
                else
                {
                    throw CreateUnknownTypeException(ob);
                }

                if (writeWrappingElem)
                {
                    WriteStartElement(element, namesp, ob);
                }

                if (needType) WriteXsiType("InventoryItemBase", "");

                WriteObject_UUID(ob.@ID, "ID", "", false, false, true);
                WriteElementString("InvType", "", ob.@InvType.ToString(CultureInfo.InvariantCulture));
                WriteObject_UUID(ob.@Folder, "Folder", "", false, false, true);
                WriteObject_UUID(ob.@Owner, "Owner", "", false, false, true);
                WriteElementString("Creator", "", ob.@CreatorId);
                WriteElementString("Name", "", ob.@Name);
                WriteElementString("Description", "", ob.@Description);
                WriteElementString("NextPermissions", "", ob.@NextPermissions.ToString(CultureInfo.InvariantCulture));
                WriteElementString("CurrentPermissions", "", ob.@CurrentPermissions.ToString(CultureInfo.InvariantCulture));
                WriteElementString("BasePermissions", "", ob.@BasePermissions.ToString(CultureInfo.InvariantCulture));
                WriteElementString("EveryOnePermissions", "", ob.@EveryOnePermissions.ToString(CultureInfo.InvariantCulture));
                WriteElementString("GroupPermissions", "", ob.@GroupPermissions.ToString(CultureInfo.InvariantCulture));
                WriteElementString("AssetType", "", ob.@AssetType.ToString(CultureInfo.InvariantCulture));
                WriteObject_UUID(ob.@AssetID, "AssetID", "", false, false, true);
                WriteObject_UUID(ob.@GroupID, "GroupID", "", false, false, true);
                WriteElementString("GroupOwned", "", (ob.@GroupOwned ? "true" : "false"));
                WriteElementString("SalePrice", "", ob.@SalePrice.ToString(CultureInfo.InvariantCulture));
                WriteElementString("SaleType", "", ob.@SaleType.ToString(CultureInfo.InvariantCulture));
                WriteElementString("Flags", "", ob.@Flags.ToString(CultureInfo.InvariantCulture));
                WriteElementString("CreationDate", "", ob.@CreationDate.ToString(CultureInfo.InvariantCulture));
                if (writeWrappingElem) WriteEndElement(ob);
            }

            void WriteObject_InventoryCollection(InventoryCollection ob, string element, string namesp, bool isNullable, bool needType, bool writeWrappingElem)
            {
                if (((object)ob) == null)
                {
                    if (isNullable)
                        WriteNullTagLiteral(element, namesp);
                    return;
                }

                System.Type type = ob.GetType();
                if (type == typeof(InventoryCollection))
                { }
                else
                {
                    throw CreateUnknownTypeException(ob);
                }

                if (writeWrappingElem)
                {
                    WriteStartElement(element, namesp, ob);
                }

                if (needType) WriteXsiType("InventoryCollection", "");

                if (ob.@Folders != null)
                {
                    WriteStartElement("Folders", "", ob.@Folders);
                    foreach (InventoryFolderWithChildren folder in ob.Folders.Values)
                    {
                        WriteObject_InventoryFolder(folder, "InventoryFolderBase", "", true, false, true);
                    }
                    WriteEndElement(ob.@Folders);
                }
                if (ob.@Items != null)
                {
                    WriteStartElement("Items", "", ob.@Items);
                    foreach (InventoryItemBase item in ob.Items.Values)
                    {
                        WriteObject_InventoryItem(item, "InventoryItemBase", "", true, false, true);
                    }
                    WriteEndElement(ob.@Items);
                }
                WriteObject_UUID(ob.@UserID, "UserID", "", false, false, true);
                if (writeWrappingElem) WriteEndElement(ob);
            }

            void WriteObject_UUID(OpenMetaverse.UUID ob, string element, string namesp, bool isNullable, bool needType, bool writeWrappingElem)
            {
                System.Type type = ob.GetType();
                if (type == typeof(OpenMetaverse.UUID))
                { }
                else
                {
                    throw CreateUnknownTypeException(ob);
                }

                if (writeWrappingElem)
                {
                    WriteStartElement(element, namesp, ob);
                }

                if (needType) WriteXsiType("UUID", "");

                WriteElementString("Guid", "", XmlConvert.ToString(ob.@Guid));
                if (writeWrappingElem) WriteEndElement(ob);
            }

            protected override void InitCallbacks()
            {
            }

        }

        public class BaseXmlSerializer : System.Xml.Serialization.XmlSerializer
        {
            protected override System.Xml.Serialization.XmlSerializationReader CreateReader()
            {
                return new GeneratedReader();
            }

            protected override System.Xml.Serialization.XmlSerializationWriter CreateWriter()
            {
                return new GeneratedWriter();
            }

            public override bool CanDeserialize(System.Xml.XmlReader xmlReader)
            {
                return true;
            }
        }

        public sealed class InventoryFolderSerializer : BaseXmlSerializer
        {
            protected override void Serialize(object obj, System.Xml.Serialization.XmlSerializationWriter writer)
            {
                ((GeneratedWriter)writer).WriteRoot_InventoryFolder(obj);
            }

            protected override object Deserialize(System.Xml.Serialization.XmlSerializationReader reader)
            {
                return ((GeneratedReader)reader).ReadRoot_InventoryFolderBase();
            }
        }

        public sealed class InventoryItemSerializer : BaseXmlSerializer
        {
            protected override void Serialize(object obj, System.Xml.Serialization.XmlSerializationWriter writer)
            {
                ((GeneratedWriter)writer).WriteRoot_InventoryItem(obj);
            }

            protected override object Deserialize(System.Xml.Serialization.XmlSerializationReader reader)
            {
                return ((GeneratedReader)reader).ReadRoot_InventoryItemBase();
            }
        }

        public sealed class InventoryCollectionSerializer : BaseXmlSerializer
        {
            protected override void Serialize(object obj, System.Xml.Serialization.XmlSerializationWriter writer)
            {
                ((GeneratedWriter)writer).WriteRoot_InventoryCollection(obj);
            }

            protected override object Deserialize(System.Xml.Serialization.XmlSerializationReader reader)
            {
                return ((GeneratedReader)reader).ReadRoot_InventoryCollection();
            }
        }

        #endregion XML Serialization
    }
}
