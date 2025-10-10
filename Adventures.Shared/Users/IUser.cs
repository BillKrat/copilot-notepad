using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adventures.Shared.Users
{
    public interface IUser
    {
        string? Id { get; set; }
        string? Name { get; set; }
        string? Email { get; set; }
        List<string>? Roles { get; set; }
        DateTime CreatedAt { get; set; }
        DateTime UpdatedAt { get; set; }
        bool IsActive { get; set; }

    }
}
