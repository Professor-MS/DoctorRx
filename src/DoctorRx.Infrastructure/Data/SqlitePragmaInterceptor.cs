using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DoctorRx.Infrastructure.Data;

public class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private const string PragmaCommands =
        "PRAGMA journal_mode=WAL;" +
        "PRAGMA foreign_keys=ON;" +
        "PRAGMA busy_timeout=5000;" +
        "PRAGMA synchronous=FULL;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = PragmaCommands;
        command.ExecuteNonQuery();
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = PragmaCommands;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
