# Patient Booking Fee Implementation Summary

## Overview
This document summarizes the implementation of consultation fee calculation and display in the patient-facing booking page (`Views/Booking/Book.cshtml`).

---

## Files Modified

### 1. **Models/BookingViewModel.cs**
Added consultation fee properties:
```csharp
public decimal ConsultationFee { get; set; }
public string FeeCategory { get; set; } = string.Empty;
```

### 2. **Views/Booking/Book.cshtml**
**Changes:**
- Added hidden fields for `ConsultationFee` and `FeeCategory`
- Replaced basic fee panel with enhanced consultation fee card
- Added JavaScript state variables for fee tracking
- Implemented `calculateConsultationFee()` function
- Added `updateFeeDisplay()` function
- Updated doctor selection to trigger fee calculation
- Updated family member selection to recalculate fees
- Added fee validation in submit guard
- Added manual fee editing support with "(Edited)" marker

**UI Features:**
- Editable fee input field with ₹ symbol
- Fee category label showing visit type
- Last visit date and days since last visit display
- Bootstrap card styling with info alert
- Real-time AJAX fee calculation

### 3. **Controllers/BookingController.cs**
**Changes:**

#### A. Updated `GetConsultationFee` API Endpoint
- Now uses dynamic `DoctorFeeRules` instead of fixed columns
- Returns enhanced information:
  - `fee`: Calculated consultation fee
  - `label`: Fee category label
  - `lastVisitDate`: Formatted last visit date
  - `daysSinceLastVisit`: Number of days since last visit
  - `isNewPatient`: Boolean flag

#### B. Updated `SaveBooking` Method
- Added `consultationFee` parameter (default: 0)
- Added `feeCategory` parameter (default: "")
- Updated SQL INSERT to include new columns

#### C. Updated `Book` POST Action
- Added server-side fee calculation before saving
- Recalculates fee using same logic as API
- Validates fee is not negative
- Saves calculated fee with booking (ignores client-submitted value)

---

## API Endpoint

### GetConsultationFee
**URL**: `/Booking/GetConsultationFee`  
**Method**: GET  
**Parameters**:
- `hospitalId` (int): Hospital ID
- `doctorName` (string): Doctor name
- `familyMemberId` (int): Family member ID (0 for self)
- `bookingDate` (string): Booking date

**Response**:
```json
{
  "fee": 100.00,
  "label": "Revisit (5d) — 0–7 days",
  "lastVisitDate": "25 May 2026",
  "daysSinceLastVisit": 5,
  "isNewPatient": false
}
```

---

## User Experience

### For New Patients
1. Patient selects doctor
2. System shows new patient fee immediately
3. Fee is editable if needed
4. On submit, fee is recalculated on server and saved

### For Returning Patients
1. Patient selects doctor
2. System calculates fee based on last visit with this doctor
3. Shows:
   - Calculated fee amount
   - Fee category (e.g., "Revisit (5d) — 0–7 days")
   - Last visit date (e.g., "25 May 2026")
   - Days since last visit (e.g., "5 days ago")
4. Patient can edit fee if needed
5. On submit, fee is recalculated on server and saved

### For Family Members
1. Patient selects family member
2. Fee is recalculated based on that family member's history
3. Each family member has independent booking history
4. Same display and editing features as above

---

## Security Features

### 1. Server-side Recalculation
- Fee submitted from client is **ignored**
- Server recalculates fee using same logic
- Prevents fee tampering

### 2. Validation
- Fee must not be negative
- Doctor must exist and be active
- Patient must be logged in
- All standard booking validations apply

### 3. Audit Trail
- Fee category saved with booking
- Shows how fee was calculated
- Historical data preserved

---

## Fee Calculation Logic

### Process
1. Get patient's last **completed** booking with the doctor (`Status='Done'`)
2. Calculate days difference: `bookingDate - lastBookingDate`
3. Find matching fee rule where `days >= FromDay AND days <= ToDay`
4. If no match found, use doctor's `NewPatientFee`
5. If no previous booking, use `NewPatientFee`

### Example Scenarios

**New Patient:**
```
Fee: ₹500
Category: "New Patient"
Details: "No previous visit found with this doctor."
```

