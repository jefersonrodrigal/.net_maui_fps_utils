using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using NoRecoilApp.Maui.ViewModels;

namespace NoRecoilApp.Maui;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage()
    {
        InitializeComponent();

        _viewModel = MauiProgram.Services.GetRequiredService<MainViewModel>();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        await _viewModel.InitializeAsync();
        await RunIntroAnimationAsync();
    }

    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnDisappearing();
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.SelectedTab))
        {
            return;
        }

        await AnimateTabTransitionAsync();
    }

    private async Task RunIntroAnimationAsync()
    {
        HeaderCard.Opacity = 0;
        HeaderCard.TranslationY = -10;

        await Task.WhenAll(
            HeaderCard.FadeToAsync(1, 220, Easing.CubicOut),
            HeaderCard.TranslateToAsync(0, 0, 220, Easing.CubicOut));
    }

    private async Task AnimateTabTransitionAsync()
    {
        if (!TabContentHost.IsVisible)
        {
            return;
        }

        await Task.WhenAll(
            TabContentHost.FadeToAsync(0.82, 70, Easing.CubicIn),
            TabContentHost.TranslateToAsync(0, 4, 70, Easing.CubicIn));

        await Task.WhenAll(
            TabContentHost.FadeToAsync(1, 140, Easing.CubicOut),
            TabContentHost.TranslateToAsync(0, 0, 140, Easing.CubicOut));
    }
}
