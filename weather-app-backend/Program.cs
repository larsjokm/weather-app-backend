using weather_app_backend.Data;
using weather_app_backend.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient<WeatherService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("WebApiDatabase")
    )
);

// had cors error so yea ai fixed it
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

app.UseHttpsRedirection();

app.MapControllers();

// check if db connected successfully
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.CanConnectAsync();
        Console.WriteLine("Database connection established");
    }
    catch (Exception e)
    {
        Console.WriteLine($"Database connection failed: {e.Message}");
    }
}

app.Run();