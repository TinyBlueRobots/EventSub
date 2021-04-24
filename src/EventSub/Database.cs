namespace EventSub
{
    enum DatabaseType
    {
        MySql,
        SqlServer,
        Postgresql
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
    }

}