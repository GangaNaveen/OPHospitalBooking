# Consultation Fee Implementation Documentation

## Overview
This document describes the implementation of dynamic consultation fee calculation and display in the Hospital OP Booking system. The feature automatically calculates fees based on doctor-specific fee rules and patient booking history.

---

## Features Implemented

### 1. **Dynamic Fee Calculation**
- Automatically calculates consultation fees based on:
  - **New Patient**: Patient has no previous booking with the selected doctor
  - **Revisit Patient**: Patient has previous booking(s) with the doctor
  - **Configurable Day Ranges**: Fees vary based on days since last visit (e.g., 0-7 days, 8-15 days, 16-30 days, etc.)

### 2. **Real-time Fee Display**
- Fee is calculated and displayed immediately when:
  - A doctor is selected
  - A patient is selected (existing patient)
  - A family member is selected for booking
- Shows fee category label (e.g., "New Patient", "Revisit (5d) — 0–7 days")

### 3. **Manual Fee Override**
- Hospital staff can manually edit the calculated fee if needed
- Edited fees are marked with "(Edited)" suffix in the category

### 4. **Server-side Validation**
- Fee is recalculated on the server before saving to prevent tampering
- Ensures data integrity and security
- Validates that fee amount is not negative

### 5. **Database Persistence**
- Consultation fee and fee category are saved with each booking
- Historical fee data is preserved for reporting and auditing

---

## Database Changes

### New Columns in `OPBookings` Table

```sql
-- ConsultationFee: Stores the actual fee charged for the consultation
ConsultationFee DECIMAL(18,2) NOT NULL DEFAULT 0

-- FeeCategory: Stores the fee category/label for reference
FeeCategory NVARCHAR(100) NOT NULL DEFAULT ''
```

### Migration Script
Run the provided `Database_Migration_ConsultationFee.sql` script to add these columns to existing databases.

---

## Code Changes

### 1. **DatabaseHelper.cs**
**Location**: `Data/DatabaseHelper.cs`

**Changes**:
- Added schema migration code to create `ConsultationFee` and `FeeCategory` columns in the `EnsureTablesCreated()` method

```csharp
// Add ConsultationFee column if upgrading from older schema
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='OPBookings' AND COLUMN_NAME='ConsultationFee')
ALTER TABLE OPBookings ADD ConsultationFee DECIMAL(18,2) NOT NULL DEFAULT 0;

// Add FeeCategory column if upgrading from older schema
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='OPBookings' AND COLUMN_NAME='FeeCategory')
ALTER TABLE OPBookings ADD FeeCategory NVARCHAR(100) NOT NULL DEFAULT '';
```

---

### 2. **HospitalBookingViewModel.cs**
**Location**: `Models/HospitalBookingViewModel.cs`

**Changes**:
- Added two new properties to store consultation fee data:

```csharp
/// <summary>Calculated consultation fee based on doctor's fee rules and patient history.</summary>
public decimal ConsultationFee { get; set; }

/// <summary>Fee category/label (e.g., "New Patient", "Revisit (5d) — 0–7 days").</summary>
public string FeeCategory { get; set; } = string.Empty;
```

---

### 3. **BookForPatient.cshtml**
**Location**: `Views/Hospital/BookForPatient.cshtml`

**Changes**:

#### A. Added Hidden Fields
```html
<input type="hidden" asp-for="ConsultationFee" id="consultationFeeHidden" />
<input type="hidden" asp-for="FeeCategory" id="feeCategoryHidden" />
```

#### B. Added Consultation Fee UI Panel
- Displays after doctor selection
- Shows calculated fee in an editable input field
- Displays fee category with contextual information
- Styled with Bootstrap card component

```html
<div id="consultationFeePanel" class="mt-3 d-none">
    <div class="card border-primary">
        <div class="card-body p-3">
            <div class="row align-items-center">
                <div class="col-md-6">
                    <label class="form-label fw-semibold mb-2">
                        <i class="bi bi-currency-rupee text-primary me-1"></i>Consultation Fee
                    </label>
                    <div class="input-group">
                        <span class="input-group-text">₹</span>
                        <input type="number" class="form-control form-control-lg" 
                               id="consultationFeeInput" 
                               placeholder="0.00" 
                               step="0.01" 
                               min="0" 
                               max="999999" />
                    </div>
                    <small class="text-muted">You can edit this amount if needed</small>
                </div>
                <div class="col-md-6">
                    <div class="alert alert-info mb-0">
                        <strong id="feeCategoryLabel">Calculating...</strong>
                        <div id="feeCategoryDetails" class="small mt-1"></div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</div>
```

#### C. Added JavaScript Functions

**State Variables**:
```javascript
let currentConsultationFee = 0;
let currentFeeCategory = '';
```

