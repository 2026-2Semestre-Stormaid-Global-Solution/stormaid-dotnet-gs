
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Stormaid.Domain.Entities;

namespace Stormaid.Infrastructure.Configurations;

public class IncidentDispatchConfiguration : IEntityTypeConfiguration<IncidentDispatch>
{
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public void Configure(EntityTypeBuilder<IncidentDispatch> builder)
    {
        builder.ToTable("TB_STORMAID_INCIDENT_DISPATCH");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("DISPATCH_ID")
            .HasColumnType("NUMBER(19,0)")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IncidentReferenceId)
            .HasColumnName("INCIDENT_REFERENCE_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.RegulationDecisionId)
            .HasColumnName("REGULATION_DECISION_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.RescueUnitId)
            .HasColumnName("RESCUE_UNIT_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("DISPATCH_STATUS")
            .HasColumnType("VARCHAR2(25 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(25)
            .IsRequired();

        builder.Property(x => x.AcceptedByResponderUserId)
            .HasColumnName("ACCEPTED_BY_USER_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired(false);

        builder.Property(x => x.CreatedByRegulatorUserId)
            .HasColumnName("CREATED_BY_USER_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasColumnName("NOTES")
            .HasColumnType("VARCHAR2(1000 CHAR)")
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(x => x.DispatchedAt)
            .HasColumnName("DISPATCHED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired();

        builder.Property(x => x.AcceptedAt)
            .HasColumnName("ACCEPTED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired(false);

        builder.Property(x => x.EnRouteAt)
            .HasColumnName("EN_ROUTE_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired(false);

        builder.Property(x => x.ArrivedAt)
            .HasColumnName("ARRIVED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired(false);

        builder.Property(x => x.ResolvedAt)
            .HasColumnName("RESOLVED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired(false);

        builder.Property(x => x.CancelledAt)
            .HasColumnName("CANCELLED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired(false);

        builder.Property(x => x.Version)
            .HasColumnName("VERSION")
            .HasColumnType("NUMBER(19,0)")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasOne<IncidentReference>()
            .WithMany()
            .HasForeignKey(x => x.IncidentReferenceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RegulationDecision>()
            .WithMany()
            .HasForeignKey(x => x.RegulationDecisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RescueUnit>()
            .WithMany()
            .HasForeignKey(x => x.RescueUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IncidentReferenceId);

        builder.HasIndex(x => x.RegulationDecisionId);

        builder.HasIndex(x => x.RescueUnitId);

        builder.HasIndex(x => x.Status);
    }
}
