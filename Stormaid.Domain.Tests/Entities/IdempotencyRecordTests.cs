using System.Text.Json;
using NUnit.Framework;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Tests.Entities;

[TestFixture]
public class IdempotencyRecordTests
{
    private static readonly DateTime UtcNow =
        new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private static readonly string Hash =
        new('a', 64);

    private static IdempotencyRecord CreateRecord()
    {
        return new IdempotencyRecord(
            10,
            "CREATE_DISPATCH",
            "dispatch-301-a1",
            Hash,
            UtcNow,
            UtcNow.AddHours(24)
        );
    }

    [Test]
    public void Constructor_WithValidData_ShouldCreateProcessingRecord()
    {
        var record = CreateRecord();

        Assert.Multiple(() =>
        {
            Assert.That(record.ActorUserId, Is.EqualTo(10));
            Assert.That(record.Operation, Is.EqualTo("CREATE_DISPATCH"));
            Assert.That(record.IdempotencyKey,
                Is.EqualTo("dispatch-301-a1"));
            Assert.That(record.RequestHash, Is.EqualTo(Hash));
            Assert.That(record.State,
                Is.EqualTo(IdempotencyState.PROCESSING));
            Assert.That(record.ResponseStatusCode, Is.Null);
            Assert.That(record.ResponseBodyJson, Is.Null);
            Assert.That(record.CreatedAt, Is.EqualTo(UtcNow));
            Assert.That(record.ExpiresAt,
                Is.EqualTo(UtcNow.AddHours(24)));
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_WithInvalidActorId_ShouldThrow(long actorId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IdempotencyRecord(
                actorId, "CREATE_DISPATCH", "key-1", Hash,
                UtcNow, UtcNow.AddHours(1)
            )
        );
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithInvalidOperation_ShouldThrow(
        string operation)
    {
        Assert.Throws<ArgumentException>(() =>
            new IdempotencyRecord(
                10, operation, "key-1", Hash,
                UtcNow, UtcNow.AddHours(1)
            )
        );
    }

    [Test]
    public void Constructor_WithLongOperation_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new IdempotencyRecord(
                10, new string('A', 81), "key-1", Hash,
                UtcNow, UtcNow.AddHours(1)
            )
        );
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithInvalidKey_ShouldThrow(string key)
    {
        Assert.Throws<ArgumentException>(() =>
            new IdempotencyRecord(
                10, "CREATE_DISPATCH", key, Hash,
                UtcNow, UtcNow.AddHours(1)
            )
        );
    }

    [Test]
    public void Constructor_WithLongKey_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new IdempotencyRecord(
                10, "CREATE_DISPATCH", new string('K', 101),
                Hash, UtcNow, UtcNow.AddHours(1)
            )
        );
    }

