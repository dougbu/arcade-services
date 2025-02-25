// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.DotNet.Darc.Models.VirtualMonoRepo;

#nullable enable
namespace Microsoft.DotNet.DarcLib.VirtualMonoRepo;

public interface ISourceMappingParser
{
    Task<IReadOnlyCollection<SourceMapping>> ParseMappings(string path);
}

public class SourceMappingParser : ISourceMappingParser
{
    public async Task<IReadOnlyCollection<SourceMapping>> ParseMappings(string vmrPath)
    {
        var mappingFilePath = Path.Combine(vmrPath, VmrDependencyTracker.VmrSourcesDir, VmrDependencyTracker.SourceMappingsFileName);
        var mappingFile = new FileInfo(mappingFilePath);

        if (!mappingFile.Exists)
        {
            throw new FileNotFoundException(
                $"Failed to find {VmrDependencyTracker.SourceMappingsFileName} file in the VMR directory",
                mappingFilePath);
        }

        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        using var stream = File.Open(mappingFile.FullName, FileMode.Open);
        var settings = await JsonSerializer.DeserializeAsync<SourceMappingFile>(stream, options)
            ?? throw new Exception($"Failed to deserialize {VmrDependencyTracker.SourceMappingsFileName}");

        var patchesPath = settings.PatchesPath;
        if (patchesPath is not null)
        {
            patchesPath = Path.Combine(vmrPath, patchesPath.Replace('/', Path.DirectorySeparatorChar));
        }

        return settings.Mappings
            .Select(mapping => CreateMapping(settings.Defaults, mapping, Path.Join(patchesPath, mapping.Name)))
            .ToImmutableArray();
    }

    private static SourceMapping CreateMapping(SourceMappingSetting defaults, SourceMappingSetting setting, string? patchesPath)
    {
        if (setting.Name is null)
        {
            throw new InvalidOperationException(
                $"Missing `{nameof(SourceMapping.Name).ToLower()}` in {VmrDependencyTracker.SourceMappingsFileName}");
        }

        if (setting.DefaultRemote is null)
        {
            throw new InvalidOperationException(
                $"Missing `{nameof(SourceMapping.DefaultRemote).ToLower()}` in {VmrDependencyTracker.SourceMappingsFileName}");
        }

        IEnumerable<string> include = setting.Include ?? Enumerable.Empty<string>();
        IEnumerable<string> exclude = setting.Exclude ?? Enumerable.Empty<string>();

        if (!setting.IgnoreDefaults)
        {
            if (defaults.Include is not null)
            {
                include = defaults.Include.Concat(include).ToArray();
            }

            if (defaults.Exclude is not null)
            {
                exclude = defaults.Exclude.Concat(exclude).ToArray();
            }
        }

        var vmrPatches = patchesPath is not null && Directory.Exists(patchesPath)
            ? Directory.GetFiles(patchesPath)
            : Array.Empty<string>();

        return new SourceMapping(
            Name: setting.Name,
            Version: setting.Version,
            DefaultRemote: setting.DefaultRemote,
            DefaultRef: setting.DefaultRef ?? defaults.DefaultRef ?? "main",
            Include: include.ToImmutableArray(),
            Exclude: exclude.ToImmutableArray(),
            VmrPatches: vmrPatches.ToImmutableArray());
    }

    private class SourceMappingFile
    {
        public SourceMappingSetting Defaults { get; set; } = new()
        {
            DefaultRef = "main",
            Include = Array.Empty<string>(),
            Exclude = Array.Empty<string>(),
        };

        public string? PatchesPath { get; set; }

        public List<SourceMappingSetting> Mappings { get; set; } = new();
    }

    private class SourceMappingSetting
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? DefaultRemote { get; set; }
        public string? DefaultRef { get; set; }
        public string[]? Include { get; set; }
        public string[]? Exclude { get; set; }
        public bool IgnoreDefaults { get; set; }
    }
}
