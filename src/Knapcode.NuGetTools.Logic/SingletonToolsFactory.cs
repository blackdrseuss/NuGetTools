﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.NuGetTools.Logic
{
    public class SingletonToolsFactory : IToolsFactory
    {
        private readonly string _version;
        private readonly IToolsService _toolsService;
        private readonly IFrameworkPrecedenceService _frameworkPrecendenceService;
        private readonly IFrameworkList _frameworkList;

        public SingletonToolsFactory(
            IToolsService toolsService,
            IFrameworkPrecedenceService frameworkPrecendenceService,
            IFrameworkList frameworkList)
        {
            _version = toolsService.Version;
            _toolsService = toolsService;
            _frameworkPrecendenceService = frameworkPrecendenceService;
            _frameworkList = frameworkList;
        }

        public IEnumerable<string> GetAvailableVersions()
        {
            return new[] { _version };
        }

        public IToolsService GetService(string version)
        {
            if (version != _version)
            {
                return null;
            }

            return _toolsService;
        }

        public Task<IEnumerable<string>> GetAvailableVersionsAsync(CancellationToken token)
        {
            var versions = new[] { _version };

            return Task.FromResult<IEnumerable<string>>(versions);
        }

        public Task<IToolsService> GetServiceAsync(string version, CancellationToken token)
        {
            IToolsService output = null;

            if (version == _version)
            {
                output = _toolsService;
            }

            return Task.FromResult(output);
        }

        public Task<IFrameworkPrecedenceService> GetFrameworkPrecedenceServiceAsync(string version, CancellationToken token)
        {
            IFrameworkPrecedenceService output = null;

            if (version == _version)
            {
                output = _frameworkPrecendenceService;
            }

            return Task.FromResult(output);
        }

        public Task<IFrameworkList> GetFrameworkListAsync(CancellationToken token)
        {
            return Task.FromResult(_frameworkList);
        }

        public Task<string> GetLatestVersionAsync(CancellationToken token)
        {
            return Task.FromResult(_version);
        }
    }
}
