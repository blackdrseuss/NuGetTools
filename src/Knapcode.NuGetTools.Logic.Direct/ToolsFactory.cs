﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.NuGetTools.Logic.Wrappers.Reflection;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Version = Knapcode.NuGetTools.Logic.Wrappers.Reflection.Version;
using VersionRange = Knapcode.NuGetTools.Logic.Wrappers.Reflection.VersionRange;

namespace Knapcode.NuGetTools.Logic.Direct
{
    public class ToolsFactory : IToolsFactory
    {
        private static NuGetFramework _framework = NuGetFramework.Parse("net472");

        private readonly IAlignedVersionsDownloader _downloader;
        private readonly IPackageLoader _packageLoader;
        private readonly IFrameworkList _frameworkList;

        private readonly Lazy<Task<Dictionary<string, NuGetVersion>>> _versions;
        private readonly Lazy<Task<List<string>>> _versionStrings;
        private readonly Lazy<Task<string>> _latestVersion;
        private readonly Lazy<Task<Dictionary<NuGetVersion, NuGetRelease>>> _releases;

        private readonly ConcurrentDictionary<NuGetVersion, Task<Logic>> _logic
            = new ConcurrentDictionary<NuGetVersion, Task<Logic>>();

        private readonly ConcurrentDictionary<NuGetVersion, Task<IToolsService>> _toolServices
            = new ConcurrentDictionary<NuGetVersion, Task<IToolsService>>();

        private readonly ConcurrentDictionary<NuGetVersion, Task<IFrameworkPrecedenceService>> _frameworkPrecendenceServices
            = new ConcurrentDictionary<NuGetVersion, Task<IFrameworkPrecedenceService>>();
        private readonly MicrosoftLogger _nuGetLog;

        public ToolsFactory(IPackageLoader packageLoader, IAlignedVersionsDownloader downloader, IFrameworkList frameworkList, ILogger<ToolsFactory> log)
        {
            _packageLoader = packageLoader;
            _downloader = downloader;
            _frameworkList = frameworkList;
            _nuGetLog = new MicrosoftLogger(log);

            _releases = new Lazy<Task<Dictionary<NuGetVersion, NuGetRelease>>>(async () =>
            {
                using (var sourceCacheContext = new SourceCacheContext())
                {
                    var versions2x = await _downloader.GetDownloadedVersionsAsync(
                        Constants.PackageIds2x,
                        sourceCacheContext,
                        _nuGetLog,
                        CancellationToken.None);
                    var pairs2x = versions2x
                        .Select(x => new KeyValuePair<NuGetVersion, NuGetRelease>(x, NuGetRelease.Version2x));

                    var versions3x = await _downloader.GetDownloadedVersionsAsync(
                        Constants.PackageIds3x,
                        sourceCacheContext,
                        _nuGetLog,
                        CancellationToken.None);
                    var pairs3x = versions3x
                        .Select(x => new KeyValuePair<NuGetVersion, NuGetRelease>(x, NuGetRelease.Version3x));

                    return pairs2x
                        .Concat(pairs3x)
                        .ToDictionary(x => x.Key, x => x.Value);
                }
            });

            _versions = new Lazy<Task<Dictionary<string, NuGetVersion>>>(async () =>
            {
                var releases = await _releases.Value;

                return releases
                    .ToDictionary(
                        x => x.Key.ToNormalizedString(),
                        x => x.Key,
                        StringComparer.OrdinalIgnoreCase);
            });

            _versionStrings = new Lazy<Task<List<string>>>(async () =>
            {
                var versions = await _versions.Value;

                return versions
                    .OrderByDescending(x => x.Value)
                    .Select(x => x.Key)
                    .ToList();
            });

            _latestVersion = new Lazy<Task<string>>(async () =>
            {
                var versions = await _versions.Value;

                return versions
                    .OrderByDescending(x => x.Value)
                    .First()
                    .Key;
            });
        }

        public async Task<IEnumerable<string>> GetAvailableVersionsAsync(CancellationToken token)
        {
            return await _versionStrings.Value;
        }

        public async Task<IToolsService> GetServiceAsync(string version, CancellationToken token)
        {
            var matchingVersion = await GetMatchingVersionAsync(version);

            if (matchingVersion == null)
            {
                return null;
            }

            try
            {
                return await _toolServices.GetOrAdd(
                    matchingVersion,
                    async key =>
                    {
                        var logic = await GetLogicAsync(key);

                        return new ToolsService<Framework, Version, VersionRange>(
                            version,
                            logic.Framework,
                            logic.Version,
                            logic.VersionRange);
                    });
            }
            catch
            {
                _toolServices.TryRemove(matchingVersion, out var _);
                throw;
            }
        }

