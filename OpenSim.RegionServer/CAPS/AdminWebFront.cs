using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace OpenSim.CAPS
{
    public class AdminWebFront : IRestHandler
    {
        private string AdminPage;
        private string NewAccountForm;
        private string LoginForm;
        private string passWord = "Admin";

        public AdminWebFront(string password)
        {
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
                            foreach (libsecondlife.LLUUID UUID in OpenSimRoot.Instance.LocalWorld.Entities.Keys)
                            {
                                if (OpenSimRoot.Instance.LocalWorld.Entities[UUID].ToString() == "OpenSim.world.Avatar")
                                {
                                    TempAv = (OpenSim.world.Avatar)OpenSimRoot.Instance.LocalWorld.Entities[UUID];
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
                            string[] comp = new string[10];
                            string[] passw = new string[3];
                            string delimStr = "&";
                            char[] delimiter = delimStr.ToCharArray();
                            string delimStr2 = "=";
                            char[] delimiter2 = delimStr2.ToCharArray();

                            //Console.WriteLine(requestBody);
                            comp = requestBody.Split(delimiter);
                            passw = comp[3].Split(delimiter2);
                            if (passw[1] == passWord)
                            {
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
