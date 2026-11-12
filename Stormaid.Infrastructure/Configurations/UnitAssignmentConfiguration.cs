
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Stormaid.Domain.Entities;

namespace Stormaid.Infrastructure.Configurations;

public class UnitAssignmentConfiguration : IEntityTypeConfiguration<UnitAssignment>
{
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter = 
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public void Configure(EntityTypeBuilder<UnitAssignment> builder)
    {
        builder.ToTable("TB_STORMAID_UNIT_ASSIGNMENT");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("UNIT_ASSIGNMENT_ID")
            .HasColumnType("NUMBER(19,0)")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.RescueUnitId)
            .HasColumnName("RESCUE_UNIT_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.ResponderUserId)
            .HasColumnName("RESPONDER_USER_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.AssignedByRegulatorUserId)
            .HasColumnName("ASSIGNED_BY_USER_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("STATUS")
            .HasColumnType("VARCHAR2(20 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.StartedAt)
            .HasColumnName("STARTED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired();

        builder.Property(x => x.EndedAt)
            .HasColumnName("ENDED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired(false);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("CREATED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired();

        builder.Property(x => x.Version)
            .HasColumnName("VERSION")
            .HasColumnType("NUMBER(19,0)")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasOne<RescueUnit>()
            .WithMany()
            .HasForeignKey(x => x.RescueUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ResponderUserId);
        builder.HasIndex(x => x.RescueUnitId);
    }
}
