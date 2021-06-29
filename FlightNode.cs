using AMSWidgetCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using WorkBridge.Modules.AMS.AMSIntegrationAPI.Mod.Intf.DataTypes;

namespace AMSWidgetBase {

    public class StandSlot {
        public DateTime startTime;
        public DateTime endTime;
        public string externalName;
        public string name;
        public string type;

        public StandSlot(XmlNode node, XmlNamespaceManager nsmgr) {
            startTime = DateTime.Parse(node.SelectSingleNode("./ams:Value[@propertyName ='StartTime']", nsmgr).InnerText).ToUniversalTime(); ;
            endTime = DateTime.Parse(node.SelectSingleNode("./ams:Value[@propertyName ='EndTime']", nsmgr).InnerText).ToUniversalTime(); ;

            externalName = node.SelectSingleNode("./ams:Stand/ams:Value[@propertyName ='ExternalName']", nsmgr)?.InnerText;
            name = node.SelectSingleNode("./ams:Stand/ams:Value[@propertyName ='Name']", nsmgr)?.InnerText;
        }

        public StandSlot(string startTime, string endTime, string externalName, string name, string type) {
            try {
                this.startTime = DateTime.Parse(startTime);
            } catch (Exception) {
                // NO-OP
            }

            try {
                this.endTime = DateTime.Parse(endTime);
            } catch (Exception) {
                // NO-OP
            }

            this.externalName = externalName;
            this.name = name;
            this.type = type;
        }

        public override string ToString() {
            return $"Stand:{name} External Name:{externalName}  From:{startTime}  To:{endTime}  Type:{type} ";
        }
    }

    public class FlightNode {
        public string nature;
        public string airlineCode;
        public string fltNumber;
        public string schedDate;

        public string lnature;
        public string lairlineCode;
        public string lfltNumber;
        public string lschedDate;

        public string apCode;
        public string descriptor;
        public string schedTimeString;
        public DateTime schedTime;

        public DateTime? actualArrival;
        public DateTime? actualDeparture;

        //  Verion 1.1
        //public List<StandSlot> standSlots = new List<StandSlot>();
        //public List<TowRecord> towRecords = new List<TowRecord>();

        // The "node" parameter is one the XElement of the "FlightIndentifier" element of the Towing message
        public FlightNode(XmlNode node, XmlNamespaceManager nsmgr) {
            this.nature = node.SelectSingleNode(".//ams:FlightKind", nsmgr).InnerText;
            this.airlineCode = node.SelectSingleNode(".//ams:AirlineDesignator[@codeContext='IATA']", nsmgr).InnerText;
            this.fltNumber = node.SelectSingleNode(".//ams:FlightNumber", nsmgr).InnerText;
            this.schedDate = node.SelectSingleNode(".//ams:ScheduledDate", nsmgr).InnerText;

            this.lnature = node.SelectSingleNode(".//ams:LinkedFlight/ams:FlightId/ams:FlightKind", nsmgr).InnerText;
            this.lairlineCode = node.SelectSingleNode(".//ams:LinkedFlight/ams:FlightId/ams:AirlineDesignator[@codeContext='IATA']", nsmgr).InnerText;
            this.lfltNumber = node.SelectSingleNode(".//ams:LinkedFlight/ams:FlightId/ams:FlightNumber", nsmgr).InnerText;
            this.lschedDate = node.SelectSingleNode(".//ams:LinkedFlight/ams:FlightId/ams:ScheduledDate", nsmgr).InnerText;

            this.apCode = node.SelectSingleNode("./ams:FlightId/ams:AirportCode[@codeContext='IATA']", nsmgr).InnerText;
            this.schedTimeString = node.SelectSingleNode("./ams:FlightState/ams:ScheduledTime", nsmgr).InnerText;
            this.descriptor = $"{airlineCode}{fltNumber}@{schedTimeString}";
            if (nature.Equals("Arrival")) {
                this.descriptor = this.descriptor + "A";
            } else {
                this.descriptor = this.descriptor + "D";
            }

            schedTime = DateTime.Parse(this.schedTimeString).ToUniversalTime();

            string actualDepartureStr = node.SelectSingleNode($".//ams:Value[@propertyName = '{Parameters.ACTUAL_DEPARTURE_FIELD}']", nsmgr)?.InnerText;

            try {
                actualDeparture = DateTime.Parse(actualDepartureStr).ToUniversalTime();
            } catch (Exception) {
                actualDeparture = null;
            }

            //  Verion 1.1
            //foreach (XmlNode slot in node.SelectNodes(".//ams:StandSlot", nsmgr)) {
            //    standSlots.Add(new StandSlot(slot, nsmgr));
            //}

            // Make fure the stand allocations are sorted in order
            //  Verion 1.1  standSlots.Sort((p, q) => p.startTime.CompareTo(q.startTime));
            //standSlots.Reverse();
        }

