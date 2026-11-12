using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Stormaid.Domain.Entities;

namespace Stormaid.Infrastructure.Configurations;

public class SupportRequestConfiguration : IEntityTypeConfiguration<SupportRequest>
{
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public void Configure(EntityTypeBuilder<SupportRequest> builder)
    {
        builder.ToTable("TB_STORMAID_SUPPORT_REQUEST");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("SUPPORT_REQUEST_ID")
            .HasColumnType("NUMBER(19,0)")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IncidentReferenceId)
            .HasColumnName("INCIDENT_REFERENCE_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.IncidentDispatchId)
            .HasColumnName("DISPATCH_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.RequestedByResponderUserId)
            .HasColumnName("REQUESTED_BY_USER_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.RequestedInstitutionType)
            .HasColumnName("REQUESTED_INSTITUTION_TYPE")
            .HasColumnType("VARCHAR2(40 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired(false);

        builder.Property(x => x.Reason)
            .HasColumnName("REASON")
            .HasColumnType("VARCHAR2(1000 CHAR)")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("STATUS")
            .HasColumnType("VARCHAR2(25 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(25)
            .IsRequired();

        builder.Property(x => x.ReviewedByRegulatorUserId)
            .HasColumnName("REVIEWED_BY_USER_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired(false);

        builder.Property(x => x.ReviewJustification)
            .HasColumnName("REVIEW_JUSTIFICATION")
            .HasColumnType("VARCHAR2(1000 CHAR)")
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("CREATED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired();

        builder.Property(x => x.ReviewedAt)
            .HasColumnName("REVIEWED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired(false);

        builder.HasOne<IncidentReference>()
            .WithMany()
            .HasForeignKey(x => x.IncidentReferenceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IncidentDispatch>()
            .WithMany()
            .HasForeignKey(x => x.IncidentDispatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IncidentReferenceId);

        builder.HasIndex(x => x.IncidentDispatchId);

        builder.HasIndex(x => x.RequestedByResponderUserId);

        builder.HasIndex(x => x.Status);
    }
}
