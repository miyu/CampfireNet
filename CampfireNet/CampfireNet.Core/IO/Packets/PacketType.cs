namespace CampfireNet.IO.Packets
{
	public enum PacketType : uint
	{
		/// <summary>
		/// "have"
		/// </summary>
		Have = 0x65766168U,

		/// <summary>
		/// "need"
		/// </summary>
		Need = 0x6465656EU,

		/// <summary>
		/// "give"
		/// </summary>
		Give = 0x65766967U,

		/// <summary>
		/// 'done'
		/// </summary>
		Done = 0x656E6F64
	}
}
