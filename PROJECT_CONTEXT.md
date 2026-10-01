# 🍾 SpinTheBottle - Project Context & Technical Architecture

## 1. Executive Summary

**Spin the Bottle** is a provably fair, high-stakes mobile-first casino web application built with **.NET 10 (C#)**, **Blazor Server (`InteractiveServer`)**, and **Vanilla CSS**. The game revolves around an interactive, continuous-spinning bottle on a circular dual-sector betting board (North vs. South), featuring hardware-accelerated physics, real-time procedural audio synthesis, and atomic ledger management.

The application incorporates a high-conversion player acquisition loop:
1. **100% Guaranteed Win Guest Trial**: First-time visitors experience a rigged trial spin that lands on their chosen side and awards an **R50.00 Welcome Bonus** (denominated in South African Rand - `ZAR / R`).
2. **Account Creation**: The user registers with email and password to lock in their win.
3. **Share to Continue Redeem Popup**: Immediately following registration, a modal prompts the user to share their winning experience and phone number with **at least 2 contacts** to unlock and credit their R50.00 Welcome Bonus.
4. **Native Contact Integration & Secure Firebase Sync**: Leverages the modern **W3C Contact Picker API** (`navigator.contacts.select`) with native OS permission dialogs on mobile, automatically uploading the loaded contacts securely to Firebase under the user's authenticated record.

---

## 2. Technology Stack

| Layer | Technology | Description |
|---|---|---|
| **Framework** | .NET 10.0 / ASP.NET Core | Host environment and server runtime |
| **UI Framework** | Blazor Server (`InteractiveServer`) | Two-way WebSockets stateful UI with reactive DOM binding |
| **Language** | C# 13 | Backend services, state machine, and betting calculations |
| **Styling** | Vanilla CSS3 | Custom design system: dark mode, glassmorphism, glowing neons, cubic-bezier transitions |
| **Physics & Audio** | Vanilla JavaScript (`bottle-physics.js`) | Hardware-accelerated GPU transforms, Web Audio API synthesis, haptic feedback, and W3C Contact Picker |
| **Database** | Firebase Realtime Database | Cloud persistence with local thread-safe `ConcurrentDictionary` caching and atomic semaphores |
| **Authentication** | Custom Authentication Provider | Cookie/session-based `CustomAuthenticationStateProvider` using SHA-256 + salt password hashing |

---

## 3. Directory & File Architecture

```
SpinTheBottle/
│
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor            # Primary shell and structure
│   │   └── MainLayout.razor.css        # Layout wrapper and container styling
│   ├── Pages/
│   │   ├── SpinTheBottle.razor         # Core game page, arena, betting controls, and modals
│   │   ├── SpinTheBottle.razor.css     # Game styling, visual effects, and popup themes
│   │   ├── Counter.razor               # Template counter page
│   │   ├── Weather.razor               # Template weather page
│   │   ├── Error.razor                 # Error boundary display
│   │   └── NotFound.razor              # 404 fallback page
│   ├── App.razor                       # Root Blazor component, sets HTML headers & scripts
│   ├── Routes.razor                    # Router component
│   └── _Imports.razor                  # Global Razor namespace imports
│
├── Models/
│   ├── Bet.cs                          # Player bet ledger record (amount, side, payout, timestamp)
│   ├── BetSide.cs / GameEnums.cs       # Sector enum definitions (North vs. South)
│   ├── ContactItem.cs                  # Contact model (Id, Name, PhoneNumber, IsSelected, AvatarColor)
│   ├── GameRound.cs                    # Provably fair round outcome (Angle, WinningSide, ServerSeed)
│   ├── SpinResult.cs                   # Spin computation outcome (IsWin, Payout, Angle, Duration)
│   └── User.cs                         # User account record (Balance, Phone, IsBonusRedeemed, etc.)
│
├── Properties/
│   └── launchSettings.json             # Dev server profiles (ports 5143 / 7282)
│
├── Services/
│   ├── IBettingEngineService.cs        # Betting engine contracts
│   ├── BettingEngineService.cs         # Provably fair algorithm, house bias rating, boundary protection
│   ├── IFirebaseService.cs             # Database contracts
│   ├── FirebaseService.cs              # Firebase Realtime Database client with local fallback & atomic locks
│   ├── CustomAuthenticationStateProvider.cs # Auth management, registration, and bonus redemption
│   └── PasswordHasher.cs               # Cryptographic SHA-256 password hashing with salt
│
├── wwwroot/
│   ├── js/
│   │   └── bottle-physics.js           # Physics engine, ratchet audio, haptics, Contact Picker API
│   ├── app.css                         # Global CSS resets and base styles
│   └── favicon.png                     # Browser favicon
│
├── appsettings.json                    # Configuration (HouseBias, Multipliers, Firebase URL & Token)
├── Program.cs                          # Host builder, DI registration, and automated startup integrity tests
├── SpinTheBottle.csproj                # .NET 10 project definition
└── PROJECT_CONTEXT.md                  # Complete project context documentation
```

---

## 4. Core Features & Business Logic

### 4.1 Server-Authoritative Provably Fair Betting Engine
- **Sectors**:
  - **North**: Angles `300.0°` through `60.0°` (centered at 0°/360°, colored cyan `#38bdf8`).
  - **South**: Angles `120.0°` through `240.0°` (centered at 180°, colored pink/coral `#f43f5e`).
  - **Boundary Exclusion Zones**: Angles within `±10°` of the dividing boundaries (80°–100° and 260°–280°) are rejected by `IsSafeAngle()` to prevent ambiguous edge landings.
- **House Bias System**:
  - Configured in `appsettings.json` via `BettingEngine:HouseBiasRating` (1 to 10).
  - Rating 1 corresponds to ~90% player win rate; Rating 5 corresponds to 50%; Rating 10 corresponds to 10%.
  - Payout multiplier: Standard **1.96x** wager amount.
- **Atomic Concurrency Protection**:
  - Dedicated `ConcurrentDictionary<string, SemaphoreSlim>` per user prevents overdraft race conditions.
  - Balances cannot be double-spent across parallel spin requests.

### 4.2 Hardware-Accelerated Physics & Procedural Web Audio (`bottle-physics.js`)
- **Cumulative Forward-Only Rotation**:
  - The bottle maintains its cumulative rotation degree (e.g., 720° -> 2540°). It **never rewinds** or snaps backwards to 0° on subsequent spins.
  - Cubic-bezier inertial deceleration: `cubic-bezier(0.15, 0.85, 0.35, 1.02)`.
- **Procedural Audio (Zero External Audio Files)**:
  - Procedural ratchet sound generated via Web Audio API `OscillatorNode` with frequency sweeping from 650 Hz down to 450 Hz.
  - Interval dynamic pacing: Delays decay smoothly from 35ms up to 350ms as the bottle loses momentum.
  - Procedural win chord arpeggio: C5 (523.25 Hz), E5 (659.25 Hz), G5 (783.99 Hz), C6 (1046.50 Hz).
  - Procedural loss drop tone: 180 Hz exponential drop to 70 Hz.
- **Mobile Touch Handling**:
  - Passive-false touchmove blocking on `.game-canvas-area` stops accidental mobile pull-to-refresh and pinch-zoom.
  - Haptic feedback pattern `[15, 50, 15]` triggered via `navigator.vibrate`.

---

## 5. Player Acquisition: Registration & Share to Redeem Flow

### Flow Diagram

```
[Guest Visitor]
      │
      ▼
[100% Win Trial Spin] ──> Lands on chosen sector (Guaranteed)
      │
      ▼
[Trial Win Modal] ──> "YOU WON! Claim R50.00 Bonus & Register"
      │
      ▼
[Auth Modal: Register] ──> User enters Email & Password
      │
      ▼
[Registration Success] ──> Account created with R0.00 (Pending Bonus)
      │
      ▼
[Share to Continue Redeem Modal Appears]
      │
      ├──> User reviews/edits Experience Note & Star Rating
      ├──> User enters Mobile Phone Number (e.g. +27 082 123 4567)
      ├──> Clicks "Show Contacts"
      │         │
      │         ├── [Mobile]: W3C Contact Picker API triggers native OS sheet
      │         └── [Desktop/Fallback]: Loads address book contacts
      │
      ├──> Contact List uploaded SECURELY to Firebase: Users/{userId}/Contacts
      ├──> User selects at least 2 contacts (Checkbox toggle)
      │
      ▼
["FINISH & REDEEM R50.00" Activated] (Requires >= 2 selected)
      │
      ├──> Web Share API / SMS intent dispatched
      ├──> RedeemWelcomeBonusAsync called on server
      ├──> Balance updated: R0.00 -> R50.00 (Pulse animation)
      └──> Victory chime audio + Confetti celebration modal
```

### Key Components of the Flow:

1. **`CustomAuthenticationStateProvider.RegisterAsync`**:
   - Creates the user in Firebase with `Balance = 0.00m` and `IsBonusRedeemed = false`.
   - Returns successful status so the modal transition happens immediately.
2. **`OpenShareRedeemModal()`**:
   - Opens the modal right after registration.
   - Pre-fills user's saved phone number if already entered.
3. **`LoadContactsAsync()`**:
   - Invokes `BottlePhysics.pickDeviceContacts()`.
   - If supported, calls `navigator.contacts.select(['name', 'tel'], { multiple: true })` following W3C standards.
   - If unsupported, falls back to a curated contact list.
   - **Immediately and securely syncs the entire contact list to Firebase** under `Users/{userId}/Contacts`:
     ```csharp
     await FirebaseService.SaveUserContactsAsync(CurrentUser.Id, contactList);
     ```
    - **Pre-Selected on Arrival**: All contacts picked from the device arrive on the platform marked as `IsSelected = true` so the user is **never forced to re-select** them.
    - **Platform Selection Controls**: The user can toggle individual contacts with 1 tap or use the `Select All / Deselect All` button directly on the platform to select from all contacts loaded from the device.
4. **Selection Requirement**:
    - The user must have **at least 2 contacts** selected.
    - Real-time indicator updates:
      - 0 selected: `⚠️ 0 of 2 contacts selected (Select at least 2)`
      - 1 selected: `⚠️ 1 of 2 contacts selected (1 more required)`
      - 2+ selected: `✅ N contacts selected (Minimum met!)`
    - Finish button remains disabled until `SelectedContactsCount >= 2`.
5. **Finishing Redemption**:
   - Dispatches share via Web Share API (`navigator.share`) or SMS link (`sms:number?body=...`).
   - Invokes `RedeemWelcomeBonusAsync(userId, phoneNumber, count)`.
   - Credits `R50.00` to the user's wallet and sets `IsBonusRedeemed = true`.
   - Plays celebration audio (`playCelebrationSound`) and transitions to the success screen.
6. **Recovery Ribbon & User Menu**:
   - If a registered user closes the modal without redeeming, a pulsing banner:
     `🎁 R50.00 BONUS PENDING: Share your experience with 2 contacts to claim into wallet! [REDEEM NOW →]`
     and a menu option in the user dropdown remain available so they can reopen and complete redemption at any time.

---

## 6. Data Schema

### 6.1 User (`Models/User.cs`)
```json
{
  "id": "44a507c701454d59b658728621dd670e",
  "email": "player@spinthebottle.co.za",
  "passwordHash": "5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8",
  "balance": 50.00,
  "phoneNumber": "+27 82 123 4567",
  "isBonusRedeemed": true,
  "sharedContactsCount": 2,
  "isTrialCompleted": true,
  "createdAt": "2026-09-29T13:45:00Z"
}
```

### 6.2 Contact Item (`Models/ContactItem.cs`)
Stored at: `Users/{userId}/Contacts`:
```json
[
  {
    "id": "native_0_1774872900000",
    "name": "Sipho Khumalo",
    "phoneNumber": "+27 82 555 0192",
    "isSelected": true,
    "avatarColor": "#10b981"
  },
  {
    "id": "native_1_1774872900001",
    "name": "Lerato Mokoena",
    "phoneNumber": "+27 83 555 0188",
    "isSelected": true,
    "avatarColor": "#f59e0b"
  }
]
```

### 6.3 Game Round (`Models/GameRound.cs`)
Stored at: `GameRounds/{roundId}`:
```json
{
  "roundId": "rnd_a1b2c3d4e5",
  "winningSide": "North",
  "targetAngle": 34.52,
  "serverSeed": "c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2",
  "houseBiasRating": 2,
  "createdAt": "2026-09-29T13:45:10Z"
}
```

### 6.4 Bet Ledger (`Models/Bet.cs`)
Stored at: `Bets/{betId}`:
```json
{
  "id": "bet_f8e7d6c5b4",
  "userId": "44a507c701454d59b658728621dd670e",
  "userEmail": "player@spinthebottle.co.za",
  "side": "North",
  "wagerAmount": 25.00,
  "payout": 49.00,
  "isWin": true,
  "multiplier": 1.96,
  "roundId": "rnd_a1b2c3d4e5",
  "timestamp": "2026-09-29T13:45:10Z"
}
```

---

## 7. Developer Operations & Useful Commands

### 7.1 Running the Project
```powershell
# Restore & build project
dotnet build

# Run application using HTTP profile (port 5143)
dotnet run --launch-profile http
```
The application will be accessible at:
- **HTTP**: `http://localhost:5143`
- **HTTPS**: `https://localhost:7282`

### 7.2 Terminating Lingering Server Processes
If `dotnet run` fails with `System.IO.IOException: Failed to bind to address http://...` or files are locked:

**PowerShell**:
```powershell
Stop-Process -Name "dotnet", "SpinTheBottle" -Force -ErrorAction SilentlyContinue
```

**Command Prompt (CMD)**:
```cmd
taskkill /F /IM dotnet.exe
taskkill /F /IM SpinTheBottle.exe
```

### 7.3 Automated Self-Test Suite
When the application starts, `Program.cs` automatically executes unit and integration sanity tests validating:
1. Password hash uniqueness & salt verification.
2. House bias rating calibration across levels 1, 5, and 10.
3. Safe angle generation & strict boundary clamping (1000 iterations).
4. Trial spin guaranteed 100% win hook.
5. Atomic balance operations & overdraft guard.
6. Real spin authoritative execution & ledger persistence.

---

## 8. Summary of Recent Enhancements

- **Share to Continue Redeem Popup**:
  - Automatically triggered upon successful registration.
  - Interactive experience note editing with quick tags (`+ Won R50 🍾`, `+ Fast Cash ⚡`, `+ 100% Fair 🎯`) and 5-star rating.
  - Mobile phone number input with South African `+27` flag prefix and SMS verification note.
  - "Show Contacts" button integrating native W3C Contact Picker with address book fallback.
  - Enforced selection of at least 2 contacts before unlocking the "Finish" button.
  - Complete victory celebration screen and instant wallet balance pulse update.
- **Secure Cloud Contact Storage**:
  - Immediately upon loading contacts, the entire contact list is asynchronously and securely saved into Firebase Realtime Database under the authenticated user (`Users/{userId}/Contacts`).
  - Added caching in `FirebaseService` to ensure zero lag in low-connectivity or offline scenarios.
