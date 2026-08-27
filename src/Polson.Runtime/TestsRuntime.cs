namespace Polson.Tests;

using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;


public class TestsRuntime : Runtime
{
    static TestsRuntime()
    {
        Runtime.WithFileAndConsoleLogging("Polson", "Tests", true);
        config = LoadConfigFile(Path.Combine(AssemblyLocation, "testappsettings.json"));
    }

    static protected new IConfigurationRoot config;  
}
