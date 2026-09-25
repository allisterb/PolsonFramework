namespace Polson.ExtendedMind.CharacterGeneration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Polson;

/// <summary>
/// A client for an automatic-rigging service: a mesh in, the same mesh with a skeleton and skin
/// weights out.
/// </summary>
/// <remarks>
/// <para>
/// <b>Named for what it does, not for the model behind it.</b> The service today is UniRig, served
/// resident by <c>src/rig_server/unirig_server.py</c>; the contract is a NIM container's —
/// <c>/v1/health/ready</c>, <c>/v1/metadata</c>, <c>POST /v1/infer</c> answering
/// <c>{ artifacts: [{ base64 }] }</c> — so another rigger served the same way drops in unchanged.
/// </para>
/// <para>
/// <b>The service measures the rig it returns, and this reads that measurement.</b> A dead rig is
/// structurally perfect — right joint count, valid bind matrices, weights summing to one — and binds
/// the whole mesh to one bone, so nothing downstream can tell it from a good one. The server refuses
/// non-finite weights and a rig with fewer than three weighted joints with a 422, and reports how
/// many joints carry weight on a success; <see cref="RigResult.WeightedJoints"/> is that number.
/// </para>
/// <para>This type is <b>not</b> exposed to the JavaScript sandbox.</para>
/// </remarks>
public sealed class RigClient : Runtime, IDisposable
{
    #region Constructors
    /// <param name="baseUrl">The service's origin, e.g. <c>http://192.168.8.171:8001</c>.</param>
    /// <param name="httpClient">Optional; when supplied it is neither mutated nor disposed.</param>
    public RigClient(string baseUrl, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        this.baseUrl = baseUrl.TrimEnd('/');
        this.ownsHttp = httpClient is null;
        this.http = httpClient ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }
    #endregion

    #region Properties
    /// <summary>The service's origin.</summary>
    public string BaseUrl => this.baseUrl;

    /// <summary>Deadline for one rig. Measured locally at roughly a minute; this is the wall.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);
    #endregion

    #region Methods
    /// <summary>Whether the service is up with its models loaded.</summary>
    /// <remarks>
    /// The server answers <c>ready</c> only once both checkpoints are in memory, which takes a
    /// minute or more after it starts, so "up" and "ready" differ for a while and only the second
    /// means a rig would succeed.
    /// </remarks>
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            using var response = await this.http.GetAsync(this.baseUrl + "/v1/health/ready", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Rigs one mesh.</summary>
    /// <param name="mesh">The mesh file's bytes, as <paramref name="format"/>.</param>
    public async Task<RigResult> RigAsync(byte[] mesh, string format = "glb", int? seed = null,
                                          TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.Length == 0)
            return RigResult.Fail(RigFailure.InvalidRequest, "The mesh is empty.", "Pass the bytes of a .glb.");

        var started = DateTime.UtcNow;
        var body = new Dictionary<string, object?> { ["mesh"] = Convert.ToBase64String(mesh), ["format"] = format };
        if (seed is { } s) body["seed"] = s;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout ?? DefaultTimeout);

