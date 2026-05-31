using Microsoft.Data.SqlClient;

namespace HospitalOPBooking.Data
{
    public class DatabaseHelper
    {
        private readonly string _connectionString;

        public DatabaseHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        public SqlConnection GetConnection() => new SqlConnection(_connectionString);
        public int? GetDoctorIdByName(int hospitalId, string doctorName)
        {
            using var conn = GetConnection(); conn.Open();
            using var cmd = new SqlCommand(
                "SELECT Id FROM Doctors WHERE HospitalId=@Hid AND Name=@Name AND IsActive=1", conn);
            cmd.Parameters.AddWithValue("@Hid", hospitalId);
            cmd.Parameters.AddWithValue("@Name", doctorName);
            var result = cmd.ExecuteScalar();
            return result == null ? null : (int?)Convert.ToInt32(result);
        }
        /// <summary>
        /// Creates the database tables if they don't exist.
        /// </summary>
        public void EnsureTablesCreated()
        {
            using var conn = GetConnection();
            conn.Open();

            var sql = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Patients' AND xtype='U')
                CREATE TABLE Patients (
                    Id           INT IDENTITY(1,1) PRIMARY KEY,
                    Name         NVARCHAR(100)  NOT NULL,
                    Age          INT            NOT NULL,
                    MobileNo     NVARCHAR(15)   NOT NULL,
                    Address      NVARCHAR(300)  NOT NULL,
                    Email        NVARCHAR(150)  NOT NULL UNIQUE,
                    PasswordHash NVARCHAR(256)  NOT NULL,
                    CreatedAt    DATETIME       DEFAULT GETDATE()
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Hospitals' AND xtype='U')
                CREATE TABLE Hospitals (
                    Id           INT IDENTITY(1,1) PRIMARY KEY,
                    HospitalName NVARCHAR(150)  NOT NULL,
                    DoctorName   NVARCHAR(100)  NOT NULL,
                    Email        NVARCHAR(150)  NOT NULL UNIQUE,
                    MobileNumber NVARCHAR(15)   NOT NULL,
                    Address      NVARCHAR(300)  NOT NULL,
                    PasswordHash NVARCHAR(256)  NOT NULL,
                    CreatedAt    DATETIME       DEFAULT GETDATE()
                );

                -- Dedicated Doctors table (one row per doctor per hospital)
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Doctors' AND xtype='U')
                CREATE TABLE Doctors (
                    Id             INT IDENTITY(1,1) PRIMARY KEY,
                    HospitalId     INT            NOT NULL,
                    Name           NVARCHAR(100)  NOT NULL,
                    Specialization NVARCHAR(150)  NOT NULL DEFAULT '',
                    NewPatientFee  DECIMAL(10,2)  NOT NULL DEFAULT 0,
                    OldPatientFee  DECIMAL(10,2)  NOT NULL DEFAULT 0,
                    IsActive       BIT            NOT NULL DEFAULT 1,
                    CreatedAt      DATETIME       DEFAULT GETDATE(),
                    FOREIGN KEY (HospitalId) REFERENCES Hospitals(Id)
                );

                -- Add Specialization column if upgrading from older schema
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME='Doctors' AND COLUMN_NAME='Specialization')
                ALTER TABLE Doctors ADD Specialization NVARCHAR(150) NOT NULL DEFAULT '';

                -- Add fee columns if upgrading from older schema
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME='Doctors' AND COLUMN_NAME='NewPatientFee')
                ALTER TABLE Doctors ADD NewPatientFee DECIMAL(10,2) NOT NULL DEFAULT 0;

                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME='Doctors' AND COLUMN_NAME='OldPatientFee')
                ALTER TABLE Doctors ADD OldPatientFee DECIMAL(10,2) NOT NULL DEFAULT 0;

