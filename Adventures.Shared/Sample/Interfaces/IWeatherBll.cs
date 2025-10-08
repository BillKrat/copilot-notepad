using Adventures.Shared.Interfaces;
using Adventures.Shared.Sample.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adventures.Shared.Sample.Interfaces
{
    public interface IWeatherBll : IBll
    {
        // Existing event-style method (kept for compatibility)
        IEnumerable<WeatherForecast> GetWeatherForecasts(object sender, EventArgs e);

    }
}
