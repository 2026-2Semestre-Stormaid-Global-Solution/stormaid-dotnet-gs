
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Stormaid.Domain.Entities;

namespace Stormaid.Infrastructure.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("TB_STORMAID_IDEMPOTENCY");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("IDEMPOTENCY_ID")
            .HasColumnType("NUMBER(19,0)")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ActorUserId)
            .HasColumnName("ACTOR_USER_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.Operation)
            .HasColumnName("OPERATION")
            .HasColumnType("VARCHAR2(80 CHAR)")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.IdempotencyKey)
            .HasColumnName("IDEMPOTENCY_KEY")
            .HasColumnType("VARCHAR2(100 CHAR)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.RequestHash)
            .HasColumnName("REQUEST_HASH")
            .HasColumnType("VARCHAR2(64 CHAR)")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ResponseStatusCode)
            .HasColumnName("RESPONSE_STATUS_CODE")
            .HasColumnType("NUMBER(3,0)")
            .IsRequired(false);

        builder.Property(x => x.ResponseBodyJson)
            .HasColumnName("RESPONSE_BODY")
            .HasColumnType("CLOB")
            .IsRequired(false);

        builder.Property(x => x.State)
            .HasColumnName("STATE")
            .HasColumnType("VARCHAR2(20 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("CREATED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("EXPIRES_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.ActorUserId,
            x.Operation,
            x.IdempotencyKey
        })
        .IsUnique();

        builder.HasIndex(x => x.State);

        builder.HasIndex(x => x.ExpiresAt);
    }
}
