// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Hämmer Electronics">
// The project is licensed under the MIT license.
// </copyright>
// <summary>
//   The main program.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SparkplugNet.Examples;

/// <summary>
/// The main program.
/// </summary>
public sealed class Program
{
    /// <summary>
    /// The cancellation token source.
    /// </summary>
    private static readonly CancellationTokenSource CancellationTokenSource = new();

    private static MqttServer MqttServer;
    private static ManualResetEvent NodeExampleCompletedEvent = new(false);

    /// <summary>
    /// The version B metrics for an application.
    /// </summary>
    private static readonly List<VersionBData.Metric> VersionBMetricsApplication = [];

    /// <summary>
    /// The version B metrics for a node.
    /// </summary>
    private static readonly List<VersionBData.Metric> VersionBMetricsNode =
    [
        new VersionBData.Metric("temperatureNode", VersionBData.DataType.Float, 1.243f)
        {
            Alias = 1000
        },
        new VersionBData.Metric("climateactiveNode", VersionBData.DataType.Boolean,true)
        {
            Properties = new VersionBData.PropertySet
            {
                Keys = ["ON", "OFF"],
                Values =
                [
                    new(VersionBData.DataType.Int8, 1)
                    {
                    },
                    new(VersionBData.DataType.Int8, 0)
                    {
                    }
                ]
            }
        }
    ];

    /// <summary>
    /// The version B metrics for a device.
    /// </summary>
    private static readonly List<VersionBData.Metric> VersionBMetricsDevice =
    [
        new VersionBData.Metric("temperatureDevice", VersionBData.DataType.Float, 1.243f),
        new VersionBData.Metric("climateactiveDevice", VersionBData.DataType.Boolean, true)
    ];

