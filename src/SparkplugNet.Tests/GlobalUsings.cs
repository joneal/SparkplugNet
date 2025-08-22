#pragma warning disable IDE0065 // Die using-Anweisung wurde falsch platziert.
global using System.Text.Json;
global using System.Buffers;

global using Microsoft.VisualStudio.TestTools.UnitTesting;

global using MQTTnet;

global using SparkplugNet.Core;
global using SparkplugNet.Core.Enumerations;
global using SparkplugNet.Core.Exceptions;
global using SparkplugNet.Core.Messages;

global using SparkplugNet.Tests.Helpers;

global using VersionBProtoBufPayload = SparkplugNet.VersionB.ProtoBuf.ProtoBufPayload;
global using VersionBProtoBuf = SparkplugNet.VersionB.ProtoBuf;

global using VersionBData = SparkplugNet.VersionB.Data;
global using VersionBMain = SparkplugNet.VersionB;
#pragma warning restore IDE0065 // Die using-Anweisung wurde falsch platziert.