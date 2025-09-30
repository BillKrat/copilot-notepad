using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotePadAI.ProjectSetup.Configuration
{
    public class RootConfig
    {
        public required Dictionary<string, EnvironmentConfig> Environments { get; set; }
    }
}