                -- Revisit-based fee columns (added in v2)
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME='Doctors' AND COLUMN_NAME='Revisit0to7Fee')
                ALTER TABLE Doctors ADD Revisit0to7Fee DECIMAL(10,2) NOT NULL DEFAULT 0;

                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME='Doctors' AND COLUMN_NAME='Revisit8to15Fee')
                ALTER TABLE Doctors ADD Revisit8to15Fee DECIMAL(10,2) NOT NULL DEFAULT 0;

                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME='Doctors' AND COLUMN_NAME='Revisit16to30Fee')
                ALTER TABLE Doctors ADD Revisit16to30Fee DECIMAL(10,2) NOT NULL DEFAULT 0;

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='OPBookings' AND xtype='U')
                CREATE TABLE OPBookings (
                    Id               INT IDENTITY(1,1) PRIMARY KEY,
                    PatientId        INT           NOT NULL,
                    HospitalId       INT           NOT NULL,
                    DoctorName       NVARCHAR(100) NOT NULL,
                    BookingDate      DATE          NOT NULL,
                    TokenNumber      INT           NOT NULL,
                    Status           NVARCHAR(20)  NOT NULL DEFAULT 'Confirmed',
                    BookedByHospital BIT           NOT NULL DEFAULT 0,
                    FamilyMemberId   INT           NULL,
                    BookingForName   NVARCHAR(100) NOT NULL DEFAULT '',
                    CreatedAt        DATETIME      DEFAULT GETDATE(),
                    FOREIGN KEY (PatientId)  REFERENCES Patients(Id),
                    FOREIGN KEY (HospitalId) REFERENCES Hospitals(Id)
                );

                -- Add BookedByHospital column if upgrading from older schema
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME='OPBookings' AND COLUMN_NAME='BookedByHospital')
                ALTER TABLE OPBookings ADD BookedByHospital BIT NOT NULL DEFAULT 0;

                -- Add FamilyMemberId column if upgrading from older schema
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME='OPBookings' AND COLUMN_NAME='FamilyMemberId')
                ALTER TABLE OPBookings ADD FamilyMemberId INT NULL;

                -- Add BookingForName column if upgrading from older schema
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME='OPBookings' AND COLUMN_NAME='BookingForName')
                ALTER TABLE OPBookings ADD BookingForName NVARCHAR(100) NOT NULL DEFAULT '';

                -- Add ConsultationFee column if upgrading from older schema
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME='OPBookings' AND COLUMN_NAME='ConsultationFee')
                ALTER TABLE OPBookings ADD ConsultationFee DECIMAL(18,2) NOT NULL DEFAULT 0;

                -- Add FeeCategory column if upgrading from older schema
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME='OPBookings' AND COLUMN_NAME='FeeCategory')
                ALTER TABLE OPBookings ADD FeeCategory NVARCHAR(100) NOT NULL DEFAULT '';

                -- Family members table
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='FamilyMembers' AND xtype='U')
                CREATE TABLE FamilyMembers (
                    Id        INT IDENTITY(1,1) PRIMARY KEY,
                    PatientId INT           NOT NULL,
                    Name      NVARCHAR(100) NOT NULL,
                    Age       INT           NOT NULL,
                    Relation  NVARCHAR(50)  NOT NULL DEFAULT '',
                    MobileNo  NVARCHAR(15)  NOT NULL DEFAULT '',
                    CreatedAt DATETIME      DEFAULT GETDATE(),
                    FOREIGN KEY (PatientId) REFERENCES Patients(Id)
                );

                -- Doctor weekly schedule (one row per doctor per day-of-week)
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DoctorWeeklySchedule' AND xtype='U')
                CREATE TABLE DoctorWeeklySchedule (
                    Id           INT IDENTITY(1,1) PRIMARY KEY,
                    DoctorId     INT          NOT NULL,
                    DayOfWeek    TINYINT      NOT NULL,  -- 0=Sun,1=Mon,...,6=Sat
                    IsAvailable  BIT          NOT NULL DEFAULT 1,
                    MorningFrom  TIME         NULL,
                    MorningTo    TIME         NULL,
                    EveningFrom  TIME         NULL,
                    EveningTo    TIME         NULL,
                    FOREIGN KEY (DoctorId) REFERENCES Doctors(Id),
                    UNIQUE (DoctorId, DayOfWeek)
                );

                -- Doctor leave / unavailable specific dates
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DoctorLeaveDate' AND xtype='U')
                CREATE TABLE DoctorLeaveDate (
                    Id        INT IDENTITY(1,1) PRIMARY KEY,
                    DoctorId  INT  NOT NULL,
                    LeaveDate DATE NOT NULL,
                    Reason    NVARCHAR(200) NOT NULL DEFAULT '',
                    FOREIGN KEY (DoctorId) REFERENCES Doctors(Id),
                    UNIQUE (DoctorId, LeaveDate)
                );

                -- Dynamic revisit fee rules per doctor (v3 — replaces fixed columns)
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DoctorFeeRules' AND xtype='U')
                CREATE TABLE DoctorFeeRules (
                    Id       INT IDENTITY(1,1) PRIMARY KEY,
                    DoctorId INT           NOT NULL,
                    FromDay  INT           NOT NULL,   -- inclusive lower bound (days since last visit)
                    ToDay    INT           NOT NULL,   -- inclusive upper bound
                    Fee      DECIMAL(10,2) NOT NULL DEFAULT 0,
                    FOREIGN KEY (DoctorId) REFERENCES Doctors(Id)
                );
            ";

            using var cmd = new SqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }
    }
}
