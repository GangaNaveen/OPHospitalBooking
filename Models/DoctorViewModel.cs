using System.ComponentModel.DataAnnotations;

namespace HospitalOPBooking.Models
{
    public class DoctorViewModel
    {
        public int Id { get; set; }
        public int HospitalId { get; set; }

        [Required(ErrorMessage = "Doctor name is required")]
        [StringLength(100)]
        [Display(Name = "Doctor Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(150)]
        [Display(Name = "Specialization")]
        public string Specialization { get; set; } = string.Empty;

        [Range(0, 999999, ErrorMessage = "Enter a valid fee")]
        [Display(Name = "New Patient Fee (₹)")]
        public decimal NewPatientFee { get; set; }

        // Kept for backward-compat DB reads only
        public decimal OldPatientFee { get; set; }
        public decimal Revisit0to7Fee { get; set; }
        public decimal Revisit8to15Fee { get; set; }
        public decimal Revisit16to30Fee { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>Dynamic revisit fee rules for this doctor.</summary>
        public List<DoctorFeeRule> FeeRules { get; set; } = new();

        // ── Fee calculation ───────────────────────────────────────────────────

        /// <summary>
        /// Calculates the applicable fee based on the patient's last visit date.
        /// Uses dynamic FeeRules if available; falls back to fixed columns for
        /// backward compatibility.
        /// </summary>
        public decimal CalculateFee(DateTime? lastVisitDate, DateTime bookingDate)
        {
            if (lastVisitDate == null) return NewPatientFee;

            int days = (bookingDate.Date - lastVisitDate.Value.Date).Days;

            // Dynamic rules take priority
            if (FeeRules.Any())
            {
                var match = FeeRules.FirstOrDefault(r => r.Matches(days));
                return match != null ? match.Fee : NewPatientFee;
            }

            // Fallback to fixed columns (legacy)
            if (days <= 7)  return Revisit0to7Fee;
            if (days <= 15) return Revisit8to15Fee;
            if (days <= 30) return Revisit16to30Fee;
            return NewPatientFee;
        }

        /// <summary>Returns a human-readable label for the applicable fee tier.</summary>
        public string GetFeeLabel(DateTime? lastVisitDate, DateTime bookingDate)
        {
            if (lastVisitDate == null) return "New Patient";

            int days = (bookingDate.Date - lastVisitDate.Value.Date).Days;

            if (FeeRules.Any())
            {
                var match = FeeRules.FirstOrDefault(r => r.Matches(days));
                return match != null
                    ? $"Revisit ({days}d) — {match.FromDay}–{match.ToDay} days"
                    : $"New Patient (last visit {days}d ago, no matching rule)";
            }

            // Fallback
            if (days <= 7)  return $"Revisit ({days}d) — 0–7 days";
            if (days <= 15) return $"Revisit ({days}d) — 8–15 days";
            if (days <= 30) return $"Revisit ({days}d) — 16–30 days";
            return $"New Patient (last visit {days}d ago)";
        }

        // Static version for callers that don't have a loaded DoctorViewModel
        public static string FeeLabel(DateTime? lastVisitDate, DateTime bookingDate)
        {
            if (lastVisitDate == null) return "New Patient";
            int days = (bookingDate.Date - lastVisitDate.Value.Date).Days;
            return $"Revisit ({days}d)";
        }
    }
}
