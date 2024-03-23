
using Microsoft.EntityFrameworkCore;
using SaveHere.WebAPI.Models.db;

namespace SaveHere.WebAPI;

public class Program
{
  public static void Main(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite("Data Source=/app/db/database.sqlite3.db"));

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddScoped<HttpClient>();

    var app = builder.Build();

    //if (app.Environment.IsDevelopment())
    {
      app.UseSwagger();
      app.UseSwaggerUI();
    }

    app.UseAuthorization();

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
