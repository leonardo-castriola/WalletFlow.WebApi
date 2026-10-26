using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using WalletFlow.WebApi.Entities;

namespace WalletFlow.Blazor.Services;

public sealed class IdentityRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(5);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        if (authenticationState.User.Identity?.IsAuthenticated != true)
            return true;

        using var scope = scopeFactory.CreateScope();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<WalletUser>>();
        return await signInManager.ValidateSecurityStampAsync(authenticationState.User) is not null;
    }
}
