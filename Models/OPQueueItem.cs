namespace HospitalOPBooking.Models
{
    public class OPQueueItem
    {
        public int    BookingId        { get; set; }
        public int    TokenNumber      { get; set; }
        public string PatientName      { get; set; } = string.Empty;
        public string PatientMobile    { get; set; } = string.Empty;
        public string DoctorName       { get; set; } = string.Empty;
        public string Status           { get; set; } = "Confirmed";
        public bool   BookedByHospital { get; set; }
        /// <summary>Actual person being seen — may differ from account holder when booked for family.</summary>
        public string BookingForName   { get; set; } = string.Empty;
        public bool   IsDone           => Status == "Done";
    }
}