    [Test]
    public void Constructor_WithInvalidHashLength_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new IdempotencyRecord(
                10, "CREATE_DISPATCH", "key-1", "abc123",
                UtcNow, UtcNow.AddHours(1)
            )
        );
    }

    [Test]
    public void Constructor_WithInvalidHashCharacters_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new IdempotencyRecord(
                10, "CREATE_DISPATCH", "key-1",
                new string('g', 64),
                UtcNow, UtcNow.AddHours(1)
            )
        );
    }

    [Test]
    public void Constructor_WithInvalidExpiration_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new IdempotencyRecord(
                10, "CREATE_DISPATCH", "key-1", Hash,
                UtcNow, UtcNow.AddHours(-1)
            )
        );
    }

    [Test]
    public void Constructor_WithEqualExpiration_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new IdempotencyRecord(
                10, "CREATE_DISPATCH", "key-1", Hash,
                UtcNow, UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithNonUtcExpiration_ShouldThrow()
    {
        var localTime = DateTime.SpecifyKind(
            UtcNow.AddHours(1),
            DateTimeKind.Local
        );

        Assert.Throws<ArgumentException>(() =>
            new IdempotencyRecord(
                10, "CREATE_DISPATCH", "key-1", Hash,
                UtcNow, localTime
            )
        );
    }

    [Test]
    public void Complete_WithValidResponse_ShouldStoreOriginalResult()
    {
        var record = CreateRecord();

        record.Complete(201, "{\"dispatchId\":301}");

        Assert.Multiple(() =>
        {
            Assert.That(record.State,
                Is.EqualTo(IdempotencyState.COMPLETED));
            Assert.That(record.ResponseStatusCode, Is.EqualTo(201));
            Assert.That(record.ResponseBodyJson,
                Is.EqualTo("{\"dispatchId\":301}"));
        });
    }

    [Test]
    public void Complete_WithoutBody_ShouldSucceed()
    {
        var record = CreateRecord();

        record.Complete(204, null);

        Assert.That(record.State,
            Is.EqualTo(IdempotencyState.COMPLETED));
        Assert.That(record.ResponseStatusCode, Is.EqualTo(204));
        Assert.That(record.ResponseBodyJson, Is.Null);
    }

    [TestCase(199)]
    [TestCase(600)]
    public void Complete_WithInvalidHttpStatus_ShouldThrow(int statusCode)
    {
        var record = CreateRecord();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            record.Complete(statusCode, "{\"ok\":true}")
        );

        Assert.That(record.State,
            Is.EqualTo(IdempotencyState.PROCESSING));
    }

    [Test]
    public void Complete_WithInvalidJson_ShouldThrow()
    {
        var record = CreateRecord();

        Assert.Catch<JsonException>(() =>
            record.Complete(201, "{invalid-json}")
        );

        Assert.That(
            record.State,
            Is.EqualTo(IdempotencyState.PROCESSING)
        );

        Assert.That(record.ResponseStatusCode, Is.Null);
    }


    [Test]
    public void Complete_WhenAlreadyCompleted_ShouldThrow()
    {
        var record = CreateRecord();
        record.Complete(201, "{\"dispatchId\":301}");

        Assert.Throws<InvalidOperationException>(() =>
            record.Complete(201, "{\"dispatchId\":302}")
        );

        Assert.That(record.ResponseBodyJson,
            Is.EqualTo("{\"dispatchId\":301}"));
    }

    [Test]
    public void MarkFailedRetryable_ShouldChangeState()
    {
        var record = CreateRecord();

        record.MarkFailedRetryable();

        Assert.That(record.State,
            Is.EqualTo(IdempotencyState.FAILED_RETRYABLE));
    }

    [Test]
    public void RestartProcessing_AfterRetryableFailure_ShouldRestart()
    {
        var record = CreateRecord();

        record.MarkFailedRetryable();
        record.RestartProcessing();

        Assert.That(record.State,
            Is.EqualTo(IdempotencyState.PROCESSING));

        record.Complete(201, "{\"dispatchId\":301}");

        Assert.That(record.State,
            Is.EqualTo(IdempotencyState.COMPLETED));
    }

    [Test]
    public void RestartProcessing_WhenCompleted_ShouldThrow()
    {
        var record = CreateRecord();
        record.Complete(201, "{\"dispatchId\":301}");

        Assert.Throws<InvalidOperationException>(() =>
            record.RestartProcessing()
        );
    }

    [Test]
    public void Complete_WhenFailedRetryable_ShouldThrow()
    {
        var record = CreateRecord();
        record.MarkFailedRetryable();

        Assert.Throws<InvalidOperationException>(() =>
            record.Complete(201, "{\"dispatchId\":301}")
        );
    }

    [Test]
    public void EnsureSameRequest_WithMatchingHash_ShouldSucceed()
    {
        var record = CreateRecord();

        Assert.DoesNotThrow(() =>
            record.EnsureSameRequest(
                10,
                "CREATE_DISPATCH",
                "dispatch-301-a1",
                Hash.ToUpperInvariant()
            )
        );
    }

    [Test]
    public void EnsureSameRequest_WithDifferentHash_ShouldThrow()
    {
        var record = CreateRecord();

        Assert.Throws<InvalidOperationException>(() =>
            record.EnsureSameRequest(
                10,
                "CREATE_DISPATCH",
                "dispatch-301-a1",
                new string('b', 64)
            )
        );
    }

    [Test]
    public void IsExpired_AtExpirationBoundary_ShouldReturnTrue()
    {
        var record = CreateRecord();

        Assert.That(
            record.IsExpired(UtcNow.AddHours(23)),
            Is.False
        );

        Assert.That(
            record.IsExpired(UtcNow.AddHours(24)),
            Is.True
        );
    }
}
