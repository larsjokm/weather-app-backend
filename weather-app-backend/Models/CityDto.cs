namespace weather_app_backend.Models;

public class CityDto
{
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lon { get; set; }
    public int Population { get; set; }
}