        public async Task<IFrameworkPrecedenceService> GetFrameworkPrecedenceServiceAsync(string version, CancellationToken token)
        {
            var matchingVersion = await GetMatchingVersionAsync(version);

            if (matchingVersion == null)
            {
                return null;
            }
            
            try
            {
                return await _frameworkPrecendenceServices.GetOrAdd(
                    matchingVersion,
                    async key =>
                    {
                        var logic = await GetLogicAsync(key);

                        return new FrameworkPrecedenceService<Framework>(
                            version,
                            _frameworkList,
                            logic.Framework);
                    });
            }
            catch
            {
                _frameworkPrecendenceServices.TryRemove(matchingVersion, out var _);
                throw;
            }
        }

        public Task<IFrameworkList> GetFrameworkListAsync(CancellationToken token)
        {
            return Task.FromResult(_frameworkList);
        }

        public Task<string> GetLatestVersionAsync(CancellationToken token)
        {
            return _latestVersion.Value;
        }

        private async Task<Logic> GetLogicAsync(NuGetVersion version)
        {
            try
            {
                return await _logic.GetOrAdd(version, GetLogicWithoutCachingAsync);
            }
            catch
            {
                _logic.TryRemove(version, out var _);
                throw;
            }
        }

        private async Task<NuGetVersion> GetMatchingVersionAsync(string version)
        {
            var versions = await _versions.Value;
            NuGetVersion matchedVersion;
            if (!versions.TryGetValue(version, out matchedVersion))
            {
                return null;
            }

            return matchedVersion;
        }

        private async Task<Logic> GetLogicWithoutCachingAsync(NuGetVersion version)
        {
            var releases = await _releases.Value;
            NuGetRelease release;
            if (!releases.TryGetValue(version, out release))
            {
                throw new ArgumentException($"The provided version '{version}' is not supported");
            }

            LoadedAssemblies loadedAssemblies;
            switch (release)
            {
                case NuGetRelease.Version2x:
                    loadedAssemblies = LoadV2Assemblies(version);
                    break;

                case NuGetRelease.Version3x:
                    loadedAssemblies = LoadV3Assemblies(version);
                    break;

                default:
                    throw new NotImplementedException();
            }
            
            return InitializeLogic(release, loadedAssemblies);
        }

        private LoadedAssemblies LoadV2Assemblies(NuGetVersion version)
        {
            var coreIdentity = new PackageIdentity(Constants.CoreId, version);
            var context = _packageLoader.LoadPackageAssemblies(
                version.ToNormalizedString(),
                _framework,
                coreIdentity);
            
            var coreAssemblyName = context.LoadedAssemblies.GetByName(Constants.CoreId);

            var loaded = new LoadedAssemblies
            {
                Context = context,
                Framework = coreAssemblyName,
                Version = coreAssemblyName,
                VersionRange = coreAssemblyName,
            };
            return loaded;
        }

        private LoadedAssemblies LoadV3Assemblies(NuGetVersion version)
        {
            var versioningIdentity = new PackageIdentity(Constants.VersioningId, version);
            _packageLoader.LoadPackageAssemblies(
                version.ToNormalizedString(),
                _framework,
                versioningIdentity);

            var frameworksIdentity = new PackageIdentity(Constants.FrameworksId, version);
            var context = _packageLoader.LoadPackageAssemblies(
                version.ToNormalizedString(),
                _framework,
                frameworksIdentity);
            
            var versioningAssemblyName = context.LoadedAssemblies.GetByName(Constants.VersioningId);
            var frameworksAssemblyName = context.LoadedAssemblies.GetByName(Constants.FrameworksId);

            var loaded = new LoadedAssemblies
            {
                Context = context,
                Framework = frameworksAssemblyName,
                Version = versioningAssemblyName,
                VersionRange = versioningAssemblyName,
            };
            return loaded;
        }

        private static Logic InitializeLogic(NuGetRelease release, LoadedAssemblies loaded)
        {
            var frameworkLogic = loaded.Context.Proxy.GetFrameworkLogic(release, loaded.Framework);
            var versionLogic = loaded.Context.Proxy.GetVersionLogic(release, loaded.Version);
            var versionRangeLogic = loaded.Context.Proxy.GetVersionRangeLogic(release, loaded.Version, loaded.VersionRange);

            return new Logic
            {
                Framework = frameworkLogic,
                Version = versionLogic,
                VersionRange = versionRangeLogic
            };
        }

        private class Logic
        {
            public FrameworkLogic Framework { get; set; }
            public VersionLogic Version { get; set; }
            public VersionRangeLogic VersionRange { get; set; }
        }

        private class LoadedAssemblies
        {
            public AppDomainContext Context { get; set; }
            public AssemblyName Framework { get; set; }
            public AssemblyName Version { get; set; }
            public AssemblyName VersionRange { get; set; }
        }
    }
}
