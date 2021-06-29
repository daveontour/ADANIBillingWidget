using AMSWidgetCore;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using WorkBridge.Modules.AMS.AMSIntegrationAPI.Mod.Intf.DataTypes;

namespace AMSWidgetBase {

    public class ADANIBillingWidget : WidgetInterface {

        //private string restServerURI;
        private string airportCodesStr;

        //  Verion 1.1
        //private readonly Dictionary<string, string> standMap = new Dictionary<string, string>();
        private static readonly NLog.Logger auditLogger = NLog.LogManager.GetLogger("Audit");

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public bool RequireInitialLoad { get => true; }

        public int ReInitPeriod { get => 0; }

        public bool RequireListen { get => true; }

        public DateTime InitTimeFrom { get => DateTime.Now.AddDays(-2); }

        public DateTime InitTimeTo { get => DateTime.Now; }

        public bool RequireXmlNode { get => false; }

        public string[] NotificationContainsStrings {
            get => new string[] {
            "<Value propertyName=\"B---_isParkingDurationRequired\">true</Value>" };
        }

        public string AirportCodes {
            get => airportCodesStr;
            set {
                airportCodesStr = value;
            }
        }

        private Dictionary<string, string> amsCodes;
        public Dictionary<string, string> AMSCodes { get => amsCodes; set { amsCodes = value; } }

        public void AdditionalInitProcessing(AMSIntegrationServiceClient client, string aptCode, string amsToken, string restServerURI) {
            //  Verion 1.1
            //this.restServerURI = restServerURI;
        }

        public void PreInitProcessing(AMSIntegrationServiceClient client, string apCode, string amsToken, string restServerURI) {
            //  Verion 1.1
            //this.restServerURI = restServerURI;
            //  Verion 1.1
            //PopulateStands(apCode, amsToken);
        }

        public Tuple<FlightId, PropertyValue[], string> ProcessFlight(XmlNode flight, AMSIntegrationServiceClient client, string token) {
            if (!flight.OuterXml.Contains("<Value propertyName=\"B---_isParkingDurationRequired\">true</Value>")) {
                return null;
            }
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(flight.OwnerDocument.NameTable);
            nsmgr.AddNamespace("ams", "http://www.sita.aero/ams6-xml-api-datatypes");
            FlightNode flt = null;
            FlightId fltId = null;
            try {
                // Get the flight node
                flt = new FlightNode(flight, nsmgr);

                // Create FlightId object which is used for updates
                fltId = flt.GetFlightId();

                // Get the Linked flight information for the arrival time info
                FlightId linkedFltId = flt.GetLinkedFlightId();
                string arrivalFlightElement = client.GetFlight(token, linkedFltId)?.OuterXml;
                flt.SetLinkedArrival(arrivalFlightElement, nsmgr);

                // Set Stand Types
                //  Verion 1.1
                //foreach (StandSlot slot in flt.standSlots) {
                //    slot.type = standMap[$"{flt.apCode}-{slot.externalName}"];
                //}

                //Create the billing record object
                FlightBillingRecord flightBilling = new FlightBillingRecord(flt);

                // Check if the flight hass all the data and is ready for processing
                bool readyToProcess = flightBilling.ReadyToProcess();

                // The information in the flight is not sufficient for processing
                // So just rurn a value to clear the flag.
                if (!readyToProcess) {
                    logger.Info("Not Processing Flight");
                    logger.Trace(flt);

                    return flightBilling.GetFieldsForReset();
                }

                flightBilling.Calculate();

                Tuple<FlightId, PropertyValue[], string> billingResult = flightBilling.GetFieldsForUpdate();

                if (billingResult != null) {
                    auditLogger.Info(flightBilling);
                } else {
                    billingResult = flightBilling.GetFieldsForReset();
                }

                return billingResult;
            } catch (Exception ex) {
                logger.Error(ex);
                string apCode = flt.apCode;
                XmlDocument doc = new XmlDocument();
                doc.Load("widget.config");
                XmlNode root = doc.DocumentElement;
                XmlNode apNode = root.SelectSingleNode($".//airport[airportCode[contains(text(), \"{apCode}\")]]");

                string NORMAL_CUSTOMFIELD = apNode.SelectSingleNode("./normalCustomField")?.InnerText;
                string NIGHT_CUSTOMFIELD = apNode.SelectSingleNode("./nightCustomField")?.InnerText;
                string NIGHT_EXTENDED_CUSTOMFIELD = apNode.SelectSingleNode("./extendedNightCustomField")?.InnerText;
                string EXTENDED_CUSTOMFIELD = apNode.SelectSingleNode("./extendedCustomField")?.InnerText;
                string DURATIONREQUIRED_CUSTOMFIELD = apNode.SelectSingleNode("./durationRequiredCustomField")?.InnerText;

                PropertyValue pvNormal = new PropertyValue {
                    propertyNameField = NORMAL_CUSTOMFIELD,
                    valueField = ""
                };
                PropertyValue pvNight = new PropertyValue {
                    propertyNameField = NIGHT_CUSTOMFIELD,
                    valueField = ""
                };
                PropertyValue pvExtended = new PropertyValue {
                    propertyNameField = EXTENDED_CUSTOMFIELD,
                    valueField = ""
                };
                PropertyValue pvExtendedNight = new PropertyValue {
                    propertyNameField = NIGHT_EXTENDED_CUSTOMFIELD,
                    valueField = ""
                };

                PropertyValue pv = new PropertyValue {
                    propertyNameField = DURATIONREQUIRED_CUSTOMFIELD,
                    valueField = "false"
                };

                PropertyValue[] values = new PropertyValue[] { pvNormal, pvNight, pvExtended, pvExtendedNight, pv };

                return new Tuple<FlightId, PropertyValue[], string>(fltId, values, token);
            }
        }

