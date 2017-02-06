using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CampfireNet.Utilities.Merkle;
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
   }

   public static class Program {
      [STAThread]
      public static void Main() {
         ThreadPool.SetMaxThreads(8, 8);
         var configuration = SimulatorConfiguration.Build(3, 1920, 1080);
         var agents = ConstructAgents(configuration);
         new SimulatorGame(configuration, agents).Run();
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

         var w = (4 * (int)Math.Sqrt(agents.Length)) / 3;
         for (var i = 0; i < agents.Length; i++) {
            agents[i].Position = new Vector2((i % w) * 80, (i / w) * 80);
         }

         var agentIndexToNeighborsByAdapterId = Enumerable.Range(0, agents.Length).ToDictionary(
            i => i,
            i => new Dictionary<Guid, SimulationBluetoothAdapter.SimulationBluetoothNeighbor>());

         for (int i = 0; i < agents.Length; i++) {
            var bluetoothAdapter = agents[i].BluetoothAdapter = new SimulationBluetoothAdapter(agents, i, agentIndexToNeighborsByAdapterId[i]);
            agents[i].BluetoothAdapter.Permit(SimulationBluetoothAdapter.MAX_RATE_LIMIT_TOKENS * (float)random.NextDouble());

            var merkleTreeFactory = new ClientMerkleTreeFactory(new CampfireNetPacketMerkleOperations(), new InMemoryCampfireNetObjectStore());
            var client = agents[i].Client = new CampfireNetClient(bluetoothAdapter, merkleTreeFactory);
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
