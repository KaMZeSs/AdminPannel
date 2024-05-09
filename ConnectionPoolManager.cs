using Npgsql;

using System;

namespace AdminPannel
{
    public sealed class NpgsqlConnectionManager : IDisposable
    {
        private static readonly Lazy<NpgsqlConnectionManager> lazy = new Lazy<NpgsqlConnectionManager>(() => new NpgsqlConnectionManager());
        private static readonly string connectionString;
        private static readonly Queue<NpgsqlConnection> connectionPool = new Queue<NpgsqlConnection>();
        private static readonly object lockObject = new object();

        public static NpgsqlConnectionManager Instance { get { return lazy.Value; } }

        static NpgsqlConnectionManager()
        {
            var connBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = Environment.GetEnvironmentVariable("host"),
                Database = Environment.GetEnvironmentVariable("database"),
                Username = Environment.GetEnvironmentVariable("user"),
                Password = Environment.GetEnvironmentVariable("password"),
                Pooling = true,
                MaxPoolSize = 10
            };

            connectionString = connBuilder.ConnectionString;
        }

        private NpgsqlConnectionManager() { }

        public NpgsqlConnection GetConnection()
        {
            lock (lockObject)
            {
                if (connectionPool.Count > 0)
                {
                    var connection = connectionPool.Dequeue();
                    if (connection.State == System.Data.ConnectionState.Closed)
                        connection.Open();
                    return connection;
                }
                else
                {
                    var connection = new NpgsqlConnection(connectionString);
                    connection.Open();
                    return connection;
                }
            }
        }

        public void ReleaseConnection(NpgsqlConnection connection)
        {
            lock (lockObject)
            {
                connectionPool.Enqueue(connection);
            }
        }

        public void Dispose()
        {
            lock (lockObject)
            {
                while (connectionPool.Count > 0)
                {
                    var connection = connectionPool.Dequeue();
                    connection.Close(); // Закрываем соединение
                    connection.Dispose(); // Освобождаем ресурсы
                }
            }
        }

    }
}