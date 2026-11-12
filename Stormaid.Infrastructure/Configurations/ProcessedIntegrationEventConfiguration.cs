using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Stormaid.Domain.Entities;

namespace Stormaid.Infrastructure.Configurations;

public class ProcessedIntegrationEventConfiguration : IEntityTypeConfiguration<ProcessedIntegrationEvent>
{
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public void Configure(EntityTypeBuilder<ProcessedIntegrationEvent> builder)
    {
        builder.ToTable("TB_STORMAID_PROCESSED_EVENT");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("PROCESSED_EVENT_ID")
            .HasColumnType("NUMBER(19,0)")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.EventId)
            .HasColumnName("EVENT_ID")
            .HasColumnType("VARCHAR2(120 CHAR)")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.EventType)
            .HasColumnName("EVENT_TYPE")
            .HasColumnType("VARCHAR2(80 CHAR)")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.ProcessedAt)
            .HasColumnName("PROCESSED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired();

        builder.HasIndex(x => x.EventId)
            .IsUnique();
    }
}