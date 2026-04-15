using Microsoft.AspNetCore.Mvc;
using weather_app_backend.Models;
using weather_app_backend.Services;

namespace weather_app_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeatherController : ControllerBase
{
    private readonly WeatherService _weatherService;

    public WeatherController(WeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    // all endpoints i had in frontend
    [HttpGet("/api/cities")]
    public async Task<IActionResult> GetCities([FromQuery] string countryCode)
    {
        var cities = await _weatherService.GetCitiesAsync(countryCode);
        return Ok(cities);
    }
    
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent([FromQuery] double lat, [FromQuery] double lon)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers["User-Agent"].ToString();

        var weather = await _weatherService.GetCurrentWeatherAsync(lat, lon);
        await _weatherService.LogIpAsync(ip, userAgent, "/api/weather/current");

        return Ok(weather);
    }
    
    // 7 day forecast endpoint
    [HttpGet("forecast")]
    public async Task<IActionResult> GetForecast([FromQuery] double lat, [FromQuery] double lon)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    
        var forecast = await _weatherService.GetForecastAsync(lat, lon);
        await _weatherService.LogForecastAsync(ip, lat, lon, forecast);
        return Ok(forecast);
    }
    
    [HttpGet("location")]
    public async Task<IActionResult> GetLocation([FromQuery] double lat, [FromQuery] double lon)
    {
        var (city, country) = await _weatherService.GetLocationAsync(lat, lon);
        return Ok(new LocationDto { City = city, Country = country });
    }
}