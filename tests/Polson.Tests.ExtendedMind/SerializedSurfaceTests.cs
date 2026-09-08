namespace Polson.Tests.ExtendedMind;

using System;
using System.Collections.Generic;
using System.Linq;
using Polson.ExtendedMind.ImageGeneration;
using Polson.ExtendedMind.Photos;
using Xunit;

/// <summary>
/// The shape every result presents to <c>JSON.stringify</c>: documented names, no raw buffers.
/// </summary>
/// <remarks>
/// The engine-level half — that Jint reaches the hook at all — is
/// <c>SerializationHookTests</c> in the MCP server suite. This is the other half: that what the hook
/// returns is the surface the reference documents, in the spelling it documents, and that no byte
/// array is anywhere in it.
/// <para>
/// Both halves are needed and neither implies the other. A correct dictionary behind an unreachable
/// hook is what this codebase had for the first hour of the fix; a reachable hook returning
/// PascalCase would leave <c>JSON.parse(...)</c> reading <c>undefined</c> on every documented name.
/// </para>
/// </remarks>
public class SerializedSurfaceTests : TestsRuntime
{
    #region Methods
    private static PhotoAsset APhoto() => new()
    {
        Success = true,
        Id = "abc123",
        Bytes = new byte[147_169],
        Width = 960,
        Height = 1189,
        MimeType = "image/jpeg",
        Source = "wikimedia",
        Subject = new SubjectMatch
        {
            Success = true,
            Query = "Stanley Kubrick",
            Title = "Stanley Kubrick",
            Description = "American filmmaker and photographer (1928-1999)",
        },
        Licence = new PhotoLicence { Name = "Public domain", Artist = "Columbia Pictures" },
    };

    private static MaterialAsset AMaterial() => new()
    {
        Success = true,
        Id = "mat1",
        Bytes = new byte[27_000],
        Size = 512,
        Provenance = new Provenance
        {
            Model = "gemini-2.5-flash-image",
            Hash = "deadbeef",
            GeneratedUtc = new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc),
            Prompt = "weathered oak planking",
        },
    };

    /// <summary>Every key in the tree, so a nested lapse cannot hide under a correct top level.</summary>
    private static IEnumerable<string> KeysDeep(Dictionary<string, object?> json)
    {
        foreach (var (key, value) in json)
        {
            yield return key;
            if (value is Dictionary<string, object?> nested)
            {
                foreach (var inner in KeysDeep(nested)) yield return inner;
            }
        }
    }
    #endregion

    #region Tests
    /// <summary>The image is a length, not a transcription.</summary>
    [Fact]
    public void TestAPhotographReportsItsSizeRatherThanItsBytes()
    {
        var json = APhoto().ToJSON();

        Assert.Equal(147_169, json["byteLength"]);
        Assert.DoesNotContain("Bytes", KeysDeep(json));
        Assert.DoesNotContain("bytes", KeysDeep(json));
    }

    /// <summary>The names the reference documents are the names that come back.</summary>
    [Fact]
    public void TestAPhotographUsesTheDocumentedSpelling()
    {
        var json = APhoto().ToJSON();

        Assert.Equal(0.8074011774600505, (double)json["aspectRatio"]!, 12);
        Assert.Equal("wikimedia", json["source"]);
        Assert.Equal("None", json["failureName"]);
    }

    /// <summary>
    /// Nested types carry the same discipline. A half-done job is worse than none: camelCase at the
    /// top and PascalCase one level down is harder to work with than being uniformly wrong.
    /// </summary>
    [Fact]
    public void TestNestedRecordsAreConvertedToo()
    {
        var json = APhoto().ToJSON();

        var licence = Assert.IsType<Dictionary<string, object?>>(json["licence"]);
        Assert.Equal("Public domain", licence["name"]);

        var subject = Assert.IsType<Dictionary<string, object?>>(json["subject"]);
        Assert.Equal("American filmmaker and photographer (1928-1999)", subject["description"]);
    }

    /// <summary>Not one PascalCase name anywhere in the tree.</summary>
    [Fact]
    public void TestEveryKeyInTheTreeStartsLowerCase()
    {
        foreach (var key in KeysDeep(APhoto().ToJSON()).Concat(KeysDeep(AMaterial().ToJSON())))
        {
            Assert.True(char.IsLower(key[0]), $"'{key}' is not the documented spelling");
        }
    }

    /// <summary>A requisitioned asset gets the same treatment, through the shared base.</summary>
    [Fact]
    public void TestARequisitionedMaterialAlsoReportsASize()
    {
        var json = AMaterial().ToJSON();

        Assert.Equal(27_000, json["byteLength"]);
        Assert.Equal(512, json["size"]);
        Assert.DoesNotContain("Bytes", KeysDeep(json));
    }

    /// <summary>The base's fields survive the override rather than being replaced by it.</summary>
    [Fact]
    public void TestTheSharedFailureSurfaceIsKeptByEverySubclass()
    {
        var json = AMaterial().ToJSON();

        Assert.Equal(true, json["success"]);
        Assert.Equal("mat1", json["id"]);
        Assert.Equal("None", json["failureName"]);
        Assert.NotNull(json["remedy"]);

        var provenance = Assert.IsType<Dictionary<string, object?>>(json["provenance"]);
        Assert.Equal("gemini-2.5-flash-image", provenance["model"]);
    }

    /// <summary>A failed result serialises too — that is when a reader most wants to read it.</summary>
    [Fact]
    public void TestAFailureSerialisesWithoutItsAbsentPieces()
    {
        var refused = new MatteAsset { Success = false, Failure = ImageGenerationFailure.RefusedLikeness };
        var json = refused.ToJSON();

        Assert.Equal(false, json["success"]);
        Assert.Equal("RefusedLikeness", json["failureName"]);
        Assert.Equal(0, json["byteLength"]);
        Assert.Null(json["provenance"]);
    }

    /// <summary>
    /// The parameter is not decoration: <c>JSON.stringify</c> calls <c>toJSON(key)</c> with the
    /// property name, and a parameterless method fails to bind with "No public methods with the
    /// specified arguments were found" — which is exactly what the first attempt at this did.
    /// </summary>
    [Fact]
    public void TestTheHookAcceptsTheKeyArgumentJsonStringifyPasses()
    {
        Assert.Equal(147_169, APhoto().ToJSON("photo")["byteLength"]);
        Assert.Equal(27_000, AMaterial().ToJSON("material")["byteLength"]);
    }
    #endregion
}
