﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace Knapcode.NuGetTools.Logic.Direct
{
    public interface IPackageRangeDownloader
    {
        Task<IEnumerable<PackageIdentity>> GetDownloadedVersionsAsync(
            string id,
            SourceCacheContext sourceCacheContext,
            ILogger log,
            CancellationToken token);

#if NETCOREAPP
        Task DownloadPackagesAsync(
            IEnumerable<string> sources,
            IEnumerable<PackageIdentity> packageIdentities,
            SourceCacheContext sourceCacheContext,
            ILogger log,
            CancellationToken token);

        Task<IEnumerable<PackageIdentity>> GetAvailableVersionsAsync(
            IEnumerable<string> sources,
            string id, 
            ILogger log,
            CancellationToken token);
#endif
    }
}