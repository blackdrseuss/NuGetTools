﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AngleSharp.Extensions;
using NuGet.Versioning;
using Xunit;

namespace Knapcode.NuGetTools.Website.Tests
{
    public class IntegrationTest : IClassFixture<TestServerFixture>
    {
        public static readonly List<NuGetVersion> AvailableVersions;
        public static readonly TheoryData<NuGetVersion> AvailableVersionData;

        static IntegrationTest()
        {
            var baseUrl = Environment.GetEnvironmentVariable("NUGETTOOLS_BASE_URL");
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsedBaseAddress))
            {
                Console.WriteLine("Using base URL for integration tests: " + parsedBaseAddress.AbsoluteUri);
                TestServerFixture.BaseAddress = parsedBaseAddress;
            }
            else
            {
                Console.WriteLine("Using in-memory server for integration tests.");
            }

            using (var tsf = new TestServerFixture())
            {
                AvailableVersions = tsf
                    .GetAvailableVersionsAsync()
                    .Result
                    .OrderBy(x => x)
                    .ToList();
            }

            // If we're testing a real URL, limit the test versions to the latest per major version.
            if (parsedBaseAddress != null)
            {
                AvailableVersions = AvailableVersions
                    .GroupBy(x => x.Major)
                    .Select(g => g.Max())
                    .OrderBy(x => x)
                    .ToList();
            }

