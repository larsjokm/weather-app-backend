namespace weather_app_backend.Models;

public class WeatherDto
{
    public double Temperature { get; set; }
    public double WindSpeed { get; set; }
    public double WindDirection { get; set; }
    public double Humidity { get; set; }
    public double CloudCover { get; set; }
    public double Pressure { get; set; }
    public string SymbolCode { get; set; } = string.Empty;
    public double Precipitation { get; set; }
    public string Time { get; set; } = string.Empty;
    public WeatherUnitsDto Units { get; set; } = new();
}

public class WeatherUnitsDto
{
    public string Temperature { get; set; } = string.Empty;
    public string WindSpeed { get; set; } = string.Empty;
    public string Precipitation { get; set; } = string.Empty;
}