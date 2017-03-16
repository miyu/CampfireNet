# CampfireNet Developers
Michael Yu, Tyler Yeats, Yufang Sun

# Overview
CampfireNet is a FireChat-inspired .NET library for building fully-decentralized, Bluetooth-based mobile ad-hoc networks. Network participants may grant other participants, identified by their cryptographically secure public key, access to the network through a chain of trust similar to the Certificate Authority model. Depending on their granted permission within the network, nodes may broadcast and multicast across the network in encrypted and cleartext forms, unicast secure messages intended for single recipients ensuring message integrity and confidentiality, and invite other participants to the network. Messages are propagated throughout the network in a delay-tolerant fashion through a naive flooding-based algorithm involving merkle tree synchronization, with the assumption that network-wide denial of service is impossible as long as network permissions are adequately restricted to specific individuals, such as protest leaders or smaller groups of friends. Messages must be tagged by sender and recipient public key fingerprints, and cohorts must validate the permissions of the fingerprints’ associated identities before accepting and further propagating messages.

# Development Environment
CampfireNet is entirely written in C# using .NET 4.6.2 / netstandard1.5 / Xamarin. We run on Mono, CLR, and .NET Core to make the application cross platform so that we can run the simulator on any OS, and the phone app on any phone OS (though we currently only support Android). To interact with this project, we recommend installing Visual Studio 2017 with the Android SDK and Xamarin options or Visual Studio for Mac (formerly Xamarin Studio) with the Android option. 

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

# Configuring Chain of Trust
In our final product, we transfer Chain of Trust claims out of band (e.g. via email). For development, we have additionally hard-coded a root public key in CampfireNet.Core/Security/__HackPrivateKeyUtilities.cs field __HACK_ROOT_PRIVATE_KEY.  In or near an application’s entry point (just follow the code) a CampfireNet client will be created with a certain identity; rather than using that identity, you may consider building with development network identities through the client builder flag.

Enabling this option uses the hard-coded root identity to generate a chain of trust claim for your local identity (e.g. Windows_Client or SomeAndroidIdentityName (grep for these strings) based on the claim assignee’s public identity, their granted permissions, the permissions they may grant, and their friendly name. Note the friendly name does nothing and is somewhat like an ssh key comment.

#Known Issues
Bluetooth on Android is incredibly flakey. We’re talking about firmware bugs that, for example, read a GUID one way on one device, but in reverse on another device, or firmware bugs that deliver events out of order. We’ve tried to program defensively to fight against these scenarios, but have only tested on two devices. Please do reach out to us if you’re experiencing crashes.

On Android, we do not distinguish between the application starting and the application being brought to focus. The Android client will start a new instance of its Bluetooth logic every time you enter the application, so if you leave the application, ensure it is killed (not just minimized) and restart it.

RSA implementations may be less performant on different platforms. We’ve found on Mono/Mac RSA key generation is incredibly slow (~20s). In contrast, on Windows it is fairly fast (~1s) and on Android, it is somewhere in the middle (~5s to 10s). As we currently regenerate your CampfireNet identity on each application startup, you will have to constantly wait through this pause. In the final product, we will quickly load an instance’s chain of trust from disk.
