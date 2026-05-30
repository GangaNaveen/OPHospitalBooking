using System.ComponentModel.DataAnnotations;

namespace HospitalOPBooking.Models
{
    public class FamilyMemberViewModel
    {
        public int Id { get; set; }
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Age is required")]
        [Range(1, 120, ErrorMessage = "Age must be between 1 and 120")]
        public int Age { get; set; }

        [StringLength(50)]
        [Display(Name = "Relation")]
        public string Relation { get; set; } = string.Empty;

        [Display(Name = "Mobile Number")]
        [RegularExpression(@"^[6-9]\d{9}$|^$", ErrorMessage = "Enter a valid 10-digit mobile number")]
        public string MobileNo { get; set; } = string.Empty;
    }
}
