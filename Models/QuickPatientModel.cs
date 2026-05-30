namespace HospitalOPBooking.Models
{
    public class QuickPatientModel
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string? MobileNo { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
    }
}
