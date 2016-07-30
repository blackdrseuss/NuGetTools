﻿using System;

namespace Knapcode.NuGetTools.Logic.Wrappers.Reflection
{
    public class ReflectionVersion : MarshalByRefObject, IVersion
    {
        private readonly VersionApi _api;

        public ReflectionVersion(object nuGetVersion, VersionApi api)
        {
            NuGetVersion = nuGetVersion;
            _api = api;
        }

        public object NuGetVersion { get; }
        public int Revision => _api.GetRevision(NuGetVersion);
        public bool IsSemVer2 => _api.IsSemVer2(NuGetVersion);
        public bool IsPrerelease => _api.IsPrerelease(NuGetVersion);
        public string NormalizedString => _api.GetNormalizedString(NuGetVersion);
        public string FullString => _api.GetFullString(NuGetVersion);
    }
}
