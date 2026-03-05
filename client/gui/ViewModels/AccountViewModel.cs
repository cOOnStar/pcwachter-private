using PCWachter.Contracts;
using PCWachter.Desktop.Services;

namespace PCWachter.Desktop.ViewModels;

public sealed class AccountViewModel : PageViewModelBase
{
    private readonly KeycloakAuthService _auth;
    private readonly LicenseService _licenseService;

    // ── State ─────────────────────────────────────────────────────────────

    private bool _isLoggedIn;
    private bool _isBusy;
    private string _userName = string.Empty;
    private string _userEmail = string.Empty;
    private string _userInitials = "?";
    private string _statusText = string.Empty;
    private bool _statusIsError;
    private LicenseStatusDto? _license;
    private string _activateLicenseKey = string.Empty;

    public AccountViewModel(KeycloakAuthService auth, LicenseService licenseService)
        : base("Mein Konto")
    {
        _auth = auth;
        _licenseService = licenseService;

        LoginCommand = new AsyncRelayCommand(LoginAsync, () => !IsBusy);
        LogoutCommand = new AsyncRelayCommand(LogoutAsync, () => !IsBusy && IsLoggedIn);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        ActivateLicenseCommand = new AsyncRelayCommand(ActivateLicenseAsync,
            () => !IsBusy && !string.IsNullOrWhiteSpace(ActivateLicenseKey));

        _auth.AuthStateChanged += (_, _) => _ = RefreshAsync();
        _licenseService.LicenseChanged += (_, _) => UpdateLicenseDisplay();
    }

