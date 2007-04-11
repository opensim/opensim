using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using OpenSim.world;
using OpenSim.UserServer;
using OpenSim.Servers;
using OpenSim.Assets;
using OpenSim.Framework.Inventory;
using libsecondlife;
using OpenSim.RegionServer.world.scripting;
using Avatar=libsecondlife.Avatar;
using OpenSim.RegionServer.world.scripting.Scripts;

namespace OpenSim.CAPS
{
    public class AdminWebFront
    {
        private string AdminPage;
        private string NewAccountForm;
        private string LoginForm;
        private string passWord = "Admin";
        private World m_world;
        private LoginServer _userServer;
        private InventoryCache _inventoryCache;

        public AdminWebFront(string password, World world, InventoryCache inventoryCache, LoginServer userserver)
        {
            _inventoryCache = inventoryCache;
            _userServer = userserver;
            m_world = world;
            passWord = password;
            LoadAdminPage();
        }

        public void LoadMethods( BaseHttpServer server )
        {
            server.AddRestHandler("GET", "/Admin", GetAdminPage);
            server.AddRestHandler("GET", "/Admin/Welcome", GetWelcomePage);
            server.AddRestHandler("GET", "/Admin/Accounts", GetAccountsPage );
            server.AddRestHandler("GET", "/Admin/Clients", GetConnectedClientsPage);
            server.AddRestHandler("GET", "/Admin/Entities", GetEntitiesPage);
            server.AddRestHandler("GET", "/Admin/Scripts", GetScriptsPage);
            server.AddRestHandler("GET", "/Admin/AddTestScript", AddTestScript );
            server.AddRestHandler("GET", "/ClientInventory", GetClientsInventory);

            server.AddRestHandler("POST", "/Admin/NewAccount", PostNewAccount );
            server.AddRestHandler("POST", "/Admin/Login", PostLogin );      
        }

        private string GetWelcomePage(string request, string path, string param)
        {
            string responseString;
            responseString = "Welcome to the OpenSim Admin Page";
            responseString += "<br><br><br> " + LoginForm;
            return responseString;
        }

        private string PostLogin(string requestBody, string path, string param)
        {
            string responseString;
// Console.WriteLine(requestBody);
            if (requestBody == passWord)
            {
                responseString = "<p> Login Successful </p>";
            }
            else
            {
                responseString = "<p> Password Error </p>";
                responseString += "<p> Please Login with the correct password </p>";
                responseString += "<br><br> " + LoginForm;
            }
            return responseString;
        }

        private string PostNewAccount(string requestBody, string path, string param)
        {
            string responseString;
            string firstName = "";
            string secondName = "";
            string userPasswd = "";
            string[] comp;
            string[] passw;
            string[] line;
            string delimStr = "&";
            char[] delimiter = delimStr.ToCharArray();
            string delimStr2 = "=";
            char[] delimiter2 = delimStr2.ToCharArray();

            //Console.WriteLine(requestBody);
            comp = requestBody.Split(delimiter);
            passw = comp[3].Split(delimiter2);
            if (passw[1] == passWord)  // check admin password is correct
            {
                                
                line = comp[0].Split(delimiter2); //split firstname
                if (line.Length > 1)
                {
                    firstName = line[1];
                }
                line = comp[1].Split(delimiter2); //split secondname
                if (line.Length > 1)
                {
                    secondName = line[1];
                }
                line = comp[2].Split(delimiter2); //split user password
                if (line.Length > 1)
                {
                    userPasswd = line[1];
                }
                if (this._userServer != null)
                {
                    this._userServer.CreateUserAccount(firstName, secondName, userPasswd);
                }
                responseString = "<p> New Account created </p>";
            }
            else
            {
                responseString = "<p> Admin password is incorrect, please login with the correct password</p>";
                responseString += "<br><br>" + LoginForm;
            }
            return responseString;
        }

