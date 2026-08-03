using System.Text.Json.Serialization;
using ViewMd.Models;

namespace ViewMd.Services;

// Source-generated JSON (de)serialization for NativeAOT/trimming: reflection-based
// System.Text.Json is not trim-safe, so every type we persist must be registered here.
[JsonSerializable(typeof(MruStore))]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
