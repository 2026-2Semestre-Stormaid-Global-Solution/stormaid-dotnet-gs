
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Stormaid.Domain.Entities;

namespace Stormaid.Infrastructure.Configurations;

public class IncidentReferenceConfiguration : IEntityTypeConfiguration<IncidentReference>
{
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public void Configure(EntityTypeBuilder<IncidentReference> builder)
    {
        builder.ToTable("TB_STORMAID_INCIDENT_REFERENCE");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("INCIDENT_REFERENCE_ID")
            .HasColumnType("NUMBER(19,0)")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.JavaIncidentId)
            .HasColumnName("JAVA_INCIDENT_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.RequesterUserId)
            .HasColumnName("REQUESTER_USER_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.RecipientType)
            .HasColumnName("RECIPIENT_TYPE")
            .HasColumnType("VARCHAR2(20 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ChannelType)
            .HasColumnName("CHANNEL_TYPE")
            .HasColumnType("VARCHAR2(20 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.EmergencyCategory)
            .HasColumnName("EMERGENCY_CATEGORY")
            .HasColumnType("VARCHAR2(35 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(35)
            .IsRequired();

        builder.Property(x => x.Latitude)
            .HasColumnName("LATITUDE")
            .HasColumnType("NUMBER(10,7)")
            .IsRequired(false);

        builder.Property(x => x.Longitude)
            .HasColumnName("LONGITUDE")
            .HasColumnType("NUMBER(11,7)")
            .IsRequired(false);

        builder.Property(x => x.AddressText)
            .HasColumnName("ADDRESS_TEXT")
            .HasColumnType("VARCHAR2(300 CHAR)")
            .HasMaxLength(300)
            .IsRequired(false);

        builder.Property(x => x.RegulationStatus)
            .HasColumnName("REGULATION_STATUS")
            .HasColumnType("VARCHAR2(25 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(25)
            .IsRequired();

        builder.Property(x => x.PublicStatusVersion)
            .HasColumnName("PUBLIC_STATUS_VERSION")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.ReceivedAt)
            .HasColumnName("RECEIVED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired();

        builder.Property(x => x.JavaCreatedAt)
            .HasColumnName("JAVA_CREATED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired(false);

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("UPDATED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired();

        builder.Property(x => x.Version)
            .HasColumnName("VERSION")
            .HasColumnType("NUMBER(19,0)")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(x => x.JavaIncidentId)
            .IsUnique();
    }
}
