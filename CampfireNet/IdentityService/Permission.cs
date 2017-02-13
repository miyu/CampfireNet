using System;


namespace IdentityService
{
	[Flags]
	public enum Permission : byte
	{
		None = 0x0,
		Unicast = 0x1,
		Broadcast = 0x2,
		Invite = 0x4,
		All = Unicast | Broadcast | Invite
	}

	public class InvalidPermissionException : Exception
	{
		public InvalidPermissionException() : base() { }
		public InvalidPermissionException(string message) : base(message) { }
		public InvalidPermissionException(string message, Exception inner) : base(message, inner) { }

		public InvalidPermissionException(System.Runtime.Serialization.SerializationInfo info,
										  System.Runtime.Serialization.StreamingContext context)
		{ }
	}
}
