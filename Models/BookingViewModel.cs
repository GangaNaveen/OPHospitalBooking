using System.ComponentModel.DataAnnotations;

namespace HospitalOPBooking.Models
{
    public class BookingViewModel
    {
        [Required]
        public int HospitalId { get; set; }

        public string HospitalName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a doctor")]
        [Display(Name = "Doctor")]
        public string DoctorName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a date")]
        [DataType(DataType.Date)]
        [Display(Name = "Appointment Date")]
        public DateTime BookingDate { get; set; } = DateTime.Today;

        /// <summary>0 = self, positive int = FamilyMember.Id</summary>
        public int FamilyMemberId { get; set; } = 0;

        /// <summary>Resolved display name for the booking (self or family member).</summary>
        public string BookingForName { get; set; } = string.Empty;

        /// <summary>Family members of the logged-in patient (for the selector).</summary>
        public List<FamilyMemberViewModel> FamilyMembers { get; set; } = new();

        public int ExistingOPCount { get; set; }
        public int NextToken => ExistingOPCount + 1;
    }
}
