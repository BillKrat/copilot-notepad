using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotePadAI.ProjectSetup.Configuration
{
    public class EnvironmentConfig
    {
        public bool Production { get; set; }
        public bool UseProxy { get; set; }
        public required string ApiUrl { get; set; }
        public required Auth0Config Auth0 { get; set; }
    }
}