        public Tuple<FlightId, PropertyValue[], string> ProcessNotification(string notification, AMSIntegrationServiceClient client, string token) {
            // Changes in Stand configuration
            //  Verion 1.1
            //if (notification.Contains("<FixedResourceCreateddNotification>")
            //    || notification.Contains("<FixedResourceUpdatedNotification>")
            //    || notification.Contains("<FixedResourceDeletedNotification>")) {
            //    // Reload the stand information

            //    PopulateStands(token);
            //    return null;
            //}

            // Only process Flight Update Notifications, not Movement Updated Notificatons
            if (!notification.Contains("<FlightUpdatedNotification>")) {
                return null;
            }

            //Only process Flights where the processing flagg is set.
            if (!notification.Contains("<Value propertyName=\"B---_isParkingDurationRequired\">true</Value>")) {
                return null;
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(notification);
            XmlNode xmlNode = doc.DocumentElement;
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlNode.OwnerDocument.NameTable);
            nsmgr.AddNamespace("ams", "http://www.sita.aero/ams6-xml-api-messages");
            XmlNode fltNode = xmlNode.SelectSingleNode(".//ams:Flight", nsmgr);

            return ProcessFlight(fltNode, client, token);
        }

        public Tuple<FlightId, PropertyValue[], string> ProcessNotification(XmlNode notification, AMSIntegrationServiceClient client, string token) {
            return null;
        }

        //  Verion 1.1
        //private void PopulateStands(string amsToken) {
        //    foreach (KeyValuePair<string, string> keyValue in AMSCodes) {
        //        if (keyValue.Value == amsToken) {
        //            PopulateStands(keyValue.Key, amsToken);
        //            break;
        //        }
        //    }
        //}

        //  Verion 1.1
        //private void PopulateStands(string apt, string amsToken) {
        //    standMap.Clear();

        //    try {
        //        string uri = restServerURI + $"{apt}/Stands";
        //        string result = GetRestURI(uri, amsToken).Result;

        //        XmlDocument doc = new XmlDocument();
        //        doc.LoadXml(result);
        //        XmlNode xmlNode = doc.DocumentElement;

