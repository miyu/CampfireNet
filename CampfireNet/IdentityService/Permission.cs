using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IdentityService
{
    [Flags]
    enum Permission
    {
        None = 0x0,
        Unicast = 0x1,
        Broadcast = 0x2,
        Invite = 0x4,
        All = 0x7
    }
}