        public FlightNode(string nature, string airlineCode, string fltNumber, string schedDate, string schedTimeString, DateTime? schedTime, DateTime? actualArrival, DateTime? actualDeparture) {
            this.nature = nature;
            this.airlineCode = airlineCode;
            this.fltNumber = fltNumber;
            this.schedDate = schedDate;
            this.schedTimeString = schedTimeString;
            this.schedTime = schedTime.Value.ToUniversalTime();
            this.actualArrival = actualArrival.Value.ToUniversalTime();
            this.actualDeparture = actualDeparture.Value.ToUniversalTime();
        }

        //  Verion 1.1
        //public void SetTowsForFlight(string xml) {
        //    if (xml == null) {
        //        return;
        //    }
        //    XmlDocument doc = new XmlDocument();
        //    doc.LoadXml(xml);
        //    XmlNode xmlNode = doc.DocumentElement;

        //    foreach (XmlNode towNode in xmlNode.SelectNodes("./Towing")) {
        //        string fromStand = towNode.SelectSingleNode("./From")?.InnerText;
        //        string toStand = towNode.SelectSingleNode("./To")?.InnerText;

        //        string schedStartString = towNode.SelectSingleNode("./ScheduledStart")?.InnerText;
        //        string schedEndString = towNode.SelectSingleNode("./ScheduledEnd")?.InnerText;

        //        string actualStartString = towNode.SelectSingleNode("./ActualStart")?.InnerText;
        //        string actualEndString = towNode.SelectSingleNode("./ActualEnd")?.InnerText;

        //        towRecords.Add(new TowRecord(fromStand, toStand, schedStartString, schedEndString, actualStartString, actualEndString));
        //    }
        //}

        internal void SetLinkedArrival(string xml, XmlNamespaceManager nsmgr) {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNode node = doc.DocumentElement;
            string actualArrvialStr = node.SelectSingleNode($".//ams:Value[@propertyName = '{Parameters.ACTUAL_ARRIVAL_FIELD}']", nsmgr)?.InnerText;

            try {
                actualArrival = DateTime.Parse(actualArrvialStr).ToUniversalTime();
            } catch (Exception) {
                actualArrival = null;
            }
        }

        internal FlightId GetLinkedFlightId() {
            LookupCode apCode = new LookupCode();
            apCode.codeContextField = CodeContext.IATA;
            apCode.valueField = this.apCode;
            LookupCode[] ap = { apCode };

            LookupCode alCode = new LookupCode();
            alCode.codeContextField = CodeContext.IATA;
            alCode.valueField = this.lairlineCode; ;
            LookupCode[] al = { alCode };

            FlightId flightID = new FlightId();
            flightID.flightKindField = lnature == "Arrival" ? FlightKind.Arrival : FlightKind.Departure;
            flightID.airportCodeField = ap;
            flightID.airlineDesignatorField = al;
            flightID.scheduledDateField = Convert.ToDateTime(this.lschedDate);
            flightID.flightNumberField = this.lfltNumber;

            return flightID;
        }

        // The "node" parameter is one the XElement of the "FlightIndentifier" element of the Towing message
        public FlightNode(XElement node) {
            this.nature = node.Element("Nature").Value;
            this.airlineCode = node.Element("AirlineCode").Value;
            this.fltNumber = node.Element("FlightNumber").Value;
            this.schedDate = node.Element("ScheduledDate").Value;
        }

        public FlightId GetFlightId() {
            LookupCode apCode = new LookupCode();
            apCode.codeContextField = CodeContext.IATA;
            apCode.valueField = this.apCode;
            LookupCode[] ap = { apCode };

            LookupCode alCode = new LookupCode();
            alCode.codeContextField = CodeContext.IATA;
            alCode.valueField = this.airlineCode; ;
            LookupCode[] al = { alCode };

            FlightId flightID = new FlightId();
            flightID.flightKindField = nature == "Arrival" ? FlightKind.Arrival : FlightKind.Departure;
            flightID.airportCodeField = ap;
            flightID.airlineDesignatorField = al;
            flightID.scheduledDateField = Convert.ToDateTime(this.schedDate);
            flightID.flightNumberField = this.fltNumber;

            return flightID;
        }

        // Is the supplied node referring to the same flight as this node?
        public bool Equals(FlightNode node) {
            if (node.nature == this.nature
                && node.airlineCode == this.airlineCode
                && node.fltNumber == this.fltNumber
                && node.schedDate == this.schedDate) {
                return true;
            } else {
                return false;
            }
        }

        override
        public string ToString() {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"{airlineCode}{fltNumber}, {nature}, Scheduled Date:{schedDate}, Actual Arrival:{actualArrival}, ActualDeparture:{actualDeparture},  Airport:{apCode}, Descriptor:{descriptor}");

            //  Verion 1.1
            //sb.AppendLine("\nStand Slots\n");
            //foreach (StandSlot slot in standSlots) {
            //    sb.AppendLine(slot.ToString());
            //}

            //if (standSlots.Count == 0) {
            //    sb.AppendLine("No Stand Allocations\n");
            //}

            return sb.ToString();
        }

        public string ToString(bool simple) {
            return $"{airlineCode}{fltNumber}, {nature}, Scheduled Date:{schedDate}, Actual Arrival:{actualArrival}, ActualDeparture:{actualDeparture},  Airport:{apCode}, Descriptor:{descriptor}";
        }
    }
}