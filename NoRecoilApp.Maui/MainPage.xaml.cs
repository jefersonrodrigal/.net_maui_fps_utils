namespace NoRecoilApp.Maui;

public partial class MainPage : ContentPage
{
    public MainPage(ViewModels.MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.MainViewModel vm)
            await vm.InitializeAsync();
    }
}
