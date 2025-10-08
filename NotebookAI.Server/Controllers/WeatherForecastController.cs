using Adventures.Shared.Event;
using Adventures.Shared.Sample.Entities;
using Adventures.Shared.Sample.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NotebookAI.Server.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize] // This ensures all actions in this controller require authentication
public class WeatherForecastController(IWeatherBll bll, ILogger<WeatherForecastController> logger) : ControllerBase
{



    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        // You can access user information from the JWT token
        var userId = User.FindFirst("sub")?.Value;
        var userEmail = User.FindFirst("email")?.Value;

        var dict = new Dictionary<string, object?>
        {
            ["userId"] = userId,
            ["userEmail"] = userEmail
        };
        var args = new JsonEventArgs(dict);

        var results = bll.GetWeatherForecasts(this, args);   
        
        logger.LogInformation("Weather forecast requested by user: {UserId}", userId);

        return results;
    }
}
