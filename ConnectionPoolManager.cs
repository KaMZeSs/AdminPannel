using Npgsql;

using System;

namespace AdminPannel
{
    public sealed class NpgsqlConnectionManager
    {
        private static readonly Lazy<NpgsqlConnectionManager> lazy = new Lazy<NpgsqlConnectionManager>(() => new NpgsqlConnectionManager());
        private static readonly string connectionString;

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

        public async Task<NpgsqlConnection> GetConnectionAsync()
        {
            var connection = new NpgsqlConnection(connectionString);
            try
            {
                await connection.OpenAsync();
            }
            catch (Exception)
            {

                throw;
            }
            
            return connection;
        }
    }
}