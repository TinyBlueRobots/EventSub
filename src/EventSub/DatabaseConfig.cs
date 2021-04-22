namespace EventSub
{
    public class DatabaseConfig
    {
        internal DatabaseConfig(string connectionString)
        {
            this.ConnectionString = connectionString;
        }

        public string ConnectionString { get; }
    }

    public class MySqlConfig : DatabaseConfig
    {
        public MySqlConfig(string connectionString) : base(connectionString) { }
    }
}