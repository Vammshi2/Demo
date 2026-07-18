using HostelPro.Data;
using HostelPro.Models;
using Microsoft.EntityFrameworkCore;

namespace HostelPro.Services;

public interface IHostelSettingsReader
{
    Task<HostelSetting> GetSettingsAsync(CancellationToken cancellationToken = default);
}

public sealed class HostelSettingsReader(IDbContextFactory<ApplicationDbContext> dbFactory) : IHostelSettingsReader
{
    public async Task<HostelSetting> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.HostelSettings
            .AsNoTracking()
            .OrderBy(setting => setting.Id)
            .FirstAsync(cancellationToken);
    }
}
