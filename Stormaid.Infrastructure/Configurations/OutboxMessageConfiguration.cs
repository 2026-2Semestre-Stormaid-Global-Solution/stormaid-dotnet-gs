using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Stormaid.Infrastructure.Data.Entities;

namespace Stormaid.Infrastructure.Configurations;

/// <summary>
/// Configura o mapeamento Oracle das mensagens
/// de integração persistidas na Outbox.
/// </summary>
public sealed class OutboxMessageConfiguration
    : IEntityTypeConfiguration<OutboxMessage>
{
    private static readonly ValueConverter<DateTime, DateTime>
        UtcConverter = new(
            value => value,
            value => DateTime.SpecifyKind(
                value,
                DateTimeKind.Utc
            )
        );

    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("TB_STORMAID_OUTBOX_MESSAGE");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("OUTBOX_MESSAGE_ID")
            .HasColumnType("NUMBER(19,0)")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.EventId)
            .HasColumnName("EVENT_ID")
            .HasColumnType("VARCHAR2(120 CHAR)")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.EventType)
            .HasColumnName("EVENT_TYPE")
            .HasColumnType("VARCHAR2(100 CHAR)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.RoutingKey)
            .HasColumnName("ROUTING_KEY")
            .HasColumnType("VARCHAR2(120 CHAR)")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnName("PAYLOAD")
            .HasColumnType("CLOB")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("CREATED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired();

        builder.Property(x => x.PublishedAt)
            .HasColumnName("PUBLISHED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired(false);

        builder.Property(x => x.Attempts)
            .HasColumnName("ATTEMPTS")
            .HasColumnType("NUMBER(10,0)")
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasColumnName("LAST_ERROR")
            .HasColumnType("VARCHAR2(1000 CHAR)")
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.HasIndex(x => x.EventId)
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.PublishedAt,
            x.CreatedAt
        });
    }
}
