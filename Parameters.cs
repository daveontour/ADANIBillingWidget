using System;
using System.Configuration;

namespace AMSWidgetBase {
    /*
     * Class to make the configuration parameters available.
     */

    public static class Parameters {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static readonly string AMS_REST_SERVICE_URI = InitRestServiceURI();

        private static string InitRestServiceURI() {
            return ConfigurationManager.AppSettings["AMSRestServiceURI"];
        }

        public static readonly string AMS_WEB_SERVICE_URI = InitWebServiceURI();

        private static string InitWebServiceURI() {
            return ConfigurationManager.AppSettings["AMSWebServiceURI"];
        }

        public static readonly string RECVQ = InitRecvQ();

        private static string InitRecvQ() {
            return ConfigurationManager.AppSettings["NotificationQueue"];
        }

        public static readonly string APPSERVICENAME = InitAPPSERVICENAME();

        private static string InitAPPSERVICENAME() {
            return string.IsNullOrEmpty(ConfigurationManager.AppSettings["ServiceName"]) ? "ServiceName" : ConfigurationManager.AppSettings["ServiceName"];
        }

        public static readonly string APPDISPLAYNAME = InitAPPDISPLAYNAME();

        private static string InitAPPDISPLAYNAME() {
            return string.IsNullOrEmpty(ConfigurationManager.AppSettings["ServiceDisplayName"]) ? "ServiceName" : ConfigurationManager.AppSettings["ServiceName"];
        }

        public static readonly string APPDESCRIPTION = InitAPPDESCRIPTION();

        private static string InitAPPDESCRIPTION() {
            return string.IsNullOrEmpty(ConfigurationManager.AppSettings["ServiceDescription"]) ? "ServiceDescription" : ConfigurationManager.AppSettings["ServiceDescription"];
        }

        public static readonly int RESTSERVER_RETRY_INTERVAL = InitRESTSERVER_RETRY_INTERVAL();

        private static int InitRESTSERVER_RETRY_INTERVAL() {
            return Int32.Parse(ConfigurationManager.AppSettings["ResetServerRetryInterval"]);
        }

        public static readonly string ACTUAL_DEPARTURE_FIELD = "de--_ActualDeparture";

        public static readonly string ACTUAL_ARRIVAL_FIELD = "de--_ActualArrival";

        public static readonly string VERSION = "Version 1.0.0, 20210519";
    }
}