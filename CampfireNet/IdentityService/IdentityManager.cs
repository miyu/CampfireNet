using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IdentityService
{
    class IdentityManager
    {
        private Dictionary<string, Identity> identityTable;
        // may leave space for broadcast scheme
        public IdentityManager()
        {
            identityTable = new Dictionary<string, Identity>();
        }

        public Identity LookupIdentity(string user)
        {
            if(identityTable.TryGetValue(user, out Identity identity))
            {
                return identity;
            }
            return null;
        }

        public void AddIdentity(string user, Identity identity)
        {
            identityTable.Add(user, identity);
        }

        public void UpdateIdentity(List<string> users, List<Identity> identities)
        {
            for(int i = 0; i < users.Count; i++)
            {
                identityTable.Add(users.ElementAt(i), identities.ElementAt(i));
            }
        }
    }
}
