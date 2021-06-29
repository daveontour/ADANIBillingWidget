using AMSWidgetBase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using WorkBridge.Modules.AMS.AMSIntegrationAPI.Mod.Intf.DataTypes;

namespace AMSWidgetCore {

    public class IntervalRecord {
        public string comment;
        public int minutes;
        public DateTime from;
        public DateTime to;

        //public bool isDay = false;
        //public bool isNight = false;
        public bool isExtendedDay = false;

        public bool isExtendedNight = false;
        public bool isFree = false;
        public bool isFirstTwoNight = false;
        public bool isFirstTwoDay = false;
        public string tzstring;

        public IntervalRecord(DateTime from, DateTime to, string comment, string tzstring) {
            this.from = from;
            this.to = to;
            this.comment = comment;
            this.tzstring = tzstring;

            TimeSpan ts = to - from;

            this.minutes = Convert.ToInt32(ts.TotalMinutes);
        }

        public override string ToString() {
            string cat = null;
            // if (isNormal) cat = "Normal";
            if (isExtendedNight) cat = "Extended Night";
            if (isExtendedDay) cat = "Extended Day";
            if (isFree) cat = "Free";
            if (isFirstTwoNight) cat = "FirstTwo Night";
            if (isFirstTwoDay) cat = "FirstTwo Day";

            return $"{ GetTimeStr(@from, tzstring)} -> { GetTimeStr(@to, tzstring)} ({minutes}). {cat}, Comments: {comment} ";
        }

        private DateTime ConvertToAirportTime(DateTime t, string tzStr) {
            TimeZoneInfo tz = CreatTimeZoneInfo(tzStr);
            return TimeZoneInfo.ConvertTimeFromUtc(t.ToUniversalTime(), tz);
        }

        private TimeZoneInfo CreatTimeZoneInfo(string tzStr) {
            string sign = tzStr.Substring(0, 1);
            string hourStr = tzStr.Substring(1, 2);
            string minuteStr = tzStr.Substring(4, 2);
            string displayName = tzStr;

            int hour = Int32.Parse(hourStr);
            int minute = Int32.Parse(minuteStr);

            if (sign == "-") {
                hour = hour * -1;
                minute = minute * -1;
            }

            TimeSpan offset = new TimeSpan(hour, minute, 00);
            TimeZoneInfo tz = TimeZoneInfo.CreateCustomTimeZone(tzStr, offset, tzStr, tzStr);

            return tz;
        }

        private string GetTimeStr(DateTime t, TimeZoneInfo tz) {
            string ss = t.ToString("yyyy-MM-ddTHH:mm:ss");
            return ss;
        }

        private string GetTimeStr(DateTime t, string tzStr) {
            TimeZoneInfo tz = CreatTimeZoneInfo(tzStr);
            return GetTimeStr(t, tz);
        }
    }

    internal enum CalculationType {
        RoundUP,
        RoundDOWN,
        Round,
        Minutes,
        DecimalHours
    }

    internal class FlightBillingRecord {
        public FlightNode flt;

        private int totalFreeTime = 0;

        private int totalNormalDayTime = 0;
        private int totalNormalNightTime = 0;

        private int totalDayExtendedTime = 0;
        private int totalNightExtendedTime = 0;

        //private double totalFirstTwoDayHours = 0;
        //private double totalFirstTwoNightHours = 0;

        private double totalFreeHours;
        private double totalNormalDayHours;
        private double totalNormalNightHours;
        private double totalDayExtendedHours;
        private double totalNightExtendedHours;

        private double billingDayNormal;
        private double billingNightNormal;
        private double billingDayExtended;
        private double billingNightExtended;

        private readonly int START_NIGHT_HOUR;
        private readonly int START_NIGHT_MINUTE;

        private readonly int START_DAY_HOUR;
        private readonly int START_DAY_MINUTE;

        private readonly int DEPARTURE_FREE_TIME;
        private readonly int ARRIVAL_FREE_TIME;

        private readonly string DAY_CUSTOMFIELD;
        private readonly string NIGHT_CUSTOMFIELD;
        private readonly string EXTENDED_DAY_CUSTOMFIELD;
        private readonly string EXTENDED_NIGHT_CUSTOMFIELD;
        private readonly string DURATIONREQUIRED_CUSTOMFIELD;
        public readonly string AMSTOKEN;
        public readonly string TZSTRING;

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly bool inValid = false;

