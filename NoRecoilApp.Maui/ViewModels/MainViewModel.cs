using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using NoRecoilApp.Maui.Data;
using NoRecoilApp.Maui.Models;
using NoRecoilApp.Maui.Services;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;

namespace NoRecoilApp.Maui.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IWeaponProfileRepository _repository;
    private readonly IRecoilEngineService _engine;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private CancellationTokenSource? _searchCts;
    private bool _initialized;
    private string _selectedTab = "Recoil";
    private string _searchQuery = string.Empty;
    private string _sideFilter = "TODOS";
    private string _operatorName = string.Empty;
    private string _weaponName = string.Empty;
    private string _side = "ATK";
    private WeaponProfile? _selectedProfile;
    private string? _activeProfileKey;
    private string? _loadedProfileKey;
    private string _feedbackMessage = "Pronto.";
    private bool _engineEnabled;
    private bool _adsEnabled;
    private int _adsHoldMilliseconds = 35;

    // ── Campos temporais (existentes) ────────────────────────────────────
    private double _strengthY = 4.5;
    private double _strengthX = 0.0;
    private double _delay = 0.012;
    private double _smooth = 5;
    private double _accelFactor = 1.2;
    private double _maxProgression = 2.5;

    // ── Campos Spray Table (NOVO) ─────────────────────────────────────────
    private bool _useSprayPattern = false;
    private double _fireRateRpm = 750;
    private double _sprayPointX = 0.0; // input temporário para novo ponto
    private double _sprayPointY = 0.0; // input temporário para novo ponto

    public MainViewModel(IWeaponProfileRepository repository, IRecoilEngineService engine)
    {
        _repository = repository;
        _engine = engine;

        SideChoices = ["ATK", "DEF"];
        SideFilterChoices = ["TODOS", "ATK", "DEF"];

        SelectTabCommand = new Command<string>(SelectTab);
        SaveProfileCommand = new Command(async () => await SaveProfileAsync());
        DeleteProfileCommand = new Command(async () => await DeleteProfileAsync());
        ResetProfilesCommand = new Command(async () => await ResetProfilesAsync());
        NewProfileCommand = new Command(ClearEditor);
        RefreshProfilesCommand = new Command(async () => await RefreshProfilesAsync());
        LoadProfileCommand = new Command<WeaponProfile>(LoadProfile);
        LoadSelectedProfileCommand = new Command(LoadSelectedProfile);
        ToggleEngineCommand = new Command(() => _engine.ToggleEnabled());
        ToggleAdsCommand = new Command(() => _engine.ToggleAds());
        ToggleSideCommand = new Command(ToggleSide);

        // ── Comandos Spray Table (NOVO) ───────────────────────────────────
        AddSprayPointCommand = new Command(AddSprayPoint);
        RemoveSprayPointCommand = new Command<SprayPointEntry>(RemoveSprayPoint);
        MoveUpSprayPointCommand = new Command<SprayPointEntry>(MoveUpSprayPoint);
        MoveDownSprayPointCommand = new Command<SprayPointEntry>(MoveDownSprayPoint);
        ClearSprayPointsCommand = new Command(ClearSprayPoints);

        _engine.EnabledChanged += OnEngineEnabledChanged;
        _engine.AdsEnabledChanged += OnAdsEnabledChanged;

        EngineEnabled = _engine.Enabled;
        AdsEnabled = _engine.AdsEnabled;
        AdsHoldMilliseconds = _engine.AdsHoldMilliseconds;
        PushCurrentEditorToEngine();
    }

    // ── Coleções ──────────────────────────────────────────────────────────
    public ObservableCollection<WeaponProfile> Profiles { get; } = [];
    public ObservableCollection<SprayPointEntry> SprayPoints { get; } = []; // ✅ NOVO

    public IReadOnlyList<string> SideChoices { get; }
    public IReadOnlyList<string> SideFilterChoices { get; }

    // ── Comandos existentes ───────────────────────────────────────────────
    public ICommand SelectTabCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand ResetProfilesCommand { get; }
    public ICommand NewProfileCommand { get; }
    public ICommand RefreshProfilesCommand { get; }
    public ICommand LoadProfileCommand { get; }
    public ICommand LoadSelectedProfileCommand { get; }
    public ICommand ToggleEngineCommand { get; }
    public ICommand ToggleAdsCommand { get; }
    public ICommand ToggleSideCommand { get; }

    // ── Comandos Spray Table (NOVO) ───────────────────────────────────────
    public ICommand AddSprayPointCommand { get; }
    public ICommand RemoveSprayPointCommand { get; }
    public ICommand MoveUpSprayPointCommand { get; }
    public ICommand MoveDownSprayPointCommand { get; }
    public ICommand ClearSprayPointsCommand { get; }

    // ── Tabs (INALTERADO) ─────────────────────────────────────────────────
    public string SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (!SetProperty(ref _selectedTab, value)) return;
            OnPropertyChanged(nameof(IsRecoilTab));
            OnPropertyChanged(nameof(IsCurveTab));
            OnPropertyChanged(nameof(IsAdsTab));
            OnPropertyChanged(nameof(IsProfilesTab));
            OnPropertyChanged(nameof(IsConfigTab));
            OnPropertyChanged(nameof(IsSprayTab));           // ✅ NOVO
            OnPropertyChanged(nameof(RecoilTabBackground));
            OnPropertyChanged(nameof(CurveTabBackground));
            OnPropertyChanged(nameof(AdsTabBackground));
            OnPropertyChanged(nameof(ProfilesTabBackground));
            OnPropertyChanged(nameof(ConfigTabBackground));
            OnPropertyChanged(nameof(SprayTabBackground));   // ✅ NOVO
            OnPropertyChanged(nameof(RecoilTabText));
            OnPropertyChanged(nameof(CurveTabText));
            OnPropertyChanged(nameof(AdsTabText));
            OnPropertyChanged(nameof(ProfilesTabText));
            OnPropertyChanged(nameof(ConfigTabText));
            OnPropertyChanged(nameof(SprayTabText));         // ✅ NOVO
        }
    }

    public bool IsRecoilTab => SelectedTab == "Recoil";
    public bool IsCurveTab => SelectedTab == "Curva";
    public bool IsAdsTab => SelectedTab == "ADS";
    public bool IsProfilesTab => SelectedTab == "Perfis";
    public bool IsConfigTab => SelectedTab == "Config";
    public bool IsSprayTab => SelectedTab == "Spray";    // ✅ NOVO

    public Color RecoilTabBackground => IsRecoilTab ? Color.FromArgb("#1F6FEB") : Color.FromArgb("#30363D");
    public Color CurveTabBackground => IsCurveTab ? Color.FromArgb("#1F6FEB") : Color.FromArgb("#30363D");
    public Color AdsTabBackground => IsAdsTab ? Color.FromArgb("#1F6FEB") : Color.FromArgb("#30363D");
    public Color ProfilesTabBackground => IsProfilesTab ? Color.FromArgb("#1F6FEB") : Color.FromArgb("#30363D");
    public Color ConfigTabBackground => IsConfigTab ? Color.FromArgb("#1F6FEB") : Color.FromArgb("#30363D");
    public Color SprayTabBackground => IsSprayTab ? Color.FromArgb("#1F6FEB") : Color.FromArgb("#30363D"); // ✅ NOVO

    public Color RecoilTabText => IsRecoilTab ? Colors.White : Color.FromArgb("#C9D1D9");
    public Color CurveTabText => IsCurveTab ? Colors.White : Color.FromArgb("#C9D1D9");
    public Color AdsTabText => IsAdsTab ? Colors.White : Color.FromArgb("#C9D1D9");
    public Color ProfilesTabText => IsProfilesTab ? Colors.White : Color.FromArgb("#C9D1D9");
    public Color ConfigTabText => IsConfigTab ? Colors.White : Color.FromArgb("#C9D1D9");
    public Color SprayTabText => IsSprayTab ? Colors.White : Color.FromArgb("#C9D1D9"); // ✅ NOVO

    // ── Busca ─────────────────────────────────────────────────────────────
    public string SearchQuery
    {
        get => _searchQuery;
        set { if (!SetProperty(ref _searchQuery, value)) return; ScheduleSearchRefresh(); }
    }

    public string SideFilter
    {
        get => _sideFilter;
        set { if (!SetProperty(ref _sideFilter, value)) return; ScheduleSearchRefresh(); }
    }

    // ── Editor de perfil ──────────────────────────────────────────────────
    public string OperatorName
    {
        get => _operatorName;
        set => SetProperty(ref _operatorName, value);
    }

    public string WeaponName
    {
        get => _weaponName;
        set => SetProperty(ref _weaponName, value);
    }

    public string Side
    {
        get => _side;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "ATK" : value.Trim().ToUpperInvariant();
            if (!SetEditorAndSync(ref _side, normalized)) return;
            OnPropertyChanged(nameof(SideToggleText));
            OnPropertyChanged(nameof(SideToggleBackground));
            OnPropertyChanged(nameof(SideToggleTextColor));
        }
    }

    public WeaponProfile? SelectedProfile
    {
        get => _selectedProfile;
        set { if (!SetProperty(ref _selectedProfile, value)) return; OnPropertyChanged(nameof(HasSelectedProfile)); }
    }

    public bool HasSelectedProfile => SelectedProfile is not null;
    public bool HasLoadedProfile => !string.IsNullOrWhiteSpace(_loadedProfileKey);

    public string LoadedProfileStatusText =>
        string.IsNullOrWhiteSpace(_loadedProfileKey)
            ? "Perfil carregado: nenhum"
            : $"Perfil carregado: {_loadedProfileKey}";

    public Color LoadedProfileStatusColor =>
        string.IsNullOrWhiteSpace(_loadedProfileKey)
            ? Color.FromArgb("#8B949E")
            : Color.FromArgb("#2ECC71");

    // ── Campos temporais (INALTERADOS) ────────────────────────────────────
    public double StrengthY { get => _strengthY; set => SetEditorAndSync(ref _strengthY, value); }
    public double StrengthX { get => _strengthX; set => SetEditorAndSync(ref _strengthX, value); }
    public double Delay { get => _delay; set => SetEditorAndSync(ref _delay, value); }
    public double Smooth { get => _smooth; set => SetEditorAndSync(ref _smooth, value); }
    public double AccelFactor { get => _accelFactor; set => SetEditorAndSync(ref _accelFactor, value); }
    public double MaxProgression { get => _maxProgression; set => SetEditorAndSync(ref _maxProgression, value); }

    // ── Campos Spray Table (NOVO) ─────────────────────────────────────────
    public bool UseSprayPattern
    {
        get => _useSprayPattern;
        set
        {
            if (!SetEditorAndSync(ref _useSprayPattern, value)) return;
            OnPropertyChanged(nameof(IsSprayPatternMode));
            OnPropertyChanged(nameof(IsTemporalMode));
        }
    }

    public double FireRateRpm
    {
        get => _fireRateRpm;
        set => SetEditorAndSync(ref _fireRateRpm, Math.Clamp(value, 60, 1200));
    }

    public double SprayPointX
    {
        get => _sprayPointX;
        set => SetProperty(ref _sprayPointX, value);
    }

    public double SprayPointY
    {
        get => _sprayPointY;
        set => SetProperty(ref _sprayPointY, value);
    }

    public bool IsSprayPatternMode => UseSprayPattern;
    public bool IsTemporalMode => !UseSprayPattern;

    public string SprayPointCount => $"{SprayPoints.Count} ponto(s)";

    public string FireRateRpmInput
    {
        get => FormatThree(FireRateRpm);
        set => TrySetNumericInput(value, 60, 1200, v => FireRateRpm = v);
    }

    public string SprayPointXInput
    {
        get => FormatThree(SprayPointX);
        set => TrySetNumericInput(value, -15, 15, v => SprayPointX = v);
    }

    public string SprayPointYInput
    {
        get => FormatThree(SprayPointY);
        set => TrySetNumericInput(value, 0, 15, v => SprayPointY = v);
    }

    // ── ADS (INALTERADO) ─────────────────────────────────────────────────
    public string SideToggleText => Side == "ATK" ? "Lado: ATK" : "Lado: DEF";
    public Color SideToggleBackground => Side == "ATK" ? Color.FromArgb("#DA3633") : Color.FromArgb("#1F6FEB");
    public Color SideToggleTextColor => Colors.White;

    public bool AdsEnabled
    {
        get => _adsEnabled;
        private set
        {
            if (!SetProperty(ref _adsEnabled, value)) return;
            OnPropertyChanged(nameof(AdsStatusText));
            OnPropertyChanged(nameof(AdsStatusColor));
        }
    }

    public string AdsStatusText => AdsEnabled ? "ADS Assist: ATIVO (F3)" : "ADS Assist: INATIVO (F3)";
    public Color AdsStatusColor => AdsEnabled ? Colors.LimeGreen : Colors.IndianRed;

    public int AdsHoldMilliseconds
    {
        get => _adsHoldMilliseconds;
        set
        {
            var normalized = Math.Clamp(value, 0, 250);
            if (!SetProperty(ref _adsHoldMilliseconds, normalized)) return;
            _engine.SetAdsHoldMilliseconds(normalized);
            OnPropertyChanged(nameof(AdsHoldInput));
        }
    }

    public string AdsHoldInput
    {
        get => AdsHoldMilliseconds.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) &&
                !int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out ms)) return;
            AdsHoldMilliseconds = ms;
        }
    }

    // ── Inputs numéricos (INALTERADOS) ────────────────────────────────────
    public string StrengthYInput { get => FormatThree(StrengthY); set => TrySetNumericInput(value, 0, 15, v => StrengthY = v); }
    public string StrengthXInput { get => FormatThree(StrengthX); set => TrySetNumericInput(value, -8, 8, v => StrengthX = v); }
    public string DelayInput { get => FormatThree(Delay); set => TrySetNumericInput(value, 0.005, 0.03, v => Delay = v); }
    public string SmoothInput { get => FormatThree(Smooth); set => TrySetNumericInput(value, 1, 15, v => Smooth = v); }
    public string AccelFactorInput { get => FormatThree(AccelFactor); set => TrySetNumericInput(value, 1, 3, v => AccelFactor = v); }
    public string MaxProgressionInput { get => FormatThree(MaxProgression); set => TrySetNumericInput(value, 1, 6, v => MaxProgression = v); }

    // ── Engine status (INALTERADO) ────────────────────────────────────────
    public bool EngineEnabled
    {
        get => _engineEnabled;
        private set
        {
            if (!SetProperty(ref _engineEnabled, value)) return;
            OnPropertyChanged(nameof(EngineStatusText));
            OnPropertyChanged(nameof(EngineStatusColor));
        }
    }

    public string EngineStatusText =>
        !_engine.IsPlatformSupported
            ? "STATUS: indisponivel nesta plataforma"
            : EngineEnabled ? "STATUS: ATIVO (F2)" : "STATUS: INATIVO (F2)";

    public Color EngineStatusColor => EngineEnabled ? Colors.LimeGreen : Colors.IndianRed;

    public string FeedbackMessage
    {
        get => _feedbackMessage;
        set => SetProperty(ref _feedbackMessage, value);
    }

    public string RuntimeSummary =>
        $"Plataforma: {DeviceInfo.Platform} | OS: {DeviceInfo.VersionString} | Runtime: .NET";

    // ── Inicialização ─────────────────────────────────────────────────────
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await _repository.InitializeAsync();
        await _engine.StartAsync();
        await RefreshProfilesAsync();
        _initialized = true;
        if (!_engine.IsPlatformSupported)
            FeedbackMessage = "Somente o Windows suporta controle global de mouse/tecla desta engine.";
    }

    // ── Comandos Spray Table ──────────────────────────────────────────────
    private void AddSprayPoint()
    {
        SprayPoints.Add(new SprayPointEntry
        {
            Index = SprayPoints.Count,
            X = SprayPointX,
            Y = SprayPointY
        });
        SprayPointX = 0.0;
        SprayPointY = 0.0;
        OnPropertyChanged(nameof(SprayPointCount));
        PushCurrentEditorToEngine();
    }

    private void RemoveSprayPoint(SprayPointEntry? entry)
    {
        if (entry is null) return;
        SprayPoints.Remove(entry);
        ReindexSprayPoints();
        PushCurrentEditorToEngine();
    }

    private void MoveUpSprayPoint(SprayPointEntry? entry)
    {
        if (entry is null) return;
        var idx = SprayPoints.IndexOf(entry);
        if (idx <= 0) return;
        SprayPoints.Move(idx, idx - 1);
        ReindexSprayPoints();
        PushCurrentEditorToEngine();
    }

    private void MoveDownSprayPoint(SprayPointEntry? entry)
    {
        if (entry is null) return;
        var idx = SprayPoints.IndexOf(entry);
        if (idx < 0 || idx >= SprayPoints.Count - 1) return;
        SprayPoints.Move(idx, idx + 1);
        ReindexSprayPoints();
        PushCurrentEditorToEngine();
    }

    private void ClearSprayPoints()
    {
        SprayPoints.Clear();
        OnPropertyChanged(nameof(SprayPointCount));
        PushCurrentEditorToEngine();
    }

    private void ReindexSprayPoints()
    {
        for (var i = 0; i < SprayPoints.Count; i++)
            SprayPoints[i].Index = i;
        OnPropertyChanged(nameof(SprayPointCount));
    }

    // ── Internos ──────────────────────────────────────────────────────────
    private void SelectTab(string? tab)
    {
        if (!string.IsNullOrWhiteSpace(tab)) SelectedTab = tab;
    }

    private void ToggleSide() => Side = Side == "ATK" ? "DEF" : "ATK";

    private void ScheduleSearchRefresh()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(160, token); await RefreshProfilesAsync(token); }
            catch (OperationCanceledException) { }
        }, token);
    }

    private bool SetEditorAndSync<T>(ref T field, T value,
        [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        var changed = SetProperty(ref field, value, propertyName);
        if (changed) { PushCurrentEditorToEngine(); NotifyInputProperty(propertyName); }
        return changed;
    }

    private void NotifyInputProperty(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(StrengthY): OnPropertyChanged(nameof(StrengthYInput)); break;
            case nameof(StrengthX): OnPropertyChanged(nameof(StrengthXInput)); break;
            case nameof(Delay): OnPropertyChanged(nameof(DelayInput)); break;
            case nameof(Smooth): OnPropertyChanged(nameof(SmoothInput)); break;
            case nameof(AccelFactor): OnPropertyChanged(nameof(AccelFactorInput)); break;
            case nameof(MaxProgression): OnPropertyChanged(nameof(MaxProgressionInput)); break;
            case nameof(FireRateRpm): OnPropertyChanged(nameof(FireRateRpmInput)); break;
        }
    }

    // ── PushCurrentEditorToEngine — envia perfil atual (temporal ou spray) ──
    private void PushCurrentEditorToEngine()
    {
        if (UseSprayPattern && SprayPoints.Count > 0)
        {
            _engine.SetProfile(RecoilSettings.WithSpray(
                fireRateRpm: FireRateRpm,
                smooth: (int)Math.Round(Smooth),
                delay: Delay,
                pattern: SprayPoints
                    .OrderBy(p => p.Index)
                    .Select(p => new RecoilPoint(p.X, p.Y))
                    .ToArray()));
        }
        else
        {
            _engine.SetProfile(new RecoilSettings(
                StrengthY,
                StrengthX,
                Delay,
                (int)Math.Round(Smooth),
                AccelFactor,
                MaxProgression,
                UseSprayPattern: false,
                FireRateRpm: FireRateRpm,
                SprayPattern: []));
        }
    }

    private async Task RefreshProfilesAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            var list = await _repository.SearchAsync(SearchQuery, SideFilter, cancellationToken);
            var search = (SearchQuery ?? string.Empty).Trim();
            var previousSelectedKey = SelectedProfile?.DisplayKey;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Profiles.Clear();
                foreach (var profile in list)
                {
                    profile.IsActive = profile.DisplayKey == _activeProfileKey;
                    Profiles.Add(profile);
                }

                WeaponProfile? preselected = null;
                if (!string.IsNullOrWhiteSpace(search))
                    preselected = list.FirstOrDefault(p =>
                        p.OperatorName.StartsWith(search, StringComparison.OrdinalIgnoreCase));

                preselected ??= list.FirstOrDefault(p => p.DisplayKey == previousSelectedKey);
                preselected ??= list.FirstOrDefault();
                SelectedProfile = preselected;
                FeedbackMessage = $"{Profiles.Count} perfil(is) carregado(s).";
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { FeedbackMessage = $"Erro ao carregar perfis: {ex.Message}"; }
        finally { _refreshGate.Release(); }
    }

    private void LoadProfile(WeaponProfile? profile)
    {
        if (profile is null) return;

        SelectedProfile = profile;
        OperatorName = profile.OperatorName;
        WeaponName = profile.WeaponName;
        Side = profile.Side;
        StrengthY = profile.StrengthY;
        StrengthX = profile.StrengthX;
        Delay = profile.Delay;
        Smooth = profile.Smooth;
        AccelFactor = profile.AccelFactor;
        MaxProgression = profile.MaxProgression;

        // ── Spray Table ───────────────────────────────────────────────────
        UseSprayPattern = profile.UseSprayPattern;
        FireRateRpm = profile.FireRateRpm;

        SprayPoints.Clear();
        foreach (var p in profile.SprayPoints.OrderBy(p => p.Index))
            SprayPoints.Add(new SprayPointEntry { Index = p.Index, X = p.X, Y = p.Y });

        OnPropertyChanged(nameof(SprayPointCount));

        _activeProfileKey = profile.DisplayKey;
        _loadedProfileKey = profile.DisplayKey;
        SelectedTab = "Recoil";

        OnPropertyChanged(nameof(HasLoadedProfile));
        OnPropertyChanged(nameof(LoadedProfileStatusText));
        OnPropertyChanged(nameof(LoadedProfileStatusColor));

        foreach (var item in Profiles)
            item.IsActive = item.DisplayKey == _activeProfileKey;

        FeedbackMessage = $"Perfil carregado: {profile.DisplayKey}";
    }

    private void LoadSelectedProfile()
    {
        if (SelectedProfile is null) { FeedbackMessage = "Selecione um perfil na lista para carregar."; return; }
        LoadProfile(SelectedProfile);
    }

    private async Task SaveProfileAsync()
    {
        var op = (OperatorName ?? string.Empty).Trim();
        var wp = (WeaponName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(op) || string.IsNullOrWhiteSpace(wp))
        {
            FeedbackMessage = "Preencha Operador e Arma para salvar.";
            return;
        }

        try
        {
            var profile = new WeaponProfile
            {
                OperatorName = op,
                WeaponName = wp,
                Side = Side.ToUpperInvariant(),
                StrengthY = StrengthY,
                StrengthX = StrengthX,
                Delay = Delay,
                Smooth = (int)Math.Clamp(Math.Round(Smooth), 1, 15),
                AccelFactor = AccelFactor,
                MaxProgression = MaxProgression,
                // ── Spray Table ────────────────────────────────────────────
                UseSprayPattern = UseSprayPattern,
                FireRateRpm = FireRateRpm,
                SprayPoints = SprayPoints
                    .Select((p, i) => new SprayPoint { Index = i, X = p.X, Y = p.Y })
                    .ToList()
            };

            await _repository.SaveAsync(profile);
            _activeProfileKey = profile.DisplayKey;
            await RefreshProfilesAsync();

            FeedbackMessage = $"Perfil salvo: {profile.DisplayKey}";
            SelectedTab = "Perfis";
        }
        catch (Exception ex) { FeedbackMessage = $"Erro ao salvar: {ex.Message}"; }
    }

    private async Task DeleteProfileAsync()
    {
        var op = (OperatorName ?? string.Empty).Trim();
        var wp = (WeaponName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(op) || string.IsNullOrWhiteSpace(wp))
        {
            FeedbackMessage = "Selecione ou informe um perfil para excluir.";
            return;
        }

        try
        {
            await _repository.DeleteAsync(op, wp);
            var deletedKey = $"{op} | {wp}";

            if (_activeProfileKey == deletedKey) _activeProfileKey = null;
            if (_loadedProfileKey == deletedKey)
            {
                _loadedProfileKey = null;
                OnPropertyChanged(nameof(HasLoadedProfile));
                OnPropertyChanged(nameof(LoadedProfileStatusText));
                OnPropertyChanged(nameof(LoadedProfileStatusColor));
            }

            await RefreshProfilesAsync();
            ClearEditor();
            FeedbackMessage = $"Perfil removido: {op} | {wp}";
        }
        catch (Exception ex) { FeedbackMessage = $"Erro ao excluir: {ex.Message}"; }
    }

    private async Task ResetProfilesAsync()
    {
        try
        {
            await _repository.ResetAsync();
            _activeProfileKey = null;
            _loadedProfileKey = null;
            OnPropertyChanged(nameof(HasLoadedProfile));
            OnPropertyChanged(nameof(LoadedProfileStatusText));
            OnPropertyChanged(nameof(LoadedProfileStatusColor));
            Profiles.Clear();
            ClearEditor();
            FeedbackMessage = "Todos os perfis foram removidos.";
        }
        catch (Exception ex) { FeedbackMessage = $"Erro no reset: {ex.Message}"; }
    }

    private void ClearEditor()
    {
        OperatorName = string.Empty;
        WeaponName = string.Empty;
        Side = "ATK";
        StrengthY = 4.5;
        StrengthX = 0.0;
        Delay = 0.012;
        Smooth = 5;
        AccelFactor = 1.2;
        MaxProgression = 2.5;
        // ── Spray Table ────────────────────────────────────────────────────
        UseSprayPattern = false;
        FireRateRpm = 750;
        SprayPoints.Clear();
        OnPropertyChanged(nameof(SprayPointCount));
        _activeProfileKey = null;
        FeedbackMessage = "Editor limpo.";
    }

    private void OnEngineEnabledChanged(object? sender, bool enabled) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            EngineEnabled = enabled;
            FeedbackMessage = enabled ? "Engine ativa. Use F2 para alternar rapidamente." : "Engine inativa.";
        });

    private void OnAdsEnabledChanged(object? sender, bool enabled) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            AdsEnabled = enabled;
            FeedbackMessage = enabled ? "ADS Assist ativo. F3 alterna rapidamente." : "ADS Assist inativo.";
        });

    private static string FormatThree(double value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);

    private static bool TryParseFlexibleDouble(string input, out double value) =>
        double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
        double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out value);

    private static double Round3(double value) =>
        Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private void TrySetNumericInput(string? text, double min, double max, Action<double> assign)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!TryParseFlexibleDouble(text.Trim(), out var parsed)) return;
        assign(Round3(Math.Clamp(parsed, min, max)));
    }

    public void Dispose()
    {
        _engine.EnabledChanged -= OnEngineEnabledChanged;
        _engine.AdsEnabledChanged -= OnAdsEnabledChanged;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
        _refreshGate.Dispose();
    }
}
