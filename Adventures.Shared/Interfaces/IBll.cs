using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adventures.Shared.Interfaces
{
    public interface IBll
    {
        IDal? Dal { get; set; }
    }
}