**calculateConsultationFee()**: Main function that:
- Checks if doctor and patient are selected
- For new patients: Shows new patient fee immediately
- For existing patients: Calls API to calculate fee based on history
- Updates UI with calculated fee and category

**updateFeeDisplay()**: Updates the UI elements with fee information

**Event Handlers**:
- Doctor selection triggers fee calculation
- Patient lookup triggers fee calculation
- Family member selection triggers fee recalculation
- Manual fee input updates hidden fields

#### D. Added Client-side Validation
```javascript
// 5. Consultation fee validation
const feeInput = document.getElementById('consultationFeeInput');
const feeValue = parseFloat(feeInput?.value || '0');
if (!isNewMode && feeInput && (isNaN(feeValue) || feeValue <= 0)) {
    errors.push('Please enter a valid consultation fee amount');
    if (feeInput) feeInput.focus();
}
```

---

### 4. **HospitalController.cs**
**Location**: `Controllers/HospitalController.cs`

**Changes**:

#### A. Updated SaveBooking Method
Added parameters for consultation fee:

```csharp
private int SaveBooking(int patientId, int hospitalId, string doctorName,
                        DateTime date, int token, int? familyMemberId, 
                        string bookingForName, bool bookedByHospital,
                        decimal consultationFee = 0, string feeCategory = "")
{
    // ... SQL INSERT includes ConsultationFee and FeeCategory
}
```

#### B. Added Server-side Fee Calculation in BookForPatient POST
**Location**: Lines 302-395 (approximately)

**Logic**:
1. Retrieves doctor information and fee rules from database
2. Gets patient's last booking date with the selected doctor
3. Calculates fee using `DoctorViewModel.CalculateFee()` method
4. Generates fee category label using `DoctorViewModel.GetFeeLabel()` method
5. Validates calculated fee (must not be negative)
6. Saves booking with calculated fee (ignores client-submitted fee for security)

```csharp
// ── Step 7: Server-side fee calculation and validation ────────────
decimal consultationFee = 0;
string feeCategory = "New Patient";

// Recalculate fee on server to prevent tampering
using (var conn = _db.GetConnection())
{
    conn.Open();
    
    // Get doctor and fee rules
    // Get last visit date
    // Calculate fee
    consultationFee = doc.CalculateFee(lastVisit, model.BookingDate);
    feeCategory = doc.GetFeeLabel(lastVisit, model.BookingDate);
}

// Validate fee
if (consultationFee < 0)
{
    ModelState.AddModelError("", "Invalid consultation fee calculated.");
    return View(model);
}

// Save with calculated fee
SaveBooking(pid.Value, hid.Value, model.DoctorName, model.BookingDate,
            token, familyMemberId, bookingForName, bookedByHospital: true,
            consultationFee, feeCategory);
```

---

## API Endpoints

### GetConsultationFee
**Endpoint**: `/Hospital/GetConsultationFee`  
**Method**: GET  
**Purpose**: Calculate consultation fee for a patient-doctor combination

**Parameters**:
- `doctorName` (string): Name of the doctor
- `patientId` (int): Patient ID
- `familyMemberId` (int): Family member ID (0 for self)
- `bookingDate` (string): Booking date in ISO format

**Response**:
```json
{
  "fee": 500.00,
  "label": "Revisit (5d) — 0–7 days"
}
```

**Note**: This endpoint already existed in the codebase and is used by the new UI.

---

## User Flow

### For New Patients
1. Hospital staff selects "New Patient" mode
2. Enters patient details
3. Selects a doctor
4. **Fee is automatically displayed** based on doctor's new patient fee
5. Staff can edit the fee if needed
6. Completes booking
7. Fee is saved with the booking

### For Existing Patients
1. Hospital staff searches for patient by mobile/email
2. Patient details are displayed
3. Staff selects doctor
4. **System automatically calculates fee** based on:
   - Last visit date with this doctor
   - Doctor's configured fee rules
   - Days since last visit
5. Fee and category are displayed (e.g., "Revisit (5d) — 0–7 days: ₹100")
6. Staff can edit the fee if needed
7. Completes booking
8. Fee is recalculated on server and saved

### For Family Members
1. After finding patient, staff can select a family member
2. When family member is selected, **fee is recalculated** based on:
   - Family member's last visit with this doctor
   - Same fee rules as above
3. Each family member has independent booking history

---

## Security Features

### 1. **Server-side Recalculation**
- Fee submitted from client is **ignored**
- Server recalculates fee using same logic as client
- Prevents fee tampering via browser developer tools

### 2. **Validation**
- Fee must not be negative
- Doctor must exist and be active
- Patient must exist
- All standard booking validations still apply

### 3. **Audit Trail**
- Fee category is saved, showing how fee was calculated
- Historical data preserved for auditing

---

## Fee Calculation Logic

### Fee Rules (DoctorFeeRules Table)
Doctors can have multiple fee rules configured:

