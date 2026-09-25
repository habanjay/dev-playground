// -----------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------------------

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.VSDiagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        private readonly Consumer consumer = new();
        private JsonSerializerOptions systemTextJsonOptions;
        private JsonSerializerSettings newtonsoftJsonSettings;
        private SerializationPayload payload;
        private string json;

        [GlobalSetup]
        public void Setup()
        {
            data = new byte[10000];
            new Random(42).NextBytes(data);
            httpClient = new HttpClient
            {
                BaseAddress = new Uri(Environment.GetEnvironmentVariable("PLAYGROUND_API_BASE_URL") ?? "http://localhost:5292")
            };

            systemTextJsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            newtonsoftJsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Include
            };
            payload = CreatePayload();
            json = System.Text.Json.JsonSerializer.Serialize(payload, systemTextJsonOptions);

            var newtonsoftJson = JsonConvert.SerializeObject(payload, newtonsoftJsonSettings);
            if (!JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(newtonsoftJson)))
            {
                throw new InvalidOperationException("The serializers do not produce the same JSON schema.");
            }

            ValidatePayload(System.Text.Json.JsonSerializer.Deserialize<SerializationPayload>(json, systemTextJsonOptions));
            ValidatePayload(JsonConvert.DeserializeObject<SerializationPayload>(json, newtonsoftJsonSettings));
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

        [Benchmark(Baseline = true)]
        public string SystemTextJsonSerialize()
        {
            var result = System.Text.Json.JsonSerializer.Serialize(payload, systemTextJsonOptions);
            consumer.Consume(result);
            return result;
        }

        [Benchmark]
        public string NewtonsoftJsonSerialize()
        {
            var result = JsonConvert.SerializeObject(payload, newtonsoftJsonSettings);
            consumer.Consume(result);
            return result;
        }

        [Benchmark]
        public SerializationPayload SystemTextJsonDeserialize()
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<SerializationPayload>(json, systemTextJsonOptions)!;
            consumer.Consume(result);
            return result;
        }

        [Benchmark]
        public SerializationPayload NewtonsoftJsonDeserialize()
        {
            var result = JsonConvert.DeserializeObject<SerializationPayload>(json, newtonsoftJsonSettings)!;
            consumer.Consume(result);
            return result;
        }

        private static SerializationPayload CreatePayload() => new()
        {
            Id = Guid.Parse("b6c2a25d-1de5-4c87-9f48-5a4a78bbd17a"),
            Name = "Benchmark payload",
            Enabled = true,
            Metadata = new PayloadMetadata
            {
                CreatedUtc = new DateTime(2026, 1, 15, 12, 30, 0, DateTimeKind.Utc),
                Tags = ["benchmark", "json", "net10"]
            },
            Items =
            [
                new PayloadItem { Code = "A-100", Quantity = 3, Price = 19.95m },
                new PayloadItem { Code = "B-200", Quantity = 7, Price = 4.50m }
            ]
        };

        private static void ValidatePayload(SerializationPayload? value)
        {
            if (value is null ||
                value.Id != Guid.Parse("b6c2a25d-1de5-4c87-9f48-5a4a78bbd17a") ||
                value.Name != "Benchmark payload" ||
                !value.Enabled ||
                value.Metadata?.CreatedUtc != new DateTime(2026, 1, 15, 12, 30, 0, DateTimeKind.Utc) ||
                value.Metadata.Tags.Count != 3 ||
                value.Items.Count != 2 ||
                value.Items[0].Code != "A-100" ||
                value.Items[1].Price != 4.50m)
            {
                throw new InvalidOperationException("A serializer did not restore the expected payload.");
            }
        }

        public sealed class SerializationPayload
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public bool Enabled { get; set; }
            public PayloadMetadata Metadata { get; set; }
            public List<PayloadItem> Items { get; set; }
        }

        public sealed class PayloadMetadata
        {
            public DateTime CreatedUtc { get; set; }
            public List<string> Tags { get; set; }
        }

        public sealed class PayloadItem
        {
            public string Code { get; set; }
            public int Quantity { get; set; }
            public decimal Price { get; set; }
        }
    }
}
