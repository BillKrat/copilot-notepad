using Adventures.Shared.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adventures.Shared.LayerBusinessLogic
{
    public class BllBase : IBll
    {
        public IDal? Dal { get; set; }
    }
}
