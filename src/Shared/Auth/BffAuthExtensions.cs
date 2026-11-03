using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json;

namespace Shared.Auth;

public static class BffAuthExtensions
{
    public static IHostApplicationBuilder AddBffAuth(
        this IHostApplicationBuilder builder,
        Action<AuthorizationOptions>? configureAuthorization = null)
    {
        var jwtSection = builder.Configuration.GetSection("Jwt");
        var oidcSection = builder.Configuration.GetSection("Oidc");

        var jwtOpts = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
        builder.Services.Configure<JwtOptions>(jwtSection);

        var keysPath = builder.Configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrEmpty(keysPath))
        {
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new System.IO.DirectoryInfo(keysPath))
                .SetApplicationName("bff-gateway")
                .DisableAutomaticKeyGeneration();
        }

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = "DualAuth";
                options.DefaultChallengeScheme = "DualAuth";
                options.DefaultForbidScheme = "DualAuth";
                options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddPolicyScheme("DualAuth", displayName: null, options =>
            {
                options.ForwardDefaultSelector = ctx =>
                {
                    var auth = ctx.Request.Headers.Authorization.ToString();
                    return auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? JwtBearerDefaults.AuthenticationScheme
                        : CookieAuthenticationDefaults.AuthenticationScheme;
                };
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.Name = "bff.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ForwardChallenge = OpenIdConnectDefaults.AuthenticationScheme;
                options.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Authority = jwtOpts.Authority;
                options.MetadataAddress = jwtOpts.MetadataAddress;
                options.RequireHttpsMetadata = false;
                options.BackchannelHttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                options.ClientId = oidcSection["ClientId"] ?? "";
                options.ClientSecret = oidcSection["ClientSecret"] ?? "";
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;

                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;

                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.CorrelationCookie.SameSite = SameSiteMode.None;
                options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.NonceCookie.SameSite = SameSiteMode.None;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("offline_access");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "preferred_username",
                };

                options.Events.OnTokenValidated = ctx =>
                {
                    var accessToken = ctx.TokenEndpointResponse?.AccessToken;
                    if (!string.IsNullOrEmpty(accessToken) &&
                        ctx.Principal?.Identity is ClaimsIdentity id &&
                        id.FindFirst("realm_access") is null)
                    {
                        var realmAccess = ReadRealmAccess(accessToken);
                        if (realmAccess is not null)
                            id.AddClaim(new Claim("realm_access", realmAccess));
                    }
                    return Task.CompletedTask;
                };
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = jwtOpts.Authority;
                options.MetadataAddress = jwtOpts.MetadataAddress;
                options.RequireHttpsMetadata = false;
                options.Audience = jwtOpts.Audience;
                options.BackchannelHttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "preferred_username",
                };
            });

        builder.Services.AddSingleton<IClaimsTransformation, KeycloakRolesTransformer>();

        builder.Services.AddAuthorization(options =>
        {
            configureAuthorization?.Invoke(options);
        });

        return builder;
    }

    private static string? ReadRealmAccess(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
            return null;

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("realm_access", out var ra)
            ? ra.GetRawText()
            : null;
    }
}