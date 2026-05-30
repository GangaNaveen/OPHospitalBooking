using HospitalOPBooking.Data;
using HospitalOPBooking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace HospitalOPBooking.Controllers
{
    public class ScheduleController : Controller
    {
        private readonly DatabaseHelper _db;
        public ScheduleController(DatabaseHelper db) => _db = db;

        // ── Auth guard ────────────────────────────────────────────────────────
        private int? HospitalId
        {
            get
            {
                var email = HttpContext.Session.GetString("UserEmail");
                var role  = HttpContext.Session.GetString("UserRole");
                if (email == null || role != "Hospital") return null;
                return GetHospitalIdByEmail(email);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // GET: /Schedule/Doctor/{doctorId}
        // ═══════════════════════════════════════════════════════════════════════
        [HttpGet]
        public IActionResult Doctor(int doctorId)
        {
            var hid = HospitalId;
            if (hid == null) return RedirectToAction("Login", "Account");

            var vm = BuildScheduleViewModel(doctorId, hid.Value);
            if (vm == null) return NotFound();
            return View(vm);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // POST: Save weekly schedule
        // ═══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveWeeklySchedule(int doctorId, List<DaySchedule> schedule)
        {
            var hid = HospitalId;
            if (hid == null) return RedirectToAction("Login", "Account");

            if (!DoctorBelongsToHospital(doctorId, hid.Value))
                return Forbid();

            using var conn = _db.GetConnection(); conn.Open();

            foreach (var day in schedule)
            {
                // Upsert: update if exists, insert if not
                var sql = @"
                    IF EXISTS (SELECT 1 FROM DoctorWeeklySchedule WHERE DoctorId=@Did AND DayOfWeek=@Dow)
                        UPDATE DoctorWeeklySchedule
                        SET IsAvailable=@Avail,
                            MorningFrom=@MF, MorningTo=@MT,
                            EveningFrom=@EF, EveningTo=@ET
                        WHERE DoctorId=@Did AND DayOfWeek=@Dow
                    ELSE
                        INSERT INTO DoctorWeeklySchedule
                            (DoctorId, DayOfWeek, IsAvailable, MorningFrom, MorningTo, EveningFrom, EveningTo)
                        VALUES (@Did, @Dow, @Avail, @MF, @MT, @EF, @ET)";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Did",   doctorId);
                cmd.Parameters.AddWithValue("@Dow",   day.DayOfWeek);
                cmd.Parameters.AddWithValue("@Avail", day.IsAvailable ? 1 : 0);
                cmd.Parameters.AddWithValue("@MF",    ToTimeOrNull(day.MorningFrom));
                cmd.Parameters.AddWithValue("@MT",    ToTimeOrNull(day.MorningTo));
                cmd.Parameters.AddWithValue("@EF",    ToTimeOrNull(day.EveningFrom));
                cmd.Parameters.AddWithValue("@ET",    ToTimeOrNull(day.EveningTo));
                cmd.ExecuteNonQuery();
            }

            TempData["ScheduleSuccess"] = "Weekly schedule saved successfully.";
            return RedirectToAction("Doctor", new { doctorId });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // POST: Add leave date
        // ═══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddLeaveDate(int doctorId, string leaveDate, string reason)
        {
            var hid = HospitalId;
            if (hid == null) return RedirectToAction("Login", "Account");

            if (!DoctorBelongsToHospital(doctorId, hid.Value))
                return Forbid();

            if (!DateTime.TryParse(leaveDate, out var date))
            {
                TempData["ScheduleError"] = "Invalid date.";
                return RedirectToAction("Doctor", new { doctorId });
            }

            using var conn = _db.GetConnection(); conn.Open();
            try
            {
                using var cmd = new SqlCommand(
                    @"IF NOT EXISTS (SELECT 1 FROM DoctorLeaveDate WHERE DoctorId=@Did AND LeaveDate=@Date)
                      INSERT INTO DoctorLeaveDate (DoctorId, LeaveDate, Reason)
                      VALUES (@Did, @Date, @Reason)", conn);
                cmd.Parameters.AddWithValue("@Did",    doctorId);
                cmd.Parameters.AddWithValue("@Date",   date.Date);
                cmd.Parameters.AddWithValue("@Reason", reason?.Trim() ?? "");
                cmd.ExecuteNonQuery();
                TempData["ScheduleSuccess"] = $"Leave date {date:dd MMM yyyy} saved.";
            }
            catch
            {
                TempData["ScheduleError"] = "Could not save leave date.";
            }
            return RedirectToAction("Doctor", new { doctorId });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // POST: Remove leave date
        // ═══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveLeaveDate(int leaveDateId, int doctorId)
        {
            var hid = HospitalId;
            if (hid == null) return Json(new { ok = false });

            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                @"DELETE FROM DoctorLeaveDate
                  WHERE Id=@Id AND DoctorId IN
                    (SELECT Id FROM Doctors WHERE HospitalId=@Hid)", conn);
            cmd.Parameters.AddWithValue("@Id",  leaveDateId);
            cmd.Parameters.AddWithValue("@Hid", hid.Value);
            cmd.ExecuteNonQuery();
            return Json(new { ok = true });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AJAX: Get availability for a doctor on a specific date
        // Called by both patient Book view and hospital BookForPatient view
        // ═══════════════════════════════════════════════════════════════════════
        [HttpGet]
        public IActionResult GetAvailability(int doctorId, string date)
        {
            if (!DateTime.TryParse(date, out var d))
                return Json(new DoctorAvailabilityInfo { IsAvailable = true, HasSchedule = false });

            var info = GetDoctorAvailability(doctorId, d);
            return Json(info);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // DB HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        private int? GetHospitalIdByEmail(string email)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand("SELECT Id FROM Hospitals WHERE Email=@E", conn);
            cmd.Parameters.AddWithValue("@E", email);
            var r = cmd.ExecuteScalar();
            return r == null ? null : (int?)Convert.ToInt32(r);
        }

        private bool DoctorBelongsToHospital(int doctorId, int hospitalId)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                "SELECT COUNT(1) FROM Doctors WHERE Id=@Did AND HospitalId=@Hid", conn);
            cmd.Parameters.AddWithValue("@Did", doctorId);
            cmd.Parameters.AddWithValue("@Hid", hospitalId);
            return (int)cmd.ExecuteScalar()! > 0;
        }

        private DoctorScheduleViewModel? BuildScheduleViewModel(int doctorId, int hospitalId)
        {
            using var conn = _db.GetConnection(); conn.Open();

            // Verify doctor belongs to this hospital
            string? docName = null;
            string  spec    = "";
            using (var cmd = new SqlCommand(
                "SELECT Name, Specialization FROM Doctors WHERE Id=@Did AND HospitalId=@Hid", conn))
            {
                cmd.Parameters.AddWithValue("@Did", doctorId);
                cmd.Parameters.AddWithValue("@Hid", hospitalId);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return null;
                docName = r.GetString(0);
                spec    = r.GetString(1);
            }

            // Load existing weekly schedule
            var scheduleMap = new Dictionary<int, DaySchedule>();
            using (var cmd = new SqlCommand(
                "SELECT Id, DayOfWeek, IsAvailable, MorningFrom, MorningTo, EveningFrom, EveningTo " +
                "FROM DoctorWeeklySchedule WHERE DoctorId=@Did", conn))
            {
                cmd.Parameters.AddWithValue("@Did", doctorId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    int dow = r.GetByte(1);
                    scheduleMap[dow] = new DaySchedule
                    {
                        Id          = r.GetInt32(0),
                        DoctorId    = doctorId,
                        DayOfWeek   = dow,
                        IsAvailable = r.GetBoolean(2),
                        MorningFrom = r.IsDBNull(3) ? "" : ((TimeSpan)r.GetValue(3)).ToString(@"hh\:mm"),
                        MorningTo   = r.IsDBNull(4) ? "" : ((TimeSpan)r.GetValue(4)).ToString(@"hh\:mm"),
                        EveningFrom = r.IsDBNull(5) ? "" : ((TimeSpan)r.GetValue(5)).ToString(@"hh\:mm"),
                        EveningTo   = r.IsDBNull(6) ? "" : ((TimeSpan)r.GetValue(6)).ToString(@"hh\:mm"),
                    };
                }
            }

            // Build 7-day list (Mon first for display: 1,2,3,4,5,6,0)
            var weekOrder = new[] { 1, 2, 3, 4, 5, 6, 0 };
            var weekly = weekOrder.Select(dow =>
                scheduleMap.TryGetValue(dow, out var s) ? s
                : new DaySchedule { DoctorId = doctorId, DayOfWeek = dow, IsAvailable = true }
            ).ToList();

            // Load leave dates
            var leaves = new List<DoctorLeaveDate>();
            using (var cmd = new SqlCommand(
                "SELECT Id, DoctorId, LeaveDate, Reason FROM DoctorLeaveDate " +
                "WHERE DoctorId=@Did AND LeaveDate >= CAST(GETDATE() AS DATE) ORDER BY LeaveDate", conn))
            {
                cmd.Parameters.AddWithValue("@Did", doctorId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    leaves.Add(new DoctorLeaveDate
                    {
                        Id        = r.GetInt32(0),
                        DoctorId  = r.GetInt32(1),
                        LeaveDate = r.GetDateTime(2),
                        Reason    = r.GetString(3)
                    });
            }

            return new DoctorScheduleViewModel
            {
                DoctorId       = doctorId,
                DoctorName     = docName,
                Specialization = spec,
                HospitalId     = hospitalId,
                WeeklySchedule = weekly,
                LeaveDates     = leaves
            };
        }

        internal static DoctorAvailabilityInfo GetDoctorAvailability(
            int doctorId, DateTime date, SqlConnection? existingConn = null)
        {
            // Dead overload — real implementation is the instance method below.
            return new DoctorAvailabilityInfo { IsAvailable = false, HasSchedule = false };
        }

        /// <summary>
        /// Returns availability info for a doctor on a given date.
        /// Rule: if NO schedule row exists for that day-of-week, the doctor is NOT available.
        /// </summary>
        public DoctorAvailabilityInfo GetDoctorAvailability(int doctorId, DateTime date)
        {
            using var conn = _db.GetConnection(); conn.Open();

            // 1. Check specific leave date first
            using (var cmd = new SqlCommand(
                "SELECT Reason FROM DoctorLeaveDate WHERE DoctorId=@Did AND LeaveDate=@Date", conn))
            {
                cmd.Parameters.AddWithValue("@Did",  doctorId);
                cmd.Parameters.AddWithValue("@Date", date.Date);
                var reason = cmd.ExecuteScalar()?.ToString();
                if (reason != null)
                    return new DoctorAvailabilityInfo
                    {
                        IsAvailable = false,
                        Reason      = string.IsNullOrEmpty(reason) ? "Doctor on leave" : reason,
                        HasSchedule = true
                    };
            }

            // 2. Check weekly schedule for this day-of-week
            int dow = (int)date.DayOfWeek; // 0=Sun, 1=Mon … 6=Sat
            using (var cmd = new SqlCommand(
                "SELECT IsAvailable, MorningFrom, MorningTo, EveningFrom, EveningTo " +
                "FROM DoctorWeeklySchedule WHERE DoctorId=@Did AND DayOfWeek=@Dow", conn))
            {
                cmd.Parameters.AddWithValue("@Did", doctorId);
                cmd.Parameters.AddWithValue("@Dow", dow);
                using var r = cmd.ExecuteReader();

                // ── KEY FIX ──────────────────────────────────────────────────
                // No row saved for this day = hospital never configured it
                // → treat as UNAVAILABLE (not open by default).
                if (!r.Read())
                    return new DoctorAvailabilityInfo
                    {
                        IsAvailable = false,
                        Reason      = $"No schedule configured for {date.DayOfWeek}s",
                        HasSchedule = false
                    };

                // Row exists but hospital explicitly marked this day off
                if (!r.GetBoolean(0))
                    return new DoctorAvailabilityInfo
                    {
                        IsAvailable = false,
                        Reason      = $"Not available on {date.DayOfWeek}s",
                        HasSchedule = true
                    };

                // Available — read timing slots
                string morningSlot = "";
                string eveningSlot = "";

                if (!r.IsDBNull(1) && !r.IsDBNull(2))
                {
                    var mf = (TimeSpan)r.GetValue(1);
                    var mt = (TimeSpan)r.GetValue(2);
                    morningSlot = $"{FormatTime(mf)} – {FormatTime(mt)}";
                }
                if (!r.IsDBNull(3) && !r.IsDBNull(4))
                {
                    var ef = (TimeSpan)r.GetValue(3);
                    var et = (TimeSpan)r.GetValue(4);
                    eveningSlot = $"{FormatTime(ef)} – {FormatTime(et)}";
                }

                return new DoctorAvailabilityInfo
                {
                    IsAvailable = true,
                    MorningSlot = morningSlot,
                    EveningSlot = eveningSlot,
                    HasSchedule = true
                };
            }
        }

        private static string FormatTime(TimeSpan t)
        {
            var dt = DateTime.Today.Add(t);
            return dt.ToString("h:mm tt");
        }

        private static object ToTimeOrNull(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return DBNull.Value;
            if (TimeSpan.TryParse(val, out var ts)) return ts;
            return DBNull.Value;
        }
    }
}
