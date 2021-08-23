using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TgBot
{
    public class UserFriendlyError:Exception
    {
        public UserFriendlyError(String msg):base(msg)
        {

        }
    }
}
