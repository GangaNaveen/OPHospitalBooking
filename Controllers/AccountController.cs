using HospitalOPBooking.Data;
using HospitalOPBooking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace HospitalOPBooking.Controllers
{
    public class AccountController : Controller
    {
        private readonly DatabaseHelper _db;

        public AccountController(DatabaseHelper db)
        {
            _db = db;
        }

        // ─── Register – Patient ──────────────────────────────────────────────────

        [HttpGet]
        public IActionResult RegisterPatient()
        {
            return View(new PatientRegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SavePatient(PatientRegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View("RegisterPatient", model);

            if (EmailExistsInPatients(model.Email))
            {
                ModelState.AddModelError("Email", "This email is already registered.");
                return View("RegisterPatient", model);
            }

            var hash = BCrypt.Net.BCrypt.HashPassword(model.Password);

            using var conn = _db.GetConnection();
            conn.Open();
            const string sql = @"INSERT INTO Patients (Name, Age, MobileNo, Address, Email, PasswordHash)
                                  VALUES (@Name, @Age, @MobileNo, @Address, @Email, @PasswordHash)";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Name",         model.Name);
            cmd.Parameters.AddWithValue("@Age",          model.Age);
            cmd.Parameters.AddWithValue("@MobileNo",     model.MobileNo);
            cmd.Parameters.AddWithValue("@Address",      model.Address);
            cmd.Parameters.AddWithValue("@Email",        model.Email);
            cmd.Parameters.AddWithValue("@PasswordHash", hash);
            cmd.ExecuteNonQuery();

            TempData["SuccessMessage"] = "Registration successful! Please log in.";
            return RedirectToAction("Login");
        }

        // ─── Register – Hospital ─────────────────────────────────────────────────

        [HttpGet]
        public IActionResult RegisterHospital()
        {
            return View(new HospitalRegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveHospital(HospitalRegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View("RegisterHospital", model);

            if (EmailExistsInHospitals(model.Email))
            {
                ModelState.AddModelError("Email", "This email is already registered.");
                return View("RegisterHospital", model);
            }

            var hash = BCrypt.Net.BCrypt.HashPassword(model.Password);

            using var conn = _db.GetConnection();
            conn.Open();
            const string sql = @"INSERT INTO Hospitals (HospitalName, DoctorName, Email, MobileNumber, Address, PasswordHash)
                                  VALUES (@HospitalName, @DoctorName, @Email, @MobileNumber, @Address, @PasswordHash)";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@HospitalName", model.HospitalName);
            cmd.Parameters.AddWithValue("@DoctorName",   model.DoctorName);
            cmd.Parameters.AddWithValue("@Email",        model.Email);
            cmd.Parameters.AddWithValue("@MobileNumber", model.MobileNumber);
            cmd.Parameters.AddWithValue("@Address",      model.Address);
            cmd.Parameters.AddWithValue("@PasswordHash", hash);
            cmd.ExecuteNonQuery();

            TempData["SuccessMessage"] = "Hospital registered successfully! Please log in.";
            return RedirectToAction("Login");
        }

        // ─── Login ───────────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("UserEmail") != null)
                return RedirectToAction("Index", "Home");

            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var input = model.EmailOrMobile.Trim();

            // Try patient (by email OR mobile)
            var (patientHash, patientName, patientEmail) = GetPatientCredentials(input);
            if (patientHash != null && BCrypt.Net.BCrypt.Verify(model.Password, patientHash))
            {
                SetSession(patientEmail!, patientName ?? input, "Patient");
                // Pass auth data to be saved by JavaScript
                TempData["LoginSuccess"] = "true";
                TempData["UserEmail"] = patientEmail!;
                TempData["UserName"] = patientName ?? input;
                TempData["UserRole"] = "Patient";
                return RedirectToAction("Index", "Home");
            }

            // Try hospital (by email OR mobile)
            var (hospitalHash, hospitalName, hospitalEmail) = GetHospitalCredentials(input);
            if (hospitalHash != null && BCrypt.Net.BCrypt.Verify(model.Password, hospitalHash))
            {
                SetSession(hospitalEmail!, hospitalName ?? input, "Hospital");
                // Pass auth data to be saved by JavaScript
                TempData["LoginSuccess"] = "true";
                TempData["UserEmail"] = hospitalEmail!;
                TempData["UserName"] = hospitalName ?? input;
                TempData["UserRole"] = "Hospital";
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Invalid email / mobile number or password.");
            return View(model);
        }

        // ─── Logout ──────────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["LogoutSuccess"] = "true";
            return RedirectToAction("Login");
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private void SetSession(string email, string name, string role)
        {
            HttpContext.Session.SetString("UserEmail", email);
            HttpContext.Session.SetString("UserName",  name);
            HttpContext.Session.SetString("UserRole",  role);
        }

        private bool EmailExistsInPatients(string email)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM Patients WHERE Email = @Email", conn);
            cmd.Parameters.AddWithValue("@Email", email);
            return (int)cmd.ExecuteScalar()! > 0;
        }

        private bool EmailExistsInHospitals(string email)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM Hospitals WHERE Email = @Email", conn);
            cmd.Parameters.AddWithValue("@Email", email);
            return (int)cmd.ExecuteScalar()! > 0;
        }

        /// <summary>Looks up a patient by email OR mobile number.</summary>
        private (string? hash, string? name, string? email) GetPatientCredentials(string input)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            const string sql = @"SELECT PasswordHash, Name, Email
                                  FROM Patients
                                  WHERE Email = @Input OR MobileNo = @Input";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Input", input);
            using var reader = cmd.ExecuteReader();
            return reader.Read()
                ? (reader.GetString(0), reader.GetString(1), reader.GetString(2))
                : (null, null, null);
        }

        /// <summary>Looks up a hospital by email OR mobile number.</summary>
        private (string? hash, string? name, string? email) GetHospitalCredentials(string input)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            const string sql = @"SELECT PasswordHash, HospitalName, Email
                                  FROM Hospitals
                                  WHERE Email = @Input OR MobileNumber = @Input";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Input", input);
            using var reader = cmd.ExecuteReader();
            return reader.Read()
                ? (reader.GetString(0), reader.GetString(1), reader.GetString(2))
                : (null, null, null);
        }
    }
}
