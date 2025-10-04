using Adventures.Shared.Ftp.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adventures.Shared.Ftp.Util
{
    public static class SlotRouter
    {
        public static async Task RouteFileAsync(IFtpClientAsync ftp, string fileName, string fromSlot, string toSlot, CancellationToken token)
        {
            var source = $"/{fromSlot}/{fileName}";
            var destination = $"/{toSlot}/{fileName}";
            await ftp.MoveFileAsync(source, destination, token);
        }
    }

}