| FromDay | ToDay | Fee   | Description              |
|---------|-------|-------|--------------------------|
| 0       | 7     | 100   | Within 7 days            |
| 8       | 15    | 200   | 8 to 15 days             |
| 16      | 30    | 300   | 16 to 30 days            |
| 31      | 9999  | 500   | More than 30 days (new)  |

### Calculation Process
1. Get patient's last **completed** booking with the doctor (`Status='Done'`)
2. Calculate days difference: `bookingDate - lastBookingDate`
3. Find matching fee rule where `days >= FromDay AND days <= ToDay`
4. If no match found, use doctor's `NewPatientFee`
5. If no previous booking, use `NewPatientFee`

### Example Scenarios

**Scenario 1: New Patient**
- Last visit: None
- Fee: ₹500 (NewPatientFee)
- Category: "New Patient"

**Scenario 2: Revisit within 7 days**
- Last visit: 5 days ago
- Fee: ₹100
- Category: "Revisit (5d) — 0–7 days"

**Scenario 3: Revisit after 20 days**
- Last visit: 20 days ago
- Fee: ₹300
- Category: "Revisit (20d) — 16–30 days"

**Scenario 4: Revisit after 45 days**
- Last visit: 45 days ago
- Fee: ₹500 (treated as new)
- Category: "Revisit (45d) — 31–9999 days" or "New Patient (last visit 45d ago)"

---

## Testing Checklist

### Manual Testing
- [ ] New patient booking shows correct new patient fee
- [ ] Existing patient with no history shows new patient fee
- [ ] Existing patient with recent visit shows correct revisit fee
- [ ] Family member selection recalculates fee correctly
- [ ] Manual fee editing works and is marked as "(Edited)"
- [ ] Fee is saved correctly in database
- [ ] Server recalculates fee on submission
- [ ] Validation prevents negative fees
- [ ] Validation requires fee for existing patients
- [ ] Success message shows fee amount

### Database Testing
- [ ] ConsultationFee column exists in OPBookings
- [ ] FeeCategory column exists in OPBookings
- [ ] New bookings save fee correctly
- [ ] Fee data can be queried for reports

### Edge Cases
- [ ] Doctor with no fee rules configured
- [ ] Patient with multiple bookings on same day
- [ ] Booking date same as last visit date (0 days)
- [ ] Very old last visit (>365 days)
- [ ] Family member with no booking history
- [ ] Manual fee override with very high/low values

---

## Future Enhancements

### Potential Improvements
1. **Fee History Report**: Show fee trends over time
2. **Discount Management**: Apply discounts or special rates
3. **Payment Integration**: Link fees to payment records
4. **Fee Approval Workflow**: Require approval for manual overrides
5. **Bulk Fee Updates**: Update fees for multiple doctors at once
6. **Fee Templates**: Predefined fee structures for different specializations
7. **Insurance Integration**: Calculate fees based on insurance coverage

---

## Troubleshooting

### Issue: Fee not calculating
**Solution**: 
- Check browser console for JavaScript errors
- Verify `/Hospital/GetConsultationFee` endpoint is accessible
- Ensure doctor has fee rules configured in database

### Issue: Fee shows as ₹0
**Solution**:
- Check if doctor's `NewPatientFee` is set
- Verify `DoctorFeeRules` table has entries for the doctor
- Check if patient lookup returned valid data

### Issue: Server recalculation fails
**Solution**:
- Check database connection
- Verify `DoctorFeeRules` table exists
- Check server logs for SQL errors
- Ensure `DoctorViewModel.CalculateFee()` method is working

### Issue: Fee not saving to database
**Solution**:
- Verify `ConsultationFee` and `FeeCategory` columns exist
- Check `SaveBooking` method parameters
- Review SQL INSERT statement
- Check for database permission issues

---

## Support and Maintenance

### Code Locations
- **View**: `Views/Hospital/BookForPatient.cshtml`
- **Controller**: `Controllers/HospitalController.cs`
- **Model**: `Models/HospitalBookingViewModel.cs`
- **Database**: `Data/DatabaseHelper.cs`
- **Fee Logic**: `Models/DoctorViewModel.cs` (CalculateFee, GetFeeLabel methods)

### Key Methods
- `calculateConsultationFee()` - Client-side fee calculation
- `GetConsultationFee()` - API endpoint for fee calculation
- `SaveBooking()` - Saves booking with fee
- `CalculateFee()` - Core fee calculation logic
- `GetFeeLabel()` - Generates fee category label

---

## Version History

### Version 1.0 (2026-05-30)
- Initial implementation
- Dynamic fee calculation based on doctor fee rules
- Real-time fee display in UI
- Server-side validation and recalculation
- Database schema updates
- Comprehensive documentation

---

## Contact
For questions or issues related to this feature, please contact the development team.

---

**End of Documentation**