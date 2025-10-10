using Adventures.Shared.Interfaces;
using Adventures.Shared.LayerDataAccess;
using Adventures.Shared.Results;
using Adventures.Shared.Sample.Entities;
using Adventures.Shared.Sample.Interfaces;
using Adventures.Shared.Users;
using Microsoft.Extensions.Logging;

namespace Adventures.Shared.Sample
{
    public class WeatherDal(IUser? user, ILogger<WeatherDal> logger) : DalBase, IWeatherDal, ILifetimeScoped
    {

        private static readonly string[] Summaries = new[] {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        public override RequestResult Read(object? sender, EventArgs? e)
        {
            var result = new RequestResult { Data = Get() };
            return result;

        }

        public IEnumerable<WeatherForecast> Get()
        {
            logger.LogInformation("Weather forecast requested by user: {user?.Id}  {user?.Name}", user?.Id, user?.Name);

            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}
