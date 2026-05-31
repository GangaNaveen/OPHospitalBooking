# Enhanced Fee Display Examples

## Overview
This document shows examples of how the consultation fee information is displayed to reception staff in the booking interface.

---

## Display Format

### New Patient (No Previous Visit)
```
┌─────────────────────────────────────────────────────────────┐
│ 💰 Consultation Fee                                         │
├─────────────────────────────────────────────────────────────┤
│ ₹ [500.00]                                                  │
│ You can edit this amount if needed                          │
│                                                              │
│ ℹ️ New Patient                                              │
│ No previous visit found with this doctor.                   │
└─────────────────────────────────────────────────────────────┘
```

### Revisit Patient - Within 7 Days
```
┌─────────────────────────────────────────────────────────────┐
│ 💰 Consultation Fee                                         │
├─────────────────────────────────────────────────────────────┤
│ ₹ [100.00]                                                  │
│ You can edit this amount if needed                          │
│                                                              │
│ ℹ️ Revisit (5d) — 0–7 days                                 │
│ Last Visit: 25 May 2026  [5 days ago]                      │
└─────────────────────────────────────────────────────────────┘
```

### Revisit Patient - Within 15 Days
```
┌─────────────────────────────────────────────────────────────┐
│ 💰 Consultation Fee                                         │
├─────────────────────────────────────────────────────────────┤
│ ₹ [200.00]                                                  │
│ You can edit this amount if needed                          │
│                                                              │
│ ℹ️ Revisit (12d) — 8–15 days                               │
│ Last Visit: 18 May 2026  [12 days ago]                     │
└─────────────────────────────────────────────────────────────┘
```

### Revisit Patient - Within 30 Days
```
┌─────────────────────────────────────────────────────────────┐
│ 💰 Consultation Fee                                         │
├─────────────────────────────────────────────────────────────┤
│ ₹ [300.00]                                                  │
│ You can edit this amount if needed                          │
│                                                              │
│ ℹ️ Revisit (25d) — 16–30 days                              │
│ Last Visit: 05 May 2026  [25 days ago]                     │
└─────────────────────────────────────────────────────────────┘
```

### Revisit Patient - After 30 Days (Treated as New)
```
┌─────────────────────────────────────────────────────────────┐
│ 💰 Consultation Fee                                         │
├─────────────────────────────────────────────────────────────┤
│ ₹ [500.00]                                                  │
│ You can edit this amount if needed                          │
│                                                              │
│ ℹ️ New Patient (last visit 45d ago)                        │
│ Last Visit: 15 Apr 2026  [45 days ago]                     │
└─────────────────────────────────────────────────────────────┘
```

---

## Key Information Displayed

### 1. **Fee Category Label**
Shows the type of visit and applicable day range:
- "New Patient" - First time with this doctor
- "Revisit (Xd) — Y–Z days" - Return visit within configured range

### 2. **Last Visit Date**
- Formatted as: "DD MMM YYYY" (e.g., "25 May 2026")
- Easy to read and verify
- Only shown for revisit patients

### 3. **Days Since Last Visit**
- Displayed as a badge: "[X days ago]"
- Shows exact number of days
- Helps staff understand the time gap
- Singular/plural handling: "1 day ago" vs "5 days ago"

### 4. **Editable Fee Amount**
- Large, clear input field with ₹ symbol
- Pre-filled with calculated amount
- Can be manually adjusted if needed
- Changes are marked as "(Edited)" in the system

---

## Benefits for Reception Staff

### ✅ **Clear Context**
Staff can immediately see:
- When the patient last visited
- How long ago that was
- Why a particular fee is being charged

### ✅ **Easy Verification**
- Can quickly verify if the fee is correct
- Can check if patient should be charged revisit rate
- Can explain to patient why they're being charged a certain amount

### ✅ **Better Patient Communication**
Staff can tell patients:
- "Your last visit was on 25 May, which was 5 days ago, so you qualify for the revisit rate of ₹100"
- "You haven't visited this doctor in over 30 days, so the regular consultation fee of ₹500 applies"

### ✅ **Audit Trail**
- All information is saved with the booking
- Can be reviewed later if questions arise
- Helps with billing disputes

---

## Technical Details

### API Response Format
```json
{
  "fee": 100.00,
  "label": "Revisit (5d) — 0–7 days",
  "lastVisitDate": "25 May 2026",
  "daysSinceLastVisit": 5,
  "isNewPatient": false
}
```

### Display Logic
```javascript
if (data.isNewPatient) {
    details = 'No previous visit found with this doctor.';
} else {
    details = `<strong>Last Visit:</strong> ${data.lastVisitDate} `;
    details += `<span class="badge bg-info text-dark ms-1">
                  ${data.daysSinceLastVisit} 
                  ${data.daysSinceLastVisit === 1 ? 'day' : 'days'} ago
                </span>`;
}
```

---

## Example Scenarios

### Scenario 1: Regular Revisit
**Patient**: John Doe  
**Doctor**: Dr. Smith  
**Last Visit**: 3 days ago (27 May 2026)  
**Today**: 30 May 2026  

**Display**:
```
Revisit (3d) — 0–7 days
Last Visit: 27 May 2026  [3 days ago]
Fee: ₹100
```

### Scenario 2: Long Gap Revisit
**Patient**: Jane Smith  
**Doctor**: Dr. Kumar  
**Last Visit**: 60 days ago (31 Mar 2026)  
**Today**: 30 May 2026  

**Display**:
```
New Patient (last visit 60d ago)
Last Visit: 31 Mar 2026  [60 days ago]
Fee: ₹500
```

### Scenario 3: First Time Patient
**Patient**: Bob Johnson  
**Doctor**: Dr. Patel  
**Last Visit**: Never  
**Today**: 30 May 2026  

**Display**:
```
New Patient
No previous visit found with this doctor.
Fee: ₹500
```

### Scenario 4: Family Member with Different History
**Patient**: Mary Wilson (booking for son)  
**Family Member**: Tom Wilson (son)  
**Doctor**: Dr. Lee  
**Tom's Last Visit**: 10 days ago (20 May 2026)  
**Today**: 30 May 2026  

**Display**:
```
Revisit (10d) — 8–15 days
Last Visit: 20 May 2026  [10 days ago]
Fee: ₹200
```

---

## Color Coding (Visual Indicators)

### Badge Colors
- **Blue Badge** (`bg-info`): Days since last visit
  - Makes the time gap stand out visually
  - Easy to spot at a glance

### Alert Box
- **Light Blue Background** (`alert-info`): Information panel
  - Non-intrusive but noticeable
  - Indicates informational content

---

## Accessibility Features

### 1. **Clear Typography**
- Large, bold labels
- Easy-to-read font sizes
- Good contrast ratios

### 2. **Semantic HTML**
- Proper use of `<strong>` for emphasis
- Badge component for highlighting
- Structured layout

### 3. **Responsive Design**
- Works on desktop and tablet
- Adapts to different screen sizes
- Touch-friendly for tablets

---

## Future Enhancements

### Potential Additions
1. **Color-coded fee categories**
   - Green for revisit rates
   - Orange for new patient rates
   
2. **Visual timeline**
   - Show booking history graphically
   - Display upcoming appointments
   
3. **Quick comparison**
   - Show all applicable fee tiers
   - Highlight the selected one

4. **Booking history link**
   - Click to see full visit history
   - View previous consultation notes

---

**End of Examples**