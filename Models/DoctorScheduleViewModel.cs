namespace HospitalOPBooking.Models
{
    /// <summary>One row = one day-of-week schedule for a doctor.</summary>
    public class DaySchedule
    {
        public int    Id          { get; set; }
        public int    DoctorId    { get; set; }
        /// <summary>0=Sunday … 6=Saturday</summary>
        public int    DayOfWeek   { get; set; }
        public string DayName     => System.Globalization.CultureInfo.InvariantCulture
                                        .DateTimeFormat.DayNames[DayOfWeek];
        public bool   IsAvailable { get; set; } = true;

        // stored as "HH:mm" strings for easy form binding
        public string MorningFrom { get; set; } = string.Empty;
        public string MorningTo   { get; set; } = string.Empty;
        public string EveningFrom { get; set; } = string.Empty;
        public string EveningTo   { get; set; } = string.Empty;

        public bool HasMorning => !string.IsNullOrEmpty(MorningFrom) && !string.IsNullOrEmpty(MorningTo);
        public bool HasEvening => !string.IsNullOrEmpty(EveningFrom) && !string.IsNullOrEmpty(EveningTo);
    }

    /// <summary>Full schedule view for one doctor.</summary>
    public class DoctorScheduleViewModel
    {
        public int    DoctorId      { get; set; }
        public string DoctorName    { get; set; } = string.Empty;
        public string Specialization{ get; set; } = string.Empty;
        public int    HospitalId    { get; set; }

        /// <summary>7 entries — one per day of week (Sun–Sat).</summary>
        public List<DaySchedule> WeeklySchedule { get; set; } = new();

        /// <summary>Specific leave / unavailable dates.</summary>
        public List<DoctorLeaveDate> LeaveDates { get; set; } = new();

        /// <summary>New leave date being added via the form.</summary>
        public string NewLeaveDate   { get; set; } = string.Empty;
        public string NewLeaveReason { get; set; } = string.Empty;
    }

    /// <summary>One specific unavailable date for a doctor.</summary>
    public class DoctorLeaveDate
    {
        public int      Id        { get; set; }
        public int      DoctorId  { get; set; }
        public DateTime LeaveDate { get; set; }
        public string   Reason    { get; set; } = string.Empty;
    }

    /// <summary>Availability info returned to booking pages via AJAX.</summary>
    public class DoctorAvailabilityInfo
    {
        public bool   IsAvailable   { get; set; }
        public string Reason        { get; set; } = string.Empty;  // why unavailable
        public string MorningSlot   { get; set; } = string.Empty;  // "10:00 AM – 2:00 PM"
        public string EveningSlot   { get; set; } = string.Empty;  // "3:00 PM – 6:00 PM"
        public bool   HasSchedule   { get; set; }                  // false = no schedule set yet
    }
}