        private CalculationType calcType;

        public List<IntervalRecord> intervals = new List<IntervalRecord>();

        public FlightBillingRecord(FlightNode flt) {
            this.flt = flt;

            string apCode = flt.apCode;
            if (apCode == null) {
                inValid = true;
                logger.Warn("Airport code for flight not present");
                return;
            }
            XmlDocument doc = new XmlDocument();
            doc.Load("widget.config");
            XmlNode root = doc.DocumentElement;
            XmlNode apNode = root.SelectSingleNode($".//airport[airportCode[contains(text(), \"{apCode}\")]]");

            if (apNode == null) {
                logger.Warn($"Airport code \"{apCode}\" for flight not configured to be handled in \"widget.config\"  ");
                inValid = true;
                return;
            }

            DAY_CUSTOMFIELD = apNode.SelectSingleNode("./normalCustomField")?.InnerText;
            NIGHT_CUSTOMFIELD = apNode.SelectSingleNode("./nightCustomField")?.InnerText;
            EXTENDED_DAY_CUSTOMFIELD = apNode.SelectSingleNode("./extendedCustomField")?.InnerText;
            EXTENDED_NIGHT_CUSTOMFIELD = apNode.SelectSingleNode("./extendedNightCustomField")?.InnerText;
            DURATIONREQUIRED_CUSTOMFIELD = apNode.SelectSingleNode("./durationRequiredCustomField")?.InnerText;
            AMSTOKEN = apNode.SelectSingleNode("./token")?.InnerText;
            TZSTRING = apNode.SelectSingleNode("./airportTimeZone")?.InnerText;

            if (DAY_CUSTOMFIELD == null || NIGHT_CUSTOMFIELD == null || EXTENDED_DAY_CUSTOMFIELD == null || DURATIONREQUIRED_CUSTOMFIELD == null || AMSTOKEN == null) {
                inValid = true;
                logger.Warn("Custom field names for billing types not set in \"widget.config\" ");
                return;
            }

            START_NIGHT_HOUR = Int32.Parse(apNode.SelectSingleNode("./startNightHour").InnerText);
            START_NIGHT_MINUTE = Int32.Parse(apNode.SelectSingleNode("./startNightMinute").InnerText);

            START_DAY_HOUR = Int32.Parse(apNode.SelectSingleNode("./startDayHour").InnerText);
            START_DAY_MINUTE = Int32.Parse(apNode.SelectSingleNode("./startDayMinute").InnerText);

            DEPARTURE_FREE_TIME = Int32.Parse(apNode.SelectSingleNode("./departureFreeTime").InnerText);
            ARRIVAL_FREE_TIME = Int32.Parse(apNode.SelectSingleNode("./arrivalFreeTime").InnerText);

            switch (apNode.SelectSingleNode("./timeCalculation").InnerText) {
                case "RoundUP":
                    calcType = CalculationType.RoundUP;
                    break;

                case "RoundDOWN":
                    calcType = CalculationType.RoundDOWN;
                    break;

                case "Round":
                    calcType = CalculationType.Round;
                    break;

                case "Minutes":
                    calcType = CalculationType.Minutes;
                    break;

                case "DecimalHours":
                    calcType = CalculationType.DecimalHours;
                    break;

                default:
                    calcType = CalculationType.Minutes;
                    break;
            }

            string AirportTZ = apNode.SelectSingleNode("./airportTimeZone").InnerText;

            flt.actualArrival = ConvertToAirportTime(flt.actualArrival.Value, AirportTZ);
            flt.actualDeparture = ConvertToAirportTime(flt.actualDeparture.Value, AirportTZ);

            // Version 1.1
            //foreach (StandSlot slot in flt.standSlots) {
            //    slot.startTime = ConvertToAirportTime(slot.startTime, AirportTZ);
            //    slot.endTime = ConvertToAirportTime(slot.endTime, AirportTZ);
            //}
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\nStart ====>\n");
            sb.AppendLine(flt.ToString());
            sb.AppendLine("\n\nInterval Records:");
            foreach (IntervalRecord rec in intervals) {
                sb.AppendLine(rec.ToString());
            }

            sb.AppendLine();
            sb.AppendLine("Summary:");
            sb.AppendLine($"Total Free Time:{totalFreeTime}\nTotal First Two Hours Day:{totalNormalDayTime}\nTotal First Two Hours Night:{totalNormalNightTime}\nTotal Day Extended Time:{totalDayExtendedTime}\nTotal Night Extended Time:{totalNightExtendedTime}\n");

            if (calcType == CalculationType.RoundUP) {
                sb.AppendLine("Calculation type = RoundUP");
            }
            if (calcType == CalculationType.RoundDOWN) {
                sb.AppendLine("Calculation type = RoundDOWN");
            }
            if (calcType == CalculationType.Round) {
                sb.AppendLine("Calculation type = Round (normal mathematical rules)");
            }
            if (calcType == CalculationType.Minutes) {
                sb.AppendLine("Calculation type = Minutes Only");
            }

            if (calcType == CalculationType.DecimalHours) {
                sb.AppendLine("Calculation type = Decimal Hours");
            }
            sb.AppendLine($"Total Free Hours:{totalFreeHours}\nTotal First Two Hours Day Hours:{totalNormalDayHours}\nTotal First Two Hours Night Hours:{totalNormalNightHours}\nTotal Day Extended Hours:{totalDayExtendedHours}\nTotal Night Extended Hours:{totalNightExtendedHours}");

            sb.AppendLine("\nBilling Values:");
            sb.AppendLine($"Billing Day Normal:{billingDayNormal}\nBilling Night Normal:{billingNightNormal}\nBilling Day Extended:{billingDayExtended}\nBilling Night Extended:{billingNightExtended}");

            sb.AppendLine("<==== End\n");
            return sb.ToString();
        }