        //        foreach (XmlNode stand in xmlNode.SelectNodes("//FixedResource")) {
        //            string standID = stand.SelectSingleNode("./Id").InnerText;
        //            string standType = stand.SelectSingleNode("./CustomFields/CustomField[./Name = 'S---_ParkingStandType']")?.SelectSingleNode("./Value")?.InnerText;

        //            if (standType == null) {
        //                standType = stand.SelectSingleNode("./CustomFields/CustomField[./Name = 'B---_AirbridgeStand']")?.SelectSingleNode("./Value")?.InnerText;
        //                if (standType.ToLower().Equals("true")) {
        //                    standType = "Contact";
        //                } else {
        //                    standType = "Remote";
        //                }
        //            }

        //            standMap.Add($"{apt}-{standID}", standType);
        //            logger.Trace($"{apt}-{standID} {standType}");
        //        }
        //    } catch (Exception e) {
        //        logger.Error(e);
        //    }
        //}

        public async Task<string> GetRestURI(string uri, string amsToken, bool addHeader = true, string user = null, string pass = null) {
            try {
                HttpClient _httpClient = new HttpClient();
                if (addHeader) {
                    _httpClient.DefaultRequestHeaders.Add("Authorization", amsToken);
                }
                if (user != null) {
                    var authenticationString = $"{user}:{pass}";
                    var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.UTF8.GetBytes(authenticationString));
                    _httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + base64EncodedAuthenticationString);
                }
                using (var result = await _httpClient.GetAsync(uri)) {
                    return result.Content.ReadAsStringAsync().Result;
                }
            } catch (Exception ex) {
                logger.Error(ex, "Error getting Rest URI " + uri);
                return "ERROR";
            }
        }

        public void Test(string inputFile) {
            XmlDocument doc = new XmlDocument();
            doc.Load(inputFile);

            foreach (XmlNode entry in doc.SelectNodes(".//flight")) {
                string nature = entry.SelectSingleNode(".//nature")?.InnerText;
                string airlineCode = entry.SelectSingleNode(".//airlineCode")?.InnerText;
                string fltNumber = entry.SelectSingleNode(".//fltNumber")?.InnerText;
                string schedDate = entry.SelectSingleNode(".//schedDate")?.InnerText;

                string schedTimeString = entry.SelectSingleNode(".//schedTimeString")?.InnerText;
                string actualArrival = entry.SelectSingleNode(".//actualArrival")?.InnerText;
                string actualDeparture = entry.SelectSingleNode(".//actualDeparture")?.InnerText;
                string apCode = entry.SelectSingleNode(".//apCode")?.InnerText;

                DateTime st = DateTime.Parse(schedTimeString);
                DateTime aa = DateTime.Parse(actualArrival);
                DateTime ad = DateTime.Parse(actualDeparture);

                FlightNode flt = new FlightNode(nature, airlineCode, fltNumber, schedDate, schedTimeString, st, aa, ad);
                flt.apCode = apCode;

                //  Verion 1.1
                //foreach (XmlNode slot in entry.SelectNodes(".//StandSlot")) {
                //    flt.standSlots.Add(
                //        new StandSlot(
                //            slot.SelectSingleNode(".//start")?.InnerText,
                //            slot.SelectSingleNode(".//end")?.InnerText,
                //            slot.SelectSingleNode(".//externalName")?.InnerText,
                //            slot.SelectSingleNode(".//name")?.InnerText,
                //            slot.SelectSingleNode(".//type")?.InnerText)
                //        );
                //}

                // Make fure the stand allocations are sorted in order
                //  Verion 1.1
                //flt.standSlots.Sort((p, q) => p.startTime.CompareTo(q.startTime));

                FlightBillingRecord flightBilling = new FlightBillingRecord(flt);

                // Check if the flight has all the data and is ready for processing
                bool readyToProcess = flightBilling.ReadyToProcess();

                if (!readyToProcess) {
                    logger.Info($"Not Processing Flight");
                    logger.Trace(flightBilling.flt.ToString(false) + "\n");
                } else {
                    logger.Info("Processing Flight\n");
                    flightBilling.Calculate();
                    logger.Trace(flightBilling);
                }
            }
        }
    }
}