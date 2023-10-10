
using QS.Plugins.Definitions;
using QS.Definitions;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using QS.Definitions.General;
using System.Collections;
using System.Reflection.Metadata;
using QS.Definitions.Converter;
using System.Collections.Generic;
using System;
using System.Net;
using QS.Definitions.PointOfInterest;
using System.IO;
using System.Security.Cryptography;
using System.Xml;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using QS.Definitions.Geodesy;
using QS.Plugins;

namespace ATAK_Plugin_Monitorh
{
    public class ATAK_Plugin : ATAKPlugin
    {
        //Local Network Configuration Settings...
        public string Callsign { get; set; }
        public IPAddress IP_Address { get; set; }
        public ushort PORT { get; set; }
        public IPType SendType { get; set; }
        public ushort ReceivePort { get; set; }
        public IPType ReceiveType { get; set; }
        //...

        //Identifier CoT Associations
        public Dictionary<string, string> POI_Type = new Dictionary<string, string> { { "Enemy", "a-h-G" }, { "Unknown", "a-u-G" }, { "Friend", "a-f-G" }, { "Neutral", "a-n-G" } };

        //GUID tracer, beofre implemntation of auto updateing POI(s)
        public List<Guid> POI_IDS = new List<Guid>();

        //Log File setup configuration, used in writeToLog()...
        private static string LogDirectory = Directory.GetCurrentDirectory() + "\\plugins\\";
        private static string LogFile = "pluginLog.txt";
        private static string LogFullPath = LogDirectory + LogFile;
        //...

        //Interface Properties...
        public string Title => "ATAK Plugin with Config";
        public string Description => "This will boradcast POI's to user devices on the same local network, including configurable settings in the from the update GUI.";

        //temporary
        public Action<List<POIRecord>> poiTemp;

        //New to interface. Unsure how we want to implement
        public Action<List<POIRecord>> POIsUpdated { get; set; }
        //...

        public ATAK_Plugin()
        {

        }


        //excissive code to handle POI point of conception.  Moving to start/stale time model.  FIX it! ~ajc
        public override void UpdatePOIs(List<POIRecord> poiList)
        {
            writeToLog("UpdatePOIs");
            string outputStr = "";
            string endChar = "___";
            Guid removeItem = Guid.Empty;

            //if id is not in List<POIRecord> but in POI_IDS, id has been delete and ID needs to be removed from the list
            foreach (var id in POI_IDS)
            {
                int index = poiList.FindIndex(item => item.Id == id);
                if (!(index >= 0))
                {
                    removeItem = id;
                }
            }
            POI_IDS.Remove(removeItem);
            //end of delete section

            //check if new ID is created
            foreach (var poi in poiList)
            {
                //if Id is not in list, then its a new ID and add to list
                if (!POI_IDS.Contains(poi.Id))
                {
                    POI_IDS.Add(poi.Id);
                }
                //end of new ID creation check ~ajc
                string newPoi = createStaticPOI(poi);
                sendPacket(newPoi);
            }
        }

        public override void UpdateUAVPosition(GeoPosition newPosition, double yaw)
        {
            writeToLog("UpdateUAVPosition");
            string COTMessage = "";
            DateTime dt = DateTime.Now;
            string timeString = dt.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");
            string staleString = dt.AddSeconds(5).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");

            COTMessage += "<?xml version='1.0' standalone='yes'?>";
            COTMessage += "<event version=\"2.0\" uid=\"Vector_ID\"";
            COTMessage += " type=\"a-f-A-M-F-Q-r\""; //" type=\"a-n-G\""; //" type=\"a-f-A-M-U-R\"";  //reg expression for uav: ="^a-f-A-M-F-Q-r"
            COTMessage += " time=\"" + timeString + "\"";
            COTMessage += " start=\"" + timeString + "\"";
            COTMessage += " stale=\"" + staleString + "\" how=\"m-g\">";
            COTMessage += " <point lat=\"" + newPosition.Latitude.ToString("N7", new CultureInfo("en-US")) + "\"";
            COTMessage += " lon =\"" + newPosition.Longitude.ToString("N7", new CultureInfo("en-US")) + "\" hae=\"253\" ce=\"12\" le=\"" + newPosition.AltitudeMSL.ToString() + "\"/>";
            COTMessage += "<detail>";
            COTMessage += "<contact callsign=\"UAV Vector\"/>";
            COTMessage += "</detail>";
            COTMessage += "</event>";
            sendPacket(COTMessage);
        }

        //Interface Methods...
        public override void ChangeConfiguration(string callsign, IPAddress serverIP, ushort serverPort, IPType sendType, ushort receivePort, IPType receiveType)
        {
            writeToLog("ChangeConfiguration");
            Callsign = callsign;
            IP_Address = serverIP;
            PORT = serverPort;
            SendType = sendType;
            ReceivePort = receivePort;
            ReceiveType = receiveType;

            writeToLog(callsign);

            UDPListener();
        }

        public override void TogglePluginActivation(bool activate)
        {
            writeToLog("TogglePluginActivation");
            Console.WriteLine("Hello from TogglePluginActivation");
        }

        //...