        public Tuple<FlightId, PropertyValue[], string> GetFieldsForReset() {
            PropertyValue pvNormal = new PropertyValue {
                propertyNameField = DAY_CUSTOMFIELD,
                valueField = ""
            };
            PropertyValue pvNight = new PropertyValue {
                propertyNameField = NIGHT_CUSTOMFIELD,
                valueField = ""
            };
            PropertyValue pvExtended = new PropertyValue {
                propertyNameField = EXTENDED_DAY_CUSTOMFIELD,
                valueField = ""
            };
            PropertyValue pvExtendedNight = new PropertyValue {
                propertyNameField = EXTENDED_NIGHT_CUSTOMFIELD,
                valueField = ""
            };

            PropertyValue pv = new PropertyValue {
                propertyNameField = DURATIONREQUIRED_CUSTOMFIELD,
                valueField = "false"
            };

            PropertyValue[] values = new PropertyValue[] { pvNormal, pvNight, pvExtended, pvExtendedNight, pv };
            FlightId fltID = flt.GetFlightId();

            return new Tuple<FlightId, PropertyValue[], string>(fltID, values, AMSTOKEN);
        }

        internal Tuple<FlightId, PropertyValue[], string> GetFieldsForUpdate() {
            if (inValid) {
                return null;
            }
            PropertyValue pvNormal = new PropertyValue {
                propertyNameField = DAY_CUSTOMFIELD,
                valueField = billingDayNormal.ToString()
            };
            PropertyValue pvNight = new PropertyValue {
                propertyNameField = NIGHT_CUSTOMFIELD,
                valueField = billingNightNormal.ToString()
            };
            PropertyValue pvExtended = new PropertyValue {
                propertyNameField = EXTENDED_DAY_CUSTOMFIELD,
                valueField = billingDayExtended.ToString()
            };

            PropertyValue pvExtendedNight = new PropertyValue {
                propertyNameField = EXTENDED_NIGHT_CUSTOMFIELD,
                valueField = billingNightExtended.ToString()
            };

            PropertyValue pv = new PropertyValue {
                propertyNameField = DURATIONREQUIRED_CUSTOMFIELD,
                valueField = "false"
            };

            PropertyValue[] values = new PropertyValue[] { pvNormal, pvNight, pvExtended, pvExtendedNight, pv };
            FlightId fltID = flt.GetFlightId();

            return new Tuple<FlightId, PropertyValue[], string>(fltID, values, AMSTOKEN);
        }

