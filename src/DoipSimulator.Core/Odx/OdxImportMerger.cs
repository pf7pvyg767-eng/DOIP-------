using DoipSimulator.Core.Configuration;

namespace DoipSimulator.Core.Odx;

public sealed class OdxImportMerger
{
    public async Task MergeAndSaveAsync(
        SimulatorConfig config,
        string configPath,
        ConfigStore configStore,
        OdxImportOperation operation,
        DidRuntimeStore? didRuntimeStore = null,
        CancellationToken cancellationToken = default)
    {
        if (!operation.Report.Success)
        {
            return;
        }

        var snapshot = Clone(config);
        ApplyEntityInfo(snapshot, operation.Result.EntityInfo, operation.Report);
        ApplyDids(snapshot, operation.Result.Dids, operation.Report);

        var validation = ConfigValidator.Validate(snapshot);
        if (!validation.IsValid)
        {
            operation.Report.Success = false;
            operation.Report.Errors.AddRange(validation.Errors.Select(error =>
                new OdxImportError(error.Field, error.Message)));
            return;
        }

        ApplyEntityInfo(config, operation.Result.EntityInfo, operation.Report, recordSkipped: false);
        ApplyDids(config, operation.Result.Dids, operation.Report, recordSkipped: false);
        await configStore.SaveAsync(configPath, config, cancellationToken);
        foreach (var did in operation.Result.Dids)
        {
            didRuntimeStore?.Upsert(did);
        }

        operation.Report.Saved = true;
    }

    private static void ApplyEntityInfo(
        SimulatorConfig config,
        ImportedEntityInfo entity,
        OdxImportReport report,
        bool recordSkipped = true)
    {
        if (!string.IsNullOrWhiteSpace(entity.Vin))
        {
            config.Entity.Vin = entity.Vin;
        }

        if (!string.IsNullOrWhiteSpace(entity.Eid))
        {
            config.Entity.Eid = entity.Eid;
        }

        if (!string.IsNullOrWhiteSpace(entity.Gid))
        {
            config.Entity.Gid = entity.Gid;
        }

        if (!string.IsNullOrWhiteSpace(entity.LogicalAddress))
        {
            config.Entity.LogicalAddress = entity.LogicalAddress;
        }

        if (recordSkipped && !string.IsNullOrWhiteSpace(entity.Name))
        {
            report.Skipped.Add(new OdxImportSkippedItem(
                "/ODX/ECU/NAME",
                "ECU name is reported but SimulatorConfig has no dedicated ECU name field."));
        }
    }

    private static void ApplyDids(
        SimulatorConfig config,
        IReadOnlyList<DidConfig> importedDids,
        OdxImportReport report,
        bool recordSkipped = true)
    {
        foreach (var imported in importedDids)
        {
            var existing = config.Uds.Dids.FindIndex(did =>
                ConfigValidator.TryParseDidIdentifier(did, out var current)
                && ConfigValidator.TryParseDidIdentifier(imported, out var next)
                && current == next);

            if (existing >= 0)
            {
                config.Uds.Dids[existing] = imported;
                if (recordSkipped)
                {
                    report.Skipped.Add(new OdxImportSkippedItem(
                        $"uds.dids[{imported.Identifier}]",
                        "Existing DID was overwritten by deterministic task-025 import policy."));
                }
            }
            else
            {
                config.Uds.Dids.Add(imported);
            }
        }
    }

    private static SimulatorConfig Clone(SimulatorConfig config)
    {
        return new SimulatorConfig
        {
            Entity = new EntityConfig
            {
                Vin = config.Entity.Vin,
                Eid = config.Entity.Eid,
                Gid = config.Entity.Gid,
                LogicalAddress = config.Entity.LogicalAddress,
            },
            Network = config.Network,
            Uds = new UdsConfig
            {
                Dids = config.Uds.Dids.Select(did => new DidConfig
                {
                    Id = did.Id,
                    Identifier = did.Identifier,
                    Name = did.Name,
                    ValueEncoding = did.ValueEncoding,
                    Value = did.Value,
                    Writable = did.Writable,
                    WriteLength = did.WriteLength,
                    AllowedWriteSessions = [.. did.AllowedWriteSessions],
                    RequiredSecurityState = did.RequiredSecurityState,
                    RequiredSecurityLevel = did.RequiredSecurityLevel,
                }).ToList(),
                Dtcs = config.Uds.Dtcs,
                Routines = config.Uds.Routines,
                Sessions = config.Uds.Sessions,
                TesterPresentTimeout = config.Uds.TesterPresentTimeout,
                ResponseDelays = config.Uds.ResponseDelays,
                SecurityAccess = config.Uds.SecurityAccess,
                Flash = config.Uds.Flash,
            },
            Tls = config.Tls,
            SecurityPlugin = config.SecurityPlugin,
            FaultProfile = config.FaultProfile,
        };
    }
}
