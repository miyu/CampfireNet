using System;
using System.Diagnostics;
using System.Windows.Forms;

using MonoMac.AppKit;
using MonoMac.Foundation;

namespace CampfireNet.Simulator
{
	//public static class Program
	//{
	//	[STAThread]
	//	public static void Main()
	//	{
	//		NSApplication.Init();
	//		NSApplication.CheckForIllegalCrossThreadCalls = false;

	//		new SimulatorGame().Run();
	//	}
	//}

	//static class Program
	//{
	//	/// <summary>
	//	/// The main entry point for the application.
	//	/// </summary>
	//	static void Main(string[] args)
	//	{
	//		NSApplication.Init();
	//		NSApplication.CheckForIllegalCrossThreadCalls = false;

	//		using (var p = new NSAutoreleasePool())
	//		{
	//			NSApplication.SharedApplication.Delegate = new AppDelegate();
	//			NSApplication.Main(args);
	//		}


	//	}
	//}

	//class AppDelegate : NSApplicationDelegate
	//{
	//	SimulatorGame game;

	//	public override void FinishedLaunching(MonoMac.Foundation.NSObject notification)
	//	{
	//		game = new SimulatorGame();
	//		game.Run();
	//	}

	//	public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender)
	//	{
	//		return true;
	//	}
	//}
	class Program : NSApplicationDelegate
	{
		private SimulatorGame game;

		public override void FinishedLaunching(MonoMac.Foundation.NSObject notification)
		{
			/* Create a listener that outputs to the console screen, and 
			* add it to the debug listeners. */
			TextWriterTraceListener debugConsoleWriter = new
				TextWriterTraceListener(System.Console.Out);
			Debug.Listeners.Add(debugConsoleWriter);

			// Fun begins..
			CampfireNet.Simulator.EntryPoint.Run();
		}

		public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender)
		{
			return true;
		}

		// This is the main entry point of the application.
		static void Main(string[] args)
		{
			NSApplication.Init();

			using (var p = new MonoMac.Foundation.NSAutoreleasePool())
			{
				NSApplication.SharedApplication.Delegate = new Program();
				NSApplication.Main(args);
			}

		}
	}
}
