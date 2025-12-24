using System;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace MOCHA.Data;

/// <summary>
/// プロバイダー固有の例外を吸収してデータベースエラー種別を判定するヘルパー
/// </summary>
internal static class DatabaseErrorDetector
{
    private const string _sqliteExceptionType = "Microsoft.Data.Sqlite.SqliteException";
    private const string _postgresExceptionType = "Npgsql.PostgresException";
    private const string _postgresUndefinedTableCode = "42P01";

    /// <summary>
    /// 例外が対象テーブルの欠如に起因するかどうかを判定する
    /// </summary>
    /// <param name="exception">例外</param>
    /// <param name="tableName">テーブル名</param>
    /// <returns>欠如エラーなら true</returns>
    public static bool IsMissingTable(Exception exception, string tableName)
    {
        if (exception is DbUpdateException updateEx && updateEx.InnerException is DbException innerDb)
        {
            return IsMissingTable(innerDb, tableName);
        }

        if (exception is DbException dbException)
        {
            return IsMissingTable(dbException, tableName);
        }

        return false;
    }

    private static bool IsMissingTable(DbException exception, string tableName)
    {
        var typeName = exception.GetType().FullName;
        if (string.Equals(typeName, _sqliteExceptionType, StringComparison.Ordinal))
        {
            var errorCode = GetIntProperty(exception, "SqliteErrorCode");
            if (errorCode == 1)
            {
                return exception.Message.Contains(tableName, StringComparison.OrdinalIgnoreCase);
            }
        }

        if (string.Equals(typeName, _postgresExceptionType, StringComparison.Ordinal))
        {
            var sqlState = GetStringProperty(exception, "SqlState");
            if (string.Equals(sqlState, _postgresUndefinedTableCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int? GetIntProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName);
        if (property is null)
        {
            return null;
        }

        var value = property.GetValue(instance);
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            _ => null
        };
    }

    private static string? GetStringProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName);
        return property?.GetValue(instance) as string;
    }
}
