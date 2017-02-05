using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IdentityService
{
    class CoTProcessor
    {
        public const int SIGN_SIZE = 256;
        public const int NAME_SIZE = 830;
        public const int PERM_SIZE = 4;
        //[root][root, perm, gperm][root signature][parent, perm, gperm][parent signature]

        public static bool CheckCoT(Identity identity)
        {
            if(identity.coT.Length == NAME_SIZE)
            {
                return true;
            }
            int signOffset = identity.coT.Length - SIGN_SIZE;
            int gPermOffset = signOffset - PERM_SIZE;
            int permOffset = gPermOffset - PERM_SIZE;
            int parentOffset = permOffset - NAME_SIZE;
            byte[] signature = new byte[SIGN_SIZE];
            byte[] segment = new byte[NAME_SIZE + PERM_SIZE * 2];
            byte[] parent = new byte[NAME_SIZE];
            byte[] perm = new byte[PERM_SIZE];
            byte[] gPerm = new byte[PERM_SIZE];
            Buffer.BlockCopy(identity.coT, signOffset, signature, 0, SIGN_SIZE);
            Buffer.BlockCopy(identity.coT, parentOffset, segment, 0, segment.Length);
            Buffer.BlockCopy(identity.coT, parentOffset, parent, 0, NAME_SIZE);
            Buffer.BlockCopy(identity.coT, gPermOffset, gPerm, 0, PERM_SIZE);
            Buffer.BlockCopy(identity.coT, permOffset, perm, 0, PERM_SIZE);
            return identity.Verify(segment, Encoding.UTF8.GetString(parent), signature) && 
                identity.permission.GetHashCode() == BitConverter.ToInt32(perm, 0) &&
                identity.gPermission.GetHashCode() == BitConverter.ToInt32(gPerm, 0);
        }

        public static byte[] FormCoT(Identity identity, Permission permission, Permission gPermission)
        {
            byte[] newCoT = new byte[identity.coT.Length + NAME_SIZE + PERM_SIZE * 2 + SIGN_SIZE];
            byte[] perm = BitConverter.GetBytes(permission.GetHashCode());
            byte[] gPerm = BitConverter.GetBytes(gPermission.GetHashCode());
            byte[] segment = new byte[NAME_SIZE + PERM_SIZE * 2];
            byte[] parent = Encoding.UTF8.GetBytes(identity.publicKey);
            Buffer.BlockCopy(parent, 0, segment, 0, NAME_SIZE);
            Buffer.BlockCopy(perm, 0, segment, NAME_SIZE, PERM_SIZE);
            Buffer.BlockCopy(gPerm, 0, segment, NAME_SIZE + PERM_SIZE, PERM_SIZE);
            byte[] signature = identity.Sign(segment);
            Buffer.BlockCopy(identity.coT, 0, newCoT, 0, identity.coT.Length);
            Buffer.BlockCopy(segment, 0, newCoT, identity.coT.Length, segment.Length);
            Buffer.BlockCopy(signature, 0, newCoT, identity.coT.Length + segment.Length, SIGN_SIZE);
            return newCoT;
        }

        public static bool CommonRoot(Identity i1, Identity i2)
        {
            byte[] root1 = new byte[NAME_SIZE];
            byte[] root2 = new byte[NAME_SIZE];
            Buffer.BlockCopy(i1.coT, 0, root1, 0, NAME_SIZE);
            Buffer.BlockCopy(i2.coT, 0, root2, 0, NAME_SIZE);
            return root1.Equals(root2);
        }
    }
}