    /// <summary>
    /// The main method.
    /// </summary>
    public static async Task Main()
    {
        try
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            await RunVersionB();

            Log.Information("Simulation is done.");
        }
        catch (Exception ex)
        {
            Log.Error("An exception occurred: {Exception}", ex);
        }
    }

    /// <summary>
    /// Runs a version B simulation.
    /// </summary>
    private static async Task RunVersionB()
    {
        await RunLocalMQTTBroker();
        await Task.Delay(1 * 1000);
        var applicationTask = Task.Run(async () => await RunVersionBApplication(), CancellationToken.None);
        var nodeTask = Task.Run(async () => await RunVersionBNode(), CancellationToken.None);

        await Task.WhenAll(applicationTask, nodeTask);
    }

    /// <summary>
    /// Runs a local, non-TLS instance of a MQTTnet-based MQTT broker.
    /// </summary>
    private static async Task RunLocalMQTTBroker()
    {
        var mqttServerOptions = new MqttServerOptionsBuilder()
                               .WithDefaultEndpoint()
                               .WithDefaultEndpointPort(1883)
                               .WithoutEncryptedEndpoint()
                               .Build();

        var mqttServerFactory = new MqttServerFactory();
        MqttServer = mqttServerFactory.CreateMqttServer(mqttServerOptions);

        MqttServer.ClientConnectedAsync += async (e) => { Log.Information($"MQTTSERVER: Client {e.ClientId} connected."); await Task.CompletedTask; };
        MqttServer.ClientDisconnectedAsync += async (e) => { Log.Information($"MQTTSERVER: Client {e.ClientId} disconnected."); await Task.CompletedTask; };
        MqttServer.InterceptingPublishAsync += async (e) => { Log.Information($"MQTTSERVER: Client {e.ClientId} published {e.ApplicationMessage.Topic}."); await Task.CompletedTask; };
        // MqttServer.LoadingRetainedMessageAsync += handlers.LoadingRetainedMessageAsync;
        // MqttServer.RetainedMessageChangedAsync += handlers.RetainedMessageChangedAsync;
        // MqttServer.RetainedMessagesClearedAsync += handlers.RetainedMessagesClearedAsync;
        // MqttServer.ApplicationMessageEnqueuedOrDroppedAsync += handlers.ApplicationMessageEnqueuedOrDroppedAsync;
        MqttServer.ClientSubscribedTopicAsync += async (e) => { Log.Information($"MQTTSERVER: Client {e.ClientId} subscribed to {e.TopicFilter}."); await Task.CompletedTask; };

        await MqttServer.StartAsync();
    }

    /// <summary>
    /// Runs the version B application.
    /// </summary>
    private static async Task RunVersionBApplication()
    {
        VersionB.SparkplugApplication? application = null;

        try
        {
            var applicationOptions = new SparkplugApplicationOptions(
                "localhost",
                1883,
                nameof(RunVersionBApplication),
                "user",
                "password",
                "scada1",
                TimeSpan.FromSeconds(30),
                SparkplugMqttProtocolVersion.V500,
                null,
                null,
                true,
                CancellationTokenSource.Token);
            application = new VersionB.SparkplugApplication(VersionBMetricsApplication, SparkplugSpecificationVersion.Version22);

            // Handles the application's connected and disconnected events.
            application.Connected += OnApplicationVersionBConnected;
            application.Disconnected += OnApplicationVersionBDisconnected;

            // Handles the application's device related events.
            application.DeviceBirthReceived += OnApplicationVersionBDeviceBirthReceived;
            application.DeviceDataReceived += OnApplicationVersionBDeviceDataReceived;
            application.DeviceDeathReceived += OnApplicationVersionBDeviceDeathReceived;

            // Handles the application's node related events.
            application.NodeBirthReceived += OnApplicationVersionBNodeBirthReceived;
            application.NodeDataReceived += OnApplicationVersionBNodeDataReceived;
            application.NodeDeathReceived += OnApplicationVersionBNodeDeathReceived;

            // Start an application.
            Log.Information("SPB_APP: Starting application...");
            await application.Start(applicationOptions);
            Log.Information("SPB_APP: Application started...");

            // Publish node commands.
            Log.Information("SPB_APP: Publishing a node command ...");
            await application.PublishNodeCommand(VersionBMetricsApplication, "group1", "edge1");

            // Publish device commands.
            Log.Information("SPB_APP: Publishing a device command ...");
            await application.PublishDeviceCommand(VersionBMetricsApplication, "group1", "edge1", "device1");

            // Get the known metrics from an application.
            var currentlyKnownMetrics = application.KnownMetrics;

            // Get the device states from an application.
            var currentDeviceStates = application.DeviceStates;

            // Get the node states from an application.
            var currentNodeStates = application.NodeStates;

            // Check whether an application is connected.
            var isApplicationConnected = application.IsConnected;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"SPB_APP: {ex.Message}");
        }
        finally
        {
            // Wait for the node example task to be completed.
            NodeExampleCompletedEvent.WaitOne();

            // Stopping an application.
            if (application != null)
            {
                await application.Stop();
            }

            Log.Information("SPB_APP: Application stopped...");
        }
    }

    /// <summary>
    /// Runs the version B node.
    /// </summary>
    private static async Task RunVersionBNode()
    {
        VersionB.SparkplugNode? node = null;

        try
        {
            var nodeOptions = new SparkplugNodeOptions(
                "localhost",
                1883,
                "node 1",
                "user",
                "password",
                "scada1B",
                TimeSpan.FromSeconds(30),
                SparkplugMqttProtocolVersion.V500,
                null,
                null,
                "group1",
                "node1",
                CancellationTokenSource.Token);
            node = new VersionB.SparkplugNode(VersionBMetricsNode, SparkplugSpecificationVersion.Version22);

            // Handles the node's connected and disconnected events.
            node.Connected += OnVersionBNodeConnected;
            node.Disconnected += OnVersionBNodeDisconnected;

            // Handles the node's device related events.
            node.DeviceBirthPublishing += OnVersionBNodeDeviceBirthPublishing;
            node.DeviceCommandReceived += OnVersionBNodeDeviceCommandReceived;
            node.DeviceDeathPublishing += OnVersionBNodeDeviceDeathPublishing;

            // Handles the node's node command received event.
            node.NodeCommandReceived += OnVersionBNodeNodeCommandReceived;

            // Handles the node's status message received event.
            node.StatusMessageReceived += OnVersionBNodeStatusMessageReceived;

            // Start a node.
            Log.Information("SPB_NODE: Starting node...");
            await node.Start(nodeOptions);
            Log.Information("SPB_NODE: Node started...");

            // Publish node metrics.
            await node.PublishMetrics(VersionBMetricsNode);

            // Get the known node metrics from a node.
            var currentlyKnownMetrics = node.KnownMetrics;

            // Check whether a node is connected.
            var isApplicationConnected = node.IsConnected;

            // Get the known devices.
            var knownDevices = node.KnownDevices;

            // Handling devices.
            const string DeviceIdentifier = "device1";

            for (var i = 0; i < 3; i++)
            {
                // Publish a device birth message.
                await node.PublishDeviceBirthMessage(VersionBMetricsDevice, DeviceIdentifier);

                // Publish a device data message.
                await node.PublishDeviceData(VersionBMetricsDevice, DeviceIdentifier);

                // Publish a device death message.
                await node.PublishDeviceDeathMessage(DeviceIdentifier);

                // Do a node rebirth to publish new metrics.
                await node.Rebirth(VersionBMetricsNode);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"SPB_NODE: {ex.Message}");
        }
        finally
        {
            // Stopping a node.
            if (node != null)
            {
                await node.Stop();
            }

            Log.Information("SPB_NODE: Node stopped...");

            // Let Application task know that this node task is completed.
            NodeExampleCompletedEvent.Set();
        }
    }

    #region VersionBEvents
    /// <summary>
    /// Handles the connected callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBConnected(Core.SparkplugBase<VersionBData.Metric>.SparkplugEventArgs args)
    {
        Log.Information($"SPB_APP: Connected.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the disconnected callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBDisconnected(VersionB.SparkplugApplication.SparkplugEventArgs args)
    {
        Log.Information($"SPB_APP: Disconnected.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the device birth received callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBDeviceBirthReceived(Core.SparkplugBase<VersionBData.Metric>.DeviceBirthEventArgs args)
    {
        Log.Information($"SPB_APP: Device birth received. {args.GroupIdentifier}/{args.EdgeNodeIdentifier}/{args.DeviceIdentifier}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the device data callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBDeviceDataReceived(VersionB.SparkplugApplication.DeviceDataEventArgs args)
    {
        Log.Information($"SPB_APP: Device data received. {args.GroupIdentifier}/{args.EdgeNodeIdentifier}/{args.DeviceIdentifier}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the device death received callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBDeviceDeathReceived(Core.SparkplugBase<VersionBData.Metric>.DeviceEventArgs args)
    {
        Log.Information($"SPB_APP: Device death received. {args.GroupIdentifier}/{args.EdgeNodeIdentifier}/{args.DeviceIdentifier}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the node birth received callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBNodeBirthReceived(Core.SparkplugBase<VersionBData.Metric>.NodeBirthEventArgs args)
    {
        Log.Information($"SPB_APP: Node birth received. {args.GroupIdentifier}/{args.EdgeNodeIdentifier}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the node data callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBNodeDataReceived(VersionB.SparkplugApplication.NodeDataEventArgs args)
    {
        Log.Information($"SPB_APP: Node data received. {args.GroupIdentifier}/{args.EdgeNodeIdentifier}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the node death received callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBNodeDeathReceived(Core.SparkplugBase<VersionBData.Metric>.NodeDeathEventArgs args)
    {
        Log.Information($"SPB_APP: Node death received. {args.GroupIdentifier}/{args.EdgeNodeIdentifier}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the connected callback for version B nodes.
    /// </summary>
    private static Task OnVersionBNodeConnected(Core.SparkplugBase<VersionBData.Metric>.SparkplugEventArgs args)
    {
        Log.Information($"SPB_NODE: Node connected.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the disconnected callback for version B nodes.
    /// </summary>
    private static Task OnVersionBNodeDisconnected(VersionB.SparkplugNode.SparkplugEventArgs args)
    {
        Log.Information($"SPB_NODE: Node disconnected.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the device birth callback for version B nodes.
    /// </summary>
    /// <param name="args">The received args.</param>
    private static Task OnVersionBNodeDeviceBirthPublishing(VersionB.SparkplugNode.DeviceBirthEventArgs args)
    {
        Log.Information($"SPB_NODE: Node device birth publishing. {args.GroupIdentifier}/{args.EdgeNodeIdentifier}/{args.DeviceIdentifier}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the device command callback for version B nodes.
    /// </summary>
    /// <param name="args">The received args.</param>
    private static Task OnVersionBNodeDeviceCommandReceived(VersionB.SparkplugNode.NodeCommandEventArgs args)
    {
        Log.Information($"SPB_NODE: Node device command received. {args.GroupIdentifier}/{args.EdgeNodeIdentifier}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the device death callback for version B nodes.
    /// </summary>
    /// <param name="args">The received args.</param>
    private static Task OnVersionBNodeDeviceDeathPublishing(VersionB.SparkplugNode.DeviceEventArgs args)
    {
        Log.Information($"SPB_NODE: Node device death publishing. {args.GroupIdentifier}/{args.EdgeNodeIdentifier}/{args.DeviceIdentifier}");
        return Task.CompletedTask;
    }

    /// <summary> 
    /// Handles the node command callback for version B nodes.
    /// </summary>
    /// <param name="args">The received args.</param>
    private static Task OnVersionBNodeNodeCommandReceived(VersionB.SparkplugNode.NodeCommandEventArgs args)
    {
        Log.Information($"SPB_NODE: Node commnand received. {args.GroupIdentifier}/{args.EdgeNodeIdentifier}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the status message callback for version B nodes.
    /// </summary>
    /// <param name="args">The args.</param>
    private static Task OnVersionBNodeStatusMessageReceived(VersionB.SparkplugNode.StatusMessageEventArgs args)
    {
        Log.Information($"SPB_NODE: Node status message received.");
        return Task.CompletedTask;
    }
    #endregion
}
