using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DoipSimulator.Core.Configuration;

namespace DoipSimulator.Core.Odx;

public sealed class OdxImportService
{
    private static readonly HashSet<string> UnsupportedBranches = new(StringComparer.OrdinalIgnoreCase)
    {
        "DTC",
        "DTCS",
        "ROUTINE",
        "ROUTINES",
        "FLASH",
        "COMPU-METHOD",
        "COMPU_METHOD",
        "FORMULA",
        "DYNAMIC-LENGTH-FIELD",
    };

    public async Task<OdxImportOperation> ImportAsync(Stream odxStream, CancellationToken cancellationToken = default)
    {
        var operation = new OdxImportOperation();
        try
        {
            if (!odxStream.CanRead)
            {
                AddError(operation.Report, "/", "ODX stream is not readable.");
                return operation;
            }

            using var reader = XmlReader.Create(
                odxStream,
                new XmlReaderSettings
                {
                    Async = true,
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = 5_000_000,
                });
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
            ParseEntityInfo(document, operation);
            ParseDids(document, operation);
            RecordUnsupportedBranches(document, operation.Report);

            operation.Report.Imported.EntityInfo = operation.Result.EntityInfo.HasSupportedFields;
            operation.Report.Imported.Dids = operation.Result.Dids.Count;
            operation.Report.Imported.Dtcs = 0;
            operation.Report.Imported.Routines = 0;
            operation.Report.Success = operation.Report.Errors.Count == 0
                && (operation.Report.Imported.EntityInfo || operation.Report.Imported.Dids > 0);

            if (!operation.Report.Success && operation.Report.Errors.Count == 0)
            {
                AddError(operation.Report, "/", "ODX file did not contain supported ECU or DID information.");
            }
        }
        catch (XmlException ex)
        {
            AddError(operation.Report, "/", $"ODX XML is invalid: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            AddError(operation.Report, "/", $"ODX import failed: {ex.Message}");
        }

        return operation;
    }

    private static void ParseEntityInfo(XDocument document, OdxImportOperation operation)
    {
        var root = document.Root;
        if (root is null)
        {
            AddError(operation.Report, "/", "ODX document has no root element.");
            return;
        }

        operation.Result.EntityInfo.Name =
            FindFirstText(root, "ECU-NAME", "ECU_NAME", "SHORT-NAME", "SHORT_NAME", "NAME");
        operation.Result.EntityInfo.Vin = FindFirstText(root, "VIN");
        operation.Result.EntityInfo.Eid = NormalizeHexWithoutPrefix(FindFirstText(root, "EID"));
        operation.Result.EntityInfo.Gid = NormalizeHexWithoutPrefix(FindFirstText(root, "GID"));
        operation.Result.EntityInfo.LogicalAddress = NormalizeUInt16Hex(
            FindFirstText(root, "LOGICAL-ADDRESS", "LOGICAL_ADDRESS", "LOGICAL-ADDR", "ADDRESS"));

        if (operation.Result.EntityInfo.HasSupportedFields)
        {
            return;
        }

        operation.Report.Skipped.Add(new OdxImportSkippedItem(
            "/ODX/ECU",
            "No supported ECU basic information fields were found."));
    }

    private static void ParseDids(XDocument document, OdxImportOperation operation)
    {
        var candidates = document
            .Descendants()
            .Where(IsDidCandidate)
            .ToArray();

        if (candidates.Length == 0)
        {
            operation.Report.Skipped.Add(new OdxImportSkippedItem(
                "/ODX/DIDS",
                "No supported DID definitions were found."));
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in candidates)
        {
            var path = BuildPath(element);
            var identifier = NormalizeUInt16Hex(ReadValue(element, "ID", "IDENTIFIER", "DID", "DATA-IDENTIFIER", "DATA_IDENTIFIER"));
            if (string.IsNullOrWhiteSpace(identifier))
            {
                operation.Report.Skipped.Add(new OdxImportSkippedItem(path, "DID identifier is missing or invalid."));
                continue;
            }

            if (!seen.Add(identifier))
            {
                operation.Report.Skipped.Add(new OdxImportSkippedItem(path, $"Duplicate DID {identifier} in import file was skipped."));
                continue;
            }

            var value = NormalizeHexWithoutPrefix(ReadValue(
                element,
                "FIXED-VALUE",
                "FIXED_VALUE",
                "DEFAULT-VALUE",
                "DEFAULT_VALUE",
                "HEX-VALUE",
                "HEX_VALUE",
                "VALUE"));
            if (string.IsNullOrWhiteSpace(value) || !DidRuntimeStore.TryParseHexBytes(value, out _))
            {
                operation.Report.Skipped.Add(new OdxImportSkippedItem(
                    path,
                    $"DID {identifier} has no supported fixed hex value."));
                continue;
            }

            RecordUnsupportedDidFields(element, path, operation.Report);
            operation.Result.Dids.Add(new DidConfig
            {
                Identifier = identifier,
                Name = ReadValue(element, "SHORT-NAME", "SHORT_NAME", "LONG-NAME", "LONG_NAME", "NAME"),
                ValueEncoding = "hex",
                Value = value,
                Writable = false,
            });
        }
    }

    private static bool IsDidCandidate(XElement element)
    {
        var localName = element.Name.LocalName;
        return localName.Equals("DID", StringComparison.OrdinalIgnoreCase)
            || localName.Equals("DATA-IDENTIFIER", StringComparison.OrdinalIgnoreCase)
            || localName.Equals("DATA_IDENTIFIER", StringComparison.OrdinalIgnoreCase)
            || localName.Equals("DATA-IDENTIFIER-REF", StringComparison.OrdinalIgnoreCase);
    }

    private static void RecordUnsupportedDidFields(XElement element, string path, OdxImportReport report)
    {
        foreach (var unsupported in element.Descendants().Where(child => UnsupportedBranches.Contains(child.Name.LocalName)))
        {
            report.Skipped.Add(new OdxImportSkippedItem(
                $"{path}/{unsupported.Name.LocalName}",
                "Unsupported DID metadata was not imported in the MVP subset."));
        }
    }

    private static void RecordUnsupportedBranches(XDocument document, OdxImportReport report)
    {
        foreach (var element in document.Descendants().Where(item => UnsupportedBranches.Contains(item.Name.LocalName)))
        {
            report.Skipped.Add(new OdxImportSkippedItem(
                BuildPath(element),
                "ODX branch is outside the task-025 import subset."));
        }
    }

    private static string? FindFirstText(XElement root, params string[] names)
    {
        foreach (var name in names)
        {
            var text = root
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?.Value;
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        return null;
    }

    private static string? ReadValue(XElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var attribute = element
                .Attributes()
                .FirstOrDefault(item => item.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(attribute?.Value))
            {
                return attribute.Value.Trim();
            }
        }

        foreach (var name in names)
        {
            var child = element
                .Descendants()
                .FirstOrDefault(item => item.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(child?.Value))
            {
                return child.Value.Trim();
            }
        }

        return null;
    }

    private static string? NormalizeHexWithoutPrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim()
            .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
        return normalized.Length > 0 && normalized.All(Uri.IsHexDigit) ? normalized : null;
    }

    private static string? NormalizeUInt16Hex(string? value)
    {
        var normalized = NormalizeHexWithoutPrefix(value);
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length > 4
            || !ushort.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
        {
            return null;
        }

        return $"0x{parsed:X4}";
    }

    private static string BuildPath(XElement element)
    {
        var names = element.AncestorsAndSelf().Reverse().Select(item => item.Name.LocalName);
        return "/" + string.Join("/", names);
    }

    private static void AddError(OdxImportReport report, string path, string message)
    {
        report.Success = false;
        report.Errors.Add(new OdxImportError(path, message));
    }
}
