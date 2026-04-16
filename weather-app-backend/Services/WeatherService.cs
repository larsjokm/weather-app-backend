using System.Globalization;
using weather_app_backend.Models;
using System.Text.Json;
using weather_app_backend.Data;

namespace weather_app_backend.Services;

public class WeatherService(HttpClient httpClient, IConfiguration configuration, AppDbContext db)
{
    private readonly HttpClient _httpClient = httpClient;
    // moved api and email to secret for repo 
    private readonly string _apiKey = configuration["RapidApiKey"]!;
    private readonly string _email = configuration["Email"]!;
    private readonly AppDbContext _db = db;
    
    // db calls

    public async Task LogIpAsync(string ip, string userAgent, string endpoint)
    {
        _db.IpLogs.Add(new IpLogDto
        {
            IpAddress = ip,
            UserAgent = userAgent,
            Endpoint = endpoint
        });
        await _db.SaveChangesAsync();
        Console.WriteLine($"Logged IP: {ip} on {endpoint}");
    }
    
    // log data of forecast requests 
    public async Task LogForecastAsync(string ip, double lat, double lon, List<ForecastDayDto> forecast)
    {
        var toLog = new ForecastLogDto
        {
            IpAddress = ip,
            Lat = lat,
            Lon = lon,
            ForecastJson = System.Text.Json.JsonSerializer.Serialize(forecast)
        };
        _db.ForecastLogs.Add(toLog);
        await _db.SaveChangesAsync();
        Console.WriteLine($"Wrote forecast to DB: {toLog.Id}");
        
    }
    
    public async Task<List<CityDto>> GetCitiesAsync(string countryCode)
    {
        // js add everything so i dont gotta do it for every request
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-rapidapi-host", "wft-geo-db.p.rapidapi.com");
        _httpClient.DefaultRequestHeaders.Add("x-rapidapi-key", _apiKey);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MyWeatherApp/1.0");
        
        var response = await _httpClient.GetAsync(
            $"https://wft-geo-db.p.rapidapi.com/v1/geo/cities?countryIds={countryCode.ToUpper()}&sort=-population&limit=10");

        // stringify
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = json.GetProperty("data"); // top level of json obj

        var cities = new List<CityDto>();
        var seenNames = new HashSet<string>(); // no idea what hashset does

        // find the city attr
        foreach (var d in data.EnumerateArray())
        {
            var name = d.GetProperty("city").GetString()!;
            if (name.ToLower().Contains("municipality")) continue;
            
            if (!seenNames.Add(name.ToLower())) continue;
            
            // append dto
            cities.Add(new CityDto
            {
                Name = name,
                Country = d.GetProperty("country").GetString()!,
                Lat = d.GetProperty("latitude").GetDouble(),
                Lon = d.GetProperty("longitude").GetDouble(),
                Population = d.GetProperty("population").GetInt32()
            });
            if (cities.Count == 5) break;
        }
        return cities;
    }
    
