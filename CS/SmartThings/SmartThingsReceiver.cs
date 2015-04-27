using System;
using System.Text;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;    // For access to HTTPS
using System.Text.RegularExpressions;
using Crestron.SimplSharp.CrestronIO;               // For FileIO
using Crestron.SimplSharp.CrestronXml;              // For XML parsing
#pragma warning disable 0168

namespace SmartThingsListener
{
    public delegate void SerialChangedEventHandler(SerialChangeEventArgs e);


    public class SerialChangeEventArgs : EventArgs
    {
        public string deviceID { get; set; }
        public string deviceValue { get; set; }

        public SerialChangeEventArgs()
        {
        }

        public SerialChangeEventArgs(string DeviceID, string DeviceValue)
        {
            this.deviceID = DeviceID;
            this.deviceValue = DeviceValue;
        }
    }  

    public static class SignalChangeEvents
    {
        public static event SerialChangedEventHandler onSerialValueChange;


        public static void SerialValueChange(string DeviceID, string DeviceValue)
        {
            SignalChangeEvents.onSerialValueChange(new SerialChangeEventArgs(DeviceID, DeviceValue));
        }
    }


    public class SmartThingsReceiver
    {
        HttpServer Server;

        public static string ClientID = "";
        public static string ClientSecret = "";
        public static int Port = 0;
        public static string AuthCode = "";
        public static string IP = "";
        public static string AccessToken = "";
        public static string InstallationURL = "";
        public static int Authorized = 0;


        /// <summary>
        /// SIMPL+ can only execute the default constructor. If you have variables that require initialization, please
        /// use an Initialize method
        /// </summary>
        public SmartThingsReceiver()
        {
            
        }

        public class Device
        {
            public string id { get; set; }
            public string label { get; set; }
            public string type { get; set; }
        }

        public void Authenticate()
        {
            HttpClient client = new HttpClient();
            //client.Verbose = false;
            //Testing
            //client.AllowAutoRedirect = true;

            HttpClientRequest request = new HttpClientRequest();
            HttpClientResponse response;
            String url = "http://checkip.dyndns.org";
            request.Url.Parse(url);
            request.RequestType = Crestron.SimplSharp.Net.Http.RequestType.Get;
            try
            {
                response = client.Dispatch(request);
            }
            catch(Exception e)
            {
                ErrorLog.Error("**Failed To Retrieve External IP Address**\n");
                ErrorLog.Error("Verify Internet Connection and Try Again\n");
                response = null;
                return;
            }

            string pattern = @".*?\: (.*?)<\/body>";
            var r = new Regex(pattern, RegexOptions.IgnoreCase);
            if (response.ContentString != null)
            {
                var match = r.Match(response.ContentString);
                if (match.Success)
                {
                    SmartThingsReceiver.IP = match.Groups[1].Value;
                    ErrorLog.Notice("Please Copy and Paste the Following URL into your browser to continue\n");
                    ErrorLog.Notice("https://graph.api.smartthings.com/oauth/authorize?response_type=code&client_id=" + ClientID + "&redirect_uri=http://" + IP + ":" + Port.ToString() + "&scope=app");
                }
                else
                    ErrorLog.Notice("{0}\n", response.ContentString);
            }
        }

