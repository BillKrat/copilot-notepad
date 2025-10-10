using Adventures.Shared.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adventures.Shared.Interfaces
{
    public interface IDal
    {
        RequestResult Create(object? sender, EventArgs? e);
        RequestResult Read(object? sender, EventArgs? e);
        RequestResult Update(object? sender, EventArgs? e);
        RequestResult Delete(object? sender, EventArgs? e);
        RequestResult List(object? sender, EventArgs? e);
        RequestResult Search(object? sender, EventArgs? e);
        RequestResult Count(object? sender, EventArgs? e);
        RequestResult Exists(object? sender, EventArgs? e);
        RequestResult Custom(object? sender, EventArgs? e);

    }
}