    // ── Properties ────────────────────────────────────────────────────────

    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        private set
        {
            if (SetProperty(ref _isLoggedIn, value))
            {
                LoginCommand.RaiseCanExecuteChanged();
                LogoutCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                LoginCommand.RaiseCanExecuteChanged();
                LogoutCommand.RaiseCanExecuteChanged();
                RefreshCommand.RaiseCanExecuteChanged();
                ActivateLicenseCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string UserName     { get => _userName;     private set => SetProperty(ref _userName, value); }
    public string UserEmail    { get => _userEmail;    private set => SetProperty(ref _userEmail, value); }
    public string UserInitials { get => _userInitials; private set => SetProperty(ref _userInitials, value); }
    public string StatusText   { get => _statusText;   private set => SetProperty(ref _statusText, value); }
    public bool   StatusIsError { get => _statusIsError; private set => SetProperty(ref _statusIsError, value); }

    public string PlanLabel        => _license?.PlanLabel ?? "Keine Lizenz";
    public string PlanState        => _license?.State ?? "none";

    public string LicenseStateText => PlanState switch
    {
        "active"  => "Aktiv",
        "grace"   => "Grace Period",
        "expired" => "Abgelaufen",
        "revoked" => "Gesperrt",
        _         => "Nicht aktiv",
    };

    public bool IsLicenseActive          => _license?.IsActive ?? false;
    public string ExpiresText            => _license?.ExpiresAt?.ToLocalTime().ToString("dd.MM.yyyy") ?? "–";
    public int DaysRemaining             => _license?.DaysRemaining ?? 0;
    public bool FeatureAutoFix           => _license?.HasFeature("auto_fix") ?? false;
    public bool FeatureReports           => _license?.HasFeature("reports") ?? false;
    public bool FeaturePrioritySupport   => _license?.HasFeature("priority_support") ?? false;

    public string ActivateLicenseKey
    {
        get => _activateLicenseKey;
        set { if (SetProperty(ref _activateLicenseKey, value)) ActivateLicenseCommand.RaiseCanExecuteChanged(); }
    }

    // ── Commands ──────────────────────────────────────────────────────────

    public AsyncRelayCommand LoginCommand           { get; }
    public AsyncRelayCommand LogoutCommand          { get; }
    public AsyncRelayCommand RefreshCommand         { get; }
    public AsyncRelayCommand ActivateLicenseCommand { get; }

    // ── Logic ─────────────────────────────────────────────────────────────

    private async Task LoginAsync()
    {
        IsBusy = true;
        StatusText = "Browser wird geöffnet…";
        StatusIsError = false;
        try
        {
            bool ok = await _auth.LoginAsync();
            if (!ok)
            {
                StatusText = "Login abgebrochen oder fehlgeschlagen.";
                StatusIsError = true;
            }
            else
            {
                await LoadUserInfoAsync();
            }
        }
        finally { IsBusy = false; }
    }

    private async Task LogoutAsync()
    {
        IsBusy = true;
        try
        {
            await _auth.LogoutAsync();
            IsLoggedIn = false;
            UserName = UserEmail = string.Empty;
            UserInitials = "?";
            _license = null;
            _licenseService.Invalidate();
            RaiseLicenseProperties();
            StatusText = "Abgemeldet.";
            StatusIsError = false;
        }
        finally { IsBusy = false; }
    }

    private async Task RefreshAsync()
    {
        if (!_auth.IsAuthenticated) { IsLoggedIn = false; return; }
        IsBusy = true;
        try { await LoadUserInfoAsync(); }
        finally { IsBusy = false; }
    }

    private async Task ActivateLicenseAsync()
    {
        IsBusy = true;
        StatusText = "Lizenz wird aktiviert…";
        StatusIsError = false;
        try
        {
            Guid installId = AgentIdentity.GetOrCreateInstallId();
            (bool success, string error) = await _licenseService.ActivateLicenseAsync(
                ActivateLicenseKey, installId, _auth.UserSub);

            if (success)
            {
                ActivateLicenseKey = string.Empty;
                StatusText = "Lizenz erfolgreich aktiviert!";
                await _licenseService.CheckLicenseAsync(installId);
                UpdateLicenseDisplay();
            }
            else
            {
                StatusText = error switch
                {
                    "trial_already_used"                 => "Du hast bereits eine Testversion genutzt.",
                    "license is expired"                 => "Diese Lizenz ist abgelaufen.",
                    "license is revoked"                 => "Diese Lizenz wurde gesperrt.",
                    _ when error.Contains("409")         => "Lizenz wurde bereits auf einem anderen Gerät aktiviert.",
                    _ when error.Contains("404")         => "Lizenzkey nicht gefunden.",
                    _                                    => $"Fehler: {error}",
                };
                StatusIsError = true;
            }
        }
        finally { IsBusy = false; }
    }

    private async Task LoadUserInfoAsync()
    {
        var info = await _auth.GetUserInfoAsync();
        if (info is not null)
        {
            IsLoggedIn = true;
            UserEmail = info.Email;
            UserName = string.IsNullOrEmpty(info.Name) ? info.PreferredUsername : info.Name;
            UserInitials = BuildInitials(UserName.Length > 0 ? UserName : UserEmail);
        }
        else
        {
            IsLoggedIn = _auth.IsAuthenticated;
        }

        _license = await _licenseService.CheckLicenseAsync(AgentIdentity.GetOrCreateInstallId());
        RaiseLicenseProperties();
    }

    private void UpdateLicenseDisplay()
    {
        _license = _licenseService.CurrentLicense;
        RaiseLicenseProperties();
    }

    private void RaiseLicenseProperties()
    {
        RaisePropertyChanged(nameof(PlanLabel));
        RaisePropertyChanged(nameof(PlanState));
        RaisePropertyChanged(nameof(LicenseStateText));
        RaisePropertyChanged(nameof(IsLicenseActive));
        RaisePropertyChanged(nameof(ExpiresText));
        RaisePropertyChanged(nameof(DaysRemaining));
        RaisePropertyChanged(nameof(FeatureAutoFix));
        RaisePropertyChanged(nameof(FeatureReports));
        RaisePropertyChanged(nameof(FeaturePrioritySupport));
    }

    private static string BuildInitials(string name)
    {
        string[] parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2) return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
        if (parts.Length == 1 && parts[0].Length > 0)
            return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return "?";
    }
}
