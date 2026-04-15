namespace weather_app_backend.Models;

public class ForecastLogDto
{
    public int Id { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lon { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string ForecastJson { get; set; } = string.Empty; 
}