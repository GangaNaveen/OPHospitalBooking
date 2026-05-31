-- ============================================================================
-- Database Migration Script: Add Consultation Fee Support to OPBookings
-- ============================================================================
-- Purpose: Add ConsultationFee and FeeCategory columns to OPBookings table
-- Date: 2026-05-30
-- Author: System Generated
-- ============================================================================

USE [YourDatabaseName]; -- Replace with your actual database name
GO

-- Add ConsultationFee column if it doesn't exist
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='OPBookings' AND COLUMN_NAME='ConsultationFee'
)
BEGIN
    ALTER TABLE OPBookings 
    ADD ConsultationFee DECIMAL(18,2) NOT NULL DEFAULT 0;
    
    PRINT 'ConsultationFee column added successfully.';
END
ELSE
BEGIN
    PRINT 'ConsultationFee column already exists.';
END
GO

-- Add FeeCategory column if it doesn't exist
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='OPBookings' AND COLUMN_NAME='FeeCategory'
)
BEGIN
    ALTER TABLE OPBookings 
    ADD FeeCategory NVARCHAR(100) NOT NULL DEFAULT '';
    
    PRINT 'FeeCategory column added successfully.';
END
ELSE
BEGIN
    PRINT 'FeeCategory column already exists.';
END
GO

-- Optional: Update existing records with default values based on doctor fees
-- This is a one-time migration to populate historical data
-- Uncomment and modify as needed

/*
UPDATE OB
SET 
    OB.ConsultationFee = D.NewPatientFee,
    OB.FeeCategory = 'Historical - New Patient'
FROM OPBookings OB
INNER JOIN Doctors D ON OB.DoctorName = D.Name AND OB.HospitalId = D.HospitalId
WHERE OB.ConsultationFee = 0 OR OB.ConsultationFee IS NULL;

PRINT 'Historical booking fees updated.';
*/

GO

PRINT 'Migration completed successfully!';
GO

-- Made with Bob
