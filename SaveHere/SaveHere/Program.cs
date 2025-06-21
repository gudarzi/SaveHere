using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using SaveHere.Components;
using SaveHere.Components.Account;
using SaveHere.Endpoints;
using SaveHere.Helpers;
using SaveHere.Hubs;
using SaveHere.Models;
using SaveHere.Models.db;
using SaveHere.Services;
using System.Net;

namespace SaveHere
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var builder = WebApplication.CreateBuilder(args);

      // Setup logging
      //builder.Logging.ClearProviders();
      //builder.Logging.AddConsole();
      //builder.Logging.SetMinimumLevel(LogLevel.Information);

      // Initializing the database
      var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "db");
      if (!Directory.Exists(dbPath))
      {
        try
        {
          Directory.CreateDirectory(dbPath);
        }
        catch
        {
          throw new InvalidOperationException("Could not create the database directory.");
        }
      }
      builder.Services.AddDbContextFactory<AppDbContext>(options =>
          options.UseSqlite($"Data Source={Path.Combine(dbPath, "database.sqlite3.db")}")
      );

      // Creating the download directory
      DirectoryBrowser.InitializeDownloadsDirectory();

      // Add MudBlazor services
      builder.Services.AddMudServices();

      // Add services to the container.
      builder.Services.AddRazorComponents()
          .AddInteractiveServerComponents()
          .AddInteractiveWebAssemblyComponents();

      builder.Services.AddCascadingAuthenticationState();
      builder.Services.AddScoped<IdentityUserAccessor>();
      builder.Services.AddScoped<IdentityRedirectManager>();
      builder.Services.AddScoped<AuthenticationStateProvider, PersistingRevalidatingAuthenticationStateProvider>();

      builder.Services.AddAuthentication(options =>
          {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
          })
          .AddIdentityCookies();

      builder.Services.AddDatabaseDeveloperPageExceptionFilter();

      builder.Services.AddIdentityCore<ApplicationUser>(options =>
      {
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
      })
          .AddRoles<IdentityRole>()
          .AddEntityFrameworkStores<AppDbContext>()
          .AddSignInManager()
          .AddRoleManager<RoleManager<IdentityRole>>()
          .AddDefaultTokenProviders();

      builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
      builder.Services.AddSingleton<DefaultCredentialsService>();
      builder.Services.AddHostedService<InitialSetupService>();
      builder.Services.AddScoped<IUserManagementService, UserManagementService>();

      builder.Services.AddScoped<IAuthorizationHandler, EnabledUserHandler>();
      builder.Services.AddAuthorizationCore(options =>
      {
        options.AddPolicy("EnabledUser", policy =>
      policy.Requirements.Add(new EnabledUserRequirement()));
      });

      builder.Services.ConfigureApplicationCookie(options =>
      {
        options.Cookie.HttpOnly = false;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
      });

      builder.Services.AddSignalR();

      builder.Services.AddSingleton<VersionState>();
      builder.Services.AddHostedService<SimpleVersionCheckerService>();

      builder.Services.AddSingleton<IProgressHubService, ProgressHubService>();

      builder.Services.AddSingleton<DownloadStateService>();

      builder.Services.AddHttpClient<IDownloadQueueService, DownloadQueueService>(client =>
          {
              // Enable HTTP/2
              client.DefaultRequestVersion = new Version(2, 0);
              client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
          })
          .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
          {
              // Allow automatic decompression
              AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli,
              // Connection pooling settings - read from configuration
              MaxConnectionsPerServer = builder.Configuration.GetValue<int>("DownloadSettings:MaxConnectionsPerServer", 10),
              // Enable cookies if needed
              UseCookies = true
          })
          .SetHandlerLifetime(TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("DownloadSettings:ConnectionPoolLifetimeMinutes", 5))); // Connection pool lifetime

      builder.Services.AddHttpClient();

      builder.Services.AddSingleton<IWarpPlusService, WarpPlusService>();

      builder.Services.AddScoped<IFileManagerService, FileManagerService>();

      builder.Services.AddScoped<IDownloadQueueService, DownloadQueueService>();

      builder.Services.AddSingleton<IYtdlpService, YtdlpService>();
      builder.Services.AddScoped<IYoutubeDownloadQueueService, YoutubeDownloadQueueService>();
      builder.Services.AddHostedService<YtdlpUpdateService>();

      builder.Services.AddScoped<SpotifySearchService>();

      builder.Services.AddSingleton<MediaConversionService>();

      builder.Services.AddSingleton<ShortLinkService>();

      var app = builder.Build();

      // Configure the HTTP request pipeline.
      if (app.Environment.IsDevelopment())
      {
        app.UseWebAssemblyDebugging();
        app.UseMigrationsEndPoint();
      }
      else
      {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        //app.UseHsts();
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
      }

      //app.UseHttpsRedirection();

      app.UseAuthentication();
      app.UseAuthorization();

      app.UseStaticFiles();
      app.UseAntiforgery();

      app.MapRazorComponents<App>()
          .AddInteractiveServerRenderMode()
          .AddInteractiveWebAssemblyRenderMode()
          .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

      // Add additional endpoints required by the Identity /Account Razor components.
      app.MapAdditionalIdentityEndpoints();

      app.MapHub<ProgressHub>("/DownloadProgressHub");

      // Register endpoints
      app.MapDownloadEndpoints();   // Handles /downloads/{filename} endpoints
      app.MapStreamingEndpoints(); // Handles /stream/{filename} endpoints
      app.MapYtdlpEndpoints();     // Handles /ytdlp/supportedsites.md endpoint

      // Ensuring the database is created
      using (var scope = app.Services.CreateScope())
      {
        var dbc = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbc.Database.EnsureCreated();

        try
        {
          dbc.Database.Migrate();
        }
        catch { /* pass for now */ }
      }

      app.Run();
    }
  }
}
