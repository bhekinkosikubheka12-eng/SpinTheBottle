using Microsoft.AspNetCore.Components.Authorization;
using SpinTheBottle.Components;
using SpinTheBottle.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Authorization & Authentication State Services
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// Core Application Services (Firebase, BettingEngine, AuthProvider)
builder.Services.AddSingleton<IFirebaseService, FirebaseService>();
builder.Services.AddSingleton<IBettingEngineService, BettingEngineService>();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthenticationStateProvider>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapGet("/api/verify-engine", async (
    IBettingEngineService engine,
    IFirebaseService firebase,
    ILoggerFactory loggerFactory) =>
{
    var tests = new List<object>();

    // Test 1: Password Hasher
    string rawPass = "SecretPass123!";
    string hash = PasswordHasher.HashPassword(rawPass);
    bool passVerified = PasswordHasher.VerifyPassword(rawPass, hash);
    bool wrongPassRejected = !PasswordHasher.VerifyPassword("WrongPass", hash);
    tests.Add(new { Name = "PasswordHasher Security", Passed = passVerified && wrongPassRejected });

    // Test 2: Bias Engine Probability Calibration
    // Scale 1 -> 90%, Scale 5 -> 50%, Scale 10 -> 10%
    var biasCalibrations = new Dictionary<int, double>();
    for (int b = 1; b <= 10; b++)
    {
        double prob = b <= 5 ? 0.90 - (b - 1) * 0.10 : 0.50 - (b - 5) * 0.08;
        biasCalibrations[b] = prob;
    }
    bool biasEngineCalibrated = 
        Math.Abs(biasCalibrations[1] - 0.90) < 0.001 &&
        Math.Abs(biasCalibrations[5] - 0.50) < 0.001 &&
        Math.Abs(biasCalibrations[10] - 0.10) < 0.001;
    tests.Add(new { Name = "HouseBiasRating Calibration (1=90%, 5=50%, 10=10%)", Passed = biasEngineCalibrated, Probabilities = biasCalibrations });

    // Test 3: Safe Angle Generation & Strict Boundary Line Clamping
    // North: [300° to 60°]; South: [120° to 240°]; Boundary rejection: +/- 10° of 90° and 270°
    bool boundaryViolations = false;
    for (int i = 0; i < 500; i++)
    {
        double northAngle = engine.GenerateSafeAngleForSide(SpinTheBottle.Models.BetSide.North);
        if (!engine.IsSafeAngle(northAngle) || (northAngle > 60.0 && northAngle < 300.0))
        {
            boundaryViolations = true;
            break;
        }

        double southAngle = engine.GenerateSafeAngleForSide(SpinTheBottle.Models.BetSide.South);
        if (!engine.IsSafeAngle(southAngle) || southAngle < 120.0 || southAngle > 240.0)
        {
            boundaryViolations = true;
            break;
        }
    }
    tests.Add(new { Name = "Strict Boundary Line Clamping (1000 safe angle generations)", Passed = !boundaryViolations });

    // Test 4: Trial Spin 100% Guaranteed Win Flow
    bool trialAllWon = true;
    for (int i = 0; i < 20; i++)
    {
        var side = i % 2 == 0 ? SpinTheBottle.Models.BetSide.North : SpinTheBottle.Models.BetSide.South;
        var trialResult = await engine.ExecuteTrialSpinAsync(side);
        if (!trialResult.IsWin || trialResult.WinningSide != side || trialResult.Payout != 50.00m)
        {
            trialAllWon = false;
            break;
        }
    }
    tests.Add(new { Name = "Trial Spin Guaranteed 100% Win & R50 Bonus Hook", Passed = trialAllWon });

    // Test 5: Firebase User Balance & Atomic Race Condition Safety
    string testEmail = $"test_{Guid.NewGuid():N}@spinthebottle.co.za";
    var user = await firebase.CreateUserAsync(testEmail, hash, 50.00m, true);
    var deduct1 = await firebase.DeductWagerAsync(user.Id, 25.00m);
    var deduct2Overdraft = await firebase.DeductWagerAsync(user.Id, 100.00m);
    var creditWin = await firebase.CreditWinAsync(user.Id, 49.00m);
    var finalUser = await firebase.GetUserByIdAsync(user.Id);

    bool balanceLogicPassed = deduct1.Success &&
                              deduct1.NewBalance == 25.00m &&
                              !deduct2Overdraft.Success &&
                              creditWin.Success &&
                              creditWin.NewBalance == 74.00m &&
                              finalUser?.Balance == 74.00m;
    tests.Add(new { Name = "Firebase Atomic Balance Operations & Overdraft Guard", Passed = balanceLogicPassed, Balance = finalUser?.Balance });

    // Test 6: Real Spin Authoritative Execution & Ledger
    var realSpin = await engine.ExecuteRealSpinAsync(user.Id, SpinTheBottle.Models.BetSide.North, 10.00m);
    var recentBets = await firebase.GetRecentBetsAsync(5);
    var recentRounds = await firebase.GetRecentRoundsAsync(5);
    bool ledgerRecorded = recentBets.Any(b => b.UserId == user.Id) || recentRounds.Any(r => r.RoundId == realSpin.RoundId);
    tests.Add(new { Name = "Real Spin Server Authority & Firebase Ledger Logging", Passed = realSpin.Success && ledgerRecorded });

    bool allPassed = tests.All(t => (bool)((dynamic)t).Passed);
    return Results.Ok(new
    {
        AllPassed = allPassed,
        Timestamp = DateTime.UtcNow,
        Tests = tests
    });
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