        public string createStaticPOI(POIRecord poi)
        {
            writeToLog(poi.Timestamp.ToString());
            string COTMessage = "";
            string timeString = poi.Timestamp.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");
            string staleString = poi.Timestamp.AddSeconds(10).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");
            string poi_type;
            POI_Type.TryGetValue(poi.Type.ToString(), out poi_type);
            string contact_callsign = poi.Name;

            COTMessage += "<?xml version='1.0' standalone='yes'?>";
            COTMessage += "<event version=\"2.0\" uid=\"" + poi.Id.ToString() + "\"";
            COTMessage += " type=\"" + poi_type + "\"";
            COTMessage += " time=\"" + timeString + "\"";
            COTMessage += " start=\"" + timeString + "\"";
            COTMessage += " stale=\"" + staleString + "\" how=\"m-g\">";
            COTMessage += " <point lat=\"" + poi.Position.Latitude.ToString("N7", new CultureInfo("en-US")) + "\"";
            COTMessage += " lon =\"" + poi.Position.Longitude.ToString("N7", new CultureInfo("en-US")) + "\" hae=\"253\" ce=\"12\" le=\"" + poi.Position.AltitudeMSL.ToString() + "\"/>";
            COTMessage += "<detail>";
            COTMessage += "<link parent_callsign=\"" + Callsign + "\"/>"; //new attribute added to the mix ~ajc
            COTMessage += "<contact callsign=\"" + contact_callsign + "\"/>";
            COTMessage += "</detail>";
            COTMessage += "</event>";
            return COTMessage;
        }

        public void sendPacket(string data)
        {
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPAddress destinationAddress = IP_Address;
            IPEndPoint Destination = new IPEndPoint(destinationAddress, PORT);

            byte[] buff = Encoding.ASCII.GetBytes(data);
            sock.SendTo(buff, Destination);
        }

        public void writeToLog(string msg)
        {
            File.AppendAllText(LogFullPath, msg + Environment.NewLine);
        }

        private async Task UDPListener()
        {
            writeToLog("TASK UDPListener Begin on Receive Port: " + ReceivePort.ToString());
            POIRecord newPOI = new POIRecord();

            using (var udpClient = new UdpClient(ReceivePort))
            {
                string rMessage = "";
                string[] rArgs = new string[0];
                while (true)
                {
                    //IPEndPoint object will allow us to read datagrams sent from any source.
                    var receivedResults = await udpClient.ReceiveAsync();
                    writeToLog("Message Reveived!");

                    rMessage += Encoding.ASCII.GetString(receivedResults.Buffer);
                    generateQbasePOI(rMessage);

                }
            }
        }

        public void generateQbasePOI(string udpPacket)
        {
            var tempList = new List<POIRecord>();

            string[] xmlMessages = udpPacket.Split("<?xml");
            foreach (string message in xmlMessages)
            {
                if (message.Contains("contact callsign"))
                {
                    writeToLog("PERFECT EXAMPLE");
                    string name = message.Split("contact callsign=\"")[1].Split("\"")[0];
                    string lat = message.Split("point lat=\"")[1].Split("\"")[0];
                    string lon = message.Split("<point")[1].Split("lon=\"")[1].Split("\"")[0];
                    string alt = message.Split("<point")[1].Split("hae=\"")[1].Split("\"")[0];
                    string uid = message.Split("uid=\"")[1].Split("\"")[0];
                    string type = message.Split("type=\"")[1].Split("\"")[0];
                    string time = message.Split("time=\"")[1].Split("\"")[0];
                    string origin = message.Split("parent_callsign=\"")[1].Split("\"")[0];

                    writeToLog(name);
                    writeToLog(lat);
                    writeToLog(lon);
                    writeToLog(alt);
                    writeToLog(uid);
                    getPOIType(type);
                    writeToLog(type);
                    writeToLog(time);
                    writeToLog(origin);
                    //writeToLog(message);

                    POIRecord poi = new POIRecord();
                    poi.Name = name;
                    poi.Position = new QS.Definitions.Geodesy.GeoPosition(Convert.ToDouble(lat), Convert.ToDouble(lon), Convert.ToDouble(alt));
                    poi.Id = new Guid(uid);

                    // DateTime myDate = DateTime.ParseExact(time, "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

                    poi.Timestamp = new DateTime();// myDate;
                    poi.Origin = QS.Plugins.Definitions.POIOriginType.External;
                    poi.Type = getPOIType(type);

                    writeToLog("PERFECT EXAMPLE END");
                    writeToLog("");

                    tempList.Add(poi);
                    POIsUpdated?.Invoke(tempList);
                }
                //else { writeToLog("no name"); }
            }
        }

        private QS.Plugins.Definitions.POIType getPOIType(string type)
        {
            if (POI_Type.FirstOrDefault(x => x.Value == type).Key == "Friend")
            {
                return QS.Plugins.Definitions.POIType.Friend;
            }
            else if (POI_Type.FirstOrDefault(x => x.Value == type).Key == "Enemy")
            {
                return QS.Plugins.Definitions.POIType.Enemy;
            }
            else if (POI_Type.FirstOrDefault(x => x.Value == type).Key == "Unknown")
            {
                return QS.Plugins.Definitions.POIType.Unknown;
            }
            else if (POI_Type.FirstOrDefault(x => x.Value == type).Key == "Neutral")
            {
                return QS.Plugins.Definitions.POIType.Neutral;
            }
            else
            {
                return QS.Plugins.Definitions.POIType.Neutral;
            }
        }
    }
}