    // the current weather displayed on the main card
    public async Task<WeatherDto> GetCurrentWeatherAsync(double lat, double lon)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "weather-app-backend/1.0");

        var response = await _httpClient.GetAsync(
            $"https://api.met.no/weatherapi/locationforecast/2.0/compact?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        
        // extract strings from the json object, not doing allat myself
        var weather = await response.Content.ReadFromJsonAsync<JsonElement>();
        var current = weather.GetProperty("properties").GetProperty("timeseries")[0];
        var details = current.GetProperty("data").GetProperty("instant").GetProperty("details");
        var units = weather.GetProperty("properties").GetProperty("meta").GetProperty("units");

        // i did NOT write ts myself
        return new WeatherDto
        {
            Temperature = details.GetProperty("air_temperature").GetDouble(),
            WindSpeed = details.GetProperty("wind_speed").GetDouble(),
            WindDirection = details.GetProperty("wind_from_direction").GetDouble(),
            Humidity = details.GetProperty("relative_humidity").GetDouble(),
            CloudCover = details.GetProperty("cloud_area_fraction").GetDouble(),
            Pressure = details.GetProperty("air_pressure_at_sea_level").GetDouble(),
            SymbolCode = current.GetProperty("data").GetProperty("next_1_hours").GetProperty("summary").GetProperty("symbol_code").GetString()!,
            Precipitation = current.GetProperty("data").GetProperty("next_1_hours").GetProperty("details").GetProperty("precipitation_amount").GetDouble(),
            Time = current.GetProperty("time").GetString()!,
            Units = new WeatherUnitsDto
            {
                Temperature = units.GetProperty("air_temperature").GetString()!,
                WindSpeed = units.GetProperty("wind_speed").GetString()!,
                Precipitation = units.GetProperty("precipitation_amount").GetString()!
            }
        };
    }
    
    // 7 day forecast
    public async Task<List<ForecastDayDto>> GetForecastAsync(double lat, double lon)
    {
        var url = $"https://api.met.no/weatherapi/locationforecast/2.0/complete?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"MyWeatherApp/1.0 ({_email})");
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var timeseries = doc.RootElement
            .GetProperty("properties")
            .GetProperty("timeseries")
            .EnumerateArray();

        // seperate days
        var daysMap = new Dictionary<string, (JsonElement ts, int diff)>();

        foreach (var ts in timeseries)
        {
            var time = DateTime.Parse(ts.GetProperty("time").GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind);
            var dayKey = time.ToString("yyyy-MM-dd");
            
            // get the forecast for 12:00 cause i felt like it
            var diffFromNoon = Math.Abs(time.Hour - 12);

            if (!daysMap.ContainsKey(dayKey) || diffFromNoon < daysMap[dayKey].diff)
                daysMap[dayKey] = (ts, diffFromNoon);
        }

        var forecast = new List<ForecastDayDto>();

        // sort out max 7 days
        foreach (var (_, (ts, _)) in daysMap)
        {
            if (forecast.Count >= 7) break;

            var time = DateTime.Parse(ts.GetProperty("time").GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind);
            var details = ts.GetProperty("data").GetProperty("instant").GetProperty("details");
            var data = ts.GetProperty("data");

            // idk, ai formatting json 
            var symbolCode = GetNestedString(data, "next_6_hours", "summary", "symbol_code")
                ?? GetNestedString(data, "next_12_hours", "summary", "symbol_code")
                ?? GetNestedString(data, "next_1_hours", "summary", "symbol_code")
                ?? "";

            var precipitation = GetNestedDouble(data, "next_6_hours", "details", "precipitation_amount")
                ?? GetNestedDouble(data, "next_12_hours", "details", "precipitation_amount")
                ?? GetNestedDouble(data, "next_1_hours", "details", "precipitation_amount")
                ?? 0;

            // format to dto
            forecast.Add(new ForecastDayDto
            {
                Date = time.ToString("ddd, MMM d", System.Globalization.CultureInfo.InvariantCulture),
                Temp = (int)Math.Round(details.GetProperty("air_temperature").GetDouble()),
                SymbolCode = symbolCode,
                Precipitation = precipitation,
                WindSpeed = details.GetProperty("wind_speed").GetDouble()
            });
        }

        return forecast;
    }

    // no idea
    private static string? GetNestedString(JsonElement root, string key1, string key2, string key3)
    {
        if (root.TryGetProperty(key1, out var l1) &&
            l1.TryGetProperty(key2, out var l2) &&
            l2.TryGetProperty(key3, out var l3))
            return l3.GetString();
        return null;
    }

    // no idea
    private static double? GetNestedDouble(JsonElement root, string key1, string key2, string key3)
    {
        if (root.TryGetProperty(key1, out var l1) &&
            l1.TryGetProperty(key2, out var l2) &&
            l2.TryGetProperty(key3, out var l3))
            return l3.GetDouble();
        return null;
    }
    
    // reverse the lat n lon from users’ granted location permission and get city name
    // simple in-memory cache (top of class)
private static readonly Dictionary<string, (string City, string Country, DateTime Expiry)> _locationCache = new();

public async Task<(string City, string Country)> GetLocationAsync(double lat, double lon)
{
    var key = $"{lat:F4},{lon:F4}";

    // caching to limit external api calls cause i got rate limited when switching to cloud hosting for some reason
    if (_locationCache.TryGetValue(key, out var cached) && cached.Expiry > DateTime.UtcNow)
    {
        return (cached.City, cached.Country);
    }

    var url = $"https://nominatim.openstreetmap.org/reverse?lat={lat.ToString(CultureInfo.InvariantCulture)}&lon={lon.ToString(CultureInfo.InvariantCulture)}&format=jsonv2";

    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.UserAgent.ParseAdd($"MyWeatherApp/1.0 ({_email})");

    try
        {
            var response = await _httpClient.SendAsync(request);

            // handle rate limit
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                Console.WriteLine("Nominatim rate limit hit");

                return ("Unknown location", "");
            }

            // in case of some random error, catch it so it doesnt crash
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Nominatim failed: {response.StatusCode}");
                return ("Unknown location", "");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("address", out var address))
                return ("Unknown location", "");

            var city = GetAddressField(address, "city")
                       ?? GetAddressField(address, "town")
                       ?? GetAddressField(address, "village")
                       ?? GetAddressField(address, "county")
                       ?? "Unknown location";

            var country = GetAddressField(address, "country") ?? "";

            // store in cache for later. maybe implement fetching new info after x interval to update information
            _locationCache[key] = (city, country, DateTime.UtcNow.AddMinutes(5));

            return (city, country);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Location error: {ex.Message}");

            // handle so it doesnt return 500 and crashes
            return ("Unknown location", "");
        }
    }

    // idk what this is
    private static string? GetAddressField(JsonElement address, string key)
    {
        if (address.TryGetProperty(key, out var val))
            return val.GetString();
        return null;
    }
}