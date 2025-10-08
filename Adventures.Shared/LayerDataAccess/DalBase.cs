using Adventures.Shared.Interfaces;
using Adventures.Shared.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adventures.Shared.LayerDataAccess
{
    public class DalBase : IDal
    {
        public virtual RequestResult Count(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public virtual RequestResult Create(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public virtual RequestResult Custom(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public virtual RequestResult Delete(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public virtual RequestResult Exists(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public virtual RequestResult List(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public virtual RequestResult Read(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public virtual RequestResult Search(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public virtual RequestResult Update(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
