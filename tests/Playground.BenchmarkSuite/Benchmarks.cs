// -----------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------------------

using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Playground.BenchmarkSuite
{
    // For more information on the VS BenchmarkDotNet Diagnosers see https://learn.microsoft.com/visualstudio/profiling/profiling-with-benchmark-dotnet
    [CPUUsageDiagnoser]
    public class Benchmarks
    {
        private SHA256 sha256 = SHA256.Create();
        private byte[] data;
        private HttpClient httpClient;

        [GlobalSetup]
        public void Setup()
        {
            data = new byte[10000];
            new Random(42).NextBytes(data);
            httpClient = new HttpClient
            {
                BaseAddress = new Uri(Environment.GetEnvironmentVariable("PLAYGROUND_API_BASE_URL") ?? "http://localhost:5292")
            };
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            httpClient.Dispose();
            sha256.Dispose();
        }

        [Benchmark]
        public byte[] Sha256()
        {
            return sha256.ComputeHash(data);
        }

        [Benchmark]
        public async Task<byte[]> WeatherForecast()
        {
            using var response = await httpClient.GetAsync("/weatherforecast");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
    }
}
