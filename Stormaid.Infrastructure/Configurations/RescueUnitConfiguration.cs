
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stormaid.Domain.Entities;

namespace Stormaid.Infrastructure.Configurations;

public class RescueUnitConfiguration : IEntityTypeConfiguration<RescueUnit>
{
    public void Configure(EntityTypeBuilder<RescueUnit> builder)
    {
        builder.ToTable("TB_STORMAID_RESCUE_UNIT");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("RESCUE_UNIT_ID")
            .HasColumnType("NUMBER(19,0)")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.UnitCode)
            .HasColumnName("UNIT_CODE")
            .HasColumnType("VARCHAR2(30 CHAR)")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasColumnName("DISPLAY_NAME")
            .HasColumnType("VARCHAR2(120 CHAR)")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.InstitutionType)
            .HasColumnName("INSTITUTION_TYPE")
            .HasColumnType("VARCHAR2(40 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.Capacity)
            .HasColumnName("CAPACITY")
            .HasColumnType("NUMBER(5,0)")
            .IsRequired();

        builder.Property(x => x.SupportsAccessibleTransport)
            .HasColumnName("ACCESSIBLE_TRANSPORT")
            .HasColumnType("NUMBER(1,0)")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.OperationalStatus)
            .HasColumnName("OPERATIONAL_STATUS")
            .HasColumnType("VARCHAR2(25 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(25)
            .IsRequired();

        builder.Property(x => x.Active)
            .HasColumnName("ACTIVE")
            .HasColumnType("NUMBER(1,0)")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("CREATED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(
                value => value,
                value => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            )
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("UPDATED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(
                value => value,
                value => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            )
            .IsRequired();

        builder.Property(x => x.Version)
            .HasColumnName("VERSION")
            .HasColumnType("NUMBER(19,0)")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(x => x.UnitCode)
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.InstitutionType,
            x.OperationalStatus
        });
    }
}
