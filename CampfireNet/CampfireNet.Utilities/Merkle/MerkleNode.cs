namespace CampfireNet.Utilities.Merkle {
   public class MerkleNode {
      public MerkleNodeTypeTag TypeTag { get; set; }
      public string LeftHash { get; set; }
      public string RightHash { get; set; }
      public uint Descendents { get; set; }
      public byte[] Contents { get; set; }
   }
}