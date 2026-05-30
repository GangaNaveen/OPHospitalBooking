namespace HospitalOPBooking.Models
{
    public class HospitalDashboardViewModel
    {
        public string HospitalName   { get; set; } = string.Empty;
        public int    TotalBookings  { get; set; }   // all-time
        public int    TodayTotal     { get; set; }   // today booked
        public int    TodayDone      { get; set; }   // today marked done
        public int    TodayPending   => TodayTotal - TodayDone;

        public List<DoctorViewModel> Doctors   { get; set; } = new();
        public List<OPQueueItem>     TodayQueue { get; set; } = new();
    }
}
