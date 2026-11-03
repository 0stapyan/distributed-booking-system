using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Auth;

public static class ServiceAuthExtensions
{
    public static IHostApplicationBuilder AddServiceAuth(
        this IHostApplicationBuilder builder,
        Action<AuthorizationOptions>? configureAuthorization = null)
    {
        var jwtSection = builder.Configuration.GetSection("Jwt");
        var jwtOpts = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
        builder.Services.Configure<JwtOptions>(jwtSection);

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
            configureAuthorization?.Invoke(options);
        });

        return builder;
    }
}