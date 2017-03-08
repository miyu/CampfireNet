using System;
using System.Linq;
using CampfireNet.IO;
using CampfireNet.Utilities.Merkle;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using CampfireNet.Identities;
using Microsoft.Xna.Framework;

namespace CampfireNet.Simulator {
   public class SimulatorConfiguration {
      public int AgentCount { get; set; }
      public int DisplayWidth { get; set; }
      public int DisplayHeight { get; set; }
      public int FieldWidth { get; set; }
      public int FieldHeight { get; set; }
      public int AgentRadius { get; set; }

      public static SimulatorConfiguration Build(int scale, int displayWidth, int displayHeight) {
         return new SimulatorConfiguration {
            AgentCount = 112 * scale * scale,
            DisplayWidth = displayWidth,
            DisplayHeight = displayHeight,
            FieldWidth = 1280 * scale,
            FieldHeight = 720 * scale,
            AgentRadius = 10
         };
      }

      public static SimulatorConfiguration Build2P(int displayWidth, int displayHeight) {
         return new SimulatorConfiguration {
            AgentCount = 2,
            DisplayWidth = displayWidth,
            DisplayHeight = displayHeight,
            FieldWidth = 160,
            FieldHeight = 90,
            AgentRadius = 10
         };
      }
   }

   public static class EntryPoint {
      public static void Run() {
//         GenerateRSAParameters();
         //			ThreadPool.SetMaxThreads(8, 8);
         //			var configuration = SimulatorConfiguration.Build2P(1920, 1080);
         var configuration = SimulatorConfiguration.Build(3, 1920, 1080);
         var agents = ConstructAgents(configuration);
         new SimulatorGame(configuration, agents).Run();
      }

      private static void GenerateRSAParameters() {
         Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "keys"));
         for (int i = 0; i < 2048; i++) {
            Console.WriteLine(i);
            var rsa = new RSACryptoServiceProvider(CryptoUtil.ASYM_KEY_SIZE_BITS);
            File.WriteAllBytes(Path.Combine(Environment.CurrentDirectory, "keys", i.ToString()), CryptoUtil.SerializeKey(rsa.ExportParameters(true)));
         }
      }

      private static DeviceAgent[] ConstructAgents(SimulatorConfiguration configuration) {
         var random = new Random(2);

         var agents = new DeviceAgent[configuration.AgentCount];
         for (int i = 0; i < agents.Length; i++) {
            agents[i] = new DeviceAgent {
               BluetoothAdapterId = Guid.NewGuid(),
               Position = new Vector2(
                  random.Next(configuration.AgentRadius, configuration.FieldWidth - configuration.AgentRadius),
                  random.Next(configuration.AgentRadius, configuration.FieldHeight - configuration.AgentRadius)
               ),
               Velocity = Vector2.Transform(new Vector2(10, 0), Matrix.CreateRotationZ((float)(random.NextDouble() * Math.PI * 2)))
            };
         }

         agents[0].Position = new Vector2(300, 300);
         agents[1].Position = new Vector2(310, 300);

         var agentsPerRow = (4 * (int)Math.Sqrt(agents.Length)) / 3;
         var hSPacing = 60;
         var vSPacing = 60;
         var gw = agentsPerRow * hSPacing;
         var gh = (agents.Length / agentsPerRow) * vSPacing;
         var ox = (configuration.FieldWidth - gw) / 2;
         var oy = (configuration.FieldHeight - gh) / 2;
         for (var i = 0; i < agents.Length; i++) {
            agents[i].Position = new Vector2((i % agentsPerRow) * hSPacing + ox, (i / agentsPerRow) * vSPacing + oy);
         }

         for (var i = 0; i < agents.Length; i++) {
            agents[i].Position += agents[i].Velocity * 10;
            agents[i].Velocity = Vector2.Zero;
         }

         var agentIndexToNeighborsByAdapterId = Enumerable.Range(0, agents.Length).ToDictionary(
            i => i,
            i => new Dictionary<Guid, SimulationBluetoothAdapter.SimulationBluetoothNeighbor>());

         for (int i = 0; i < agents.Length; i++) {
            var agent = agents[i];
            var bluetoothAdapter = agent.BluetoothAdapter = new SimulationBluetoothAdapter(agent, agentIndexToNeighborsByAdapterId[i]);
            agent.BluetoothAdapter.Permit(SimulationBluetoothAdapter.MAX_RATE_LIMIT_TOKENS * (float)random.NextDouble());

            var broadcastMessageSerializer = new BroadcastMessageSerializer();
            var merkleTreeFactory = new ClientMerkleTreeFactory(broadcastMessageSerializer, new InMemoryCampfireNetObjectStore());
            var rsaParameters = CryptoUtil.DeserializeKey(File.ReadAllBytes(Path.Combine(Environment.CurrentDirectory, "keys", i.ToString())));
            var identity = agent.CampfireNetIdentity = (Identity)new Identity(new IdentityManager(), rsaParameters, $"Agent_{i}");
            if (i == 0) {
               agent.CampfireNetIdentity.GenerateRootChain();
            } else {
               var rootAgent = agents[0];
               agent.CampfireNetIdentity.AddTrustChain(rootAgent.CampfireNetIdentity.GenerateNewChain(identity.PublicIdentity, Permission.All, Permission.None, identity.Name));
            }

            var client = agent.Client = new CampfireNetClient(identity, bluetoothAdapter, broadcastMessageSerializer, merkleTreeFactory);
            client.MessageReceived += e => {
               var epoch = BitConverter.ToInt32(e.Message.DecryptedPayload, 0);
//               Console.WriteLine($"{client.AdapterId:n} recv {epoch}");
               agent.Value = Math.Max(agent.Value, epoch);
            };
            client.RunAsync().ContinueWith(task => {
               if (task.IsFaulted) {
                  Console.WriteLine(task.Exception);
               }
            });
         }

         return agents;
      }
   }
}
