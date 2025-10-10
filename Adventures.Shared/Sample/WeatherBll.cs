using Adventures.Shared.Extensions;
using Adventures.Shared.Interfaces;
using Adventures.Shared.LayerBusinessLogic;
using Adventures.Shared.Sample.Entities;
using Adventures.Shared.Sample.Interfaces;
using Adventures.Shared.Users;
using Microsoft.Extensions.Logging;

namespace Adventures.Shared.Sample
{
    public class WeatherBll(IWeatherDal dal, IUser? user, ILogger<WeatherBll> logger) : BllBase, IWeatherBll, ILifetimeScoped
    {

        public IEnumerable<WeatherForecast>? GetWeatherForecasts(object? sender = null, EventArgs? e = null)
        {
            logger.LogInformation("Weather forecast requested by user: {user?.Id}  {user?.Name}", user?.Id, user?.Name);


            var results = dal?.Read(sender, e);
            return results?.Data.As<IEnumerable<WeatherForecast>>();
        }

    }
}
