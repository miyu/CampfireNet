using System;

namespace CampfireNet.Utilities {
   public class InvalidStateException : Exception {
      public InvalidStateException() : base() { }
      public InvalidStateException(string message) : base(message) { }
   }
}