using HospitalOPBooking.Data;
using HospitalOPBooking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace HospitalOPBooking.Controllers
{
    public class BookingController : Controller
    {
        private readonly DatabaseHelper _db;
        public BookingController(DatabaseHelper db) => _db = db;

        // ── Auth guard ────────────────────────────────────────────────────────
        private int? PatientId
        {
            get
            {
                var email = HttpContext.Session.GetString("UserEmail");
                var role  = HttpContext.Session.GetString("UserRole");
                if (email == null || role != "Patient") return null;
                return GetPatientId(email);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PATIENT DASHBOARD
        // ═══════════════════════════════════════════════════════════════════════
        [HttpGet]
        public IActionResult Dashboard()
        {
            var pid = PatientId;
            if (pid == null) return RedirectToAction("Login", "Account");

            var vm = new PatientDashboardViewModel
            {
                PatientName   = HttpContext.Session.GetString("UserName") ?? "",
                PatientId     = pid.Value,
                Hospitals     = GetHospitalList(pid.Value),
                MyBookings    = GetMyBookings(pid.Value),
                FamilyMembers = GetFamilyMembers(pid.Value)
            };
            return View(vm);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // FAMILY MEMBERS – Add
        // ═══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddFamilyMember([FromBody] FamilyMemberViewModel model)
        {
            var pid = PatientId;
            if (pid == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                // If this is an AJAX/JSON request, return JSON error, else redirect
                if (Request.ContentType != null && Request.ContentType.Contains("application/json"))
                    return Json(new { success = false, message = "Please fill all required fields correctly." });

                TempData["FamilyError"] = "Please fill all required fields correctly.";
                return RedirectToAction("Dashboard");
            }

            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                @"INSERT INTO FamilyMembers (PatientId, Name, Age, Relation, MobileNo)
                  VALUES (@Pid, @Name, @Age, @Rel, @Mobile)", conn);
            cmd.Parameters.AddWithValue("@Pid",    pid.Value);
            cmd.Parameters.AddWithValue("@Name",   model.Name.Trim());
            cmd.Parameters.AddWithValue("@Age",    model.Age);
            cmd.Parameters.AddWithValue("@Rel",    model.Relation?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@Mobile", model.MobileNo?.Trim() ?? "");
            cmd.ExecuteNonQuery();

            TempData["FamilySuccess"] = $"{model.Name} added to your family members.";

            // If called by AJAX (JSON), return a JSON success response
            if (Request.ContentType != null && Request.ContentType.Contains("application/json"))
                return Json(new { success = true });

            return RedirectToAction("Dashboard");
        }

        [HttpGet]
        public IActionResult GetFamilyMembers()
        {
            var pid = PatientId;
            if (pid == null) return Json(new List<object>());
            var list = GetFamilyMembers(pid.Value);
            var result = list.Select(f => new { id = f.Id, name = f.Name, age = f.Age, relation = f.Relation, mobileNo = f.MobileNo }).ToList();
            return Json(result);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // FAMILY MEMBERS – Edit (AJAX)
        // ═══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditFamilyMember(int id, string name, int age,
                                               string relation, string mobileNo)
        {
            var pid = PatientId;
            if (pid == null) return Json(new { ok = false, msg = "Unauthorized" });
            if (string.IsNullOrWhiteSpace(name))
                return Json(new { ok = false, msg = "Name is required." });

            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                @"UPDATE FamilyMembers
                  SET Name=@Name, Age=@Age, Relation=@Rel, MobileNo=@Mobile
                  WHERE Id=@Id AND PatientId=@Pid", conn);
            cmd.Parameters.AddWithValue("@Name",   name.Trim());
            cmd.Parameters.AddWithValue("@Age",    age);
            cmd.Parameters.AddWithValue("@Rel",    relation?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@Mobile", mobileNo?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@Id",     id);
            cmd.Parameters.AddWithValue("@Pid",    pid.Value);
            int rows = cmd.ExecuteNonQuery();
            return Json(new { ok = rows > 0, msg = rows > 0 ? "Saved" : "Not found" });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // FAMILY MEMBERS – Delete
        // ═══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteFamilyMember(int id)
        {
            var pid = PatientId;
            if (pid == null) return Json(new { ok = false });

            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                "DELETE FROM FamilyMembers WHERE Id=@Id AND PatientId=@Pid", conn);
            cmd.Parameters.AddWithValue("@Id",  id);
            cmd.Parameters.AddWithValue("@Pid", pid.Value);
            return Json(new { ok = cmd.ExecuteNonQuery() > 0 });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BOOK – GET
        // ═══════════════════════════════════════════════════════════════════════
        [HttpGet]
        public IActionResult Book(int hospitalId)
        {
            var pid = PatientId;
            if (pid == null) return RedirectToAction("Login", "Account");

            var hospital = GetHospitalById(hospitalId);
            if (hospital == null) return NotFound();

            return View(new BookingViewModel
            {
                HospitalId    = hospitalId,
                HospitalName  = hospital.HospitalName,
                BookingDate   = DateTime.Today,
                FamilyMembers = GetFamilyMembers(pid.Value),
                BookingForName = HttpContext.Session.GetString("UserName") ?? "",
            });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BOOK – POST
        // ═══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Book(BookingViewModel model)
        {
            var pid = PatientId;
            if (pid == null) return RedirectToAction("Login", "Account");

            // Remove non-validated fields
            ModelState.Remove("HospitalName");
            ModelState.Remove("BookingForName");
            ModelState.Remove("FamilyMembers");
            ModelState.Remove("ExistingOPCount");

            if (model.BookingDate.Date < DateTime.Today)
                ModelState.AddModelError("BookingDate", "Booking date cannot be in the past.");

            if (!ModelState.IsValid)
            {
                model.HospitalName    = GetHospitalById(model.HospitalId)?.HospitalName ?? "";
                model.FamilyMembers   = GetFamilyMembers(pid.Value);
                model.ExistingOPCount = GetOPCount(model.HospitalId, model.DoctorName, model.BookingDate);
                return View(model);
            }

            // ── Server-side availability check ────────────────────────────────
            int? doctorId = _db.GetDoctorIdByName(model.HospitalId, model.DoctorName);
            if (doctorId != null)
            {
                var svc   = HttpContext.RequestServices.GetRequiredService<ScheduleController>();
                var avail = svc.GetDoctorAvailability(doctorId.Value, model.BookingDate);
                if (!avail.IsAvailable)
                {
                    var reason = string.IsNullOrEmpty(avail.Reason)
                        ? "not available on this date"
                        : avail.Reason;
                    ModelState.AddModelError("BookingDate",
                        $"Dr. {model.DoctorName} is {reason}. Please choose a different date.");
                    model.HospitalName    = GetHospitalById(model.HospitalId)?.HospitalName ?? "";
                    model.FamilyMembers   = GetFamilyMembers(pid.Value);
                    model.ExistingOPCount = GetOPCount(model.HospitalId, model.DoctorName, model.BookingDate);
                    return View(model);
                }
            }
            else
            {
                // Doctor not found in Doctors table — block booking
                ModelState.AddModelError("DoctorName", "Selected doctor is not available.");
                model.HospitalName  = GetHospitalById(model.HospitalId)?.HospitalName ?? "";
                model.FamilyMembers = GetFamilyMembers(pid.Value);
                return View(model);
            }

            // Resolve who the booking is for
            string bookingForName;

            if (model.FamilyMemberId > 0)
            {
                var fm = GetFamilyMemberById(model.FamilyMemberId, pid.Value);
                if (fm == null)
                {
                    ModelState.AddModelError("", "Selected family member not found.");
                    model.HospitalName  = GetHospitalById(model.HospitalId)?.HospitalName ?? "";
                    model.FamilyMembers = GetFamilyMembers(pid.Value);
                    return View(model);
                }
                bookingForName = fm.Name;
            }
            else
            {
                bookingForName = HttpContext.Session.GetString("UserName") ?? "";
            }

            // Duplicate check — same account + same family member (or self) + same doctor + date
            if (AlreadyBooked(pid.Value, model.FamilyMemberId, model.HospitalId,
                              model.DoctorName, model.BookingDate))
            {
                var who = model.FamilyMemberId > 0 ? bookingForName : "you";
                ModelState.AddModelError("",
                    $"A booking already exists for {who} with this doctor on the selected date.");
                model.HospitalName    = GetHospitalById(model.HospitalId)?.HospitalName ?? "";
                model.FamilyMembers   = GetFamilyMembers(pid.Value);
                model.ExistingOPCount = GetOPCount(model.HospitalId, model.DoctorName, model.BookingDate);
                return View(model);
            }

            // ── Server-side fee calculation and validation ────────────────────────
            decimal consultationFee = 0;
            string feeCategory = "New Patient";

            // Recalculate fee on server to prevent tampering
            using (var conn = _db.GetConnection())
            {
                conn.Open();

                // Get doctor id + NewPatientFee
                int? doctorIdForFee = null;
                decimal newPatientFee = 0;
                using (var cmd = new SqlCommand(
                    "SELECT Id, NewPatientFee FROM Doctors WHERE HospitalId=@Hid AND Name=@Name AND IsActive=1", conn))
                {
                    cmd.Parameters.AddWithValue("@Hid", model.HospitalId);
                    cmd.Parameters.AddWithValue("@Name", model.DoctorName);
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        doctorIdForFee = r.GetInt32(0);
                        newPatientFee = r.GetDecimal(1);
                    }
                }

                if (doctorIdForFee != null)
                {
                    // Load dynamic fee rules
                    var rules = new List<DoctorFeeRule>();
                    using (var cmd = new SqlCommand(
                        "SELECT FromDay, ToDay, Fee FROM DoctorFeeRules WHERE DoctorId=@Did ORDER BY FromDay", conn))
                    {
                        cmd.Parameters.AddWithValue("@Did", doctorIdForFee.Value);
                        using var r = cmd.ExecuteReader();
                        while (r.Read())
                            rules.Add(new DoctorFeeRule { FromDay = r.GetInt32(0), ToDay = r.GetInt32(1), Fee = r.GetDecimal(2) });
                    }

                    var doc = new DoctorViewModel { NewPatientFee = newPatientFee, FeeRules = rules };

                    // Get last visit date for fee calculation
                    DateTime? lastVisit = null;
                    var lastVisitSql = model.FamilyMemberId > 0
                        ? @"SELECT MAX(BookingDate) FROM OPBookings
                            WHERE HospitalId=@Hid AND DoctorName=@Doc
                              AND PatientId=@Pid AND FamilyMemberId=@Fid AND Status='Done'"
                        : @"SELECT MAX(BookingDate) FROM OPBookings
                            WHERE HospitalId=@Hid AND DoctorName=@Doc
                              AND PatientId=@Pid AND (FamilyMemberId IS NULL OR FamilyMemberId=0)
                              AND Status='Done'";

                    using (var cmd = new SqlCommand(lastVisitSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Hid", model.HospitalId);
                        cmd.Parameters.AddWithValue("@Doc", model.DoctorName);
                        cmd.Parameters.AddWithValue("@Pid", pid.Value);
                        if (model.FamilyMemberId > 0)
                            cmd.Parameters.AddWithValue("@Fid", model.FamilyMemberId);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            lastVisit = Convert.ToDateTime(result);
                    }

                    // Calculate fee based on rules
                    consultationFee = doc.CalculateFee(lastVisit, model.BookingDate);
                    feeCategory = doc.GetFeeLabel(lastVisit, model.BookingDate);
                }
            }

            int token     = GetOPCount(model.HospitalId, model.DoctorName, model.BookingDate) + 1;
            int bookingId = SaveBooking(pid.Value, model.HospitalId, model.DoctorName,
                                        model.BookingDate, token,
                                        model.FamilyMemberId > 0 ? model.FamilyMemberId : null,
                                        bookingForName, consultationFee, feeCategory);

            return RedirectToAction("Confirm", new { id = bookingId });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CONFIRM
        // ═══════════════════════════════════════════════════════════════════════
        [HttpGet]
        public IActionResult Confirm(int id)
        {
            if (PatientId == null) return RedirectToAction("Login", "Account");
            var vm = GetBookingById(id);
            if (vm == null) return NotFound();
            return View(vm);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AJAX
        // ═══════════════════════════════════════════════════════════════════════
        [HttpGet]
        public IActionResult GetDoctors(int hospitalId)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                "SELECT Name, Specialization FROM Doctors WHERE HospitalId=@Hid AND IsActive=1 ORDER BY Name", conn);
            cmd.Parameters.AddWithValue("@Hid", hospitalId);
            using var r   = cmd.ExecuteReader();
            var list = new List<object>();
            while (r.Read())
                list.Add(new { name = r.GetString(0), specialization = r.GetString(1) });
            return Json(list);
        }

        [HttpGet]
        public IActionResult GetOPCountAjax(int hospitalId, string doctorName, string date)
        {
            if (!DateTime.TryParse(date, out var d))
                return Json(new { count = 0, next = 1 });
            int count = GetOPCount(hospitalId, doctorName, d);
            return Json(new { count, next = count + 1 });
        }

        // ── AJAX: doctor availability (schedule) — called by patient booking page ──
        [HttpGet]
        public IActionResult GetDoctorAvailability(int hospitalId, string doctorName, string date)
        {
            if (!DateTime.TryParse(date, out var d))
                return Json(new { isAvailable = false, hasSchedule = false,
                                  morningSlot = "", eveningSlot = "",
                                  reason = "Invalid date" });

            int? doctorId = _db.GetDoctorIdByName(hospitalId, doctorName);
            if (doctorId == null)
                return Json(new { isAvailable = false, hasSchedule = false,
                                  morningSlot = "", eveningSlot = "",
                                  reason = "Doctor not found" });

            var svc  = HttpContext.RequestServices.GetRequiredService<ScheduleController>();
            var info = svc.GetDoctorAvailability(doctorId.Value, d);
            return Json(new
            {
                isAvailable = info.IsAvailable,
                hasSchedule = info.HasSchedule,
                morningSlot = info.MorningSlot,
                eveningSlot = info.EveningSlot,
                reason      = info.Reason
            });
        }

        // ── AJAX: calculate consultation fee for patient-side booking ─────────
        [HttpGet]
        public IActionResult GetConsultationFee(int hospitalId, string doctorName,
                                                  int familyMemberId, string bookingDate)
        {
            var pid = PatientId;
            if (pid == null) return Json(new { fee = 0m, label = "New Patient", isNewPatient = true });

            if (!DateTime.TryParse(bookingDate, out var bDate))
                bDate = DateTime.Today;

            using var conn = _db.GetConnection(); conn.Open();

            // Get doctor id + NewPatientFee
            int? doctorId = null;
            decimal newPatientFee = 0;
            using (var cmd = new SqlCommand(
                "SELECT Id, NewPatientFee FROM Doctors WHERE HospitalId=@Hid AND Name=@Name AND IsActive=1", conn))
            {
                cmd.Parameters.AddWithValue("@Hid", hospitalId);
                cmd.Parameters.AddWithValue("@Name", doctorName);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return Json(new { fee = 0m, label = "Doctor not found", isNewPatient = true });
                doctorId = r.GetInt32(0);
                newPatientFee = r.GetDecimal(1);
            }

            // Load dynamic fee rules
            var rules = new List<DoctorFeeRule>();
            using (var cmd = new SqlCommand(
                "SELECT FromDay, ToDay, Fee FROM DoctorFeeRules WHERE DoctorId=@Did ORDER BY FromDay", conn))
            {
                cmd.Parameters.AddWithValue("@Did", doctorId.Value);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    rules.Add(new DoctorFeeRule { FromDay = r.GetInt32(0), ToDay = r.GetInt32(1), Fee = r.GetDecimal(2) });
            }

            var doc = new DoctorViewModel { NewPatientFee = newPatientFee, FeeRules = rules };

            // Get last visit date
            DateTime? lastVisit = null;
            var lastVisitSql = familyMemberId > 0
                ? @"SELECT MAX(BookingDate) FROM OPBookings
                    WHERE HospitalId=@Hid AND DoctorName=@Doc
                      AND PatientId=@Pid AND FamilyMemberId=@Fid AND Status='Done'"
                : @"SELECT MAX(BookingDate) FROM OPBookings
                    WHERE HospitalId=@Hid AND DoctorName=@Doc
                      AND PatientId=@Pid AND (FamilyMemberId IS NULL OR FamilyMemberId=0)
                      AND Status='Done'";

            using (var cmd = new SqlCommand(lastVisitSql, conn))
            {
                cmd.Parameters.AddWithValue("@Hid", hospitalId);
                cmd.Parameters.AddWithValue("@Doc", doctorName);
                cmd.Parameters.AddWithValue("@Pid", pid.Value);
                if (familyMemberId > 0)
                    cmd.Parameters.AddWithValue("@Fid", familyMemberId);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    lastVisit = Convert.ToDateTime(result);
            }

            var fee = doc.CalculateFee(lastVisit, bDate);
            var label = doc.GetFeeLabel(lastVisit, bDate);

            // Calculate days since last visit
            int? daysSinceLastVisit = null;
            string lastVisitDateFormatted = null;
            if (lastVisit.HasValue)
            {
                daysSinceLastVisit = (bDate.Date - lastVisit.Value.Date).Days;
                lastVisitDateFormatted = lastVisit.Value.ToString("dd MMM yyyy");
            }

            return Json(new
            {
                fee,
                label,
                lastVisitDate = lastVisitDateFormatted,
                daysSinceLastVisit = daysSinceLastVisit,
                isNewPatient = !lastVisit.HasValue
            });
        }

        // ═════════════════════════════════════════════════════════════════════
        // DB HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private int? GetPatientId(string email)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand("SELECT Id FROM Patients WHERE Email=@E", conn);
            cmd.Parameters.AddWithValue("@E", email);
            var r = cmd.ExecuteScalar();
            return r == null ? null : (int?)Convert.ToInt32(r);
        }

        private List<FamilyMemberViewModel> GetFamilyMembers(int patientId)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                "SELECT Id, PatientId, Name, Age, Relation, MobileNo FROM FamilyMembers WHERE PatientId=@Pid ORDER BY Name", conn);
            cmd.Parameters.AddWithValue("@Pid", patientId);
            using var r   = cmd.ExecuteReader();
            var list = new List<FamilyMemberViewModel>();
            while (r.Read())
                list.Add(new FamilyMemberViewModel
                {
                    Id        = r.GetInt32(0),
                    PatientId = r.GetInt32(1),
                    Name      = r.GetString(2),
                    Age       = r.GetInt32(3),
                    Relation  = r.GetString(4),
                    MobileNo  = r.GetString(5)
                });
            return list;
        }

        private FamilyMemberViewModel? GetFamilyMemberById(int id, int patientId)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                "SELECT Id, PatientId, Name, Age, Relation, MobileNo FROM FamilyMembers WHERE Id=@Id AND PatientId=@Pid", conn);
            cmd.Parameters.AddWithValue("@Id",  id);
            cmd.Parameters.AddWithValue("@Pid", patientId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new FamilyMemberViewModel
            {
                Id        = r.GetInt32(0),
                PatientId = r.GetInt32(1),
                Name      = r.GetString(2),
                Age       = r.GetInt32(3),
                Relation  = r.GetString(4),
                MobileNo  = r.GetString(5)
            };
        }

        private List<HospitalListItem> GetHospitalList(int patientId)
        {
            using var conn = _db.GetConnection(); conn.Open();

            var bookedIds = new HashSet<int>();
            using (var cmd = new SqlCommand(
                "SELECT DISTINCT HospitalId FROM OPBookings WHERE PatientId=@Pid", conn))
            {
                cmd.Parameters.AddWithValue("@Pid", patientId);
                using var r = cmd.ExecuteReader();
                while (r.Read()) bookedIds.Add(r.GetInt32(0));
            }

            var todayCounts = new Dictionary<int, int>();
            using (var cmd = new SqlCommand(
                @"SELECT HospitalId, COUNT(*) FROM OPBookings
                  WHERE CAST(BookingDate AS DATE)=CAST(GETDATE() AS DATE)
                  GROUP BY HospitalId", conn))
            {
                using var r = cmd.ExecuteReader();
                while (r.Read()) todayCounts[r.GetInt32(0)] = r.GetInt32(1);
            }

            var list = new List<HospitalListItem>();
            using (var cmd = new SqlCommand(
                @"SELECT h.Id, h.HospitalName, h.DoctorName, h.Address, h.MobileNumber,
                         ISNULL(d.NewPatientFee, 0) AS NewPatientFee
                  FROM Hospitals h
                  LEFT JOIN Doctors d ON d.HospitalId = h.Id AND d.Name = h.DoctorName AND d.IsActive = 1
                  ORDER BY h.HospitalName", conn))
            {
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    int hid = r.GetInt32(0);
                    list.Add(new HospitalListItem
                    {
                        Id                = hid,
                        HospitalName      = r.GetString(1),
                        DoctorName        = r.GetString(2),
                        Address           = r.GetString(3),
                        MobileNumber      = r.GetString(4),
                        PreviouslyBooked  = bookedIds.Contains(hid),
                        TodayBookingCount = todayCounts.TryGetValue(hid, out var c) ? c : 0,
                        NewPatientFee     = r.GetDecimal(5)
                    });
                }
            }

            return list.OrderByDescending(h => h.PreviouslyBooked)
                       .ThenBy(h => h.HospitalName)
                       .ToList();
        }

        private HospitalListItem? GetHospitalById(int id)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                "SELECT Id, HospitalName, DoctorName, Address, MobileNumber FROM Hospitals WHERE Id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new HospitalListItem
            {
                Id           = r.GetInt32(0),
                HospitalName = r.GetString(1),
                DoctorName   = r.GetString(2),
                Address      = r.GetString(3),
                MobileNumber = r.GetString(4)
            };
        }

        private int GetOPCount(int hospitalId, string doctorName, DateTime date)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                @"SELECT COUNT(*) FROM OPBookings
                  WHERE HospitalId=@Hid AND DoctorName=@Doc
                    AND CAST(BookingDate AS DATE)=CAST(@Date AS DATE)", conn);
            cmd.Parameters.AddWithValue("@Hid",  hospitalId);
            cmd.Parameters.AddWithValue("@Doc",  doctorName);
            cmd.Parameters.AddWithValue("@Date", date.Date);
            return (int)cmd.ExecuteScalar()!;
        }

        private bool AlreadyBooked(int patientId, int familyMemberId,
                                    int hospitalId, string doctorName, DateTime date)
        {
            using var conn = _db.GetConnection(); conn.Open();
            // For self: FamilyMemberId IS NULL; for family: FamilyMemberId = id
            var sql = familyMemberId > 0
                ? @"SELECT COUNT(*) FROM OPBookings
                    WHERE PatientId=@Pid AND FamilyMemberId=@Fid
                      AND HospitalId=@Hid AND DoctorName=@Doc
                      AND CAST(BookingDate AS DATE)=CAST(@Date AS DATE)"
                : @"SELECT COUNT(*) FROM OPBookings
                    WHERE PatientId=@Pid AND (FamilyMemberId IS NULL OR FamilyMemberId=0)
                      AND HospitalId=@Hid AND DoctorName=@Doc
                      AND CAST(BookingDate AS DATE)=CAST(@Date AS DATE)";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Pid",  patientId);
            if (familyMemberId > 0)
                cmd.Parameters.AddWithValue("@Fid", familyMemberId);
            cmd.Parameters.AddWithValue("@Hid",  hospitalId);
            cmd.Parameters.AddWithValue("@Doc",  doctorName);
            cmd.Parameters.AddWithValue("@Date", date.Date);
            return (int)cmd.ExecuteScalar()! > 0;
        }

        private int SaveBooking(int patientId, int hospitalId, string doctorName,
                                DateTime date, int token, int? familyMemberId, string bookingForName,
                                decimal consultationFee = 0, string feeCategory = "")
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                @"INSERT INTO OPBookings
                    (PatientId, HospitalId, DoctorName, BookingDate, TokenNumber,
                     BookedByHospital, FamilyMemberId, BookingForName, ConsultationFee, FeeCategory)
                  OUTPUT INSERTED.Id
                  VALUES (@Pid, @Hid, @Doc, @Date, @Token, 0, @Fid, @ForName, @Fee, @FeeCategory)", conn);
            cmd.Parameters.AddWithValue("@Pid",     patientId);
            cmd.Parameters.AddWithValue("@Hid",     hospitalId);
            cmd.Parameters.AddWithValue("@Doc",     doctorName);
            cmd.Parameters.AddWithValue("@Date",    date.Date);
            cmd.Parameters.AddWithValue("@Token",   token);
            cmd.Parameters.AddWithValue("@Fid",     familyMemberId.HasValue ? (object)familyMemberId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ForName", bookingForName);
            cmd.Parameters.AddWithValue("@Fee",     consultationFee);
            cmd.Parameters.AddWithValue("@FeeCategory", feeCategory ?? "");
            return (int)cmd.ExecuteScalar()!;
        }

        private BookingConfirmViewModel? GetBookingById(int id)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                @"SELECT b.Id, h.HospitalName, b.DoctorName, b.BookingDate,
                         b.TokenNumber, p.Name, b.Status, b.BookingForName
                  FROM OPBookings b
                  JOIN Hospitals h ON h.Id = b.HospitalId
                  JOIN Patients  p ON p.Id = b.PatientId
                  WHERE b.Id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            var bookingForName = r.GetString(7);
            return new BookingConfirmViewModel
            {
                BookingId      = r.GetInt32(0),
                HospitalName   = r.GetString(1),
                DoctorName     = r.GetString(2),
                BookingDate    = r.GetDateTime(3),
                TokenNumber    = r.GetInt32(4),
                PatientName    = r.GetString(5),
                Status         = r.GetString(6),
                BookingForName = string.IsNullOrEmpty(bookingForName) ? r.GetString(5) : bookingForName
            };
        }

        private List<PatientBookingSummary> GetMyBookings(int patientId)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                @"SELECT b.Id, h.HospitalName, b.DoctorName, b.BookingDate,
                         b.TokenNumber, p.Name, b.Status, b.BookingForName,
                         (SELECT COUNT(*) FROM OPBookings x
                          WHERE x.HospitalId = b.HospitalId
                            AND x.DoctorName = b.DoctorName
                            AND CAST(x.BookingDate AS DATE) = CAST(b.BookingDate AS DATE)
                            AND x.TokenNumber < b.TokenNumber
                            AND x.Status = 'Done') AS DoneBeforeMe
                  FROM OPBookings b
                  JOIN Hospitals h ON h.Id = b.HospitalId
                  JOIN Patients  p ON p.Id = b.PatientId
                  WHERE b.PatientId=@Pid
                  ORDER BY b.BookingDate DESC, b.TokenNumber", conn);
            cmd.Parameters.AddWithValue("@Pid", patientId);
            using var r   = cmd.ExecuteReader();
            var list = new List<PatientBookingSummary>();
            while (r.Read())
            {
                var bookingForName = r.GetString(7);
                var patientName    = r.GetString(5);
                list.Add(new PatientBookingSummary
                {
                    BookingId      = r.GetInt32(0),
                    HospitalName   = r.GetString(1),
                    DoctorName     = r.GetString(2),
                    BookingDate    = r.GetDateTime(3),
                    TokenNumber    = r.GetInt32(4),
                    PatientName    = patientName,
                    BookingForName = string.IsNullOrEmpty(bookingForName) ? patientName : bookingForName,
                    Status         = r.GetString(6),
                    DoneBeforeMe   = r.GetInt32(8)
                });
            }
            return list;
        }
    }
}
