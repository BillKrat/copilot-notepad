using Adventures.Shared.Event;
using Adventures.Shared.Sample.Entities;
using Adventures.Shared.Sample.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NotebookAI.Server.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize] 
public class WeatherForecastController(IWeatherBll bll, ILogger<WeatherForecastController> logger) : ControllerBase
{
    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast>? Get()
    {
        logger.LogInformation("Weather forecast requested by user: {UserId}", User?.Identity?.Name ?? "Anonymous");
        var results = bll.GetWeatherForecasts();   
        
        return results;
    }
}
