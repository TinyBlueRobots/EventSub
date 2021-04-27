
[![NuGet Version](http://img.shields.io/nuget/v/EventSub.svg?style=flat)](https://www.nuget.org/packages/EventSub/)

# EventSub
Azure Event Grid/AWS Eventbridge style ASP .NET service that stores messages in a database and forwards them to a subscriber URL. It's built on top of the excellent [Rebus](https://github.com/rebus-org/Rebus) service bus and currently supports MySQL, SQL Server, and PostgreSQL.

## Run docker container
`docker run -e "DATABASE=PostgreSql" -e "APIKEY=myapikey" -e "CONNECTIONSTRING=Server=host.docker.internal;Port=5432;Database=test;User Id=postgres;Password=password;maximum pool size=30" -p 80:80 --name eventsub tinybluerobots/eventsub`

## Run from code
Install from nuget and call `UseEventSub` in your builder

```
Host.CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(webBuilder => webBuilder.UseEventSub(Database.PostgreSql("connectionstring"), "apikey"))
    .Build()
    .Run()
```

There's an example host in `./src/Web`, you will need to set the following environment variables:

- `DATABASE` : The type of database, either `MySql`, `SqlServer`, or `PostgreSql`
- `CONNECTIONSTRING` : Database connection string
- `APIKEY` : Api key provided either by query string value `apikey` or `X-API-KEY` header
- `PORT`: An optional port to run the service on, defaults to 80

`startLocal.sh` shows an example of running the service against a local Docker instance of Postgres.\
`docker.sh` shows how to start an instance of each supported DB.

## Create a subscriber
`POST` to `/subscribers`
```
{ "name": "mysubscribername",
  "types": ["mymessagetype"],
  "url":"https://myeventhandler.com",
  "retryIntervals": [10, 20, 40],
  "maxParallelism": 1,
  "numberOfWorkers": 1 }
  ```

`name` : lower case alphanumeric name\
`types` : message types that the subscriber will accept, effectively a list of topics\
`url` : URL of handler\
`retryIntervals` : optional array of seconds between retry attempts before message is dead lettered\
`maxParallelism` : optional value to set parallelism of delivery to a subscriber, useful for throttling\
`numberOfWorkers` : optional value of worker threads\
Check out the [Rebus guide](https://github.com/rebus-org/Rebus/wiki/Workers-and-parallelism) on workers and parallelism for more information on the last two values.

## Send a message
`POST` to `/`
```
{ "type": "mymessagetype",
  "data": {"name":"jon"} }
```
Based on the `type` value, `data` will be sent to appropriate subscribers.

Have a look at `test.http` to see how to read and delete subscribers.
