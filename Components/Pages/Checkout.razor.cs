using Microsoft.AspNetCore.Components;

namespace HealthHackSgSeedFundUsPlease.Components.Pages;

public partial class Checkout
{
    [Inject]
    public NavigationManager NavigationManager { get; set; }

    private void NavigateHome()
    {
        NavigationManager.NavigateTo("/");
    }
}
