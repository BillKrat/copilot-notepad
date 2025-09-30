using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotePadAI.ProjectSetup.Configuration
{
    public class Auth0Config
    {
        public required string Domain { get; set; }
        public required string ClientId { get; set; }
        public required string Audience { get; set; }
    }
}
