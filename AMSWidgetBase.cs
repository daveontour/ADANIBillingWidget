using AMSWidgetCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Messaging;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using WorkBridge.Modules.AMS.AMSIntegrationAPI.Mod.Intf.DataTypes;
using WorkBridge.Modules.AMS.AMSIntegrationWebAPI.Srv;

//Version 4.0.1

namespace AMSWidgetBase {

    internal class AMSWidgetBase {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private bool startListenLoop = true;    // Flag controlling the execution of the update notificaiton listener

        public bool stopProcessing = false;
        private Thread startThread;
        private Thread receiveThread;           // Thread the notification listener runs in
        private BasicHttpBinding binding;
        private EndpointAddress address;
        private System.Timers.Timer reInitTimer;
        private readonly WidgetInterface widget = new ADANIBillingWidget();
        private string testFile;
        private List<string> airportCodes = new List<string>();
        public Dictionary<string, string> amsCodes = new Dictionary<string, string>();

        public AMSWidgetBase(string test) {
            if (test != null) {
                testFile = test;
                widget.Test(test);
            }
        }

        public bool Start() {
            if (testFile != null) {
                return false;
            }
            Logger.Info($"Starting Service ({Parameters.APPDISPLAYNAME})");

            // Set the binding and address for use by the web services client
            binding = new BasicHttpBinding {
                MaxReceivedMessageSize = 20000000,
                MaxBufferSize = 20000000,
                MaxBufferPoolSize = 20000000
            };
            address = new EndpointAddress(Parameters.AMS_WEB_SERVICE_URI);

            stopProcessing = false;
            startThread = new Thread(new ThreadStart(StartThread));
            startThread.Start();

            Logger.Info($"{Parameters.APPDISPLAYNAME} Service Started");

            return true;
        }

        public void StartThread() {
            XmlDocument doc = new XmlDocument();
            doc.Load("widget.config");
            XmlNode root = doc.DocumentElement;
            XmlNodeList apcodes = root.SelectNodes($".//airport");

            foreach (XmlNode node in apcodes) {
                string ac = node.SelectSingleNode("./airportCode").InnerText;
                string amsCode = node.SelectSingleNode("./token").InnerText;
                airportCodes.Add(ac);
                amsCodes.Add(ac, amsCode);
                Logger.Info($"Configured Airport Code - {ac}");
            }

            widget.AMSCodes = amsCodes;

            using (AMSIntegrationServiceClient client = new AMSIntegrationServiceClient(binding, address)) {
                Logger.Trace(">>>>>>> Starting Widget Specific Pre Initialization");
                foreach (string acc in airportCodes) {
                    this.widget.PreInitProcessing(client, acc, amsCodes[acc], Parameters.AMS_REST_SERVICE_URI);
                }

                Logger.Trace("<<<<<<< Completed Widget Specific Pre Initialization");
            }

            if (this.widget.RequireInitialLoad) {
                Logger.Trace(">>>>>>> Starting Existing Flight Processing");
                foreach (string acc in airportCodes) {
                    ProcessInitialFlights(acc);
                }

                Logger.Trace("<<<<<<< Finished Existing Flight Processing");
            }

            if (widget.ReInitPeriod != 0) {
                Logger.Trace(">>>>>>> Configuring Flight Refresh Job");
                reInitTimer = new System.Timers.Timer() {
                    AutoReset = true,
                    Interval = widget.ReInitPeriod * 1000 * 60
                };
                reInitTimer.Elapsed += (source, eventArgs) => {
                    reInitTimer.Stop();
                    Logger.Trace(">>>>>>> Running Flight Refresh Job");
                    foreach (string acc in airportCodes) {
                        Task.Run(() => ProcessInitialFlights(acc)).Wait();
                    }
                    Logger.Trace(">>>>>>> Flight Refresh Job Complete");
                    reInitTimer.Start();
                };
                reInitTimer.Start();
                Logger.Trace("<<<<<<<< Flight Refresh Job Configured");
            }

            using (AMSIntegrationServiceClient client = new AMSIntegrationServiceClient(binding, address)) {
                Logger.Trace(">>>>>>> Starting Widget Specific Initialization");
                foreach (string acc in airportCodes) {
                    this.widget.AdditionalInitProcessing(client, acc, amsCodes[acc], Parameters.AMS_REST_SERVICE_URI);
                }

                Logger.Trace("<<<<<<< Completed Widget Specific Notification");
            }

            if (this.widget.RequireListen) {
                Logger.Trace(">>>>>>> Starting Notification Listener");
                StartNotificationListener();
                Logger.Info("<<<<<<< Started Notification Listener");
            }
        }

        public void Stop() {
            Logger.Trace($"{Parameters.APPDISPLAYNAME}   Service Stopping");
            stopProcessing = true;
            startListenLoop = false;
        }