        private string GetConnectedClientsPage(string request, string path, string param)
        {
            string responseString;
            responseString = " <p> Listing connected Clients </p>";
            OpenSim.world.Avatar TempAv;
            foreach (libsecondlife.LLUUID UUID in m_world.Entities.Keys)
            {
                if (m_world.Entities[UUID].ToString() == "OpenSim.world.Avatar")
                {
                    TempAv = (OpenSim.world.Avatar)m_world.Entities[UUID];
                    responseString += "<p> Client: ";
                    responseString += TempAv.firstname + " , " + TempAv.lastname + " , <A HREF=\"javascript:loadXMLDoc('ClientInventory/" + UUID.ToString() + "')\">" + UUID + "</A> , " + TempAv.ControllingClient.SessionID + " ,  " + TempAv.ControllingClient.CircuitCode + " , " + TempAv.ControllingClient.userEP.ToString();
                    responseString += "</p>";
                }
            }
            return responseString;
        }

        private string AddTestScript(string request, string path, string param)
        {
            int index = path.LastIndexOf('/');

            string lluidStr = path.Substring(index+1);
            
            LLUUID id;
            
            if( LLUUID.TryParse( lluidStr, out id ) )
            {
                // This is just here for concept purposes... Remove!
                m_world.AddScript( m_world.Entities[id], new FollowRandomAvatar());
                return String.Format("Added new script to object [{0}]", id);
            }
            else
            {
                return String.Format("Couldn't parse [{0}]", lluidStr );
            }
        }

        private string GetScriptsPage(string request, string path, string param)
        {
            return String.Empty;
        }

        private string GetEntitiesPage(string request, string path, string param)
        {
            string responseString;
            responseString = " <p> Listing current entities</p><ul>";
            
            foreach (Entity entity in m_world.Entities.Values)
            {
                string testScriptLink = "javascript:loadXMLDoc('Admin/AddTestScript/" + entity.uuid.ToString() + "');";
                responseString += String.Format( "<li>[{0}] \"{1}\" @ {2} <a href=\"{3}\">add test script</a></li>", entity.uuid, entity.Name, entity.Pos, testScriptLink  );
            }
            responseString += "</ul>";
            return responseString;
        }

        private string GetClientsInventory(string request, string path, string param)
        {
            string[] line;
            string delimStr = "/";
            char[] delimiter = delimStr.ToCharArray();
            string responseString;
            responseString = " <p> Listing Inventory </p>";

            line = path.Split(delimiter);
            if (line.Length > 2)
            {
                if (line[1] == "ClientInventory")
                {
                    AgentInventory inven = this._inventoryCache.GetAgentsInventory(new libsecondlife.LLUUID(line[2]));
                    responseString += " <p> Client: " + inven.AgentID.ToStringHyphenated() +" </p>";
                    if (inven != null)
                    {
                        foreach (InventoryItem item in inven.InventoryItems.Values)
                        {
                            responseString += "<p> InventoryItem: ";
                            responseString +=  item.Name +" , "+ item.ItemID +" , "+ item.Type +" , "+ item.FolderID +" , "+ item.AssetID +" , "+ item.Description ; 
                            responseString += "</p>";
                        }
                    }
                }
            }
            return responseString;
        }

        private string GetCachedAssets(string request, string path, string param)
        {
            return "";
        }

        private string GetAccountsPage(string request, string path, string param)
        {
            string responseString;
            responseString = "<p> Account management </p>";
            responseString += "<br> ";
            responseString += "<p> Create New Account </p>";
            responseString += NewAccountForm;
            return responseString;
        }

        private string GetAdminPage(string request, string path, string param)
        {
            return AdminPage;
        }

        private void LoadAdminPage()
        {
            try
            {
                StreamReader SR;
                
                SR = File.OpenText("testadmin.htm");
                AdminPage = SR.ReadToEnd();                
                SR.Close();

                SR = File.OpenText("newaccountform.htm");
                NewAccountForm = SR.ReadToEnd();
                SR.Close();

                SR = File.OpenText("login.htm");
                LoginForm = SR.ReadToEnd();
                SR.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }

    }
}
