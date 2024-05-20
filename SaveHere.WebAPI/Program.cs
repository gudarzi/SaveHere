using Microsoft.EntityFrameworkCore;
using SaveHere.WebAPI.Models.db;
using Microsoft.AspNetCore.WebSockets;

namespace SaveHere.WebAPI;

public class Program
{
  public static void Main(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite("Data Source=/app/db/database.sqlite3.db"));

    var apiCorsPolicy = "ApiCorsPolicy";
    builder.Services.AddCors(options =>
    {
      options.AddPolicy(name: apiCorsPolicy,
                        builder =>
                        {
                          builder
                                  .AllowAnyOrigin()
                                  .AllowAnyHeader()
                                  .AllowAnyMethod();
                        });
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddWebSockets(options => { options.KeepAliveInterval = TimeSpan.FromMinutes(2); });

    builder.Services.AddScoped<HttpClient>();

    var app = builder.Build();

    // Configure WebSocket route
    app.UseWebSockets();

    // WebSocket endpoint
    app.Map("/ws", async context =>
    {
      if (context.WebSockets.IsWebSocketRequest)
      {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await WebSocketHandler.HandleWebSocketAsync(webSocket);
      }
      else
      {
        context.Response.StatusCode = 400;
      }
    });

    //if (app.Environment.IsDevelopment())
    {
      app.UseSwagger();
      app.UseSwaggerUI();
    }

    app.UseAuthorization();
    if (app.Environment.IsDevelopment()) app.UseCors(apiCorsPolicy);
    app.MapControllers();

    using (var scope = app.Services.CreateScope())
    {
      var dbc = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      dbc.Database.EnsureCreated();
      dbc.Database.Migrate();

      //dbc.FileDownloadQueueItems.Add(new Models.FileDownloadQueueItem() { InputUrl = "https://dummy.me" });
      //dbc.SaveChanges();
    }

    app.Run();
  }
}
