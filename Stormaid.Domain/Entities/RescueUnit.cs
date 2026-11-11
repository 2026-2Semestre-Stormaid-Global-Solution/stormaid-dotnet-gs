
using Stormaid.Domain.Common;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Entities;

public class RescueUnit : Entity
{
    public string UnitCode { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;

    public ResponderInstitutionType InstitutionType { get; private set; }

    public int Capacity { get; private set; }
    public bool SupportsAccessibleTransport { get; private set; }

    public UnitOperationalStatus OperationalStatus { get; private set; }

    public bool Active { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public long Version { get; private set; }

    private RescueUnit()
    {
    }

    public RescueUnit(
        string unitCode,
        string displayName,
        ResponderInstitutionType institutionType,
        int capacity,
        bool supportsAccessibleTransport,
        DateTime utcNow)
    {
        UnitCode = ValidateUnitCode(unitCode);
        DisplayName = ValidateDisplayName(displayName);

        ValidateInstitutionType(institutionType);
        ValidateCapacity(capacity);
        ValidateUtc(utcNow);

        InstitutionType = institutionType;
        Capacity = capacity;
        SupportsAccessibleTransport = supportsAccessibleTransport;

        OperationalStatus = UnitOperationalStatus.AVAILABLE;
        Active = true;

        CreatedAt = utcNow;
        UpdatedAt = utcNow;

        Version = 0;
    }

    public void UpdateDetails(
        string unitCode,
        string displayName,
        ResponderInstitutionType institutionType,
        int capacity,
        bool supportsAccessibleTransport,
        DateTime utcNow)
    {
        var validatedCode = ValidateUnitCode(unitCode);
        var validatedName = ValidateDisplayName(displayName);

        ValidateInstitutionType(institutionType);
        ValidateCapacity(capacity);

        if (UnitCode == validatedCode &&
            DisplayName == validatedName &&
            InstitutionType == institutionType &&
            Capacity == capacity &&
            SupportsAccessibleTransport == supportsAccessibleTransport)
        {
            return;
        }

        ValidateUpdateTime(utcNow);

        UnitCode = validatedCode;
        DisplayName = validatedName;
        InstitutionType = institutionType;
        Capacity = capacity;
        SupportsAccessibleTransport = supportsAccessibleTransport;

        Touch(utcNow);
    }

    public void MarkAsBusy(DateTime utcNow)
    {
        EnsureActive();

        if (OperationalStatus == UnitOperationalStatus.BUSY)
            return;

        if (OperationalStatus != UnitOperationalStatus.AVAILABLE)
        {
            throw new InvalidOperationException(
                "Only available rescue units can become busy."
            );
        }

        ValidateUpdateTime(utcNow);

        OperationalStatus = UnitOperationalStatus.BUSY;

        Touch(utcNow);
    }

    public void MarkAsAvailable(DateTime utcNow)
    {
        EnsureActive();

        if (OperationalStatus == UnitOperationalStatus.AVAILABLE) return;

        ValidateUpdateTime(utcNow);

        OperationalStatus = UnitOperationalStatus.AVAILABLE;

        Touch(utcNow);
    }

    public void MarkOutOfService(DateTime utcNow)
    {
        if (OperationalStatus == UnitOperationalStatus.OUT_OF_SERVICE) return;

        ValidateUpdateTime(utcNow);

        OperationalStatus = UnitOperationalStatus.OUT_OF_SERVICE;

        Touch(utcNow);
    }

    public void Deactivate(DateTime utcNow)
    {
        if (!Active) return;

        ValidateUpdateTime(utcNow);

        Active = false;
        OperationalStatus = UnitOperationalStatus.OUT_OF_SERVICE;

        Touch(utcNow);
    }

    public void Activate(DateTime utcNow)
    {
        if (Active) return;

        ValidateUpdateTime(utcNow);

        Active = true;
        OperationalStatus = UnitOperationalStatus.OUT_OF_SERVICE;

        Touch(utcNow);
    }

    private static string ValidateUnitCode(string unitCode)
    {
        if (string.IsNullOrWhiteSpace(unitCode))
        {
            throw new ArgumentException(
                "Unit code is required.",
                nameof(unitCode)
            );
        }

        var normalizedCode = unitCode.Trim();

        if (normalizedCode.Length is < 3 or > 30)
        {
            throw new ArgumentException(
                "Unit code must contain between 3 and 30 characters.",
                nameof(unitCode)
            );
        }

        return normalizedCode;
    }

    private static string ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Display name is required.",
                nameof(displayName)
            );
        }

        var normalizedName = displayName.Trim();

        if (normalizedName.Length is < 3 or > 120)
        {
            throw new ArgumentException(
                "Display name must contain between 3 and 120 characters.",
                nameof(displayName)
            );
        }

        return normalizedName;
    }

    private static void ValidateInstitutionType(
        ResponderInstitutionType institutionType)
    {
        if (!Enum.IsDefined(institutionType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(institutionType),
                "Invalid responder institution type."
            );
        }
    }

    private static void ValidateCapacity(int capacity)
    {
        if (capacity is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "Capacity must be between 1 and 50."
            );
        }
    }

    private static void ValidateUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "The date must be in UTC.",
                nameof(utcNow)
            );
        }
    }

    private void ValidateUpdateTime(DateTime utcNow)
    {
        ValidateUtc(utcNow);

        if (utcNow < UpdatedAt)
        {
            throw new ArgumentException(
                "Update time cannot be earlier than the last update.",
                nameof(utcNow)
            );
        }
    }

    private void EnsureActive()
    {
        if (!Active)
        {
            throw new InvalidOperationException(
                "Inactive rescue units cannot receive operational status changes."
            );
        }
    }

    private void Touch(DateTime utcNow)
    {
        Version = checked(Version + 1);
        UpdatedAt = utcNow;
    }
}
