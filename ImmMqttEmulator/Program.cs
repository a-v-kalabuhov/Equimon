using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Client;
using System.Text.Json;
using System.Text;

var factory = new MqttFactory();
var mqttClient = factory.CreateMqttClient();

var options = new MqttClientOptionsBuilder()
    .WithTcpServer("localhost", 1883)  // В Docker: имя сервиса
    .WithClientId("tpa-emulator-1")
    .WithCredentials("admin", "public")  // EMQX default
    .WithCleanSession()
    .Build();

await mqttClient.ConnectAsync(options);

//mqttClient.ApplicationMessageReceivedAsync += async e => {
//    Console.WriteLine($"Получено: {Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment.Array)}");
//};

//await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("tpa/+/command").Build());

// Симуляция ТПА #1, датчики каждые 10 сек
var rnd = new Random();
while (true) {
    var timestamp = DateTime.UtcNow.ToString("o");

    // Датчик temp_001
    var tempData = new { value = 210 + rnd.NextDouble() * 20, timestamp };
    var tempMsg = new MqttApplicationMessageBuilder()
        .WithTopic("tpa/1/temp_sensor_001/value")
        .WithPayload(JsonSerializer.SerializeToUtf8Bytes(tempData))
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .Build();
    await mqttClient.PublishAsync(tempMsg);

    // Датчик cycle_002
    var cycleData = new { value = 22 + rnd.NextDouble() * 5, timestamp, status = "running" };
    var cycleMsg = new MqttApplicationMessageBuilder()
        .WithTopic("tpa/1/cycle_sensor_002/cycle_time")
        .WithPayload(JsonSerializer.SerializeToUtf8Bytes(cycleData))
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .Build();
    await mqttClient.PublishAsync(cycleMsg);

    Console.WriteLine($"Опубликовано: {timestamp}");
    await Task.Delay(10000);  // 10 сек
}
