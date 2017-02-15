using System;

namespace CampfireNet.Identities
{
	public class BroadcastMessageDto
	{
		public byte[] SourceIdHash { get; set; }
		public byte[] DestinationIdHash { get; set; }
		public byte[] Payload { get; set; }
		public byte[] Signature { get; set; }
	}
}