        internal string Calculate() {
            if (inValid) {
                return "INVALID";
            }

            intervals.Clear();
            TimeSpan totalTimeOnGround = flt.actualDeparture.Value - flt.actualArrival.Value;
            DateTime departureFreeTimeStart = flt.actualDeparture.Value.AddMinutes(-DEPARTURE_FREE_TIME);
            DateTime arrivalFreeTimeEnd = flt.actualArrival.Value.AddMinutes(ARRIVAL_FREE_TIME);
            DateTime cursorTime;

            // Duration less than arrival free time
            if (totalTimeOnGround.TotalMinutes <= ARRIVAL_FREE_TIME) {
                IntervalRecord rec = new IntervalRecord(flt.actualArrival.Value, flt.actualDeparture.Value, $"Portion of Initial {ARRIVAL_FREE_TIME} minutes Free", TZSTRING);
                intervals.Add(rec);
                return "Total Duration Less Than Arrival Free Time";
            }

            // Duration less than arrival free time + departure free time
            if (totalTimeOnGround.TotalMinutes <= ARRIVAL_FREE_TIME + DEPARTURE_FREE_TIME) {
                IntervalRecord rec = new IntervalRecord(flt.actualArrival.Value, arrivalFreeTimeEnd, $"Initial {ARRIVAL_FREE_TIME} minutes Free", TZSTRING);
                intervals.Add(rec);
                IntervalRecord rec2 = new IntervalRecord(arrivalFreeTimeEnd, flt.actualDeparture.Value, $"Departure {ARRIVAL_FREE_TIME} minutes Free", TZSTRING);
                intervals.Add(rec2);
                return "Total Duration Less Than Arrival Free Time + Departure Free Time";
            }

            // If we reach here, then the duration id longer than the accumulated free time allowance (arrival and departure)
            // so start calculation with the initial free time and first two hours

            // The initial arrival free time allowance
            IntervalRecord initialFree = new IntervalRecord(flt.actualArrival.Value, arrivalFreeTimeEnd, $"Initial {ARRIVAL_FREE_TIME} minutes Free", TZSTRING) { isFree = true };
            intervals.Add(initialFree);

            DateTime startFirstTwoHours = arrivalFreeTimeEnd;
            DateTime endFirstTwoHours = startFirstTwoHours.AddMinutes(120);
            Tuple<IntervalRecord, IntervalRecord> firstTwoSplit;
            IntervalRecord finalFree;

            // The end of the first two chargeable hours may be after the beginning of the departure free time
            if (departureFreeTimeStart < endFirstTwoHours) {
                DateTime to = endFirstTwoHours;
                if (departureFreeTimeStart < to) {
                    to = departureFreeTimeStart;
                }

                firstTwoSplit = GetDayNightAllocations(startFirstTwoHours, to);
                if (firstTwoSplit.Item1 != null) {
                    firstTwoSplit.Item1.comment = "First 2 hour portion";
                    //FirstTwoNomalise(firstTwoSplit.Item1);
                    intervals.Add(firstTwoSplit.Item1);
                }
                if (firstTwoSplit.Item2 != null) {
                    firstTwoSplit.Item2.comment = "First 2 hour portion";
                    //FirstTwoNomalise(firstTwoSplit.Item2);
                    intervals.Add(firstTwoSplit.Item2);
                }
                finalFree = new IntervalRecord(departureFreeTimeStart, flt.actualDeparture.Value, $"Final {DEPARTURE_FREE_TIME} minutes Free", TZSTRING) { isFree = true };
                intervals.Add(finalFree);
                CalcBillingTotals();
                return "End of First Two Chargeable Hours Is After Start of Pre Departure Free Time";
            }

            firstTwoSplit = GetDayNightAllocations(startFirstTwoHours, endFirstTwoHours);
            if (firstTwoSplit.Item1 != null) {
                firstTwoSplit.Item1.comment = "First 2 hour portion";
                //FirstTwoNomalise(firstTwoSplit.Item1);
                intervals.Add(firstTwoSplit.Item1);
            }
            if (firstTwoSplit.Item2 != null) {
                firstTwoSplit.Item2.comment = "First 2 hour portion";
                //FirstTwoNomalise(firstTwoSplit.Item2);
                intervals.Add(firstTwoSplit.Item2);
            }

            cursorTime = endFirstTwoHours;

            // If there is only one slot, then this will be skipped
            //Version 1.1
            //for (int slotIndex = 0; slotIndex < flt.standSlots.Count - 1; slotIndex++) {
            //    StandSlot slot = flt.standSlots[slotIndex];
            //    StandSlot nextslot = flt.standSlots[slotIndex + 1];
            //    // May have been towed to another stand withing initial period
            //    if (cursorTime > slot.endTime) {
            //        continue;
            //    }
            //    DateTime startSlotTime = slot.startTime;
            //    DateTime endSlotTime = nextslot.startTime;

            //    while (cursorTime < endSlotTime) {
            //        cursorTime = ProcessNextInterval(cursorTime, endSlotTime);
            //    }
            //}

            // Processing for a single slot and for the final slot
            while (cursorTime < departureFreeTimeStart) {
                cursorTime = ProcessNextInterval(cursorTime, departureFreeTimeStart);
            }

            finalFree = new IntervalRecord(departureFreeTimeStart, flt.actualDeparture.Value, $"Final {DEPARTURE_FREE_TIME} minutes Free", TZSTRING) { isFree = true };
            intervals.Add(finalFree);

            CalcBillingTotals();

            return "Complete";
        }

