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

// Simulates IMM #1, sensors, every 10 seconds
var rnd = new Random();
while (true) {
    var timestamp = DateTime.UtcNow.ToString("o");

    // Sensor temp_001
    var tempData = new { value = 210 + rnd.NextDouble() * 20, timestamp };
    var tempMsg = new MqttApplicationMessageBuilder()
        .WithTopic("imm/1/temp_sensor_001/value")
        .WithPayload(JsonSerializer.SerializeToUtf8Bytes(tempData))
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .Build();
    await mqttClient.PublishAsync(tempMsg);

    // Sensor cycle_002
    var cycleData = new { value = 22 + rnd.NextDouble() * 5, timestamp, status = "running" };
    var cycleMsg = new MqttApplicationMessageBuilder()
        .WithTopic("imm/1/cycle_sensor_002/cycle_time")
        .WithPayload(JsonSerializer.SerializeToUtf8Bytes(cycleData))
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .Build();
    await mqttClient.PublishAsync(cycleMsg);

    Console.WriteLine($"Published: {timestamp}");
    await Task.Delay(10000);  // 10 seconds
}
