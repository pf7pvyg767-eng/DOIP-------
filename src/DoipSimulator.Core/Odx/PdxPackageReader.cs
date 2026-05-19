using System.IO.Compression;

namespace DoipSimulator.Core.Odx;

public sealed class PdxPackageReader
{
    private const long MaxPackageBytes = 20 * 1024 * 1024;
    private const long MaxEntryBytes = 5 * 1024 * 1024;
    private readonly OdxImportService odxImportService;

    public PdxPackageReader(OdxImportService? odxImportService = null)
    {
        this.odxImportService = odxImportService ?? new OdxImportService();
    }

    public async Task<OdxImportOperation> ImportAsync(Stream pdxStream, CancellationToken cancellationToken = default)
    {
        var operation = new OdxImportOperation();
        try
        {
            if (pdxStream.CanSeek && pdxStream.Length > MaxPackageBytes)
            {
                AddError(operation.Report, "/", "PDX package exceeds the supported MVP size limit.");
                return operation;
            }

            using var archive = new ZipArchive(pdxStream, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in archive.Entries)
            {
                if (IsUnsafePath(entry.FullName))
                {
                    AddError(operation.Report, entry.FullName, "PDX package contains an unsafe entry path.");
                    return operation;
                }
            }

            var odxEntries = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name)
                    && entry.FullName.EndsWith(".odx", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (odxEntries.Length == 0)
            {
                AddError(operation.Report, "/", "PDX package does not contain an ODX entry.");
                return operation;
            }

            var entryPoint = odxEntries.Length == 1
                ? odxEntries[0]
                : odxEntries.FirstOrDefault(entry => entry.Name.Contains("index", StringComparison.OrdinalIgnoreCase)
                    || entry.Name.Contains("main", StringComparison.OrdinalIgnoreCase));
            if (entryPoint is null || odxEntries.Length > 1 && odxEntries.Count(entry => entry == entryPoint) != 1)
            {
                AddError(operation.Report, "/", "PDX package contains multiple ODX entries without a clear index/main entry.");
                return operation;
            }

            if (entryPoint.Length > MaxEntryBytes)
            {
                AddError(operation.Report, entryPoint.FullName, "ODX entry exceeds the supported MVP size limit.");
                return operation;
            }

            await using var stream = entryPoint.Open();
            var imported = await odxImportService.ImportAsync(stream, cancellationToken);
            imported.Report.Skipped.Insert(0, new OdxImportSkippedItem(
                entryPoint.FullName,
                "Selected as PDX ODX entry point."));
            foreach (var entry in archive.Entries.Where(entry => entry != entryPoint))
            {
                imported.Report.Skipped.Add(new OdxImportSkippedItem(
                    entry.FullName,
                    "PDX resource is outside the task-025 import subset."));
            }

            return imported;
        }
        catch (InvalidDataException ex)
        {
            AddError(operation.Report, "/", $"PDX package is not a valid zip archive: {ex.Message}");
        }

        return operation;
    }

    private static bool IsUnsafePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            || path.StartsWith("/", StringComparison.Ordinal)
            || path.StartsWith("\\", StringComparison.Ordinal)
            || path.Contains("..", StringComparison.Ordinal);
    }

    private static void AddError(OdxImportReport report, string path, string message)
    {
        report.Success = false;
        report.Errors.Add(new OdxImportError(path, message));
    }
}