        HttpResponseMessage response;
        string text;
        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            response = await this.http.PostAsync(this.baseUrl + "/v1/infer", content, cts.Token);
            text = await response.Content.ReadAsStringAsync(cts.Token);
        }
        catch (Exception e) when (e is TaskCanceledException or OperationCanceledException)
        {
            return cancellationToken.IsCancellationRequested
                ? RigResult.Fail(RigFailure.Cancelled, "Cancelled.", "")
                : RigResult.Fail(RigFailure.Timeout, $"No answer within {(timeout ?? DefaultTimeout).TotalMinutes:F0} minutes.",
                                 "The rig service may still be working; check it before retrying.", retryable: true);
        }
        catch (HttpRequestException e)
        {
            return RigResult.Fail(RigFailure.Network, $"Could not reach the rig service at {this.baseUrl}: {e.Message}",
                                  "Start the rig service on the GPU machine (src/rig_server), or check Characters:RigUrl.",
                                  retryable: true);
        }

        using (response)
        {
            var ms = (int)(DateTime.UtcNow - started).TotalMilliseconds;
            if (!response.IsSuccessStatusCode)
            {
                var detail = Detail(text) ?? text;
                return response.StatusCode switch
                {
                    HttpStatusCode.ServiceUnavailable => RigResult.Fail(RigFailure.NotReady, detail,
                        "The rig service is still loading its models; wait a minute and retry.", retryable: true, ms: ms),
                    HttpStatusCode.UnprocessableEntity => RigResult.Fail(RigFailure.Unriggable, detail,
                        "The mesh could not be rigged as it is. A different seed sometimes helps; a mesh "
                        + "that is not one connected figure usually does not.", ms: ms),
                    _ => RigResult.Fail(RigFailure.ServiceError, $"{(int)response.StatusCode}: {detail}",
                        "The rig service failed; its console has the traceback.", retryable: true, ms: ms),
                };
            }

            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var artifact = root.GetProperty("artifacts")[0];
                var bytes = Convert.FromBase64String(artifact.GetProperty("base64").GetString() ?? "");
                var rig = root.TryGetProperty("rig", out var r) ? r : default;

                return new RigResult
                {
                    Success = true,
                    Bytes = bytes,
                    Joints = Int(rig, "joints"),
                    WeightedJoints = Int(rig, "weighted_joints"),
                    Vertices = Int(rig, "vertices"),
                    Influences = rig.ValueKind == JsonValueKind.Object && rig.TryGetProperty("influences", out var inf)
                        ? inf.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32())
                        : new Dictionary<string, int>(),
                    Timing = root.TryGetProperty("timing_ms", out var t) && t.ValueKind == JsonValueKind.Object
                        ? t.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32())
                        : new Dictionary<string, int>(),
                    Ms = ms
                };
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or FormatException
                                            or InvalidOperationException or IndexOutOfRangeException)
            {
                return RigResult.Fail(RigFailure.ServiceError, $"The rig service answered 200 with an unreadable body: {e.Message}",
                                      "Check that Characters:RigUrl points at a rig service.", ms: ms);
            }
        }
    }

    public void Dispose()
    {
        if (this.ownsHttp) this.http.Dispose();
    }
    #endregion

    #region Methods (private)
    static string? Detail(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.TryGetProperty("detail", out var d) ? d.ToString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static int Int(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32() : 0;
    #endregion

    #region Fields
    readonly string baseUrl;
    readonly HttpClient http;
    readonly bool ownsHttp;
    #endregion
}

/// <summary>Why a rig did not come back.</summary>
public enum RigFailure
{
    None,
    InvalidRequest,
    Network,
    Timeout,
    NotReady,
    Unriggable,
    ServiceError,
    Cancelled
}

/// <summary>A rigged mesh, or why there is none.</summary>
public sealed class RigResult
{
    public bool Success { get; init; }

    public RigFailure Failure { get; init; }

    public string? Error { get; init; }

    public string Remedy { get; init; } = string.Empty;

    public bool Retryable { get; init; }

    /// <summary>The rigged .glb.</summary>
    public byte[] Bytes { get; init; } = [];

    /// <summary>Joints the skeleton has.</summary>
    public int Joints { get; init; }

    /// <summary>Joints carrying a weight above 0.05 on some vertex. Far below <see cref="Joints"/> is a warning.</summary>
    public int WeightedJoints { get; init; }

    public int Vertices { get; init; }

    /// <summary>How many vertices have 1, 2, 3 and 4 joints above 0.05.</summary>
    public IReadOnlyDictionary<string, int> Influences { get; init; } = new Dictionary<string, int>();

    /// <summary>The service's own stage timings, in milliseconds.</summary>
    public IReadOnlyDictionary<string, int> Timing { get; init; } = new Dictionary<string, int>();

    public int Ms { get; init; }

    internal static RigResult Fail(RigFailure failure, string error, string remedy, bool retryable = false, int ms = 0) =>
        new() { Failure = failure, Error = error, Remedy = remedy, Retryable = retryable, Ms = ms };
}
