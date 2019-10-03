# redis-failover-test

This is a test bed for testing failover scenarios between a master / slave Redis
configuration.

## Start the Redis Instances

To start the master / slave Redis setup with 3 sentinels monitoring master using
[Docker](https://www.docker.com/) and [Docker
compose](https://github.com/docker/compose):

```bash
docker-compose up
```
Running the above command without the `-d` flag will allow you to monitor the
logging of Redis instances.

## Run the client

The thing I'm primarily interested in is how
[StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis)
behaves in the event of a failover, which is why I'm using it here for the
client.

To run a [.NET Core](https://dotnet.microsoft.com/download) client that uses
`StackExchange.Redis`.

```bash
dotnet run --project client
```

The above assumes your current directory is the project root directory.

The program just performs some basic set / get operations in a loop to ensure
connectivity and prints which node it's currently connected to. Pressing `enter`
or `Ctrl-C` will stop the program.

## Trigger a Failover

Connect a terminal to the container running the master instance.

Initiate a failover by sending a command to master to sleep for 30 seconds:

```bash
docker exec -it redis-failover-test_redis-master_1 redis-cli debug sleep 30
```

If you prefer, connect to the container running the master instance with bash,
and then run the command:

```bash
docker exec -it redis-failover-test_redis-master_1 bash
redis-cli debug sleep 30
```

## Things I learned

Currently, `StackExchange.Redis` does not have sentinel support for graceful
failover, but it appears a pull request is in the pipeline.

From testing it's clear that `StackExchange.Redis` will throw an exception in
the event of a failover and will not automatically become aware of the new
master unless it reconnects. The client program utilizes retry logic which will
reconnect in the event of an exception in order for `StackExchange.Redis` to
realize the new master.

I did not realize, until testing this and looking into it, that Redis sentinels
have no built in way to fail-back to a preferred master.
