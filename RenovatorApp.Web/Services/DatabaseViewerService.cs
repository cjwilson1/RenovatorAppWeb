using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Services;

public sealed class DatabaseViewerService
{
    private const int MaxCellLength = 200;
    private static readonly HashSet<string> TablesExcludedFromClear = new(StringComparer.OrdinalIgnoreCase)
    {
        "BuildingType",
        "InspectionAreaCategory",
        "InspectionAreaType"
    };
    private readonly RenovatorAppDbContext _dbContext;

    public DatabaseViewerService(RenovatorAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<string> GetTableNames()
    {
        return _dbContext.Model.GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .Where(tableName => !string.IsNullOrWhiteSpace(tableName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tableName => tableName)
            .ToList()!;
    }

    public async Task<DatabaseTablePageViewModel?> GetTablePageAsync(
        string tableName,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var tableNames = GetTableNames();
        var matchedTableName = tableNames.FirstOrDefault(name => string.Equals(name, tableName, StringComparison.OrdinalIgnoreCase));

        if (matchedTableName is null)
        {
            return null;
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var quotedTableName = QuoteIdentifier(matchedTableName);
        var totalRows = await GetTotalRowsAsync(quotedTableName, cancellationToken);
        var totalPages = totalRows == 0 ? 1 : (int)Math.Ceiling(totalRows / (double)pageSize);
        page = Math.Min(page, totalPages);

        var rows = await GetRowsAsync(quotedTableName, page, pageSize, cancellationToken);

        return new DatabaseTablePageViewModel
        {
            TableNames = tableNames,
            SelectedTableName = matchedTableName,
            Columns = rows.Columns,
            Rows = rows.Values,
            Page = page,
            PageSize = pageSize,
            TotalRows = totalRows,
            TotalPages = totalPages
        };
    }

    public async Task ClearDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var tableNames = GetTableNames()
            .Where(tableName => !TablesExcludedFromClear.Contains(tableName))
            .ToList();

        if (tableNames.Count == 0)
        {
            return;
        }

        var quotedTableNames = string.Join(", ", tableNames.Select(QuoteIdentifier));
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"truncate table {quotedTableNames} restart identity cascade";

        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<int> GetTotalRowsAsync(string quotedTableName, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"select count(*) from {quotedTableName}";

        var count = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(count);
    }

    private async Task<(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Values)> GetRowsAsync(
        string quotedTableName,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"select * from {quotedTableName} limit @pageSize offset @offset";

        AddParameter(command, "pageSize", pageSize);
        AddParameter(command, "offset", (page - 1) * pageSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToList();
        var rows = new List<IReadOnlyList<string>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var values = new List<string>(reader.FieldCount);

            for (var index = 0; index < reader.FieldCount; index++)
            {
                values.Add(FormatCellValue(reader.GetValue(index)));
            }

            rows.Add(values);
        }

        return (columns, rows);
    }

    private static async Task EnsureOpenAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string FormatCellValue(object value)
    {
        if (value is DBNull)
        {
            return string.Empty;
        }

        var formatted = value switch
        {
            byte[] bytes => $"Binary data ({bytes.Length:N0} bytes)",
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            _ => Convert.ToString(value) ?? string.Empty
        };

        if (formatted.Length <= MaxCellLength)
        {
            return formatted;
        }

        return formatted[..MaxCellLength] + "...";
    }
}
