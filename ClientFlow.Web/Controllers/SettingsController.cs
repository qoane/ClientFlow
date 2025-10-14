using System.Linq;
using System.Text.Json;
using ClientFlow.Domain.Settings;
using ClientFlow.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace ClientFlow.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public SettingsController(AppDbContext db) => _db = db;

    // Use a single JsonSerializerOptions instance with case‑insensitive property names
    // so incoming JSON payloads can use "key" and "value" in any casing.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public sealed class SettingDto
    {
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<Setting>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Setting>>> GetAll(CancellationToken ct)
    {
        var all = await _db.Settings.AsNoTracking().OrderBy(s => s.Key).ToListAsync(ct);
        return Ok(all);
    }

    [HttpGet("{key}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Setting), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Setting>> Get(string key, CancellationToken ct)
    {
        var k = (key ?? string.Empty).Trim();
        if (k.Length == 0) return NotFound();
        // Look up setting using case-insensitive comparison to avoid missing entries
        // when callers use different casing.  Use ToLowerInvariant() so EF can
        // translate it to SQL LOWER() and perform the comparison on the server.
        var klower = k.ToLowerInvariant();
        var setting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key.ToLower() == klower, ct);
        return setting is null ? NotFound() : Ok(setting);
    }

    /// <summary>
    /// Upserts one or more settings. Accepts an object or array of objects:
    /// { "key": "...", "value": "..." }  or  [ { "key": "...", "value": "..." }, ... ]
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(IEnumerable<Setting>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert([FromBody] JsonElement body, CancellationToken ct)
    {
        // ---- Normalize payload to List<SettingDto> ----
        List<SettingDto> items;
        try
        {
            if (body.ValueKind == JsonValueKind.Array)
            {
                // Deserialize using case‑insensitive settings so lowercase "key"/"value" are accepted
                items = JsonSerializer.Deserialize<List<SettingDto>>(body.GetRawText(), JsonOptions) ?? new();
            }
            else if (body.ValueKind == JsonValueKind.Object)
            {
                var single = JsonSerializer.Deserialize<SettingDto>(body.GetRawText(), JsonOptions);
                items = single is null ? new() : new() { single };
            }
            else
            {
                return BadRequest("Body must be a JSON object or array of objects.");
            }
        }
        catch (JsonException)
        {
            return BadRequest("Invalid JSON for settings.");
        }

        if (items.Count == 0) return BadRequest("No settings supplied.");

        // ---- Upsert each setting ----
        foreach (var item in items)
        {
            // Trim key and ignore blank keys
            var key = (item.Key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            // Normalise ReportTime:
            // Only validate when a value is supplied.  Blank values are allowed and
            // will simply clear the setting.  Previously a blank ReportTime caused
            // the entire request to fail because TryParseHourMinute returned false.
            if (string.Equals(key, "ReportTime", StringComparison.OrdinalIgnoreCase))
            {
                var rawValue = item.Value ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(rawValue))
                {
                    if (!TryParseHourMinute(rawValue, out var h, out var m))
                        return BadRequest("ReportTime must be HH:mm or HH:mm:ss (24-hour).");
                    // Normalise to HH:mm
                    item.Value = $"{h:00}:{m:00}";
                }
                // else: leave item.Value as-is (empty string), meaning clear the setting
            }

            // Look up existing setting ignoring case.  Without this, callers using
            // different casing (e.g. "branchname") could cause duplicate keys and
            // violate the unique index on Settings.Key.  ToLower() is translated to
            // SQL LOWER() by EF Core and is safe for small tables.
            var lowerKey = key.ToLowerInvariant();
            var existing = await _db.Settings.FirstOrDefaultAsync(s => s.Key.ToLower() == lowerKey, ct);
            if (existing is null)
            {
                _db.Settings.Add(new Setting { Id = Guid.NewGuid(), Key = key, Value = item.Value ?? string.Empty });
            }
            else
            {
                existing.Value = item.Value ?? string.Empty;
                // Also update the Key to use the casing provided by the caller to
                // ensure consistent casing for subsequent queries
                existing.Key = key;
            }
        }

        await _db.SaveChangesAsync(ct);

        // ---- Return what was saved so the UI can refresh immediately ----
        var keys = items.Select(i => i.Key.Trim()).ToList();
        var saved = await _db.Settings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .OrderBy(s => s.Key)
            .ToListAsync(ct);

        return Ok(saved);
    }

    private static bool TryParseHourMinute(string? value, out int hour, out int minute)
    {
        hour = 0; minute = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value.Trim().Split(':');
        if (parts.Length < 2) return false;

        if (!int.TryParse(parts[0], out hour) || hour < 0 || hour > 23) return false;
        if (!int.TryParse(parts[1], out minute) || minute < 0 || minute > 59) return false;

        return true;
    }
}
