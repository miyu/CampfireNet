using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Collections;

namespace CSE561 {
   public class CSE561Overnet {
      private readonly ConcurrentDictionary<Guid, CohortNode> nodes = new ConcurrentDictionary<Guid, CohortNode>();
      private readonly object synchronization = new object();

      public void UpdateConnectivities(Guid localId, Dictionary<Guid, double> updatedPeers) {
         var localNode = nodes.GetOrAdd(localId, x => new CohortNode(x));
         foreach (var peerNode in localNode.Peers.Keys.ToArray()) {
            if (!updatedPeers.ContainsKey(peerNode.Id)) {
               localNode.Peers.TryRemove(peerNode, out var _);
               peerNode.Peers.TryRemove(localNode, out var _);
            }
         }
         foreach (var kvp in updatedPeers) {
            var peer = nodes.GetOrAdd(kvp.Key, x => new CohortNode(x));
            localNode.Peers[peer] = kvp.Value;
            peer.Peers[localNode] = kvp.Value;
         }
      }

      public (Guid, double)[] ComputeRoutesAndCosts(Guid source, Guid destination) {
         var s = nodes[source];
         var d = nodes[destination];
         var costToD = Dijkstras(d);
         var orderedPeersAndCosts = s.Peers.Select(peer => (peer.Key, costToD.TryGetValue(peer.Key, out var cost) ? cost : Double.PositiveInfinity))
                             .OrderBy(x => x.Item2);
         return orderedPeersAndCosts.Where(x => !double.IsInfinity(x.Item2))
                                    .Select(kvp => (kvp.Item1.Id, kvp.Item2))
                                    .ToArray();
      }

      private Dictionary<CohortNode, double> Dijkstras(CohortNode s) {
         var visited = new HashSet<CohortNode>();
         var optimalCost = new Dictionary<CohortNode, double>();
         var q = new PriorityQueue<(double, CohortNode)>((a, b) => a.Item1.CompareTo(b.Item1));
         q.Enqueue((0, s));
         optimalCost[s] = 0;
         while (q.Count != 0) {
            var (cost, node) = q.Dequeue();
            if (visited.Contains(node)) continue;
            foreach (var kvp in node.Peers) {
               var nextCost = cost + kvp.Value;
               if (visited.Contains(kvp.Key) || (optimalCost.TryGetValue(kvp.Key, out var bestCost) && bestCost < nextCost)) continue;
               optimalCost[kvp.Key] = nextCost;
               q.Enqueue((nextCost, kvp.Key));
            }
         }
         return optimalCost;
      }

      public class CohortNode {
         public CohortNode(Guid id) {
            this.Id = id;
         }

         public Guid Id { get; }
         public ConcurrentDictionary<CohortNode, double> Peers { get; } = new ConcurrentDictionary<CohortNode, double>();
      }
   }
}
