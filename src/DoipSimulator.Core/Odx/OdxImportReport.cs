using DoipSimulator.Core.Configuration;

namespace DoipSimulator.Core.Odx;

public sealed class OdxImportReport
{
    public bool Success { get; set; }

    public OdxImportedSummary Imported { get; set; } = new();

    public List<OdxImportSkippedItem> Skipped { get; set; } = [];

    public List<OdxImportError> Errors { get; set; } = [];

    public bool Saved { get; set; }
}

public sealed class OdxImportedSummary
{
    public bool EntityInfo { get; set; }

    public int Dids { get; set; }

    public int Dtcs { get; set; }

    public int Routines { get; set; }
}

public sealed record OdxImportSkippedItem(string Path, string Reason);

public sealed record OdxImportError(string Path, string Message);

public sealed class OdxImportResult
{
    public ImportedEntityInfo EntityInfo { get; set; } = new();

    public List<DidConfig> Dids { get; set; } = [];
}

public sealed class ImportedEntityInfo
{
    public string? Name { get; set; }

    public string? Vin { get; set; }

    public string? Eid { get; set; }

    public string? Gid { get; set; }

    public string? LogicalAddress { get; set; }

    public bool HasSupportedFields =>
        !string.IsNullOrWhiteSpace(Name)
        || !string.IsNullOrWhiteSpace(Vin)
        || !string.IsNullOrWhiteSpace(Eid)
        || !string.IsNullOrWhiteSpace(Gid)
        || !string.IsNullOrWhiteSpace(LogicalAddress);
}

public sealed class OdxImportOperation
{
    public OdxImportReport Report { get; set; } = new();

    public OdxImportResult Result { get; set; } = new();
}
