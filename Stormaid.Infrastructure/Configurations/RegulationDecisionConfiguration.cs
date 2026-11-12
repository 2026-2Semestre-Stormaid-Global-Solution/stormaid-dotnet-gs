
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Stormaid.Domain.Entities;

namespace Stormaid.Infrastructure.Configurations;

public class RegulationDecisionConfiguration : IEntityTypeConfiguration<RegulationDecision>
{
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public void Configure(EntityTypeBuilder<RegulationDecision> builder)
    {
        builder.ToTable("TB_STORMAID_REGULATION_DECISION");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("REGULATION_DECISION_ID")
            .HasColumnType("NUMBER(19,0)")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IncidentReferenceId)
            .HasColumnName("INCIDENT_REFERENCE_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.RegulatorUserId)
            .HasColumnName("REGULATOR_USER_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.DecisionType)
            .HasColumnName("DECISION_TYPE")
            .HasColumnType("VARCHAR2(35 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(35)
            .IsRequired();

        builder.Property(x => x.InstitutionType)
            .HasColumnName("INSTITUTION_TYPE")
            .HasColumnType("VARCHAR2(40 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired(false);

        builder.Property(x => x.Justification)
            .HasColumnName("JUSTIFICATION")
            .HasColumnType("VARCHAR2(1000 CHAR)")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.DecidedAt)
            .HasColumnName("DECIDED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired();

        builder.HasOne<IncidentReference>()
            .WithMany()
            .HasForeignKey(x => x.IncidentReferenceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IncidentReferenceId);

        builder.HasIndex(x => x.RegulatorUserId);
    }
}