**Revisit within 7 days:**
```
Fee: ₹100
Category: "Revisit (5d) — 0–7 days"
Details: "Last Visit: 25 May 2026 [5 days ago]"
```

**Revisit within 15 days:**
```
Fee: ₹200
Category: "Revisit (12d) — 8–15 days"
Details: "Last Visit: 18 May 2026 [12 days ago]"
```

---

## Database Schema

### OPBookings Table
Added columns:
```sql
ConsultationFee DECIMAL(18,2) NOT NULL DEFAULT 0
FeeCategory NVARCHAR(100) NOT NULL DEFAULT ''
```

### DoctorFeeRules Table
Used for dynamic fee configuration:
```sql
CREATE TABLE DoctorFeeRules (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    DoctorId INT           NOT NULL,
    FromDay  INT           NOT NULL,
    ToDay    INT           NOT NULL,
    Fee      DECIMAL(10,2) NOT NULL DEFAULT 0,
    FOREIGN KEY (DoctorId) REFERENCES Doctors(Id)
);
```

---

## Testing Checklist

### Functional Testing
- [ ] New patient booking shows correct fee
- [ ] Returning patient shows correct revisit fee
- [ ] Last visit date displays correctly
- [ ] Days since last visit calculates correctly
- [ ] Family member selection recalculates fee
- [ ] Manual fee editing works
- [ ] Fee is saved correctly in database
- [ ] Server recalculates fee on submission

### Edge Cases
- [ ] Doctor with no fee rules
- [ ] Patient with no booking history
- [ ] Very old last visit (>365 days)
- [ ] Booking on same day as last visit (0 days)
- [ ] Family member with different history than patient

### Security Testing
- [ ] Cannot tamper with fee via browser tools
- [ ] Server always recalculates fee
- [ ] Negative fees are rejected
- [ ] Unauthorized access is blocked

---

## Differences from Hospital Booking

### Similarities
- Same fee calculation logic
- Same API endpoint structure
- Same security measures
- Same database schema

### Differences
1. **Authentication**: Patient must be logged in (vs hospital staff)
2. **Patient Context**: Uses logged-in patient ID automatically
3. **Family Members**: Patient can book for self or family members
4. **UI Style**: Simpler, patient-friendly interface
5. **Validation**: Slightly different validation messages

---

## Integration with Existing Features

### Works With
- ✅ Doctor availability checking
- ✅ OP count display
- ✅ Family member management
- ✅ Booking confirmation
- ✅ Booking history

### Compatible With
- ✅ Dynamic doctor fee rules
- ✅ Multiple hospitals
- ✅ Multiple doctors per hospital
- ✅ Schedule management

---

## Future Enhancements

### Potential Improvements
1. **Fee Comparison**: Show fees for all doctors side-by-side
2. **Discount Codes**: Apply promotional discounts
3. **Payment Integration**: Link to payment gateway
4. **Fee History**: Show patient's fee payment history
5. **Insurance**: Calculate fees based on insurance coverage
6. **Notifications**: Send fee information via SMS/email

---

## Troubleshooting

### Issue: Fee not calculating
**Check:**
- Browser console for JavaScript errors
- Network tab for API call failures
- Doctor has fee rules configured
- Patient is logged in

### Issue: Wrong fee displayed
**Check:**
- Last booking date is correct
- Fee rules are configured properly
- Days calculation is accurate
- Family member selection is correct

### Issue: Fee not saving
**Check:**
- Database columns exist
- SaveBooking method has correct parameters
- Server-side calculation is working
- No SQL errors in logs

---

## Support

### Key Files
- **View**: `Views/Booking/Book.cshtml`
- **Controller**: `Controllers/BookingController.cs`
- **Model**: `Models/BookingViewModel.cs`
- **Fee Logic**: `Models/DoctorViewModel.cs`

### Key Methods
- `calculateConsultationFee()` - Client-side calculation
- `GetConsultationFee()` - API endpoint
- `SaveBooking()` - Saves booking with fee
- `CalculateFee()` - Core fee calculation
- `GetFeeLabel()` - Generates fee label

---

## Version History

### Version 1.0 (2026-05-30)
- Initial implementation for patient booking
- Dynamic fee calculation
- Enhanced fee display with visit history
- Server-side validation
- Complete integration with existing booking flow

---

**End of Document**