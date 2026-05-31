using System.ComponentModel.DataAnnotations;

namespace HospitalOPBooking.Models
{
    /// <summary>Used when a hospital books an OP on behalf of a patient.</summary>
    public class HospitalBookingViewModel
    {
        public int    HospitalId   { get; set; }
        public string HospitalName { get; set; } = string.Empty;

        // Only DoctorName and BookingDate are always required — keep their attributes.
        [Required(ErrorMessage = "Please select a doctor")]
        [Display(Name = "Doctor")]
        public string DoctorName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a date")]
        [DataType(DataType.Date)]
        [Display(Name = "Appointment Date")]
        public DateTime BookingDate { get; set; } = DateTime.Today;

        // ── Patient lookup (existing) ─────────────────────────────────────────
        // No [Required] — validated manually in controller based on IsNewPatient
        [Display(Name = "Patient Mobile / Email")]
        public string PatientIdentifier { get; set; } = string.Empty;

        public int?   ResolvedPatientId   { get; set; }
        public string ResolvedPatientName { get; set; } = string.Empty;

        // ── New patient inline registration ───────────────────────────────────
        // No data-annotation validators — all validated manually in controller
        public bool   IsNewPatient      { get; set; }
        public string NewPatientName    { get; set; } = string.Empty;
        public int?   NewPatientAge     { get; set; }
        public string NewPatientMobile  { get; set; } = string.Empty;
        public string NewPatientAddress { get; set; } = string.Empty;
        public string NewPatientEmail   { get; set; } = string.Empty;   // optional

        // ── Fee info (display only — not validated) ───────────────────────────
        public decimal NewPatientFee { get; set; }
        public decimal OldPatientFee { get; set; }

        public int ExistingOPCount { get; set; }

        /// <summary>When hospital selects a specific family member for booking, its Id goes here (0 = self).</summary>
        public int FamilyMemberId { get; set; } = 0;

        // ── Consultation Fee (calculated and saved) ───────────────────────────
        /// <summary>Calculated consultation fee based on doctor's fee rules and patient history.</summary>
        public decimal ConsultationFee { get; set; }

        /// <summary>Fee category/label (e.g., "New Patient", "Revisit (5d) — 0–7 days").</summary>
        public string FeeCategory { get; set; } = string.Empty;
    }
}