        private void CalcBillingTotals() {
            foreach (IntervalRecord rec in intervals) {
                totalFreeTime = rec.isFree ? totalFreeTime + rec.minutes : totalFreeTime;
                totalNormalDayTime = rec.isFirstTwoDay ? totalNormalDayTime + rec.minutes : totalNormalDayTime;
                totalNormalNightTime = rec.isFirstTwoNight ? totalNormalNightTime + rec.minutes : totalNormalNightTime;

                totalNightExtendedTime = rec.isExtendedNight ? totalNightExtendedTime + rec.minutes : totalNightExtendedTime;
                totalDayExtendedTime = rec.isExtendedDay ? totalDayExtendedTime + rec.minutes : totalDayExtendedTime;
            }

            /*
             *  Change to manage the new handling of the first two hour split request on June 1 2021
             */

            if (totalNormalDayTime > 60 && totalNormalNightTime < 60) {
                totalNormalNightTime = 0;
            }

            if (totalNormalDayTime < 60 && totalNormalNightTime > 60) {
                totalNormalNightTime = 60;
            }

            //  End June 1 2021 changes

            if (calcType == CalculationType.RoundUP) {
                totalFreeHours = Convert.ToInt32(Math.Ceiling((totalFreeTime / 60.0)));

                totalNormalDayHours = Convert.ToInt32(Math.Ceiling(((totalNormalDayTime) / 60.0)));
                totalNormalNightHours = Convert.ToInt32(Math.Ceiling(((totalNormalNightTime) / 60.0)));

                totalDayExtendedHours = Convert.ToInt32(Math.Ceiling((totalDayExtendedTime / 60.0)));
                totalNightExtendedHours = Convert.ToInt32(Math.Ceiling((totalNightExtendedTime / 60.0)));
            }
            if (calcType == CalculationType.RoundDOWN) {
                totalFreeHours = Convert.ToInt32(Math.Floor((totalFreeTime / 60.0)));

                totalNormalDayHours = Convert.ToInt32(Math.Floor(((totalNormalDayTime) / 60.0)));
                totalNormalNightHours = Convert.ToInt32(Math.Floor(((totalNormalNightTime) / 60.0)));

                totalDayExtendedHours = Convert.ToInt32(Math.Floor((totalDayExtendedTime / 60.0)));
                totalNightExtendedHours = Convert.ToInt32(Math.Floor((totalNightExtendedTime / 60.0)));
            }
            if (calcType == CalculationType.Round) {
                totalFreeHours = Convert.ToInt32(Math.Round((totalFreeTime / 60.0)));

                totalNormalDayHours = Convert.ToInt32(Math.Round(((totalNormalDayTime) / 60.0)));
                totalNormalNightHours = Convert.ToInt32(Math.Round(((totalNormalNightTime) / 60.0)));

                totalDayExtendedHours = Convert.ToInt32(Math.Round((totalDayExtendedTime / 60.0)));
                totalNightExtendedHours = Convert.ToInt32(Math.Round((totalNightExtendedTime / 60.0)));
            }
            if (calcType == CalculationType.Minutes) {
                totalFreeHours = totalFreeTime;
                totalNormalDayHours = totalNormalDayTime;
                totalDayExtendedHours = totalDayExtendedTime;
                totalNormalNightHours = totalNormalNightTime;
                totalNightExtendedHours = totalNightExtendedTime;
            }

            if (calcType == CalculationType.DecimalHours) {
                totalFreeHours = RoundToSignificantDigits(totalFreeTime / 60.0, 6);

                totalNormalDayHours = RoundToSignificantDigits(totalNormalDayTime / 60.0, 6);
                totalNormalNightHours = RoundToSignificantDigits(totalNormalNightTime / 60.0, 6);

                totalDayExtendedHours = RoundToSignificantDigits(totalDayExtendedTime / 60.0, 6);
                totalNightExtendedHours = RoundToSignificantDigits(totalNightExtendedTime / 60.0, 6);
            }

            billingDayNormal = totalNormalDayHours;
            billingNightNormal = totalNormalNightHours;
            billingDayExtended = totalDayExtendedHours;
            billingNightExtended = totalNightExtendedHours;
        }

