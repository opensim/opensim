using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using OpenSim.world;
using OpenSim.UserServer;

namespace OpenSim.CAPS
{
    public class AdminWebFront : IRestHandler
    {
        private string AdminPage;
        private string NewAccountForm;
        private string LoginForm;
        private string passWord = "Admin";
        private World m_world;
        private LoginServer _userServer;

        public AdminWebFront(string password, World world, LoginServer userserver)
        {
            _userServer = userserver;
            m_world = world;
            passWord = password;
            LoadAdminPage();
        }

        public string HandleREST(string requestBody, string requestURL, string requestMethod)
        {
            string responseString = "";
            try
            {
                switch (requestURL)
                {
                    case "/Admin":
                        if (requestMethod == "GET")
                        {
                            responseString = AdminPage;
                        }
                        break;
                    case "/Admin/Accounts":
                        if (requestMethod == "GET")
                        {
                            responseString = "<p> Account management </p>";
                            responseString += "<br> ";
                            responseString += "<p> Create New Account </p>";
                            responseString += NewAccountForm;
                        }
                        break;
                    case "/Admin/Clients":
                        if (requestMethod == "GET")
                        {
                            responseString = " <p> Listing connected Clients </p>";
                            OpenSim.world.Avatar TempAv;
                            foreach (libsecondlife.LLUUID UUID in m_world.Entities.Keys)
                            {
                                if (m_world.Entities[UUID].ToString() == "OpenSim.world.Avatar")
                                {
                                    TempAv = (OpenSim.world.Avatar)m_world.Entities[UUID];
                                    responseString += "<p>";
                                    responseString += String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}", TempAv.firstname, TempAv.lastname, UUID, TempAv.ControllingClient.SessionID, TempAv.ControllingClient.CircuitCode, TempAv.ControllingClient.userEP.ToString());
                                    responseString += "</p>";
                                }
                            }
                        }
                        break;
                    case "/Admin/NewAccount":
                        if (requestMethod == "POST")
                        {
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
                            if (passw[1] == passWord)
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
                        }
                        break;
                    case "/Admin/Login":
                        if (requestMethod == "POST")
                        {
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
                        }
                        break;
                    case "/Admin/Welcome":
                        if (requestMethod == "GET")
                        {
                            responseString = "Welcome to the OpenSim Admin Page";
                            responseString += "<br><br><br> " + LoginForm;

                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return responseString;
        }

        private void LoadAdminPage()
        {
            try
            {
                StreamReader SR;
                string lines;
                AdminPage = "";
                NewAccountForm = "";
                LoginForm = "";
                SR = File.OpenText("testadmin.htm");

                while (!SR.EndOfStream)
                {
                    lines = SR.ReadLine();
                    AdminPage += lines + "\n";

                }
                SR.Close();

                SR = File.OpenText("newaccountform.htm");

                while (!SR.EndOfStream)
                {
                    lines = SR.ReadLine();
                    NewAccountForm += lines + "\n";

                }
                SR.Close();

                SR = File.OpenText("login.htm");

                while (!SR.EndOfStream)
                {
                    lines = SR.ReadLine();
                    LoginForm += lines + "\n";

                }
                SR.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }

    }
}
