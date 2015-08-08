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
using System.Reflection;
using System.Text;
using System.Xml;
using System.Collections.Generic;
using System.IO;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;
using log4net;
using OpenMetaverse;

using System.Threading;

namespace OpenSim.Server.Handlers.Inventory
{
    public class XInventoryInConnector : ServiceConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IInventoryService m_InventoryService;
        private string m_ConfigName = "InventoryService";

        public XInventoryInConnector(IConfigSource config, IHttpServer server, string configName) :
                base(config, server, configName)
        {
            if (configName != String.Empty)
                m_ConfigName = configName;

            m_log.DebugFormat("[XInventoryInConnector]: Starting with config name {0}", m_ConfigName);

            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section '{0}' in config file", m_ConfigName));

            string inventoryService = serverConfig.GetString("LocalServiceModule",
                    String.Empty);

            if (inventoryService == String.Empty)
                throw new Exception("No InventoryService in config file");

            Object[] args = new Object[] { config, m_ConfigName };
            m_InventoryService =
                    ServerUtils.LoadPlugin<IInventoryService>(inventoryService, args);

            IServiceAuth auth = ServiceAuth.Create(config, m_ConfigName);

            server.AddStreamHandler(new XInventoryConnectorPostHandler(m_InventoryService, auth));
        }
    }

    public class XInventoryConnectorPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IInventoryService m_InventoryService;

        public XInventoryConnectorPostHandler(IInventoryService service, IServiceAuth auth) :
                base("POST", "/xinventory", auth)
        {
            m_InventoryService = service;
        }

        protected override byte[] ProcessRequest(string path, Stream requestData,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            //m_log.DebugFormat("[XXX]: query String: {0}", body);

            try
            {
                Dictionary<string, object> request =
                        ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("METHOD"))
                    return FailureResult();

                string method = request["METHOD"].ToString();
                request.Remove("METHOD");

                switch (method)
                {
                    case "CREATEUSERINVENTORY":
                        return HandleCreateUserInventory(request);
                    case "GETINVENTORYSKELETON":
                        return HandleGetInventorySkeleton(request);
                    case "GETROOTFOLDER":
                        return HandleGetRootFolder(request);
                    case "GETFOLDERFORTYPE":
                        return HandleGetFolderForType(request);
                    case "GETFOLDERCONTENT":
                        return HandleGetFolderContent(request);
                    case "GETMULTIPLEFOLDERSCONTENT":
                        return HandleGetMultipleFoldersContent(request);
                    case "GETFOLDERITEMS":
                        return HandleGetFolderItems(request);
                    case "ADDFOLDER":
                        return HandleAddFolder(request);
                    case "UPDATEFOLDER":
                        return HandleUpdateFolder(request);
                    case "MOVEFOLDER":
                        return HandleMoveFolder(request);
                    case "DELETEFOLDERS":
                        return HandleDeleteFolders(request);
                    case "PURGEFOLDER":
                        return HandlePurgeFolder(request);
                    case "ADDITEM":
                        return HandleAddItem(request);
                    case "UPDATEITEM":
                        return HandleUpdateItem(request);
                    case "MOVEITEMS":
                        return HandleMoveItems(request);
                    case "DELETEITEMS":
                        return HandleDeleteItems(request);
                    case "GETITEM":
                        return HandleGetItem(request);
                    case "GETMULTIPLEITEMS":
                        return HandleGetMultipleItems(request);
                    case "GETFOLDER":
                        return HandleGetFolder(request);
                    case "GETACTIVEGESTURES":
                        return HandleGetActiveGestures(request);
                    case "GETASSETPERMISSIONS":
                        return HandleGetAssetPermissions(request);
                }
                m_log.DebugFormat("[XINVENTORY HANDLER]: unknown method request: {0}", method);
            }
            catch (Exception e)
            {
                m_log.Error(string.Format("[XINVENTORY HANDLER]: Exception {0} ", e.Message), e);
            }

            return FailureResult();
        }

        private byte[] FailureResult()
        {
            return BoolResult(false);
        }

        private byte[] SuccessResult()
        {
            return BoolResult(true);
        }

        private byte[] BoolResult(bool value)
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "RESULT", "");
            result.AppendChild(doc.CreateTextNode(value.ToString()));

            rootElement.AppendChild(result);

            return Util.DocToBytes(doc);
        }

        byte[] HandleCreateUserInventory(Dictionary<string,object> request)
        {
            Dictionary<string,object> result = new Dictionary<string,object>();

            if (!request.ContainsKey("PRINCIPAL"))
                return FailureResult();

            if (m_InventoryService.CreateUserInventory(new UUID(request["PRINCIPAL"].ToString())))
                result["RESULT"] = "True";
            else
                result["RESULT"] = "False";

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetInventorySkeleton(Dictionary<string,object> request)
        {
            Dictionary<string,object> result = new Dictionary<string,object>();

            if (!request.ContainsKey("PRINCIPAL"))
                return FailureResult();


            List<InventoryFolderBase> folders = m_InventoryService.GetInventorySkeleton(new UUID(request["PRINCIPAL"].ToString()));

            Dictionary<string, object> sfolders = new Dictionary<string, object>();
            if (folders != null)
            {
                int i = 0;
                foreach (InventoryFolderBase f in folders)
                {
                    sfolders["folder_" + i.ToString()] = EncodeFolder(f);
                    i++;
                }
            }
            result["FOLDERS"] = sfolders;

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetRootFolder(Dictionary<string,object> request)
        {
            Dictionary<string,object> result = new Dictionary<string,object>();

            UUID principal = UUID.Zero;
            UUID.TryParse(request["PRINCIPAL"].ToString(), out principal);
            InventoryFolderBase rfolder = m_InventoryService.GetRootFolder(principal);
            if (rfolder != null)
                result["folder"] = EncodeFolder(rfolder);

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetFolderForType(Dictionary<string,object> request)
        {
            Dictionary<string,object> result = new Dictionary<string,object>();
            UUID principal = UUID.Zero;
            UUID.TryParse(request["PRINCIPAL"].ToString(), out principal);
            int type = 0;
            Int32.TryParse(request["TYPE"].ToString(), out type);
            InventoryFolderBase folder = m_InventoryService.GetFolderForType(principal, (FolderType)type);
            if (folder != null)
                result["folder"] = EncodeFolder(folder);

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetFolderContent(Dictionary<string,object> request)
        {
            Dictionary<string,object> result = new Dictionary<string,object>();
            UUID principal = UUID.Zero;
            UUID.TryParse(request["PRINCIPAL"].ToString(), out principal);
            UUID folderID = UUID.Zero;
            UUID.TryParse(request["FOLDER"].ToString(), out folderID);

            InventoryCollection icoll = m_InventoryService.GetFolderContent(principal, folderID);
            if (icoll != null)
            {
                result["FID"] = icoll.FolderID.ToString();
                result["VERSION"] = icoll.Version.ToString();
                Dictionary<string, object> folders = new Dictionary<string, object>();
                int i = 0; 
                if (icoll.Folders != null)
                {
                    foreach (InventoryFolderBase f in icoll.Folders)
                    {
                        folders["folder_" + i.ToString()] = EncodeFolder(f);
                        i++;
                    }
                    result["FOLDERS"] = folders;
                }
                if (icoll.Items != null)
                {
                    i = 0;
                    Dictionary<string, object> items = new Dictionary<string, object>();
                    foreach (InventoryItemBase it in icoll.Items)
                    {
                        items["item_" + i.ToString()] = EncodeItem(it);
                        i++;
                    }
                    result["ITEMS"] = items;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetMultipleFoldersContent(Dictionary<string, object> request)
        {
            Dictionary<string, object> resultSet = new Dictionary<string, object>();
            UUID principal = UUID.Zero;
            UUID.TryParse(request["PRINCIPAL"].ToString(), out principal);
            string folderIDstr = request["FOLDERS"].ToString();
            int count = 0;
            Int32.TryParse(request["COUNT"].ToString(), out count);

            UUID[] fids = new UUID[count];
            string[] uuids = folderIDstr.Split(',');
            int i = 0;
            foreach (string id in uuids)
            {
                UUID fid = UUID.Zero;
                if (UUID.TryParse(id, out fid))
                    fids[i] = fid;
                i += 1;
            }

            count = 0;
            InventoryCollection[] icollList = m_InventoryService.GetMultipleFoldersContent(principal, fids);
            if (icollList != null && icollList.Length > 0)
            {
                foreach (InventoryCollection icoll in icollList)
                {
                    Dictionary<string, object> result = new Dictionary<string, object>();
                    result["FID"] = icoll.FolderID.ToString();
                    result["VERSION"] = icoll.Version.ToString();
                    result["OWNER"] = icoll.OwnerID.ToString();
                    Dictionary<string, object> folders = new Dictionary<string, object>();
                    i = 0;
                    if (icoll.Folders != null)
                    {
                        foreach (InventoryFolderBase f in icoll.Folders)
                        {
                            folders["folder_" + i.ToString()] = EncodeFolder(f);
                            i++;
                        }
                        result["FOLDERS"] = folders;
                    }
                    i = 0;
                    if (icoll.Items != null)
                    {
                        Dictionary<string, object> items = new Dictionary<string, object>();
                        foreach (InventoryItemBase it in icoll.Items)
                        {
                            items["item_" + i.ToString()] = EncodeItem(it);
                            i++;
                        }
                        result["ITEMS"] = items;
                    }

                    resultSet["F_" + fids[count++]] = result;
                    //m_log.DebugFormat("[XXX]: Sending {0} {1}", fids[count-1], icoll.FolderID);
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(resultSet);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetFolderItems(Dictionary<string, object> request)
        {
            Dictionary<string,object> result = new Dictionary<string,object>();
            UUID principal = UUID.Zero;
            UUID.TryParse(request["PRINCIPAL"].ToString(), out principal);
            UUID folderID = UUID.Zero;
            UUID.TryParse(request["FOLDER"].ToString(), out folderID);

            List<InventoryItemBase> items = m_InventoryService.GetFolderItems(principal, folderID);
            Dictionary<string, object> sitems = new Dictionary<string, object>();

            if (items != null)
            {
                int i = 0;
                foreach (InventoryItemBase item in items)
                {
                    sitems["item_" + i.ToString()] = EncodeItem(item);
                    i++;
                }
            }
            result["ITEMS"] = sitems;
            
            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleAddFolder(Dictionary<string,object> request)
        {
            InventoryFolderBase folder = BuildFolder(request);

            if (m_InventoryService.AddFolder(folder))
                return SuccessResult();
            else
                return FailureResult();
        }

        byte[] HandleUpdateFolder(Dictionary<string,object> request)
        {
            InventoryFolderBase folder = BuildFolder(request);

            if (m_InventoryService.UpdateFolder(folder))
                return SuccessResult();
            else
                return FailureResult();
        }

        byte[] HandleMoveFolder(Dictionary<string,object> request)
        {
            UUID parentID = UUID.Zero;
            UUID.TryParse(request["ParentID"].ToString(), out parentID);
            UUID folderID = UUID.Zero;
            UUID.TryParse(request["ID"].ToString(), out folderID);
            UUID principal = UUID.Zero;
            UUID.TryParse(request["PRINCIPAL"].ToString(), out principal);

            InventoryFolderBase folder = new InventoryFolderBase(folderID, "", principal, parentID);
            if (m_InventoryService.MoveFolder(folder))
                return SuccessResult();
            else
                return FailureResult();

        }

        byte[] HandleDeleteFolders(Dictionary<string,object> request)
        {
            UUID principal = UUID.Zero;
            UUID.TryParse(request["PRINCIPAL"].ToString(), out principal);
            List<string> slist = (List<string>)request["FOLDERS"];
            List<UUID> uuids = new List<UUID>();
            foreach (string s in slist)
            {
                UUID u = UUID.Zero;
                if (UUID.TryParse(s, out u))
                    uuids.Add(u);
            }

            if (m_InventoryService.DeleteFolders(principal, uuids))
                return SuccessResult();
            else
                return
                    FailureResult();
        }

        byte[] HandlePurgeFolder(Dictionary<string,object> request)
        {
            UUID folderID = UUID.Zero;
            UUID.TryParse(request["ID"].ToString(), out folderID);

            InventoryFolderBase folder = new InventoryFolderBase(folderID);
            if (m_InventoryService.PurgeFolder(folder))
                return SuccessResult();
            else
                return FailureResult();
        }

        byte[] HandleAddItem(Dictionary<string,object> request)
        {
            InventoryItemBase item = BuildItem(request);

            if (m_InventoryService.AddItem(item))
                return SuccessResult();
            else
                return FailureResult();
        }

        byte[] HandleUpdateItem(Dictionary<string,object> request)
        {
            InventoryItemBase item = BuildItem(request);

            if (m_InventoryService.UpdateItem(item))
                return SuccessResult();
            else
                return FailureResult();
        }

        byte[] HandleMoveItems(Dictionary<string,object> request)
        {
            List<string> idlist = (List<string>)request["IDLIST"];
            List<string> destlist = (List<string>)request["DESTLIST"];
            UUID principal = UUID.Zero;
            UUID.TryParse(request["PRINCIPAL"].ToString(), out principal);

            List<InventoryItemBase> items = new List<InventoryItemBase>();
            int n = 0;
            try
            {
                foreach (string s in idlist)
                {
                    UUID u = UUID.Zero;
                    if (UUID.TryParse(s, out u))
                    {
                        UUID fid = UUID.Zero;
                        if (UUID.TryParse(destlist[n++], out fid))
                        {
                            InventoryItemBase item = new InventoryItemBase(u, principal);
                            item.Folder = fid;
                            items.Add(item);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[XINVENTORY IN CONNECTOR]: Exception in HandleMoveItems: {0}", e.Message);
                return FailureResult();
            }

            if (m_InventoryService.MoveItems(principal, items))
                return SuccessResult();
            else
                return FailureResult();
        }

        byte[] HandleDeleteItems(Dictionary<string,object> request)
        {
            UUID principal = UUID.Zero;
            UUID.TryParse(request["PRINCIPAL"].ToString(), out principal);
            List<string> slist = (List<string>)request["ITEMS"];
            List<UUID> uuids = new List<UUID>();
            foreach (string s in slist)
            {
                UUID u = UUID.Zero;
                if (UUID.TryParse(s, out u))
                    uuids.Add(u);
            }

            if (m_InventoryService.DeleteItems(principal, uuids))
                return SuccessResult();
            else
                return
                    FailureResult();
        }

        byte[] HandleGetItem(Dictionary<string,object> request)
        {
            Dictionary<string,object> result = new Dictionary<string,object>();
            UUID id = UUID.Zero;
            UUID.TryParse(request["ID"].ToString(), out id);

            InventoryItemBase item = new InventoryItemBase(id);
            item = m_InventoryService.GetItem(item);
            if (item != null)
                result["item"] = EncodeItem(item);

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetMultipleItems(Dictionary<string, object> request)
        {
            Dictionary<string, object> resultSet = new Dictionary<string, object>();
            UUID principal = UUID.Zero;
            UUID.TryParse(request["PRINCIPAL"].ToString(), out principal);
            string itemIDstr = request["ITEMS"].ToString();
            int count = 0;
            Int32.TryParse(request["COUNT"].ToString(), out count);

            UUID[] fids = new UUID[count];
            string[] uuids = itemIDstr.Split(',');
            int i = 0;
            foreach (string id in uuids)
            {
                UUID fid = UUID.Zero;
                if (UUID.TryParse(id, out fid))
                    fids[i] = fid;
                i += 1;
            }

            InventoryItemBase[] itemsList = m_InventoryService.GetMultipleItems(principal, fids);
            if (itemsList != null && itemsList.Length > 0)
            {
                count = 0;
                foreach (InventoryItemBase item in itemsList)
                    resultSet["item_" + count++] = (item == null) ? (object)"NULL" : EncodeItem(item);
            }

            string xmlString = ServerUtils.BuildXmlResponse(resultSet);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetFolder(Dictionary<string,object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            UUID id = UUID.Zero;
            UUID.TryParse(request["ID"].ToString(), out id);

            InventoryFolderBase folder = new InventoryFolderBase(id);
            folder = m_InventoryService.GetFolder(folder);
            if (folder != null)
                result["folder"] = EncodeFolder(folder);

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetActiveGestures(Dictionary<string,object> request)
        {
            Dictionary<string,object> result = new Dictionary<string,object>();
            UUID principal = UUID.Zero;
            UUID.TryParse(request["PRINCIPAL"].ToString(), out principal);

            List<InventoryItemBase> gestures = m_InventoryService.GetActiveGestures(principal);
            Dictionary<string, object> items = new Dictionary<string, object>();
            if (gestures != null)
            {
                int i = 0;
                foreach (InventoryItemBase item in gestures)
                {
                    items["item_" + i.ToString()] = EncodeItem(item);
                    i++;
                }
            }
            result["ITEMS"] = items;

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetAssetPermissions(Dictionary<string,object> request)
        {
            Dictionary<string,object> result = new Dictionary<string,object>();
            UUID principal = UUID.Zero;
            UUID.TryParse(request["PRINCIPAL"].ToString(), out principal);
            UUID assetID = UUID.Zero;
            UUID.TryParse(request["ASSET"].ToString(), out assetID);

            int perms = m_InventoryService.GetAssetPermissions(principal, assetID);

            result["RESULT"] = perms.ToString();
            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        private Dictionary<string, object> EncodeFolder(InventoryFolderBase f)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();

            ret["ParentID"] = f.ParentID.ToString();
            ret["Type"] = f.Type.ToString();
            ret["Version"] = f.Version.ToString();
            ret["Name"] = f.Name;
            ret["Owner"] = f.Owner.ToString();
            ret["ID"] = f.ID.ToString();

            return ret;
        }

        private Dictionary<string, object> EncodeItem(InventoryItemBase item)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();

            ret["AssetID"] = item.AssetID.ToString();
            ret["AssetType"] = item.AssetType.ToString();
            ret["BasePermissions"] = item.BasePermissions.ToString();
            ret["CreationDate"] = item.CreationDate.ToString();
            if (item.CreatorId != null)
                ret["CreatorId"] = item.CreatorId.ToString();
            else
                ret["CreatorId"] = String.Empty;
            if (item.CreatorData != null)
                ret["CreatorData"] = item.CreatorData;
            else
                ret["CreatorData"] = String.Empty;
            ret["CurrentPermissions"] = item.CurrentPermissions.ToString();
            ret["Description"] = item.Description.ToString();
            ret["EveryOnePermissions"] = item.EveryOnePermissions.ToString();
            ret["Flags"] = item.Flags.ToString();
            ret["Folder"] = item.Folder.ToString();
            ret["GroupID"] = item.GroupID.ToString();
            ret["GroupOwned"] = item.GroupOwned.ToString();
            ret["GroupPermissions"] = item.GroupPermissions.ToString();
            ret["ID"] = item.ID.ToString();
            ret["InvType"] = item.InvType.ToString();
            ret["Name"] = item.Name.ToString();
            ret["NextPermissions"] = item.NextPermissions.ToString();
            ret["Owner"] = item.Owner.ToString();
            ret["SalePrice"] = item.SalePrice.ToString();
            ret["SaleType"] = item.SaleType.ToString();

            return ret;
        }

        private InventoryFolderBase BuildFolder(Dictionary<string,object> data)
        {
            InventoryFolderBase folder = new InventoryFolderBase();

            folder.ParentID =  new UUID(data["ParentID"].ToString());
            folder.Type = short.Parse(data["Type"].ToString());
            folder.Version = ushort.Parse(data["Version"].ToString());
            folder.Name = data["Name"].ToString();
            folder.Owner =  new UUID(data["Owner"].ToString());
            folder.ID = new UUID(data["ID"].ToString());

            return folder;
        }

        private InventoryItemBase BuildItem(Dictionary<string,object> data)
        {
            InventoryItemBase item = new InventoryItemBase();

            item.AssetID = new UUID(data["AssetID"].ToString());
            item.AssetType = int.Parse(data["AssetType"].ToString());
            item.Name = data["Name"].ToString();
            item.Owner = new UUID(data["Owner"].ToString());
            item.ID = new UUID(data["ID"].ToString());
            item.InvType = int.Parse(data["InvType"].ToString());
            item.Folder = new UUID(data["Folder"].ToString());
            item.CreatorId = data["CreatorId"].ToString();
            item.CreatorData = data["CreatorData"].ToString();
            item.Description = data["Description"].ToString();
            item.NextPermissions = uint.Parse(data["NextPermissions"].ToString());
            item.CurrentPermissions = uint.Parse(data["CurrentPermissions"].ToString());
            item.BasePermissions = uint.Parse(data["BasePermissions"].ToString());
            item.EveryOnePermissions = uint.Parse(data["EveryOnePermissions"].ToString());
            item.GroupPermissions = uint.Parse(data["GroupPermissions"].ToString());
            item.GroupID = new UUID(data["GroupID"].ToString());
            item.GroupOwned = bool.Parse(data["GroupOwned"].ToString());
            item.SalePrice = int.Parse(data["SalePrice"].ToString());
            item.SaleType = byte.Parse(data["SaleType"].ToString());
            item.Flags = uint.Parse(data["Flags"].ToString());
            item.CreationDate = int.Parse(data["CreationDate"].ToString());

            return item;
        }

    }
}
