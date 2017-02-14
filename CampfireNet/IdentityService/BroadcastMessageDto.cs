using System;

namespace CampfireNet.Identities
{
	public class BroadcastMessageDto
	{
		public byte[] SourceId { get; set; }
		public byte[] DestinationId { get; set; }
		public byte[] Payload { get; set; }
		public byte[] Signature { get; set; }
	}
}
