namespace EventSub
{
    enum DatabaseType
    {
        MySql,
        SqlServer,
        PostgreSql
    }

    public class Database
    {
        Database(DatabaseType databaseType, string connectionString)
        {
            this.Type = databaseType;
            this.ConnectionString = connectionString;
        }

        internal string ConnectionString { get; }
        internal DatabaseType Type { get; }

        public static Database MySql(string connectionString)
        {
            return new Database(DatabaseType.MySql, connectionString);
        }

        public static Database SqlServer(string connectionString)
        {
            return new Database(DatabaseType.SqlServer, connectionString);
        }

        public static Database PostgreSql(string connectionString)
        {
            return new Database(DatabaseType.PostgreSql, connectionString);
        }
    }

}