        private static double RoundToSignificantDigits(double d, int digits) {
            if (d == 0)
                return 0;

            double scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(d))) + 1);
            return scale * Math.Round(d / scale, digits);
        }

        //private void FirstTwoNomalise(IntervalRecord rec) {
        //    if (rec.isNight) {
        //        rec.isFirstTwoNight = true;
        //        rec.comment = "First 2 hours night portion";
        //        rec.isNight = false;
        //        return;
        //    }
        //    if (rec.isNormal) {
        //        rec.isFirstTwoDay = true;
        //        rec.comment = "First 2 hours day portion";
        //        rec.isNormal = false;
        //        return;
        //    }
        //}

        private DateTime ProcessNextInterval(DateTime cursorTime, DateTime endTime) {
            DateTime nextNight;
            DateTime nextDay;

            if (cursorTime.Hour == START_NIGHT_HOUR && cursorTime.Minute == START_NIGHT_MINUTE) {
                nextNight = cursorTime.AddDays(1);
            } else {
                nextNight = NextStartNightTime(cursorTime);
            }

            if (cursorTime.Hour == START_DAY_HOUR && cursorTime.Minute == START_DAY_MINUTE) {
                nextDay = cursorTime.AddDays(1);
            } else {
                nextDay = NextStartDayTime(cursorTime);
            }

            IntervalRecord rec;
            if (nextNight < nextDay) {
                if (endTime < nextNight) {
                    rec = new IntervalRecord(cursorTime, endTime, $"Day", TZSTRING) { isExtendedDay = true };
                    intervals.Add(rec);
                    return endTime;
                } else {
                    rec = new IntervalRecord(cursorTime, nextNight, $"Day", TZSTRING) { isExtendedDay = true };
                    intervals.Add(rec);
                    return nextNight;
                }
            } else {
                if (endTime < nextDay) {
                    rec = new IntervalRecord(cursorTime, endTime, $"Night", TZSTRING) { isExtendedNight = true };
                    intervals.Add(rec);
                    return endTime;
                } else {
                    rec = new IntervalRecord(cursorTime, nextDay, $"Night", TZSTRING) { isExtendedNight = true };
                    intervals.Add(rec);
                    return nextDay;
                }
            }
        }

        private Tuple<IntervalRecord, IntervalRecord> GetDayNightAllocations(DateTime from, DateTime to) {
            // Day
            if (IsDay(from) && IsDay(to)) {
                IntervalRecord normal = new IntervalRecord(from, to, "First 2 Hour Day", TZSTRING);
                normal.isFirstTwoDay = true;
                return new Tuple<IntervalRecord, IntervalRecord>(normal, null);
            }

            // Night
            if (IsNight(from) && IsNight(to)) {
                IntervalRecord night = new IntervalRecord(from, to, "First 2 Hour Night", TZSTRING);
                night.isFirstTwoNight = true;
                return new Tuple<IntervalRecord, IntervalRecord>(null, night);
            }

            // Split
            if ((IsNight(from) && IsDay(to)) || (IsDay(from) && IsNight(to))) {
                return SplitAllocation(from, to);
            }

            return null;
        }

        private bool IsDay(DateTime t) {
            if (t.Hour > START_DAY_HOUR && t.Hour < START_NIGHT_HOUR) {
                return true;
            }

            if (t.Hour > START_DAY_HOUR && t.Hour == START_NIGHT_HOUR && t.Minute <= START_NIGHT_MINUTE) {
                return true;
            }

            if (t.Hour == START_DAY_HOUR && t.Minute >= START_NIGHT_MINUTE && t.Hour < START_NIGHT_HOUR) {
                return true;
            }

            if (t.Hour == START_DAY_HOUR && t.Minute >= START_NIGHT_MINUTE && t.Hour == START_NIGHT_HOUR && t.Minute <= START_NIGHT_MINUTE) {
                return true;
            }

            return false;
        }

        private bool IsNight(DateTime t) {
            return !IsDay(t);
        }

        private Tuple<IntervalRecord, IntervalRecord> SplitAllocation(DateTime from, DateTime to) {
            if (IsDay(from)) {
                IntervalRecord part1 = new IntervalRecord(from, NextStartNightTime(from), "First 2 Hour Day", TZSTRING) { isFirstTwoDay = true };
                IntervalRecord part2 = new IntervalRecord(NextStartNightTime(from), to, "First 2 Hour Night", TZSTRING) { isFirstTwoNight = true };
                return new Tuple<IntervalRecord, IntervalRecord>(part1, part2);
            } else {
                IntervalRecord part1 = new IntervalRecord(from, NextStartDayTime(from), "First 2 Hour Night", TZSTRING) { isFirstTwoNight = true };
                IntervalRecord part2 = new IntervalRecord(NextStartDayTime(from), to, "First 2 Hour Day", TZSTRING) { isFirstTwoDay = true };
                return new Tuple<IntervalRecord, IntervalRecord>(part1, part2);
            }
        }

        private DateTime NextStartNightTime(DateTime time) {
            if (IsDay(time)) {
                DateTime nextNight = new DateTime(time.Year, time.Month, time.Day, START_NIGHT_HOUR, START_NIGHT_MINUTE, 0, DateTimeKind.Local);
                return nextNight;
            } else {
                DateTime nextNight = new DateTime(time.Year, time.Month, time.Day, START_NIGHT_HOUR, START_NIGHT_MINUTE, 0, DateTimeKind.Local).AddDays(1);
                if (time.Hour > 0 && (time.Hour < START_NIGHT_HOUR || (time.Hour == START_NIGHT_HOUR && time.Minute <= START_NIGHT_MINUTE))) {
                    nextNight = nextNight.AddDays(-1);
                }

                return nextNight;
            }
        }

        private DateTime NextStartDayTime(DateTime time) {
            if (IsNight(time)) {
                DateTime nextDay = new DateTime(time.Year, time.Month, time.Day, START_DAY_HOUR, START_DAY_MINUTE, 0, DateTimeKind.Local);

                // If it is night, but before midnight
                if (time.Hour > START_NIGHT_HOUR || (time.Hour == START_NIGHT_HOUR && time.Minute >= START_NIGHT_MINUTE)) {
                    nextDay = nextDay.AddDays(1);
                }

                return nextDay;
            } else {
                return new DateTime(time.Year, time.Month, time.Day, START_DAY_HOUR, START_DAY_MINUTE, 0, DateTimeKind.Local).AddDays(1);
            }
        }

        internal bool ReadyToProcess() {
            if (this.inValid) {
                logger.Warn("Flight Record Invalid");
                return false;
            }

            if (!flt.actualArrival.HasValue) {
                logger.Warn("Actual Arrival Not Set");
                return false;
            }
            if (!flt.actualDeparture.HasValue) {
                logger.Warn("Actual Departure Not Set");
                return false;
            }

            return true;
        }

        private DateTime ConvertToAirportTime(DateTime t, string tzStr) {
            TimeZoneInfo tz = CreatTimeZoneInfo(tzStr);
            return TimeZoneInfo.ConvertTimeFromUtc(t.ToUniversalTime(), tz);
        }

        private TimeZoneInfo CreatTimeZoneInfo(string tzStr) {
            string sign = tzStr.Substring(0, 1);
            string hourStr = tzStr.Substring(1, 2);
            string minuteStr = tzStr.Substring(4, 2);
            string displayName = tzStr;

            int hour = Int32.Parse(hourStr);
            int minute = Int32.Parse(minuteStr);

            if (sign == "-") {
                hour = hour * -1;
                minute = minute * -1;
            }

            TimeSpan offset = new TimeSpan(hour, minute, 00);
            TimeZoneInfo tz = TimeZoneInfo.CreateCustomTimeZone(tzStr, offset, tzStr, tzStr);

            return tz;
        }

        private string GetTimeStr(DateTime t, TimeZoneInfo tz) {
            DateTime s = TimeZoneInfo.ConvertTimeFromUtc(t.ToUniversalTime(), tz);
            string ss = s.ToString("yyyy-MM-ddThh:mm:ss") + tz.DisplayName;

            return ss;
        }

        private string GetTimeStr(DateTime t, string tzStr) {
            TimeZoneInfo tz = CreatTimeZoneInfo(tzStr);
            return GetTimeStr(t, tz);
        }
    }
}