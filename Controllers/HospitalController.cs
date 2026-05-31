using HospitalOPBooking.Data;
using HospitalOPBooking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace HospitalOPBooking.Controllers
{
    public class HospitalController : Controller
    {
        private readonly DatabaseHelper _db;
        public HospitalController(DatabaseHelper db) => _db = db;

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
        // DASHBOARD
        // ═══════════════════════════════════════════════════════════════════════
        [HttpGet]
        public IActionResult Dashboard()
        {
            var hid = HospitalId;
            if (hid == null) return RedirectToAction("Login", "Account");
            return View(BuildDashboard(hid.Value));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // DOCTORS – Add
        // ═══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddDoctor(DoctorViewModel model)
        {
            var hid = HospitalId;
            if (hid == null) return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                TempData["DoctorError"] = "Doctor name is required.";
                return RedirectToAction("Dashboard");
            }

            if (DoctorExists(hid.Value, model.Name))
            {
                TempData["DoctorError"] = $"Doctor '{model.Name}' already exists.";
                return RedirectToAction("Dashboard");
            }

            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                @"INSERT INTO Doctors (HospitalId, Name, Specialization, NewPatientFee, OldPatientFee, IsActive)
                  VALUES (@Hid, @Name, @Spec, @NewFee, 0, 1)", conn);
            cmd.Parameters.AddWithValue("@Hid",    hid.Value);
            cmd.Parameters.AddWithValue("@Name",   model.Name.Trim());
            cmd.Parameters.AddWithValue("@Spec",   model.Specialization?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@NewFee", model.NewPatientFee);
            cmd.ExecuteNonQuery();

            TempData["DoctorSuccess"] = $"Dr. {model.Name} added successfully.";
            return RedirectToAction("Dashboard");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // DOCTORS – Edit (AJAX)
        // ═══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditDoctor(int doctorId, string name, string specialization, decimal newPatientFee)
        {
            var hid = HospitalId;
            if (hid == null) return Json(new { ok = false, msg = "Unauthorized" });

            if (string.IsNullOrWhiteSpace(name))
                return Json(new { ok = false, msg = "Name cannot be empty." });

            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                "UPDATE Doctors SET Name=@Name, Specialization=@Spec, NewPatientFee=@NewFee WHERE Id=@Id AND HospitalId=@Hid", conn);
            cmd.Parameters.AddWithValue("@Name",   name.Trim());
            cmd.Parameters.AddWithValue("@Spec",   specialization?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@NewFee", newPatientFee);
            cmd.Parameters.AddWithValue("@Id",     doctorId);
            cmd.Parameters.AddWithValue("@Hid",    hid.Value);
            int rows = cmd.ExecuteNonQuery();

            return Json(new { ok = rows > 0, msg = rows > 0 ? "Saved" : "Not found",
                              name = name.Trim(), spec = specialization?.Trim() ?? "", newFee = newPatientFee });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // DOCTORS – Toggle Active/Inactive
        // ═══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleDoctor(int doctorId)
        {
            var hid = HospitalId;
            if (hid == null) return Json(new { ok = false });

            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                "UPDATE Doctors SET IsActive = CASE WHEN IsActive=1 THEN 0 ELSE 1 END " +
                "OUTPUT INSERTED.IsActive WHERE Id=@Id AND HospitalId=@Hid", conn);
            cmd.Parameters.AddWithValue("@Id",  doctorId);
            cmd.Parameters.AddWithValue("@Hid", hid.Value);
            var result = cmd.ExecuteScalar();
            if (result == null) return Json(new { ok = false });
            return Json(new { ok = true, isActive = Convert.ToBoolean(result) });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // MARK OP DONE
        // ═══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkDone(int bookingId)
        {
            var hid = HospitalId;
            if (hid == null) return Json(new { ok = false });

            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                "UPDATE OPBookings SET Status='Done' WHERE Id=@Id AND HospitalId=@Hid", conn);
            cmd.Parameters.AddWithValue("@Id",  bookingId);
            cmd.Parameters.AddWithValue("@Hid", hid.Value);
            return Json(new { ok = cmd.ExecuteNonQuery() > 0 });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BOOK FOR PATIENT – GET
        // ═══════════════════════════════════════════════════════════════════════
        [HttpGet]
        public IActionResult BookForPatient()
        {
            var hid = HospitalId;
            if (hid == null) return RedirectToAction("Login", "Account");

            return View(new HospitalBookingViewModel
            {
                HospitalId   = hid.Value,
                HospitalName = GetHospitalName(hid.Value),
                BookingDate  = DateTime.Today
            });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BOOK FOR PATIENT – POST
        // ═══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BookForPatient(HospitalBookingViewModel model)
        {
            var hid = HospitalId;
            if (hid == null) return RedirectToAction("Login", "Account");

            model.HospitalId   = hid.Value;
            model.HospitalName = GetHospitalName(hid.Value);

            // ── Step 1: strip every key that we validate manually ─────────────
            // This prevents auto-attribute errors on fields that are conditionally
            // required or have no annotations at all.
            var keysToRemove = new[]
            {
                "PatientIdentifier", "ResolvedPatientId", "ResolvedPatientName",
                "NewPatientName", "NewPatientAge", "NewPatientMobile",
                "NewPatientAddress", "NewPatientEmail",
                "HospitalName", "NewPatientFee", "OldPatientFee", "ExistingOPCount"
            };
            foreach (var key in keysToRemove)
                ModelState.Remove(key);

            // ── Step 2: resolve patient ───────────────────────────────────────
            int?    pid   = null;
            string? pname = null;

            if (model.IsNewPatient)
            {
                // Manual validation for new patient fields
                if (string.IsNullOrWhiteSpace(model.NewPatientName))
                    ModelState.AddModelError("NewPatientName", "Patient name is required.");

                if (model.NewPatientAge == null || model.NewPatientAge < 1 || model.NewPatientAge > 120)
                    ModelState.AddModelError("NewPatientAge", "Enter a valid age (1–120).");

                if (string.IsNullOrWhiteSpace(model.NewPatientMobile) ||
                    !System.Text.RegularExpressions.Regex.IsMatch(
                        model.NewPatientMobile.Trim(), @"^[6-9]\d{9}$"))
                    ModelState.AddModelError("NewPatientMobile", "Enter a valid 10-digit mobile number.");

                if (string.IsNullOrWhiteSpace(model.NewPatientAddress))
                    ModelState.AddModelError("NewPatientAddress", "Address is required.");

                // Validate email only when provided
                if (!string.IsNullOrWhiteSpace(model.NewPatientEmail) &&
                    !new System.ComponentModel.DataAnnotations.EmailAddressAttribute()
                        .IsValid(model.NewPatientEmail))
                    ModelState.AddModelError("NewPatientEmail", "Enter a valid email address.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(model.PatientIdentifier))
                    ModelState.AddModelError("PatientIdentifier",
                        "Patient mobile or email is required.");
            }

            // ── Step 3: check always-required fields (DoctorName, BookingDate) ─
            if (model.BookingDate.Date < DateTime.Today)
                ModelState.AddModelError("BookingDate", "Date cannot be in the past.");

            // ── Step 3b: Validate doctor availability on the selected date ────
            // This checks if the doctor has a schedule configured for that day-of-week
            if (!string.IsNullOrWhiteSpace(model.DoctorName) && model.BookingDate.Date >= DateTime.Today)
            {
                var doctorId = _db.GetDoctorIdByName(hid.Value, model.DoctorName);
                if (doctorId != null)
                {
                    var svc = HttpContext.RequestServices.GetRequiredService<ScheduleController>();
                    var avail = svc.GetDoctorAvailability(doctorId.Value, model.BookingDate);
                    if (!avail.IsAvailable)
                    {
                        string dayName = model.BookingDate.DayOfWeek.ToString();
                        string reason = avail.Reason ?? $"Doctor not available on {dayName}s";
                        ModelState.AddModelError("BookingDate", $"Cannot book: {reason}");
                    }
                }
            }

            // ── Step 4: bail out if anything is invalid ───────────────────────
            if (!ModelState.IsValid)
                return View(model);

            // ── Step 5: resolve / register patient ───────────────────────────
            if (model.IsNewPatient)
            {
                var existing = LookupPatientById(model.NewPatientMobile.Trim());
                if (existing.id != null)
                {
                    pid   = existing.id;
                    pname = existing.name;
                }
                else
                {
                    pid   = RegisterPatientByHospital(model);
                    pname = model.NewPatientName.Trim();
                }
            }
            else
            {
                // Prefer resolved patient id (set by the lookup/quick-add flow).
                if (model.ResolvedPatientId != null && model.ResolvedPatientId > 0)
                {
                    pid = model.ResolvedPatientId;
                    pname = string.IsNullOrWhiteSpace(model.ResolvedPatientName) ? GetPatientNameById(pid.Value) : model.ResolvedPatientName;
                }
                else
                {
                    var found = LookupPatientById(model.PatientIdentifier.Trim());
                    if (found.id == null)
                    {
                        ModelState.AddModelError("PatientIdentifier",
                            "No patient found with that mobile/email. Use 'New Patient' to register.");
                        return View(model);
                    }
                    pid   = found.id;
                    pname = found.name;
                }
            }

            // ── Step 6: duplicate booking check ──────────────────────────────
            if (AlreadyBooked(pid!.Value, hid.Value, model.DoctorName, model.BookingDate, model.FamilyMemberId))
            {
                ModelState.AddModelError("",
                    "This patient already has a booking with this doctor on that date.");
                model.ExistingOPCount = OPCount(hid.Value, model.DoctorName, model.BookingDate);
                return View(model);
            }

            // Resolve booking-for name (self or selected family member)
            string bookingForName = pname!;
            int? familyMemberId = model.FamilyMemberId > 0 ? model.FamilyMemberId : null;
            if (familyMemberId.HasValue)
            {
                var fm = GetFamilyMemberById(familyMemberId.Value, pid.Value);
                if (fm == null)
                {
                    ModelState.AddModelError("", "Selected family member not found.");
                    model.ExistingOPCount = OPCount(hid.Value, model.DoctorName, model.BookingDate);
                    return View(model);
                }
                bookingForName = fm.Name;
            }

            // ── Step 7: Server-side fee calculation and validation ────────────
            decimal consultationFee = 0;
            string feeCategory = "New Patient";

            // Recalculate fee on server to prevent tampering
            using (var conn = _db.GetConnection())
            {
                conn.Open();

                // Get doctor id + NewPatientFee
                int? doctorId = null;
                decimal newPatientFee = 0;
                using (var cmd = new SqlCommand(
                    "SELECT Id, NewPatientFee FROM Doctors WHERE HospitalId=@Hid AND Name=@Name AND IsActive=1", conn))
                {
                    cmd.Parameters.AddWithValue("@Hid", hid.Value);
                    cmd.Parameters.AddWithValue("@Name", model.DoctorName);
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        doctorId = r.GetInt32(0);
                        newPatientFee = r.GetDecimal(1);
                    }
                }

                if (doctorId == null)
                {
                    ModelState.AddModelError("DoctorName", "Doctor not found.");
                    return View(model);
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

                // Get last visit date for fee calculation
                DateTime? lastVisit = null;
                var lastVisitSql = familyMemberId.HasValue && familyMemberId.Value > 0
                    ? @"SELECT MAX(BookingDate) FROM OPBookings
                        WHERE HospitalId=@Hid AND DoctorName=@Doc
                          AND PatientId=@Pid AND FamilyMemberId=@Fid AND Status='Done'"
                    : @"SELECT MAX(BookingDate) FROM OPBookings
                        WHERE HospitalId=@Hid AND DoctorName=@Doc
                          AND PatientId=@Pid AND (FamilyMemberId IS NULL OR FamilyMemberId=0)
                          AND Status='Done'";

                using (var cmd = new SqlCommand(lastVisitSql, conn))
                {
                    cmd.Parameters.AddWithValue("@Hid", hid.Value);
                    cmd.Parameters.AddWithValue("@Doc", model.DoctorName);
                    cmd.Parameters.AddWithValue("@Pid", pid.Value);
                    if (familyMemberId.HasValue && familyMemberId.Value > 0)
                        cmd.Parameters.AddWithValue("@Fid", familyMemberId.Value);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        lastVisit = Convert.ToDateTime(result);
                }

                // Calculate fee based on rules
                consultationFee = doc.CalculateFee(lastVisit, model.BookingDate);
                feeCategory = doc.GetFeeLabel(lastVisit, model.BookingDate);
            }

            // Validate that fee is not zero (unless it's a free consultation)
            if (consultationFee < 0)
            {
                ModelState.AddModelError("", "Invalid consultation fee calculated.");
                return View(model);
            }

            // Optional: Compare with user-submitted fee and allow manual override if within reasonable range
            // For now, we trust the server-calculated fee
            // If you want to allow manual editing, you can compare model.ConsultationFee with consultationFee
            // and accept it if it's within a reasonable range (e.g., ±20%)

            // ── Step 8: save ──────────────────────────────────────────────────
            int token = OPCount(hid.Value, model.DoctorName, model.BookingDate) + 1;
            SaveBooking(pid.Value, hid.Value, model.DoctorName, model.BookingDate,
                        token, familyMemberId, bookingForName, bookedByHospital: true,
                        consultationFee, feeCategory);

            TempData["BookSuccess"] =
                $"Booked for {bookingForName} — Token #{token}, Dr. {model.DoctorName}, " +
                $"{model.BookingDate:dd MMM yyyy} — Fee: ₹{consultationFee:F2}";
            return RedirectToAction("Dashboard");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AJAX ENDPOINTS
        // ═══════════════════════════════════════════════════════════════════════

        [HttpGet]
        public IActionResult LookupPatient(string identifier)
        {
            if (HospitalId == null) return Json(new { found = false });
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                @"SELECT 
                    P.Id, P.Name, P.MobileNo, ISNULL(P.Age, 0) AS Age, 
                    ISNULL(P.Address, '') AS Address,
                    ISNULL(CONVERT(VARCHAR, OB.LastBookingDate, 106), '') AS LastBookingDate
                  FROM Patients P
                  LEFT JOIN (
                    SELECT PatientId, MAX(BookingDate) AS LastBookingDate
                    FROM OPBookings
                    WHERE FamilyMemberId IS NULL OR FamilyMemberId = 0
                    GROUP BY PatientId
                  ) OB ON P.Id = OB.PatientId
                  WHERE Email=@I OR MobileNo=@I", conn);
            cmd.Parameters.AddWithValue("@I", identifier.Trim());
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return Json(new { found = false });
            return Json(new { 
                found = true, 
                id = r.GetInt32(0), 
                name = r.GetString(1), 
                mobile = r.GetString(2),
                age = r.GetInt32(3),
                village = r.GetString(4),  // Address field (village/area)
                lastBookingDate = r.GetString(5)
            });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddFamilyMemberForPatient([FromBody] FamilyMemberViewModel model)
        {
            var hid = HospitalId;
            if (hid == null) return Json(new { success = false, message = "Unauthorized" });

            if (model == null || model.PatientId <= 0 || string.IsNullOrWhiteSpace(model.Name) || model.Age < 1)
                return Json(new { success = false, message = "Invalid data" });

            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                @"INSERT INTO FamilyMembers (PatientId, Name, Age, Relation, MobileNo)
                  OUTPUT INSERTED.Id
                  VALUES (@Pid, @Name, @Age, @Rel, @Mobile)", conn);
            cmd.Parameters.AddWithValue("@Pid",    model.PatientId);
            cmd.Parameters.AddWithValue("@Name",   model.Name.Trim());
            cmd.Parameters.AddWithValue("@Age",    model.Age);
            cmd.Parameters.AddWithValue("@Rel",    model.Relation?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@Mobile", model.MobileNo?.Trim() ?? "");
            var id = (int)cmd.ExecuteScalar()!;

            return Json(new { success = true, id });
        }

        [HttpGet]
        public IActionResult GetFamilyMembers(int patientId)
        {
            using var conn = _db.GetConnection();
            conn.Open();

            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
        SELECT 
            FM.Id,
            FM.Name,
            FM.Age,
            FM.Relation,
            FM.MobileNo,
            ISNULL(CONVERT(VARCHAR, OB.LastBookingDate, 106), '') AS LastBookingDate
        FROM FamilyMembers FM
        LEFT JOIN
        (
            SELECT 
                FamilyMemberId,
                MAX(BookingDate) AS LastBookingDate
            FROM OPBookings
            GROUP BY FamilyMemberId
        ) OB ON FM.Id = OB.FamilyMemberId
        WHERE FM.PatientId = @Pid
        ORDER BY FM.Name
    ", conn);

            cmd.Parameters.AddWithValue("@Pid", patientId);

            using var r = cmd.ExecuteReader();

            var list = new List<object>();

            while (r.Read())
            {
                list.Add(new
                {
                    id = r.GetInt32(0),
                    name = r.GetString(1),
                    age = r.GetInt32(2),
                    relation = r.GetString(3),
                    mobileNo = r.GetString(4),
                    lastBookingDate = r.GetString(5) // empty if no booking
                });
            }

            return Json(list);
        }

        [HttpGet]
        public IActionResult GetPatientDetails(int patientId)
        {
            using var conn = _db.GetConnection();
            conn.Open();

            using var cmd = new SqlCommand(@"
        SELECT 
            P.Id,
            P.Name,
            P.Age,
            P.MobileNo,
            P.Email,
            P.Address,
            ISNULL(CONVERT(VARCHAR, OB.LastBookingDate, 106), '') AS LastBookingDate
        FROM Patients P
        LEFT JOIN
        (
            SELECT 
                PatientId,
                MAX(BookingDate) AS LastBookingDate
            FROM OPBookings
            WHERE FamilyMemberId IS NULL OR FamilyMemberId = 0
            GROUP BY PatientId
        ) OB ON P.Id = OB.PatientId
        WHERE P.Id = @Id
    ", conn);

            cmd.Parameters.AddWithValue("@Id", patientId);

            using var r = cmd.ExecuteReader();

            if (!r.Read())
                return Json(new { found = false });

            return Json(new
            {
                found = true,
                id = r.GetInt32(0),
                name = r.GetString(1),
                age = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
                mobile = r.IsDBNull(3) ? "" : r.GetString(3),
                email = r.IsDBNull(4) ? "" : r.GetString(4),
                address = r.IsDBNull(5) ? "" : r.GetString(5),
                lastBookingDate = r.IsDBNull(6) ? "" : r.GetString(6)
            });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddPatientQuick([FromBody] QuickPatientModel model)
        {
            var hid = HospitalId;
            if (hid == null) return Json(new { success = false, message = "Unauthorized" });

            if (model == null || string.IsNullOrWhiteSpace(model.Name) || model.Age <= 0)
                return Json(new { success = false, message = "Invalid input" });

            // Create a minimal patient record for hospital use. Email must be unique — generate placeholder.
            var hash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());
            string email = model.Email;
            if (string.IsNullOrWhiteSpace(email))
                email = $"hospital_{Guid.NewGuid():N}@noemail.local";
            string mobile = model.MobileNo ?? "";

            using var conn = _db.GetConnection(); conn.Open();
            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                @"INSERT INTO Patients (Name, Age, MobileNo, Address, Email, PasswordHash)
                  OUTPUT INSERTED.Id
                  VALUES (@Name, @Age, @Mobile, @Address, @Email, @Hash)", conn);
            cmd.Parameters.AddWithValue("@Name", model.Name.Trim());
            cmd.Parameters.AddWithValue("@Age", model.Age);
            cmd.Parameters.AddWithValue("@Mobile", mobile);
            cmd.Parameters.AddWithValue("@Address", model.Address ?? "");
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@Hash", hash);
            int newId = (int)cmd.ExecuteScalar()!;

            return Json(new { success = true, id = newId, identifier = email, name = model.Name.Trim() });
        }

        // ── AJAX: get fee rules for a doctor ─────────────────────────────────
        [HttpGet]
        public IActionResult GetFeeRules(int doctorId)
        {
            var hid = HospitalId;
            if (hid == null) return Json(new List<object>());

            // Verify ownership
            if (!DoctorBelongsToHospital(doctorId, hid.Value))
                return Json(new List<object>());

            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                "SELECT Id, FromDay, ToDay, Fee FROM DoctorFeeRules WHERE DoctorId=@Did ORDER BY FromDay", conn);
            cmd.Parameters.AddWithValue("@Did", doctorId);
            using var r   = cmd.ExecuteReader();
            var list = new List<object>();
            while (r.Read())
                list.Add(new { id = r.GetInt32(0), fromDay = r.GetInt32(1), toDay = r.GetInt32(2), fee = r.GetDecimal(3) });
            return Json(list);
        }

        // ── AJAX: save fee rules for a doctor (replaces all existing rules) ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveFeeRules(int doctorId, decimal newPatientFee,
                                           [FromBody] List<DoctorFeeRule>? rules)
        {
            var hid = HospitalId;
            if (hid == null) return Json(new { ok = false, msg = "Unauthorized" });

            if (!DoctorBelongsToHospital(doctorId, hid.Value))
                return Json(new { ok = false, msg = "Doctor not found" });

            rules ??= new List<DoctorFeeRule>();

            // Validate: no overlapping ranges
            for (int i = 0; i < rules.Count; i++)
            {
                var a = rules[i];
                if (a.FromDay > a.ToDay)
                    return Json(new { ok = false, msg = $"Row {i + 1}: From Day must be ≤ To Day." });
                if (a.Fee < 0)
                    return Json(new { ok = false, msg = $"Row {i + 1}: Fee cannot be negative." });

                for (int j = i + 1; j < rules.Count; j++)
                {
                    var b = rules[j];
                    if (a.FromDay <= b.ToDay && b.FromDay <= a.ToDay)
                        return Json(new { ok = false, msg = $"Rows {i + 1} and {j + 1} have overlapping day ranges." });
                }
            }

            using var conn = _db.GetConnection(); conn.Open();
            using var tx   = conn.BeginTransaction();
            try
            {
                // Update NewPatientFee on the doctor
                using (var cmd = new SqlCommand(
                    "UPDATE Doctors SET NewPatientFee=@Fee WHERE Id=@Did AND HospitalId=@Hid", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@Fee", newPatientFee);
                    cmd.Parameters.AddWithValue("@Did", doctorId);
                    cmd.Parameters.AddWithValue("@Hid", hid.Value);
                    cmd.ExecuteNonQuery();
                }

                // Delete existing rules
                using (var cmd = new SqlCommand(
                    "DELETE FROM DoctorFeeRules WHERE DoctorId=@Did", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@Did", doctorId);
                    cmd.ExecuteNonQuery();
                }

                // Insert new rules
                foreach (var rule in rules)
                {
                    using var cmd = new SqlCommand(
                        "INSERT INTO DoctorFeeRules (DoctorId, FromDay, ToDay, Fee) VALUES (@Did, @F, @T, @Fee)",
                        conn, tx);
                    cmd.Parameters.AddWithValue("@Did", doctorId);
                    cmd.Parameters.AddWithValue("@F",   rule.FromDay);
                    cmd.Parameters.AddWithValue("@T",   rule.ToDay);
                    cmd.Parameters.AddWithValue("@Fee", rule.Fee);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                return Json(new { ok = true, msg = "Fee rules saved." });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return Json(new { ok = false, msg = ex.Message });
            }
        }
        // ── AJAX: get active doctors with fee rules ───────────────────────────
        [HttpGet]
        public IActionResult GetActiveDoctors()
        {
            var hid = HospitalId;
            if (hid == null) return Json(new List<object>());

            using var conn = _db.GetConnection(); conn.Open();

            // Load doctors
            var doctors = new List<(int id, string name, string spec, decimal newFee)>();
            using (var cmd = new SqlCommand(
                "SELECT Id, Name, Specialization, NewPatientFee FROM Doctors WHERE HospitalId=@Hid AND IsActive=1 ORDER BY Name", conn))
            {
                cmd.Parameters.AddWithValue("@Hid", hid.Value);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    doctors.Add((r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetDecimal(3)));
            }

            // Load all fee rules for this hospital's doctors in one query
            var ruleMap = new Dictionary<int, List<object>>();
            if (doctors.Any())
            {
                var ids = string.Join(",", doctors.Select(d => d.id));
                using var cmd = new SqlCommand(
                    $"SELECT DoctorId, FromDay, ToDay, Fee FROM DoctorFeeRules WHERE DoctorId IN ({ids}) ORDER BY DoctorId, FromDay", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    int did = r.GetInt32(0);
                    if (!ruleMap.ContainsKey(did)) ruleMap[did] = new List<object>();
                    ruleMap[did].Add(new { fromDay = r.GetInt32(1), toDay = r.GetInt32(2), fee = r.GetDecimal(3) });
                }
            }

            var list = doctors.Select(d => new
            {
                name           = d.name,
                specialization = d.spec,
                newPatientFee  = d.newFee,
                feeRules       = ruleMap.TryGetValue(d.id, out var rules) ? rules : new List<object>()
            }).ToList<object>();

            return Json(list);
        }

        // ── AJAX: calculate consultation fee for a patient+doctor combination ──
        [HttpGet]
        public IActionResult GetConsultationFee(string doctorName, int patientId,
                                                  int familyMemberId, string bookingDate)
        {
            var hid = HospitalId;
            if (hid == null) return Json(new { fee = 0m, label = "New Patient" });

            if (!DateTime.TryParse(bookingDate, out var bDate))
                bDate = DateTime.Today;

            using var conn = _db.GetConnection(); conn.Open();

            // Get doctor id + NewPatientFee
            int? doctorId = null;
            decimal newPatientFee = 0;
            using (var cmd = new SqlCommand(
                "SELECT Id, NewPatientFee FROM Doctors WHERE HospitalId=@Hid AND Name=@Name AND IsActive=1", conn))
            {
                cmd.Parameters.AddWithValue("@Hid",  hid.Value);
                cmd.Parameters.AddWithValue("@Name", doctorName);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return Json(new { fee = 0m, label = "Doctor not found" });
                doctorId     = r.GetInt32(0);
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
                cmd.Parameters.AddWithValue("@Hid", hid.Value);
                cmd.Parameters.AddWithValue("@Doc", doctorName);
                cmd.Parameters.AddWithValue("@Pid", patientId);
                if (familyMemberId > 0)
                    cmd.Parameters.AddWithValue("@Fid", familyMemberId);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    lastVisit = Convert.ToDateTime(result);
            }

            var fee   = doc.CalculateFee(lastVisit, bDate);
            var label = doc.GetFeeLabel(lastVisit, bDate);
            
            // Calculate days since last visit
            int? daysSinceLastVisit = null;
            string lastVisitDateFormatted = null;
            if (lastVisit.HasValue)
            {
                daysSinceLastVisit = (bDate.Date - lastVisit.Value.Date).Days;
                lastVisitDateFormatted = lastVisit.Value.ToString("dd MMM yyyy");
            }
            
            return Json(new {
                fee,
                label,
                lastVisitDate = lastVisitDateFormatted,
                daysSinceLastVisit = daysSinceLastVisit,
                isNewPatient = !lastVisit.HasValue
            });
        }

        [HttpGet]
        public IActionResult GetOPCount(string doctorName, string date)
        {
            var hid = HospitalId;
            if (hid == null || !DateTime.TryParse(date, out var d))
                return Json(new { count = 0, next = 1 });
            int count = OPCount(hid.Value, doctorName, d);
            return Json(new { count, next = count + 1 });
        }

        // ── AJAX: doctor availability for hospital booking page ───────────────
        [HttpGet]
        public IActionResult GetDoctorAvailability(string doctorName, string date)
        {
            var hid = HospitalId;
            if (hid == null)
                return Json(new { isAvailable = false, hasSchedule = false,
                                  morningSlot = "", eveningSlot = "", reason = "Unauthorized" });

            if (!DateTime.TryParse(date, out var d))
                return Json(new { isAvailable = false, hasSchedule = false,
                                  morningSlot = "", eveningSlot = "", reason = "Invalid date" });

            // Resolve doctorId from name within this hospital
            int? doctorId = _db.GetDoctorIdByName(hid.Value, doctorName);
            if (doctorId == null)
                return Json(new { isAvailable = false, hasSchedule = false,
                                  morningSlot = "", eveningSlot = "", reason = "Doctor not found" });

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

        private string GetHospitalName(int id)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand("SELECT HospitalName FROM Hospitals WHERE Id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            return cmd.ExecuteScalar()?.ToString() ?? "";
        }

        private bool DoctorExists(int hospitalId, string name)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                "SELECT COUNT(1) FROM Doctors WHERE HospitalId=@Hid AND Name=@Name", conn);
            cmd.Parameters.AddWithValue("@Hid",  hospitalId);
            cmd.Parameters.AddWithValue("@Name", name.Trim());
            return (int)cmd.ExecuteScalar()! > 0;
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

        private int OPCount(int hospitalId, string doctorName, DateTime date)
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

        private bool AlreadyBooked(int patientId, int hospitalId, string doctorName, DateTime date, int familyMemberId = 0)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
        @"SELECT COUNT(*) FROM OPBookings
          WHERE PatientId=@Pid AND HospitalId=@Hid AND DoctorName=@Doc
            AND CAST(BookingDate AS DATE)=CAST(@Date AS DATE)
            AND ((@Fid>0 AND FamilyMemberId=@Fid) OR (@Fid=0 AND (FamilyMemberId IS NULL OR FamilyMemberId=0)))", conn);
            cmd.Parameters.AddWithValue("@Pid",  patientId);
            cmd.Parameters.AddWithValue("@Hid",  hospitalId);
            cmd.Parameters.AddWithValue("@Doc",  doctorName);
            cmd.Parameters.AddWithValue("@Date", date.Date);
        cmd.Parameters.AddWithValue("@Fid", familyMemberId);
            return (int)cmd.ExecuteScalar()! > 0;
        }

        private int SaveBooking(int patientId, int hospitalId, string doctorName,
                                DateTime date, int token, int? familyMemberId, string bookingForName, bool bookedByHospital,
                                decimal consultationFee = 0, string feeCategory = "")
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                @"INSERT INTO OPBookings
                    (PatientId, HospitalId, DoctorName, BookingDate, TokenNumber, BookedByHospital, FamilyMemberId, BookingForName, ConsultationFee, FeeCategory)
                  OUTPUT INSERTED.Id
                  VALUES (@Pid, @Hid, @Doc, @Date, @Token, @ByHosp, @Fid, @ForName, @Fee, @FeeCategory)", conn);
            cmd.Parameters.AddWithValue("@Pid",    patientId);
            cmd.Parameters.AddWithValue("@Hid",    hospitalId);
            cmd.Parameters.AddWithValue("@Doc",    doctorName);
            cmd.Parameters.AddWithValue("@Date",   date.Date);
            cmd.Parameters.AddWithValue("@Token",  token);
            cmd.Parameters.AddWithValue("@ByHosp", bookedByHospital ? 1 : 0);
            cmd.Parameters.AddWithValue("@Fid",    familyMemberId.HasValue ? (object)familyMemberId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ForName", bookingForName ?? "");
            cmd.Parameters.AddWithValue("@Fee",    consultationFee);
            cmd.Parameters.AddWithValue("@FeeCategory", feeCategory ?? "");
            return (int)cmd.ExecuteScalar()!;
        }

        private FamilyMemberViewModel? GetFamilyMemberById(int id, int patientId)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                "SELECT Id, PatientId, Name, Age, Relation, MobileNo FROM FamilyMembers WHERE Id=@Id AND PatientId=@Pid", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Pid", patientId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new FamilyMemberViewModel
            {
                Id = r.GetInt32(0),
                PatientId = r.GetInt32(1),
                Name = r.GetString(2),
                Age = r.GetInt32(3),
                Relation = r.GetString(4),
                MobileNo = r.GetString(5)
            };
        }

        private string GetPatientNameById(int id)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd = new SqlCommand("SELECT Name FROM Patients WHERE Id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            var r = cmd.ExecuteScalar();
            return r?.ToString() ?? "";
        }

        private (int? id, string? name) LookupPatientById(string identifier)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                "SELECT Id, Name FROM Patients WHERE Email=@I OR MobileNo=@I", conn);
            cmd.Parameters.AddWithValue("@I", identifier.Trim());
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return (null, null);
            return (r.GetInt32(0), r.GetString(1));
        }

        /// <summary>Registers a new patient created by the hospital (no login password).</summary>
        private int RegisterPatientByHospital(HospitalBookingViewModel model)
        {
            var hash   = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());
            var mobile = model.NewPatientMobile.Trim();

            // If a real email was provided, use it — otherwise build a unique placeholder
            // using the mobile number. If even that placeholder is taken (re-registration
            // attempt), append a short unique suffix so the INSERT never violates UNIQUE.
            string email;
            if (!string.IsNullOrWhiteSpace(model.NewPatientEmail))
            {
                email = model.NewPatientEmail.Trim().ToLower();
            }
            else
            {
                var baseEmail = $"{mobile}@noemail.local";
                email = EmailIsAvailable(baseEmail)
                    ? baseEmail
                    : $"{mobile}_{Guid.NewGuid().ToString("N")[..6]}@noemail.local";
            }

            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                @"INSERT INTO Patients (Name, Age, MobileNo, Address, Email, PasswordHash)
                  OUTPUT INSERTED.Id
                  VALUES (@Name, @Age, @Mobile, @Address, @Email, @Hash)", conn);
            cmd.Parameters.AddWithValue("@Name",    model.NewPatientName.Trim());
            cmd.Parameters.AddWithValue("@Age",     model.NewPatientAge!.Value);
            cmd.Parameters.AddWithValue("@Mobile",  mobile);
            cmd.Parameters.AddWithValue("@Address", model.NewPatientAddress.Trim());
            cmd.Parameters.AddWithValue("@Email",   email);
            cmd.Parameters.AddWithValue("@Hash",    hash);
            return (int)cmd.ExecuteScalar()!;
        }

        private bool EmailIsAvailable(string email)
        {
            using var conn = _db.GetConnection(); conn.Open();
            using var cmd  = new SqlCommand(
                "SELECT COUNT(1) FROM Patients WHERE Email = @Email", conn);
            cmd.Parameters.AddWithValue("@Email", email);
            return (int)cmd.ExecuteScalar()! == 0;
        }

        private HospitalDashboardViewModel BuildDashboard(int hospitalId)
        {
            using var conn = _db.GetConnection(); conn.Open();

            string hospName;
            using (var cmd = new SqlCommand("SELECT HospitalName FROM Hospitals WHERE Id=@Id", conn))
            {
                cmd.Parameters.AddWithValue("@Id", hospitalId);
                hospName = cmd.ExecuteScalar()?.ToString() ?? "";
            }

            int totalBookings;
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM OPBookings WHERE HospitalId=@Id", conn))
            {
                cmd.Parameters.AddWithValue("@Id", hospitalId);
                totalBookings = (int)cmd.ExecuteScalar()!;
            }

            int todayTotal, todayDone;
            using (var cmd = new SqlCommand(
                @"SELECT COUNT(*), SUM(CASE WHEN Status='Done' THEN 1 ELSE 0 END)
                  FROM OPBookings
                  WHERE HospitalId=@Id AND CAST(BookingDate AS DATE)=CAST(GETDATE() AS DATE)", conn))
            {
                cmd.Parameters.AddWithValue("@Id", hospitalId);
                using var r = cmd.ExecuteReader();
                r.Read();
                todayTotal = r.GetInt32(0);
                todayDone  = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            }

            var doctors = new List<DoctorViewModel>();
            using (var cmd = new SqlCommand(
                @"SELECT Id, Name, Specialization, NewPatientFee,
                         ISNULL(Revisit0to7Fee,0), ISNULL(Revisit8to15Fee,0), ISNULL(Revisit16to30Fee,0),
                         IsActive
                  FROM Doctors WHERE HospitalId=@Id ORDER BY Name", conn))
            {
                cmd.Parameters.AddWithValue("@Id", hospitalId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    doctors.Add(new DoctorViewModel
                    {
                        Id               = r.GetInt32(0),
                        HospitalId       = hospitalId,
                        Name             = r.GetString(1),
                        Specialization   = r.GetString(2),
                        NewPatientFee    = r.GetDecimal(3),
                        Revisit0to7Fee   = r.GetDecimal(4),
                        Revisit8to15Fee  = r.GetDecimal(5),
                        Revisit16to30Fee = r.GetDecimal(6),
                        IsActive         = r.GetBoolean(7)
                    });
            }

            var queue = new List<OPQueueItem>();
            using (var cmd = new SqlCommand(
                @"SELECT b.Id, b.TokenNumber, p.Name, p.MobileNo, b.DoctorName,
                         b.Status, b.BookedByHospital, b.BookingForName,
                         ISNULL(b.ConsultationFee, 0), ISNULL(b.FeeCategory, '')
                  FROM OPBookings b
                  JOIN Patients p ON p.Id = b.PatientId
                  WHERE b.HospitalId=@Id
                    AND CAST(b.BookingDate AS DATE)=CAST(GETDATE() AS DATE)
                  ORDER BY b.TokenNumber", conn))
            {
                cmd.Parameters.AddWithValue("@Id", hospitalId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    queue.Add(new OPQueueItem
                    {
                        BookingId        = r.GetInt32(0),
                        TokenNumber      = r.GetInt32(1),
                        // Use BookingForName if present, otherwise fall back to patient account name
                        PatientName      = (r.IsDBNull(7) || string.IsNullOrWhiteSpace(r.GetString(7))) ? r.GetString(2) : r.GetString(7),
                        PatientMobile    = r.GetString(3),
                        DoctorName       = r.GetString(4),
                        Status           = r.GetString(5),
                        BookedByHospital = r.GetBoolean(6),
                        BookingForName   = r.IsDBNull(7) ? string.Empty : r.GetString(7),
                        ConsultationFee  = r.GetDecimal(8),
                        FeeCategory      = r.GetString(9)
                    });
            }

            return new HospitalDashboardViewModel
            {
                HospitalName  = hospName,
                TotalBookings = totalBookings,
                TodayTotal    = todayTotal,
                TodayDone     = todayDone,
                Doctors       = doctors,
                TodayQueue    = queue
            };
        }
    }
}
