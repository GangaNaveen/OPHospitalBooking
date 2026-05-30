using System.ComponentModel.DataAnnotations;

namespace HospitalOPBooking.Models
{
    /// <summary>One revisit fee slab for a doctor.</summary>
    public class DoctorFeeRule
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }

        /// <summary>Inclusive lower bound — days since last visit.</summary>
        [Range(0, 9999)]
        public int FromDay { get; set; }

        /// <summary>Inclusive upper bound — days since last visit.</summary>
        [Range(0, 9999)]
        public int ToDay { get; set; }

        [Range(0, 999999)]
        public decimal Fee { get; set; }

        /// <summary>Checks whether a given number of days falls within this slab.</summary>
        public bool Matches(int days) => days >= FromDay && days <= ToDay;
    }
}
