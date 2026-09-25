namespace Polson.Tests.ExtendedMind;

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using global::Polson.Tests;
using Polson.ExtendedMind.CharacterGeneration;
using Xunit;

/// <summary>
/// The rig service client: what it sends, and how it reads each answer the service can give.
/// </summary>
/// <remarks>
/// Against a stub rather than the service, because what is ours to get right is the contract — the
/// request shape and the classification of each status — and that must be checkable on a machine
/// with no rigging GPU. The service itself is exercised by <c>CharacterGenerationLiveTests</c>.
/// </remarks>
public class RigClientTests : TestsRuntime
{
    sealed class Stub(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? Path, Sent;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Path = request.RequestUri!.AbsolutePath;
            Sent = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    const string Success = """
        {"artifacts":[{"base64":"Z2xURgIAAAA=","format":"glb","finishReason":"SUCCESS","seed":12345}],
         "rig":{"joints":52,"weighted_joints":50,"vertices":25099,"influences":{"1":737,"2":2446,"3":731,"4":283},
                "names":["bone_0"],"parents":[null]},
         "timing_ms":{"extract":900,"skeleton":4000,"skin":6000,"merge":1100}}
        """;

    [Fact]
    public async Task ARigIsReadWithItsMeasurement()
    {
        var stub = new Stub(HttpStatusCode.OK, Success);
        using var client = new RigClient("http://rig/", new HttpClient(stub));
        var result = await client.RigAsync([1, 2, 3], seed: 7);

        Assert.True(result.Success, result.Error);
        Assert.Equal("/v1/infer", stub.Path);
        using (var sent = JsonDocument.Parse(stub.Sent!))
        {
            Assert.Equal("AQID", sent.RootElement.GetProperty("mesh").GetString());
            Assert.Equal("glb", sent.RootElement.GetProperty("format").GetString());
            Assert.Equal(7, sent.RootElement.GetProperty("seed").GetInt32());
        }
        Assert.Equal(Convert.FromBase64String("Z2xURgIAAAA="), result.Bytes);
        Assert.Equal(52, result.Joints);
        Assert.Equal(50, result.WeightedJoints);
        Assert.Equal(2446, result.Influences["2"]);
        Assert.Equal(6000, result.Timing["skin"]);
    }

    /// <summary>Each refusal is classified, so a caller can tell "wait" from "change the mesh" from "tell someone".</summary>
    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, RigFailure.NotReady, true)]
    [InlineData(HttpStatusCode.UnprocessableEntity, RigFailure.Unriggable, false)]
    [InlineData(HttpStatusCode.InternalServerError, RigFailure.ServiceError, true)]
    public async Task EachRefusalIsClassified(HttpStatusCode status, RigFailure failure, bool retryable)
    {
        using var client = new RigClient("http://rig", new HttpClient(new Stub(status, """{"detail":"the weights are not finite"}""")));
        var result = await client.RigAsync([1]);

        Assert.False(result.Success);
        Assert.Equal(failure, result.Failure);
        Assert.Equal(retryable, result.Retryable);
        Assert.Contains(status == HttpStatusCode.InternalServerError ? "500" : "not finite", result.Error);
        Assert.NotEmpty(result.Remedy);
    }

    /// <summary>A 200 that is not a rig is a fault, not an empty success.</summary>
    [Fact]
    public async Task AnUnreadableSuccessIsAFailure()
    {
        using var client = new RigClient("http://rig", new HttpClient(new Stub(HttpStatusCode.OK, """{"status":"ok"}""")));
        var result = await client.RigAsync([1]);

        Assert.False(result.Success);
        Assert.Equal(RigFailure.ServiceError, result.Failure);
        Assert.Empty(result.Bytes);
    }

    [Fact]
    public async Task AnEmptyMeshIsRefusedWithoutACall()
    {
        var stub = new Stub(HttpStatusCode.OK, Success);
        using var client = new RigClient("http://rig", new HttpClient(stub));
        var result = await client.RigAsync([]);

        Assert.Equal(RigFailure.InvalidRequest, result.Failure);
        Assert.Null(stub.Path);
    }
}
