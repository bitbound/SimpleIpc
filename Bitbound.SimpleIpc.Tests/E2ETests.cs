using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bitbound.SimpleIpc.Tests;

public class E2ETests
{
  private readonly ServiceProvider _services;
  private readonly string _pipeName;
  private readonly IIpcConnectionFactory _connectionFactory;
  private readonly IIpcServer _server;
  private readonly IIpcClient _client;

  public E2ETests()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSimpleIpc();
    _services = serviceCollection.BuildServiceProvider();

    _pipeName = Guid.NewGuid().ToString();

    _connectionFactory = _services.GetRequiredService<IIpcConnectionFactory>();
    _server = Task.Run(async () => await _connectionFactory.CreateServer(_pipeName)).Result;
    _client = Task.Run(async () => await _connectionFactory.CreateClient(".", _pipeName)).Result;
  }

  [Fact]
  public async Task WaitForConnection_GivenTokenIsCancelled_ReturnsFalse()
  {
    using var cts = new CancellationTokenSource();
    var waitTask = _server.WaitForConnection(cts.Token);

    await Task.Delay(10, TestContext.Current.CancellationToken);
    cts.Cancel();
    await Task.Delay(10, TestContext.Current.CancellationToken);

    Assert.False(await waitTask);
  }

  [Fact]
  public async Task Send_GivenIdealScenario_ReceivesMessages()
  {
    var token = TestContext.Current.CancellationToken;

    _ = _server.WaitForConnection(token);
    var result = await _client.Connect(token);

    Assert.True(result);

    var pingFromServer = string.Empty;
    var pongFromClient = string.Empty;

    _client.On((Ping ping) =>
    {
      Console.WriteLine("Received ping from server.");
      pingFromServer = ping.Message;
      _client.Send(new Pong("Pong from client"));
    });

    _server.On((Pong pong) =>
    {
      Console.WriteLine("Received pong from client.");
      pongFromClient = pong.Message;
    });

    _client.BeginRead(token);
    _server.BeginRead(token);

    await _server.Send(new Ping("Ping from server"));

    TaskHelper.WaitFor(() =>
        !string.IsNullOrWhiteSpace(pingFromServer) &&
        !string.IsNullOrWhiteSpace(pongFromClient),
        TimeSpan.FromSeconds(1));

    Assert.Equal("Ping from server", pingFromServer);
    Assert.Equal("Pong from client", pongFromClient);
  }

  [Fact]
  public async Task RemoveAll_GivenValidType_RemovesAll()
  {
    var token = TestContext.Current.CancellationToken;

    _ = _server.WaitForConnection(token);
    var result = await _client.Connect(token);

    Assert.True(result);

    var count = 0;

    _server.On((Ping ping) =>
    {
      count++;
    });

    _client.BeginRead(token);
    _server.BeginRead(token);

    await _client.Send(new Ping());

    TaskHelper.WaitFor(() =>
        count > 0,
        TimeSpan.FromSeconds(1));

    Assert.Equal(1, count);

    _server.Off<Ping>();

    await _client.Send(new Ping());
    await Task.Delay(1_000, TestContext.Current.CancellationToken);

    Assert.Equal(1, count);
  }

  [Fact]
  public async Task RemoveAll_GivenInvalidType_RemovesNone()
  {
    var token = TestContext.Current.CancellationToken;

    _ = _server.WaitForConnection(token);
    var result = await _client.Connect(token);

    Assert.True(result);

    var count = 0;

    _server.On((Ping ping) =>
    {
      count++;
    });

    _client.BeginRead(token);
    _server.BeginRead(token);

    await _client.Send(new Ping());

    TaskHelper.WaitFor(() =>
        count > 0,
        TimeSpan.FromSeconds(1));

    Assert.Equal(1, count);

    _server.Off<Pong>();

    await _client.Send(new Ping());
    TaskHelper.WaitFor(() =>
      count > 1,
      TimeSpan.FromSeconds(1));

    Assert.Equal(2, count);
  }

  [Fact]
  public async Task RemoveAll_GivenValidToken_RemovesOne()
  {
    var token = TestContext.Current.CancellationToken;

    _ = _server.WaitForConnection(token);
    var result = await _client.Connect(token);

    Assert.True(result);

    var count = 0;

    var token1 = _server.On((Ping ping) =>
    {
      count++;
    });
    var token2 = _server.On((Ping ping) =>
    {
      count++;
    });

    _client.BeginRead(token);
    _server.BeginRead(token);

    await _client.Send(new Ping());

    TaskHelper.WaitFor(() =>
        count > 1,
        TimeSpan.FromSeconds(1));

    Assert.Equal(2, count);

    _server.Off<Ping>(token1);

    await _client.Send(new Ping());
    TaskHelper.WaitFor(() =>
      count > 2,
      TimeSpan.FromSeconds(1));

    Assert.Equal(3, count);
  }

  [Fact]
  public async Task Invoke_GivenIdealScenario_ReturnsValue()
  {
    var token = TestContext.Current.CancellationToken;

    _ = _server.WaitForConnection(token);
    var result = await _client.Connect(token);

    Assert.True(result);

    _client.On((Ping pong) =>
    {
      return new Pong($"Pong from Client: {pong.Message}");
    });

    _server.On((Ping pong) =>
    {
      return Task.FromResult(new Pong($"Pong from Server: {pong.Message}"));
    });

    _client.BeginRead(token);
    _server.BeginRead(token);

    var serverResponse = await _client.Invoke<Ping, Pong>(new Ping("Client Ping"), 1000);
    var clientResponse = await _server.Invoke<Ping, Pong>(new Ping("Server Ping"), 1000);

    Assert.Equal("Pong from Client: Server Ping", clientResponse.Value.Message);
    Assert.Equal("Pong from Server: Client Ping", serverResponse.Value.Message);
  }

  [Fact]
  public async Task Send_GivenIdealScenario_OkThroughput()
  {
    var token = TestContext.Current.CancellationToken;

    var connectionFactory = _services.GetRequiredService<IIpcConnectionFactory>();
    var server = await connectionFactory.CreateServer("throughput-test");
    var client = await connectionFactory.CreateClient(".", "throughput-test");

    _ = server.WaitForConnection(CancellationToken.None);
    var result = await client.Connect(token);

    Assert.True(result);

    var bytesReceived = 0;

    server.On((TestImage image) =>
    {
      bytesReceived += image.EncodedImage.Length;
    });

    client.BeginRead(token);
    server.BeginRead(token);

    var buffer = RandomNumberGenerator.GetBytes(2_097_152);

    var testImage = new TestImage()
    {
      EncodedImage = buffer,
      Height = 1080,
      Width = 1920
    };

    var sw = Stopwatch.StartNew();
    for (var i = 0; i < 100; i++)
    {
      await client.Send(testImage);
    }
    sw.Stop();

    var mbps = bytesReceived / 1024 / 1024 * 8.0 / sw.Elapsed.TotalSeconds;

    Console.WriteLine($"{bytesReceived:N0} total bytes received in {sw.Elapsed.TotalMilliseconds:N} milliseconds.");
    Console.WriteLine($"Mbps: {mbps:N}");
    Assert.True(mbps > 1_000);
  }

  [DataContract]
  public class Ping
  {
    public Ping()
    { }

    public Ping(string message)
    {
      Message = message;
    }

    [DataMember]
    public string Message { get; set; }
  }

  [DataContract]
  public class Pong
  {
    public Pong()
    { }

    public Pong(string message)
    {
      Message = message;
    }

    [DataMember]
    public string Message { get; set; }
  }

  [DataContract]
  public class TestImage
  {
    [DataMember]
    public byte[] EncodedImage { get; set; } = [];

    [DataMember]
    public int Width { get; set; }

    [DataMember]
    public int Height { get; set; }
  }
}
