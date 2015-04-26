using System;
using System.Text;
using Crestron.SimplSharp;              // For Basic SIMPL# Classes
using Crestron.SimplSharp.Net.Https;    // For access to HTTPS
using Crestron.SimplSharp.Net;          // For access to HTTPS
using SmartThingsListener;
using System.Text.RegularExpressions;

namespace SmartThings
{
    public class SmartThingsCommandProcessor
    {
        public SmartThingsCommandProcessor()
        { }

        public ushort getAuthorized()
        {
            return (ushort) SmartThingsReceiver.Authorized;
        }
        
        public void getStatus(string deviceID, string category)
        {
            HttpsClient client = new HttpsClient();
            client.AllowAutoRedirect = true;
            client.PeerVerification = false;
            client.HostVerification = false;
            client.KeepAlive = true;
            HttpsClientRequest request = new HttpsClientRequest();
            HttpsClientResponse response;

            //Get Switches
            string url = SmartThingsReceiver.InstallationURL + "/"+category+"/"+deviceID+"?access_token=" + SmartThingsReceiver.AccessToken;
            request.Url.Parse(url);
            request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
            response = client.Dispatch(request);
            ErrorLog.Notice("Response = {0}", response.ContentString);
            string pattern = "value\":\"(.*?)\"";
            foreach (Match m in Regex.Matches(response.ContentString, pattern, RegexOptions.IgnoreCase))
                SignalChangeEvents.SerialValueChange(deviceID, m.Groups[1].ToString());
        }

        public void setSwitchState(string command, string deviceID, string category)
        {
            HttpsClient client = new HttpsClient();
            //client.Verbose = true;
            client.PeerVerification = false;
            client.HostVerification = false;
            //Testing
            //client.AllowAutoRedirect = true;

            HttpsClientRequest request = new HttpsClientRequest();
            HttpsClientResponse response;
            String url = "";
            url = SmartThingsReceiver.InstallationURL + "/" + category + "/" + deviceID + "/" + command + "?access_token=" + SmartThingsReceiver.AccessToken;
            request.KeepAlive = true;

            request.Url.Parse(url);
            request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
            response = client.Dispatch(request);
            if (response.Code >= 200 && response.Code < 300)
            {
                //ErrorLog.Notice("Wink https response code: " + response.Code);
                //ErrorLog.Notice(response.ContentString.ToString() + "\n");                
            }
            else
            {
                // A reponse code outside this range means the server threw an error.
                ErrorLog.Notice("HTTPS response code: " + response.Code);
            }
        }

        public void setDimmerLevel(string level, string deviceID, string category)
        {
            HttpsClient client = new HttpsClient();
            //client.Verbose = true;
            client.PeerVerification = false;
            client.HostVerification = false;
            //Testing
            //client.AllowAutoRedirect = true;

            HttpsClientRequest request = new HttpsClientRequest();
            HttpsClientResponse response;
            String url = "";
            url = SmartThingsReceiver.InstallationURL + "/" + category + "/" + deviceID + "/" + "level/" + level + "?access_token=" + SmartThingsReceiver.AccessToken;
            request.KeepAlive = true;

            request.Url.Parse(url);
            request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
            response = client.Dispatch(request);
            if (response.Code >= 200 && response.Code < 300)
            {
                //ErrorLog.Notice("Wink https response code: " + response.Code);
                //ErrorLog.Notice(response.ContentString.ToString() + "\n");                
            }
            else
            {
                // A reponse code outside this range means the server threw an error.
                ErrorLog.Notice("HTTPS response code: " + response.Code);
            }
        }
    }
}