            AvailableVersionData = new TheoryData<NuGetVersion>();
            foreach (var version in AvailableVersions)
            {
                AvailableVersionData.Add(version);
            }
        }
        
        private readonly TestServerFixture f;

        public IntegrationTest(TestServerFixture testServerFixture)
        {
            f = testServerFixture;
        }
        
        [Theory]
        [MemberData(nameof(AvailableVersionData))]
        public async Task ParseFramework(NuGetVersion version)
        {
            // Arrange
            var requestUri = $"/{version}/parse-framework?framework=net45";

            // Act
            using (var response = await f.Client.GetAsync(requestUri))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var text = await f.GetFlattenedTextAsync(response);
                Assert.Contains("The input value is net45.", text);
                Assert.Contains("The short folder name is net45.", text);
                Assert.Contains("The .NET framework name is .NETFramework,Version=v4.5.", text);
            }
        }

        [Theory]
        [MemberData(nameof(AvailableVersionData))]
        public async Task FrameworkCompatibility(NuGetVersion version)
        {
            // Arrange
            var requestUri = $"/{version}/framework-compatibility?project=net46&package=net45";

            // Act
            using (var response = await f.Client.GetAsync(requestUri))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var text = await f.GetFlattenedTextAsync(response);
                Assert.Contains("net46 (net46) projects support net45 (net45) packages.", text);
            }
        }

        [Theory]
        [MemberData(nameof(AvailableVersionData))]
        public async Task GetNearestFramework(NuGetVersion version)
        {
            // Arrange
            var requestUri = $"/{version}/get-nearest-framework?project=net46&package=net40%0D%0Anet45%0D%0Anet461";

            // Act
            using (var response = await f.Client.GetAsync(requestUri))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var text = await f.GetFlattenedTextAsync(response);
                Assert.Contains("The net45 (net45) package framework is the nearest to the net46 (net46) project framework.", text);
            }
        }

        [Theory]
        [MemberData(nameof(AvailableVersionData))]
        public async Task FrameworkPrecedence(NuGetVersion version)
        {
            // Arrange
            var requestUri = $"/{version}/framework-precedence?framework=net45";

            // Act
            using (var response = await f.Client.GetAsync(requestUri))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var text = await f.GetFlattenedTextAsync(response);
                Assert.Contains("The .NETFramework,Version=v4.5 (net45) project framework has the following package framework precedence list.", text);
            }
        }

        [Theory]
        [MemberData(nameof(AvailableVersionData))]
        public async Task ParseVersion(NuGetVersion version)
        {
            // Arrange
            var requestUri = $"/{version}/parse-version?version=1.0.0-beta01";

            // Act
            using (var response = await f.Client.GetAsync(requestUri))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var text = await f.GetFlattenedTextAsync(response);
                Assert.Contains("The result of ToString() is 1.0.0-beta01.", text);
            }
        }

        [Theory]
        [MemberData(nameof(AvailableVersionData))]
        public async Task VersionComparison(NuGetVersion version)
        {
            // Arrange
            var requestUri = $"/{version}/version-comparison?versionA=1.0.0-beta&versionB=2.0.0";

            // Act
            using (var response = await f.Client.GetAsync(requestUri))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var text = await f.GetFlattenedTextAsync(response);
                Assert.Contains("1.0.0-beta (1.0.0-beta) < 2.0.0 (2.0.0).", text);
            }
        }

        [Theory]
        [MemberData(nameof(AvailableVersionData))]
        public async Task SortVersions(NuGetVersion version)
        {
            // Arrange
            var requestUri = $"/{version}/sort-versions?versions=10.2.0-beta%0D%0A0.9.0%0D%0A2.0.0";

            // Act
            using (var response = await f.Client.GetAsync(requestUri))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var text = await f.GetFlattenedTextAsync(response);
                Assert.Contains("0.9.0 (0.9.0) 2.0.0 (2.0.0) 10.2.0-beta (10.2.0-beta)", text);
            }
        }

        [Theory]
        [MemberData(nameof(AvailableVersionData))]
        public async Task ParseVersionRange(NuGetVersion version)
        {
            // Arrange
            var requestUri = $"/{version}/parse-version-range?versionRange=%5B1.0.0%2C+2.0.0%5D";

            // Act
            using (var response = await f.Client.GetAsync(requestUri))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var text = await f.GetFlattenedTextAsync(response);
                Assert.Contains("The normalized version range is [1.0.0, 2.0.0].", text);
            }
        }

        [Theory]
        [MemberData(nameof(AvailableVersionData))]
        public async Task ParseStarDashStarVersionRange(NuGetVersion version)
        {
            // Arrange
            var requestUri = $"/{version}/parse-version-range?versionRange=1.0.%2A-%2A";

            // Act
            using (var response = await f.Client.GetAsync(requestUri))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var text = await f.GetFlattenedTextAsync(response);
                if (version >= NuGetVersion.Parse("5.6.0-preview.3.6558"))
                {
                    Assert.Contains("The normalized version range is [1.0.*-*, ).", text);
                }
                else
                {
                    Assert.Contains("The version range 1.0.*-* could not be parsed.", text);
                }
            }
        }

        [Theory]
        [MemberData(nameof(AvailableVersionData))]
        public async Task VersionSatisfies(NuGetVersion version)
        {
            // Arrange
            var requestUri = $"/{version}/version-satisfies?versionRange=%5B1.0.0%2C+2.0.0%5D&version=1.5.0";

            // Act
            using (var response = await f.Client.GetAsync(requestUri))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var text = await f.GetFlattenedTextAsync(response);
                Assert.Contains("1.5.0 (1.5.0) satisfies [1.0.0, 2.0.0] ([1.0.0, 2.0.0]).", text);
            }
        }

        [Theory]
        [MemberData(nameof(AvailableVersionData))]
        public async Task FindBestVersionMatch(NuGetVersion version)
        {
            // Arrange
            var requestUri = $"/{version}/find-best-version-match?versionRange=%5B1.0.0%2C+2.0.0%5D&versions=0.9.0%0D%0A1.5.0%0D%0A2.1.0";

            // Act
            using (var response = await f.Client.GetAsync(requestUri))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var text = await f.GetFlattenedTextAsync(response);
                if (version.Major >= 3)
                {
                    Assert.Contains("The 1.5.0 (1.5.0) version is the best match to the [1.0.0, 2.0.0] ([1.0.0, 2.0.0]) version range.", text);
                }
                else
                {
                    Assert.Contains("Finding the best version match is only supported in NuGet 3.x and greater.", text);
                }
            }
        }

        [Fact]
        public async Task RootRedirectsToLatestVersion()
        {
            // Arrange
            var maxVersion = AvailableVersions.Max();

            // Act
            using (var response = await f.Client.GetAsync("/"))
            {
                // Assert
                Assert.Equal(HttpStatusCode.Found, response.StatusCode);
                Assert.EndsWith($"/{maxVersion}", response.Headers.Location.ToString());
            }
        }

        [Fact]
        public async Task LatestPlaceholderRedirectToLatest()
        {
            // Arrange
            var maxVersion = AvailableVersions.Max();

            // Act
            using (var response = await f.Client.GetAsync("/latest/parse-version?version=1.0.0-beta01"))
            {
                // Assert
                Assert.Equal(HttpStatusCode.Found, response.StatusCode);
                Assert.EndsWith($"/{maxVersion}/parse-version?version=1.0.0-beta01", response.Headers.Location.ToString());
            }
        }
    }
}
