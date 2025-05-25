using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using JetBrains.Annotations;
using NexusMods.Paths;
using OneOf;

namespace GameFinder.StoreHandlers.EGS;

[UsedImplicitly]
internal record ManifestFile(
    string CatalogItemId,
    string AppName,
    string DisplayName,
    string InstallLocation,
    bool bIsApplication);

/// <summary>
/// Handler for finding games installed with the Epic Games Store.
/// </summary>
[PublicAPI]
public class EGSHandler : AHandler<EGSGame, EGSGameId>
{
    internal const string RegKey = @"Software\Epic Games\EOS";
    internal const string ModSdkMetadataDir = "ModSdkMetadataDir";

    private readonly IRegistry _registry;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="registry">
    /// The implementation of <see cref="IRegistry"/> to use. For a shared instance
    /// use <see cref="WindowsRegistry.Shared"/> on Windows. For tests either use
    /// <see cref="InMemoryRegistry"/>, a custom implementation or just a mock
    /// of the interface. See the README for more information if you want to use
    /// Wine.
    /// </param>
    /// <param name="fileSystem">
    /// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
    /// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
    /// a custom implementation or just a mock of the interface. See the README for more information
    /// if you want to use Wine.
    /// </param>
    public EGSHandler(IRegistry registry, IFileSystem fileSystem)
    {
        _registry = registry;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc/>
    public override IEqualityComparer<EGSGameId> IdEqualityComparer => EGSGameIdComparer.Default;

    /// <inheritdoc/>
    public override Func<EGSGame, EGSGameId> IdSelector => game => game.CatalogItemId;

    /// <inheritdoc/>
    public override IEnumerable<OneOf<EGSGame, ErrorMessage>> FindAllGames()
    {
        var manifestDir = GetManifestDir();
        if (!_fileSystem.DirectoryExists(manifestDir))
        {
            yield return new ErrorMessage($"The manifest directory {manifestDir.GetFullPath()} does not exist!");
            yield break;
        }

        var itemFiles = _fileSystem
            .EnumerateFiles(manifestDir, "*.item")
            .ToArray();

        if (itemFiles.Length == 0)
        {
            yield return new ErrorMessage(
                $"The manifest directory {manifestDir.GetFullPath()} does not contain any .item files");
            yield break;
        }

        foreach (var itemFile in itemFiles)
        {
            yield return DeserializeGame(itemFile);
        }
    }

    private OneOf<EGSGame, ErrorMessage> DeserializeGame(AbsolutePath itemFile)
    {
        using var stream = _fileSystem.ReadFile(itemFile);

        try
        {
            var manifest = JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.ManifestFile);

            if (manifest is null)
            {
                return new ErrorMessage($"Unable to deserialize file {itemFile.GetFullPath()}");
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (manifest.CatalogItemId is null)
            {
                return new ErrorMessage($"Manifest {itemFile.GetFullPath()} does not have a value \"CatalogItemId\"");
            }

            if (manifest.AppName is null)
            {
                return new ErrorMessage($"Manifest {itemFile.GetFullPath()} does not have a value \"AppName\"");
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (manifest.DisplayName is null)
            {
                return new ErrorMessage($"Manifest {itemFile.GetFullPath()} does not have a value \"DisplayName\"");
            }

            if (string.IsNullOrEmpty(manifest.InstallLocation))
            {
                return new ErrorMessage($"Manifest {itemFile.GetFullPath()} does not have a value \"InstallLocation\"");
            }

            var game = new EGSGame(
                EGSGameId.From(manifest.CatalogItemId),
                manifest.AppName,
                manifest.DisplayName,
                _fileSystem.FromUnsanitizedFullPath(manifest.InstallLocation),
                manifest.bIsApplication
            );

            return game;
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Unable to deserialize file {itemFile.GetFullPath()}");
        }
    }

    private AbsolutePath GetManifestDir()
    {
        return TryGetManifestDirFromRegistry(out var manifestDir)
            ? manifestDir
            : GetDefaultManifestsPath(_fileSystem);
    }

    internal static AbsolutePath GetDefaultManifestsPath(IFileSystem fileSystem)
    {
        return fileSystem
            .GetKnownPath(KnownPath.CommonApplicationDataDirectory)
            .Combine("Epic/EpicGamesLauncher/Data/Manifests");
    }

    private bool TryGetManifestDirFromRegistry(out AbsolutePath manifestDir)
    {
        manifestDir = default;

        try
        {
            var currentUser = _registry.OpenBaseKey(RegistryHive.CurrentUser);
            using var regKey = currentUser.OpenSubKey(RegKey);

            if (regKey is null || !regKey.TryGetString("ModSdkMetadataDir",
                    out var registryMetadataDir)) return false;

            manifestDir = _fileSystem.FromUnsanitizedFullPath(registryMetadataDir);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