        public void AuthenticateStep2()
        {
            HttpsClient client = new HttpsClient();
            //client.Verbose = true;
            //Testing
            client.AllowAutoRedirect = true;
            client.PeerVerification = false;
            client.HostVerification = false;
            client.KeepAlive = true;
            HttpsClientRequest request = new HttpsClientRequest();
            HttpsClientResponse response;

            //Get AccessToken
            String url = "https://graph.api.smartthings.com/oauth/token?grant_type=authorization_code&client_id=" + ClientID + "&client_secret=" + ClientSecret + "&redirect_uri=http://" + IP + ":" + Port.ToString() + "&scope=app&code=" + AuthCode;
            request.Url.Parse(url);
            request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
            try
            {
                response = client.Dispatch(request);
                //ErrorLog.Notice("Response = {0}\n", response.ContentString);
                string pattern = ".*?\"([a-z0-9-]+)\"";
                var r = new Regex(pattern, RegexOptions.IgnoreCase);
                if (response.ContentString != null)
                {
                    var match = r.Match(response.ContentString);
                    if (match.Success)
                    {
                        SmartThingsReceiver.AccessToken = match.Groups[1].Value;
                        //ErrorLog.Notice("AccessToken = {0}\n", SmartThingsReceiver.AccessToken);
                    }
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error("**Failed To Retrieve External Complete Authentication**\n");
                ErrorLog.Error("URL = {0}\n", url);
                response = null;
                return;
            }

            //Get Installation ID and URL

            url = "https://graph.api.smartthings.com/api/smartapps/endpoints/" + SmartThingsReceiver.ClientID + "?access_token=" + SmartThingsReceiver.AccessToken;
            request.Url.Parse(url);
            request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
            try
            {
                response = client.Dispatch(request);
                //ErrorLog.Notice("Response = {0}\n", response.ContentString);
                string pattern = ".*?url\":\"(.*?)\"}";
                var r = new Regex(pattern, RegexOptions.IgnoreCase);
                if (response.ContentString != null)
                {
                    var match = r.Match(response.ContentString);
                    if (match.Success)
                    {
                        SmartThingsReceiver.InstallationURL = "https://graph.api.smartthings.com" + match.Groups[1].Value;
                        //ErrorLog.Notice("InstallationURL = {0}\n", SmartThingsReceiver.InstallationURL);
                    }
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error("**Failed To Retrieve External Complete Authentication**\n");
                ErrorLog.Error("URL = {0}\n", url);
                response = null;
                return;
            }

            //Write Authentication Credentials to a File
            SmartThingsReceiver.WriteFile();
            SmartThingsReceiver.Authorized = 1;
        }

        private static CCriticalSection myCC = new CCriticalSection();

        public static ushort ReadFile()
        {
            ErrorLog.Notice(String.Format("Checking For SmartThings Credentials File"));
            bool success = false;
            int isRead = 0;
            FileStream myFS = null;
            XmlReader myXMLReader = null;
            myCC.Enter(); // Will not finish, until you have the Critical Section
            try
            {
                //Check if the File Exists
                if (!Crestron.SimplSharp.CrestronIO.File.Exists(String.Format("\\NVRAM\\{0}\\SmartThings\\Credentials.xml", InitialParametersClass.ProgramIDTag)))
                {
                    ErrorLog.Error(String.Format("SmartThings Credentials File Not Found\n"));
                    ErrorLog.Error("Please Re Run the Authentication Sequence");
                    isRead = 1;
                }

                //File was Found, Now Read It.
                if (isRead == 0)
                {
                    myFS = new FileStream(String.Format("\\NVRAM\\{0}\\SmartThings\\Credentials.xml", InitialParametersClass.ProgramIDTag), FileMode.Open);
                    myXMLReader = new XmlReader(myFS);
                    myXMLReader.Read();
                    while (!myXMLReader.EOF)
                    {
                        if (myXMLReader.NodeType == XmlNodeType.Element)
                        {
                            switch (myXMLReader.Name.ToUpper())
                            {
                                case "CLIENTID":
                                    {
                                        string sTemp = myXMLReader.ReadElementContentAsString();
                                        if (SmartThingsReceiver.ClientID != sTemp)
                                        {
                                            ErrorLog.Error("SmartThings Credential File Does Not Match the Client ID provided in the Program!");
                                            ErrorLog.Error("Please Re Run the Authentication Sequence");
                                            ErrorLog.Notice("Module = {0}, File = {1}", ClientID, sTemp);
                                            break;
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                case "AUTHCODE":
                                    {
                                        SmartThingsReceiver.AuthCode = myXMLReader.ReadElementContentAsString();
                                        break;
                                    }
                                case "ACCESSTOKEN":
                                    {
                                        SmartThingsReceiver.AccessToken = myXMLReader.ReadElementContentAsString();
                                        break;
                                    }
                                case "INSTALLATIONURL":
                                    {
                                        SmartThingsReceiver.InstallationURL = myXMLReader.ReadElementContentAsString();
                                        break;
                                    }
                               

                                default:
                                    {
                                        myXMLReader.Read();
                                        break;
                                    }
                            }
                        }
                        else
                        {
                            myXMLReader.Read();
                        }
                    }
                    success = true;
                    Authorized = 1;

                }
                else
                {
                    success = true;
                }
            }
            catch (Exception e)
            {
                //Crestron.SimplSharp.ErrorLog.Error(String.Format("ReadFile Error: {0}\n", e.Message));
                success = false;
            }
            finally
            {
                if (myXMLReader != null)
                    myXMLReader.Close();
                if (myFS != null)
                    myFS.Close();
                myCC.Leave();
            }
            if (success)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public static ushort WriteFile()
        {
            bool success = false;
            FileStream myFS = null;
            XmlWriter myXMLWriter = null;
            myCC.Enter(); // Will not finish, until you have the Critical Section
            try
            {
                myFS = new FileStream(String.Format("\\NVRAM\\{0}\\SmartThings\\Credentials.xml", InitialParametersClass.ProgramIDTag), FileMode.Create);
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.IndentChars = "     "; // note: default is two spaces
                settings.NewLineOnAttributes = false;
                settings.OmitXmlDeclaration = true;
                myXMLWriter = new XmlWriter(myFS, settings);

                myXMLWriter.WriteStartElement("SmartThingsCredentials");
                myXMLWriter.WriteElementString("ClientID", SmartThingsReceiver.ClientID);
                myXMLWriter.WriteElementString("ClientSecret", SmartThingsReceiver.ClientSecret);
                myXMLWriter.WriteElementString("AuthCode", SmartThingsReceiver.AuthCode);
                myXMLWriter.WriteElementString("AccessToken", SmartThingsReceiver.AccessToken);
                myXMLWriter.WriteElementString("InstallationURL", SmartThingsReceiver.InstallationURL);
                myXMLWriter.WriteEndElement();                
                myXMLWriter.WriteEndDocument();
                myXMLWriter.Flush();
                success = true;
            }
            catch (Exception e)
            {
                Crestron.SimplSharp.ErrorLog.Error(String.Format("Write File Error: {0}\n", e.Message));
                success = false;
            }
            finally
            {
                if (myXMLWriter != null)
                    myXMLWriter.Close();
                if (myFS != null)
                    myFS.Close();
                myCC.Leave();
            }
            if (success)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public void DeviceList()
        {
            HttpsClient client = new HttpsClient();
            //client.Verbose = true;
            //Testing
            client.AllowAutoRedirect = true;
            client.PeerVerification = false;
            client.HostVerification = false;
            client.KeepAlive = true;
            HttpsClientRequest request = new HttpsClientRequest();
            HttpsClientResponse response;

            //Get Switches
            string url = SmartThingsReceiver.InstallationURL + "/switches/?access_token=" + SmartThingsReceiver.AccessToken;            
            request.Url.Parse(url);
            request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
            response = client.Dispatch(request);
            string pattern = "id\":\"(.*?)\".*?label\":\"(.*?)\"";
            ErrorLog.Notice("**Switches**");         
            foreach (Match m in Regex.Matches(response.ContentString, pattern, RegexOptions.IgnoreCase))
                ErrorLog.Notice("'{0}' found with ID {1}.",m.Groups[2], m.Groups[1]);
            ErrorLog.Notice("\n");

            //Get Dimmers
            url = SmartThingsReceiver.InstallationURL + "/dimmers/?access_token=" + SmartThingsReceiver.AccessToken;
            request.Url.Parse(url);
            request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
            response = client.Dispatch(request);
            pattern = "id\":\"(.*?)\".*?label\":\"(.*?)\"";
            ErrorLog.Notice("**Dimmers**");
            foreach (Match m in Regex.Matches(response.ContentString, pattern, RegexOptions.IgnoreCase))
                ErrorLog.Notice("'{0}' found with ID {1}.", m.Groups[2], m.Groups[1]);
            ErrorLog.Notice("\n");

            //Get Presence
            url = SmartThingsReceiver.InstallationURL + "/presence/?access_token=" + SmartThingsReceiver.AccessToken;
            request.Url.Parse(url);
            request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
            response = client.Dispatch(request);
            pattern = "id\":\"(.*?)\".*?label\":\"(.*?)\"";
            ErrorLog.Notice("**Presence**");
            foreach (Match m in Regex.Matches(response.ContentString, pattern, RegexOptions.IgnoreCase))
                ErrorLog.Notice("'{0}' found with ID {1}.", m.Groups[2], m.Groups[1]);
            ErrorLog.Notice("\n");

            //Get thermostats
            url = SmartThingsReceiver.InstallationURL + "/thermostats/?access_token=" + SmartThingsReceiver.AccessToken;
            request.Url.Parse(url);
            request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
            response = client.Dispatch(request);
            pattern = "id\":\"(.*?)\".*?label\":\"(.*?)\"";
            ErrorLog.Notice("**thermostats**");
            foreach (Match m in Regex.Matches(response.ContentString, pattern, RegexOptions.IgnoreCase))
                ErrorLog.Notice("'{0}' found with ID {1}.", m.Groups[2], m.Groups[1]);
            ErrorLog.Notice("\n");

            //Get Locks
            url = SmartThingsReceiver.InstallationURL + "/locks/?access_token=" + SmartThingsReceiver.AccessToken;
            request.Url.Parse(url);
            request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
            response = client.Dispatch(request);
            pattern = "id\":\"(.*?)\".*?label\":\"(.*?)\"";
            ErrorLog.Notice("**locks**");
            foreach (Match m in Regex.Matches(response.ContentString, pattern, RegexOptions.IgnoreCase))
                ErrorLog.Notice("'{0}' found with ID {1}.", m.Groups[2], m.Groups[1]);
            ErrorLog.Notice("\n");

        }

        public void StartServer()
        {
            //Start listening for incoming connections
            ErrorLog.Notice("Starting SmartThings Listener Service\n");
            Server.Active = true;
        }

        public void InitializeHTTPServer(String addressToAcceptConnectionFrom, int port, string clientID, string clientSecret)
        {
            SmartThingsReceiver.Port = port;
            SmartThingsReceiver.ClientID = clientID;
            SmartThingsReceiver.ClientSecret = clientSecret;
            ErrorLog.Notice("Initializing SmartThings Receiver on Port {0}\n",Port);
            //Create a new instance of a server
            Server = new HttpServer();
            //Set the server's IP address
            Server.ServerName = addressToAcceptConnectionFrom;
            //Set the server's port
            Server.Port = port;
            //Assign an event handling method to the server
            Server.OnHttpRequest += new OnHttpRequestHandler(HTTPRequestEventHandler);
            ReadFile();
            if (Authorized == 1)
            {
                ErrorLog.Notice("SmartThings Credentials Found, Authenticate Not Needed");
            }
        }

        public void HTTPRequestEventHandler(Object sender, OnHttpRequestArgs requestArgs) //requestArgs
        {
            //int bytesSent = 0;
            string QueryString = requestArgs.Request.Header.RequestPath;
            string DeviceID = "";
            string DeviceValue = "";
            string AuthCode = "";
            string[] words;

            //ErrorLog.Notice(requestArgs.Request.Header.RequestType.ToString());
            //IP/DeviceID/Value
            if (requestArgs.Request.Header.RequestType.ToString() == "GET")
            {
                //ErrorLog.Notice("QueryString = {0}\n", QueryString);
                if (QueryString.Contains("code"))       //Need to Save this Code for Authorization Sequence
                {
                    words = QueryString.Split('=');
                    SmartThingsReceiver.AuthCode = words[1];
                    ErrorLog.Notice("SmartThingsReceiver Authorization Code Received {0}. Authorization will now Continue\n", AuthCode);
                    requestArgs.Response.ContentString = "You May Now Close Your Browser, The Processor Will Finish The Authorization Sequence...Monitor Console For Status\n";
                    AuthenticateStep2();        //Continue Authentication Procedure
                }
                else
                {
                    words = QueryString.Split('/');
                    DeviceID = words[1];
                    DeviceValue = words[2];
                    if (DeviceID != "" && DeviceValue != "")
                    {
                        SignalChangeEvents.SerialValueChange(DeviceID, DeviceValue);
                        requestArgs.Response.ContentString = "OK";
                    }
                    else
                    {
                        requestArgs.Response.ContentString = "Invalid Request";
                    }
                }
            }
        }
    }
}
