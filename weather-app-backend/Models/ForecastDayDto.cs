namespace weather_app_backend.Models;

public class ForecastDayDto
{
    public string Date { get; set; } = string.Empty;
    public int Temp { get; set; }
    public string SymbolCode { get; set; } = string.Empty;
    public double Precipitation { get; set; }
    public double WindSpeed { get; set; }
}