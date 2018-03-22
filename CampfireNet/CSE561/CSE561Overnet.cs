using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CampfireNet.Utilities;
using CampfireNet.Utilities.Collections;

namespace CSE561 {
   public class CSE561Overnet {
      private readonly ConcurrentDictionary<Guid, CohortNode> nodes = new ConcurrentDictionary<Guid, CohortNode>();
      private readonly object synchronization = new object();

      public string CaredSig { get; set; }

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

      private ConcurrentDictionary<string, ConcurrentDictionary<Guid, Context>> winners = new ConcurrentDictionary<string, ConcurrentDictionary<Guid, Context>>();

      public void InitWinDict(string sig) {
         winners[sig] = new ConcurrentDictionary<Guid, Context>();
      }

      public bool TryWin(string sig, Guid g) {
         if (!winners.TryGetValue(sig, out var dict)) return false;
         if (!dict.TryGetValue(g, out var con)) {
            return false;
         }
         if (Interlocked.CompareExchange(ref con.val, 1, 0) != 0) {
            return false;
         }
         foreach (var og in con.guids) {
            dict.TryRemove(og, out var _);
         }
         Console.WriteLine("Winner");
         return true;
      }

      public void KillWinDIct(string sig) {
         winners.TryRemove(sig, out var _);
      }

      public void HandleNexts(string sig, ConcurrentSet<Guid> hashSet) {
         if (!winners.TryGetValue(sig, out var dict)) return;
         var con = new Context { sig = sig, val = 0, guids = hashSet };
         foreach (var g in hashSet) {
            dict[g] = con;
         }
      }

      public class Context {
         public string sig;
         public int val;
         public ConcurrentSet<Guid> guids;
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
