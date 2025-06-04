using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace SportManager.API;

public static class ConfigureServices
{
    public static IServiceCollection AddServicesDependency(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
       
        // services.AddCachingWorkspaceResolverService();
        return services;
    }

    public static IServiceCollection AddIdempotent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        //services.AddIdempotentAPI();
        //services.AddIdempotentRedisCache(configuration);
        return services;
    }

    public static IServiceCollection AddJwtBearer(
    this IServiceCollection services,
    IConfiguration configuration,
    Action<JwtBearerOptions> configureOptions = null)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT Secret key not configured");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];

                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) &&
                        path.StartsWithSegments("/chatHub"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };

            configureOptions?.Invoke(options);
        });

        return services;
    }

    public static IServiceCollection AddAuthentication(
     this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        if (env.EnvironmentName.Equals("IntegrationTest"))
        {
            return services;
        }

        services.AddJwtBearer(configuration, options =>
        {
            if (env.IsDevelopment())
            {
                options.RequireHttpsMetadata = false;
            }
        });

        services.AddAuthorization(options =>
        {
            // Policy mặc định
            var defaultAuthorizationPolicyBuilder =
                new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme);
            defaultAuthorizationPolicyBuilder =
                defaultAuthorizationPolicyBuilder.RequireAuthenticatedUser();
            options.DefaultPolicy = defaultAuthorizationPolicyBuilder.Build();

            // Thêm policy phân quyền
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireRole("Admin"));

            options.AddPolicy("UserOnly", policy =>
                policy.RequireRole("User"));

            options.AddPolicy("AdminOrManager", policy =>
                policy.RequireRole("Admin", "Manager"));

        });

        return services;
    }

    public static IServiceCollection AddCustomDapr(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        //services.AddActors(options =>
        //{
        //    //options.Actors.RegisterActor<SoStatusUpdateActor>();
        //    //options.Actors.RegisterActor<SalesOrderRequestCanceledActor>();
        //    //options.Actors.RegisterActor<PurchaseOrderCreatePackageActor>();
        //    //options.Actors.RegisterActor<PurchaseOrderActor>();
        //    //options.Actors.RegisterActor<ShipmentActor>();
        //});

        //services.AddDaprClient(
        //    opt =>
        //    {
        //        // opt.UseHttpEndpoint(configuration.GetValue<string>("Dapr:HttpEndpoint", "http://localhost:3500"));
        //        // opt.UseGrpcEndpoint(configuration.GetValue<string>("Dapr:GrpcEndpoint", "http://localhost:50001"));
        //    }
        //);

        return services;
    }

    public static WebApplication MapDaprEndpoints(this WebApplication app)
    {
        //app.MapActorsHandlers();
        //app.MapSubscribeHandler();
        return app;
    }
}