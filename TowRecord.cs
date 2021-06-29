using System;

namespace AMSWidgetCore {

    public class TowRecord {
        public DateTime? scheduledStart;
        public DateTime? scheduledEnd;
        public DateTime? actualStart;
        public DateTime? actualEnd;
        public string fromStand;
        public string toStand;

        public TowRecord(string f, string t, string ss, string se, string acs, string ace) {
            fromStand = f;
            toStand = t;

            try {
                scheduledStart = DateTime.Parse(ss);
            } catch (Exception) {
                scheduledStart = null;
            }

            try {
                scheduledEnd = DateTime.Parse(se);
            } catch (Exception) {
                scheduledEnd = null;
            }

            try {
                actualStart = DateTime.Parse(acs);
            } catch (Exception) {
                actualStart = null;
            }

            try {
                actualEnd = DateTime.Parse(ace);
            } catch (Exception) {
                actualEnd = null;
            }
        }

        public override string ToString() {
            return $"From: {fromStand}, To: {toStand}, Actual Start: {actualStart}, Actual End: {actualEnd}";
        }
    }
}