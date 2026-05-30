namespace HospitalOPBooking.Models
{
    public class HospitalListItem
    {
        public int    Id           { get; set; }
        public string HospitalName { get; set; } = string.Empty;
        public string DoctorName   { get; set; } = string.Empty;
        public string Address      { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        /// <summary>True when this patient has previously booked this hospital.</summary>
        public bool   PreviouslyBooked { get; set; }
        /// <summary>Total OP bookings for today across all doctors in this hospital.</summary>
        public int    TodayBookingCount { get; set; }
    }
}
