# Development Environment
CampfireNet is entirely written in C# using .NET 4.6.2 / netstandard1.5 / Xamarin. We run on Mono, CLR, and .NET Core to make the application cross platform so that we can run the simulator on any OS, and the phone app on any phone OS (though we currently only support Android). To interact with this project, we recommend installing Visual Studio 2017 RC with the ¡°Android SDK¡± and ¡°Xamarin¡± options or Visual Studio for Mac (formerly Xamarin Studio) with the Android option. 

Additionally, install .NET Core SDK 1.0 RC4 Build 004771 available at:
https://github.com/dotnet/core/blob/689c5037ed6ef6d53a1a6cbd3f041af9501f64d4/release-notes/rc4-download.md 

These are command-line tools necessary for both Windows and Mac, even if you decide to run our code through an IDE.

# Codebase
After cloning CampfireNet, open CampfireNet.sln. If you¡¯ve followed the above instructions, you should be able to build all projects targeting your platform and/or Android. The Android-deployed project is CampfireChat while the windows-deployed project is CampfireNet.Windows. Simulators for MacOS and Windows exist at CampfireNet.Simulator.
Note that on Windows, Bluetooth stacks vary - we depend on 32Feet.NET for Bluetooth support. We have not yet implemented MacOS or iOS Bluetooth support.

We recommend you test the project using an android phone with debug chord within Visual Studio 2017. Visual studio will automatically have the option deploy to your phone.

# CampfireNet Developers
Michael Yu, Tyler Yeats, Yufang Sun