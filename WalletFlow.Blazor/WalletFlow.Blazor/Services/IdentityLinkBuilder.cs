using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace WalletFlow.Blazor.Services;

public static class IdentityLinkBuilder
{
    public static string Build(
        string path,
        string userId,
        string token,
        IConfiguration configuration,
        IHostEnvironment environment,
        NavigationManager navigation)
    {
        var publicBaseUrl = configuration["Identity:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Configure Identity:PublicBaseUrl antes de enviar links de autenticação.");
            publicBaseUrl = navigation.BaseUri;
        }

        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var encodedUserId = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(userId));
        var query = QueryString.Create(new Dictionary<string, string?>
        {
            ["userId"] = encodedUserId,
            ["token"] = encodedToken
        });
        return $"{publicBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}{query}";
    }
}
