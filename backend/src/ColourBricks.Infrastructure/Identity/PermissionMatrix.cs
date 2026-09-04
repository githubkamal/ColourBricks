using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColourBricks.Infrastructure.Identity;

/// <summary>
/// The checked-in permission matrix (<c>Identity/Permissions/permission-matrix.json</c>),
/// loaded from the embedded resource. Both the seeder and the seed test read this so
/// the seed is verified against an independent source (P0-T05).
/// </summary>
public sealed class PermissionMatrix
{
    private const string WildcardAll = "*";

    [JsonPropertyName("actions")]
    public required IReadOnlyList<string> Actions { get; init; }

    [JsonPropertyName("modules")]
    public required IReadOnlyList<ModuleDefinition> Modules { get; init; }

    [JsonPropertyName("roles")]
    public required IReadOnlyList<RoleDefinition> Roles { get; init; }

    /// <summary>The full catalogue: every module crossed with every action (plan.md §9).</summary>
    public IReadOnlyList<string> AllPermissionKeys =>
        Modules.SelectMany(m => Actions.Select(a => $"{m.Key}.{a}")).ToArray();

    public static PermissionMatrix Load()
    {
        Assembly assembly = typeof(PermissionMatrix).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith("permission-matrix.json", StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        PermissionMatrix? matrix = JsonSerializer.Deserialize<PermissionMatrix>(stream, JsonOptions);
        return matrix ?? throw new InvalidOperationException("permission-matrix.json failed to parse.");
    }

    /// <summary>The set of <c>module.action</c> keys a role grants, expanding <c>"*"</c>.</summary>
    public IReadOnlySet<string> KeysFor(RoleDefinition role)
    {
        if (role.RawPermissions.ValueKind == JsonValueKind.String
            && role.RawPermissions.GetString() == WildcardAll)
        {
            return AllPermissionKeys.ToHashSet(StringComparer.Ordinal);
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (ModulePermission grant in role.RawPermissions.Deserialize<List<ModulePermission>>(JsonOptions)!)
        {
            foreach (string action in grant.Actions)
            {
                keys.Add($"{grant.Module}.{action}");
            }
        }

        return keys;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public sealed class ModuleDefinition
    {
        [JsonPropertyName("key")] public required string Key { get; init; }
        [JsonPropertyName("name")] public required string Name { get; init; }
    }

    public sealed class RoleDefinition
    {
        [JsonPropertyName("name")] public required string Name { get; init; }
        [JsonPropertyName("description")] public string? Description { get; init; }
        [JsonPropertyName("isSystem")] public bool IsSystem { get; init; }
        [JsonPropertyName("permissions")] public JsonElement RawPermissions { get; init; }
    }

    private sealed class ModulePermission
    {
        [JsonPropertyName("module")] public required string Module { get; init; }
        [JsonPropertyName("actions")] public required IReadOnlyList<string> Actions { get; init; }
    }
}
