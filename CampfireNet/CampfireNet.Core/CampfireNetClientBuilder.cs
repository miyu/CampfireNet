using System;
using System.Collections.Generic;
using System.Text;
using CampfireNet.Identities;
using CampfireNet.IO;
using CampfireNet.IO.Transport;
using CampfireNet.Security;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Merkle;

namespace CampfireNet {
   public class CampfireNetClientBuilder {
      private Identity identity;
      private IBluetoothAdapter bluetoothAdapter;

      public CampfireNetClientBuilder WithDevelopmentNetworkClaims() {
         var rootRsa = __HackPrivateKeyUtilities.DeserializePrivateKey(__HackPrivateKeyUtilities.__HACK_ROOT_PRIVATE_KEY);
         var rootIdentity = new Identity(rootRsa, new IdentityManager(), "hack_root");
         rootIdentity.GenerateRootChain();

         identity = new Identity(new IdentityManager(), "SomeAndroidIdentityName");
         identity.AddTrustChain(rootIdentity.GenerateNewChain(identity.PublicIdentity, Permission.All, Permission.All, identity.Name));
         return this;
      }

      public CampfireNetClientBuilder WithBluetoothAdapter(IBluetoothAdapter bluetoothAdapter) {
         this.bluetoothAdapter = bluetoothAdapter;
         return this;
      }

      public CampfireNetClient Build() {
         if (identity == null) throw new InvalidStateException($"{nameof(identity)} Null");
         if (bluetoothAdapter == null) throw new InvalidStateException($"{nameof(bluetoothAdapter)} Null");

         var broadcastMessageSerializer = new BroadcastMessageSerializer();
         var objectStore = new InMemoryCampfireNetObjectStore();
         var clientMerkleTreeFactory = new ClientMerkleTreeFactory(broadcastMessageSerializer, objectStore);
         var client = new CampfireNetClient(identity, bluetoothAdapter, broadcastMessageSerializer, clientMerkleTreeFactory);
         return client;
      }

      public static CampfireNetClientBuilder CreateNew() => new CampfireNetClientBuilder();
   }
}