        // Start the thread to listen to incoming update notifications
        public void StartNotificationListener() {
            try {
                this.startListenLoop = true;
                receiveThread = new Thread(this.ListenToQueue) {
                    IsBackground = false
                };
                receiveThread.Start();
            } catch (Exception ex) {
                Logger.Error("Error starting notification queue listener");
                Logger.Error(ex.Message);
            }
        }

        // Listen for incoming notifications
        private void ListenToQueue() {
            MessageQueue recvQueue = new MessageQueue(Parameters.RECVQ);

            //Clear the queue first.
            recvQueue.Purge();

            while (startListenLoop) {
                //Put it in a Try/Catch so on bad message or reading problem dont stop the system
                try {
                    using (Message msg = recvQueue.Receive(new TimeSpan(0, 0, 5))) {
                        string xml;
                        using (StreamReader reader = new StreamReader(msg.BodyStream)) {
                            xml = reader.ReadToEnd();
                        }
                        ProcessMessage(xml);
                    }
                } catch (MessageQueueException e) {
                    // Handle no message arriving in the queue.
                    if (e.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout) {
                        Logger.Trace("No Message in Queue");
                    }
                } catch (Exception e) {
                    Logger.Error($"Error in Reciveving and Processing Notification Message. {e.Message}");
                    Thread.Sleep(Parameters.RESTSERVER_RETRY_INTERVAL);
                }
            }
            Logger.Info("Queue Listener Stopped");
            receiveThread.Abort();
        }

        private void ProcessMessage(string xml) {
            try {
                if (this.widget.NotificationContainsStrings != null) {
                    foreach (string s in this.widget.NotificationContainsStrings) {
                        if (xml.Contains(s)) {
                            Logger.Trace($"Processing Notification Message");
                            Tuple<FlightId, PropertyValue[], string> resultTuple = null;
                            using (AMSIntegrationServiceClient client = new AMSIntegrationServiceClient(binding, address)) {
                                resultTuple = this.widget.ProcessNotification(xml, client, null);
                            }
                            SendUpdate(resultTuple);
                            return;
                        } else {
                            Logger.Trace($"Un handled Notification");
                        }
                    }
                }

                if (this.widget.RequireXmlNode) {
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(xml);
                    XmlNode xmlNode = doc.DocumentElement;
                    Logger.Trace($"Passing XmlNode to Widget for Decision");
                    Tuple<FlightId, PropertyValue[], string> resultTuple = null;
                    using (AMSIntegrationServiceClient client = new AMSIntegrationServiceClient(binding, address)) {
                        resultTuple = this.widget.ProcessNotification(xmlNode, client, null);
                    }
                    SendUpdate(resultTuple);
                    return;
                }

                Logger.Trace($"Ignoring Notification Message");
            } catch (Exception e) {
                Logger.Trace($"Message Processing Error.{e.Message}");
            }
        }

        private void ProcessInitialFlights(string apCode) {
            string amsToken = amsCodes[apCode];

            try {
                using (AMSIntegrationServiceClient client = new AMSIntegrationServiceClient(binding, address)) {
                    try {
                        XmlElement flightsElement = client.GetFlights(amsToken, widget.InitTimeFrom, widget.InitTimeTo, apCode, AirportIdentifierType.IATACode);

                        XmlNamespaceManager nsmgr = new XmlNamespaceManager(flightsElement.OwnerDocument.NameTable);
                        nsmgr.AddNamespace("ams", "http://www.sita.aero/ams6-xml-api-datatypes");

                        XmlNodeList fls = flightsElement.SelectNodes("//ams:Flight", nsmgr);
                        foreach (XmlNode fl in fls) {
                            var resultTuple = widget.ProcessFlight(fl, client, amsToken);
                            if (resultTuple != null) {
                                SendUpdate(resultTuple);
                            }
                        }
                    } catch (Exception e) {
                        Logger.Error(e.Message);
                    }
                }
            } catch (Exception e) {
                Logger.Error(e.Message);
            }
        }

        private void SendUpdate(Tuple<FlightId, PropertyValue[], string> resultTuple) {
            if (resultTuple == null) {
                return;
            }

            Logger.Trace($"Updating Flight {resultTuple.Item1.flightNumberField}");

            try {
                if (resultTuple.Item1 != null) {
                    using (AMSIntegrationServiceClient client = new AMSIntegrationServiceClient(binding, address)) {
                        System.Xml.XmlElement res = client.UpdateFlight(resultTuple.Item3, resultTuple.Item1, resultTuple.Item2);
                        Logger.Trace(res.OuterXml);
                    }
                }
            } catch (Exception e) {
                Logger.Error(e, "Failed to update the custom field");
            }
        }
    }
}