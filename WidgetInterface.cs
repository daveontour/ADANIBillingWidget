using System;
using System.Collections.Generic;
using System.Xml;
using WorkBridge.Modules.AMS.AMSIntegrationAPI.Mod.Intf.DataTypes;

namespace AMSWidgetCore {

    internal interface WidgetInterface {
        bool RequireInitialLoad { get; }

        int ReInitPeriod { get; }
        bool RequireListen { get; }
        string AirportCodes { get; set; }

        DateTime InitTimeFrom { get; }
        DateTime InitTimeTo { get; }

        bool RequireXmlNode { get; }
        string[] NotificationContainsStrings { get; }
        Dictionary<string, string> AMSCodes { get; set; }

        Tuple<FlightId, PropertyValue[], string> ProcessNotification(string notification, AMSIntegrationServiceClient client, string token);

        Tuple<FlightId, PropertyValue[], string> ProcessNotification(XmlNode notification, AMSIntegrationServiceClient client, string token);

        Tuple<FlightId, PropertyValue[], string> ProcessFlight(XmlNode flight, AMSIntegrationServiceClient client, string token);

        void PreInitProcessing(AMSIntegrationServiceClient client, string apCode, string amsToken, string restServerURI);

        void AdditionalInitProcessing(AMSIntegrationServiceClient client, string apCode, string amsToken, string restServerURI);

        void Test(string testParam);
    }
}