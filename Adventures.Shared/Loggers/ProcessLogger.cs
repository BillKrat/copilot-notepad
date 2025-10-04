using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.Extensions.Logging;

namespace Adventures.Shared.Loggers
{


    public class ProgressLogger : IProgress<FtpProgress>
    {
        private readonly ILogger _logger;

        public ProgressLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void Report(FtpProgress value)
        {
            _logger.LogInformation("Progress: {Progress}% - {Path}", value.Progress, value.LocalPath);
        }
    }

}
