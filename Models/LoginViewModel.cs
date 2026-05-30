using System.ComponentModel.DataAnnotations;

namespace HospitalOPBooking.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email or Mobile Number is required")]
        [Display(Name = "Email / Mobile Number")]
        public string EmailOrMobile { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }
    }
}
