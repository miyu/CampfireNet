# Development Environment
CampfireNet is entirely written in C# using .NET 4.6.2 / netstandard1.5 / Xamarin. We run on Mono, CLR, and .NET Core to make the application cross platform so that we can run the simulator on any OS, and the phone app on any phone OS (though we currently only support Android). To interact with this project, we recommend installing Visual Studio 2017 RC with the ¡°Android SDK¡± and ¡°Xamarin¡± options or Visual Studio for Mac (formerly Xamarin Studio) with the Android option. 

Additionally, install .NET Core SDK 1.0 RC4 Build 004771 available at:
https://github.com/dotnet/core/blob/689c5037ed6ef6d53a1a6cbd3f041af9501f64d4/release-notes/rc4-download.md 

These are command-line tools necessary for both Windows and Mac, even if you decide to run our code through an IDE.

# Codebase
After cloning CampfireNet, open CampfireNet.sln. If you¡¯ve followed the above instructions, you should be able to build all projects targeting your platform and/or Android. The Android-deployed project is CampfireChat while the windows-deployed project is CampfireNet.Windows. Simulators for MacOS and Windows exist at CampfireNet.Simulator.
Note that on Windows, Bluetooth stacks vary - we depend on 32Feet.NET for Bluetooth support. We have not yet implemented MacOS or iOS Bluetooth support.

We recommend you test the project using an android phone in debug mode within Visual Studio 2017. Visual studio will automatically have the option deploy to your phone.

# Running Android Projects
After plugging in your Android phone, installing appropriate USB drivers for your device, which is per-manufacturer, enabling USB debugging in Android, and setting your phone to media transfer mode (so that it may send data to your desktop or laptop), set AndroidTest.Droid as your startup project and your device as the deployment target (automatically discovered by Xamarin tooling). Then, using Build/Debug menus (depending on your IDE), either deploy to your device (which will create an application icon in your application finder) or deploy via debugging.

Entry point: MainActivity.cs

# Running Simulators
This should be as simple as running CampfireNet.Simulator.MacOS or CampfireNet.Simulator.Windows, depending on your platform.

Entry point: Program.cs
You can run through IDE or via `dotnet run`. 

# Running Windows Bluetooth Client
Note: Windows Bluetooth Client only supports being a client in a CampfireNet session. That is, it is never the receiving end of a connection. This is due to time constraints rather than technical limitations. To this end, in AndroidBluetoothAdapter.cs#92 you must hard-code your device’s name. We suggest running the Android program in debug mode and viewing log output to determine your device name.

Entry point: Program.cs
You can run through IDE or via `dotnet run`. 

NOTE: Windows/Android were our #1 targets while integrating Chain of Trust. Simulator support may be flakey on MacOS.


# CampfireNet Developers
Michael Yu, Tyler Yeats, Yufang Sun