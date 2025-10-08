using Adventures.Shared.Interfaces;
using Adventures.Shared.LayerBusinessLogic;
using Adventures.Shared.Sample.Entities;
using Adventures.Shared.Sample.Interfaces;

namespace Adventures.Shared.Sample
{
    public class WeatherBll : BllBase, IWeatherBll, ILifetimeScoped
    {
        public WeatherBll(IWeatherDal dal)
        {
            Dal = dal;
        }

        public IEnumerable<WeatherForecast> GetWeatherForecasts(object sender, EventArgs e)
        {
            var results = Dal?.Read(sender, e);
            return [];
        }

        // Parameterless overload for typical callers (e.g., controllers) that do not use event pattern
        public IEnumerable<WeatherForecast> GetWeatherForecasts()
        {
            // Use 'this' as sender and EventArgs.Empty for now.
            return GetWeatherForecasts(this, EventArgs.Empty);
        }
    }
}
