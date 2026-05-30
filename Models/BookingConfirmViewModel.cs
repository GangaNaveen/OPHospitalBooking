namespace HospitalOPBooking.Models
{
    public class BookingConfirmViewModel
    {
        public int      BookingId      { get; set; }
        public string   HospitalName   { get; set; } = string.Empty;
        public string   DoctorName     { get; set; } = string.Empty;
        public DateTime BookingDate    { get; set; }
        public int      TokenNumber    { get; set; }
        public string   PatientName    { get; set; } = string.Empty;
        /// <summary>The actual person the booking is for (self or family member).</summary>
        public string   BookingForName { get; set; } = string.Empty;
        public string   Status         { get; set; } = "Confirmed";
        public bool     IsDone         => Status == "Done";
    }
}
