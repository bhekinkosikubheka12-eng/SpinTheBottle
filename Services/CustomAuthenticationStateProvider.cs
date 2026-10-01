using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using SpinTheBottle.Models;

namespace SpinTheBottle.Services;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IFirebaseService _firebaseService;
    private readonly ILogger<CustomAuthenticationStateProvider> _logger;
    private ClaimsPrincipal _currentPrincipal = new(new ClaimsIdentity());
    private User? _currentUser;

    public User? CurrentUser => _currentUser;
    public event Action<decimal>? OnBalanceChanged;

    public CustomAuthenticationStateProvider(
        IFirebaseService firebaseService,
        ILogger<CustomAuthenticationStateProvider> logger)
    {
        _firebaseService = firebaseService;
        _logger = logger;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_currentPrincipal));
    }

    public async Task<(bool Success, string Message)> RegisterAsync(string email, string password, bool hasUsedTrial = true)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return (false, "Please provide a valid email address.");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return (false, "Password must be at least 6 characters.");

        var existingUser = await _firebaseService.GetUserByEmailAsync(email);
        if (existingUser != null)
            return (false, "An account with this email already exists. Please log in.");

        string hashedPassword = PasswordHasher.HashPassword(password);
        var newUser = await _firebaseService.CreateUserAsync(email, hashedPassword, 0.00m, hasUsedTrial);
        newUser.IsBonusRedeemed = false;
        await _firebaseService.UpdateUserAsync(newUser);

        SetAuthenticatedUser(newUser);
        return (true, "Account created! Share your experience to unlock your R50.00 Welcome Bonus.");
    }

    public async Task<(bool Success, string Message, decimal NewBalance)> RedeemWelcomeBonusAsync(string userId, string phoneNumber, int contactsCount)
    {
        var user = await _firebaseService.GetUserByIdAsync(userId);
        if (user == null)
            return (false, "User not found.", 0m);

        if (user.IsBonusRedeemed)
            return (false, "Welcome bonus has already been redeemed.", user.Balance);

        user.PhoneNumber = phoneNumber;
        user.IsBonusRedeemed = true;
        user.SharedContactsCount = contactsCount;
        user.Balance += 50.00m;

        await _firebaseService.UpdateUserAsync(user);

        if (_currentUser != null && _currentUser.Id == userId)
        {
            _currentUser.PhoneNumber = phoneNumber;
            _currentUser.IsBonusRedeemed = true;
            _currentUser.SharedContactsCount = contactsCount;
            _currentUser.Balance = user.Balance;
        }

        UpdateBalance(user.Balance);
        return (true, "R50.00 Welcome Bonus successfully redeemed!", user.Balance);
    }

    public async Task<(bool Success, string Message)> LoginAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "Email and password are required.");

        var user = await _firebaseService.GetUserByEmailAsync(email);
        if (user == null)
            return (false, "Invalid email or password.");

        if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
            return (false, "Invalid email or password.");

        SetAuthenticatedUser(user);
        return (true, "Logged in successfully.");
    }

    public Task LogoutAsync()
    {
        _currentUser = null;
        _currentPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return Task.CompletedTask;
    }

    public void UpdateBalance(decimal newBalance)
    {
        if (_currentUser != null)
        {
            _currentUser.Balance = newBalance;
            OnBalanceChanged?.Invoke(newBalance);
        }
    }

    private void SetAuthenticatedUser(User user)
    {
        _currentUser = user;
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Email.Split('@')[0]),
        }, "FirebaseCustomAuth");

        _currentPrincipal = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        OnBalanceChanged?.Invoke(user.Balance);
    }
}
