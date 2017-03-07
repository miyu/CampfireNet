using System;

namespace CampfireNet.IO
{
	public class NotConnectedException : Exception
	{
      public NotConnectedException() { }
	   public NotConnectedException(Exception e) : base("", e) { }
	}
}
