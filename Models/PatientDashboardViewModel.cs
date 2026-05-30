namespace HospitalOPBooking.Models
{
    public class PatientDashboardViewModel
    {
        public string PatientName  { get; set; } = string.Empty;
        public int    PatientId    { get; set; }
        public List<HospitalListItem>      Hospitals     { get; set; } = new();
        public List<PatientBookingSummary> MyBookings    { get; set; } = new();
        public List<FamilyMemberViewModel> FamilyMembers { get; set; } = new();
    }

    public class PatientBookingSummary
    {
        public int      BookingId      { get; set; }
        public string   HospitalName   { get; set; } = string.Empty;
        public string   DoctorName     { get; set; } = string.Empty;
        public DateTime BookingDate    { get; set; }
        public int      TokenNumber    { get; set; }
        public string   PatientName    { get; set; } = string.Empty;
        /// <summary>The actual person the booking is for (self or family member name).</summary>
        public string   BookingForName { get; set; } = string.Empty;
        public string   Status         { get; set; } = "Confirmed";
        public bool     IsDone         => Status == "Done";
        public int      DoneBeforeMe   { get; set; }
    }
}
