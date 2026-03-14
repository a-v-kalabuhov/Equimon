using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Client;
using System.Text.Json;
using System.Text;

var factory = new MqttFactory();
var mqttClient = factory.CreateMqttClient();

var options = new MqttClientOptionsBuilder()
    .WithTcpServer("emqx", 1883)  
    .WithClientId("imm-emulator-1")
    .WithCredentials("admin", "public")  // EMQX default
    .WithCleanSession()
    .Build();

while (true)
{
    try
    {
        await mqttClient.ConnectAsync(options);
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"EMQX is not available: {ex.Message}. Retry in 5 seconds...");
        await Task.Delay(5000);
    }
}

// Configuration
int cycleDurationSeconds = int.Parse(Environment.GetEnvironmentVariable("CYCLE_DURATION_SECONDS") ?? "12");
int sensorIntervalSeconds = int.Parse(Environment.GetEnvironmentVariable("SENSOR_INTERVAL_SECONDS") ?? "1");
int maxCycleCount = int.Parse(Environment.GetEnvironmentVariable("MAX_CYCLE_COUNT") ?? "1000000");

Console.WriteLine($"Configuration: Cycle duration={cycleDurationSeconds}s, Sensor interval={sensorIntervalSeconds}s, Max cycle count={maxCycleCount}");

var rnd = new Random();
int cycleCount = 0;
double lastCycleValue = 0;
DateTime nextCycleTime = DateTime.UtcNow.AddSeconds(cycleDurationSeconds);

// Separate tasks for cycle counter and other sensors
var cts = new CancellationTokenSource();

// Task for other sensors (every 1 second)
var sensorTask = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        var timestamp = DateTime.UtcNow.ToString("o");
        
        // Readings from non-cycle sensors
        var readings = new List<object>
        {
            new { sensor_id = "temp_sensor_001", timestamp, value = 210 + rnd.NextDouble() * 20 }
        };
        
        var payload = JsonSerializer.Serialize(readings);
        var msg = new MqttApplicationMessageBuilder()
            .WithTopic("imm/1/readings")
            .WithPayload(Encoding.UTF8.GetBytes(payload))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        
        await mqttClient.PublishAsync(msg);
        Console.WriteLine($"Published sensor data: {timestamp}");
        
        await Task.Delay(sensorIntervalSeconds * 1000, cts.Token);
    }
}, cts.Token);

// Task for cycle counter (every cycleDurationSeconds)
var cycleTask = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        var now = DateTime.UtcNow;
        if (now >= nextCycleTime)
        {
            // Increment cycle count monotonically
            if (cycleCount < maxCycleCount)
            {
                cycleCount++;
                lastCycleValue = cycleCount;
            }
            
            nextCycleTime = now.AddSeconds(cycleDurationSeconds);
            
            var timestamp = now.ToString("o");
            
            // Only send cycle sensor reading
            var readings = new[]
            {
                new { sensor_id = "cycle_sensor_002", timestamp, value = lastCycleValue, status = "running" }
            };
            
            var payload = JsonSerializer.Serialize(readings);
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("imm/1/readings")
                .WithPayload(Encoding.UTF8.GetBytes(payload))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            
            await mqttClient.PublishAsync(msg);
            Console.WriteLine($"Published cycle count: {cycleCount} at {timestamp}");
        }
        
        await Task.Delay(100, cts.Token); // Check every 100ms
    }
}, cts.Token);

// Wait for any task to complete or fail
await Task.WhenAny(sensorTask, cycleTask);
cts.Cancel();
