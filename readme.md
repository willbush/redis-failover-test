**Table of Contents**

- [redis-failover-test](#redis-failover-test)
    - [Start the Redis Instances](#start-the-redis-instances)
    - [Run the client](#run-the-client)
    - [Trigger a Failover](#trigger-a-failover)
    - [Manual Failover Without Sentinels](#manual-failover-without-sentinels)
    - [Things I learned](#things-i-learned)
    - [Troubleshooting Sentinel Issues](#troubleshooting-sentinel-issues)


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

The thing I'm currently interested in is how
[StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis)
behaves in the event of a failover, which is why I'm using it here for the
client.

To run a [.NET Core](https://dotnet.microsoft.com/download) client that uses
`StackExchange.Redis`.

```bash
dotnet run --project client
```

or

```bash
cd client
dotnet run
```

The above assumes your current directory is the project root directory.

The program just performs some basic set / get operations in a loop to ensure
connectivity and prints which node it's currently connected to. Pressing `enter`
or `Ctrl-C` will stop the program.

## Trigger a Failover

Initiate a failover by sending a command to master to simulate a segfault:

```bash
docker exec -it redis-failover-test_redis-master_1 redis-cli debug segfault
```

The segfault will cause the entire container to crash. Be sure to also test
`debug sleep 100` as well because I have noticed different behavior in the
client between the two. See [Things I learned](#things-i-learned).

Note that you can connect to a container in a bash shell be doing:

```bash
docker exec -it redis-failover-test_redis-master_1 bash
```

## Manual Failover Without Sentinels

If you want to test manually failing over without sentinels

1. Comment out the sentinel services in the `docker-compose.yml` file.

2. Run `docker-compose up --build`.

3. Connect two shells to bash with `docker exec -it
   redis-failover-test_redis-master_1 bash` and `docker exec -it
   redis-failover-test_redis-slave_1 bash`.

4. Run `redis-cli` in each shell.

5. On the slave `redis-cli` run `slaveof no one` and on master run `slaveof
   redis-slave 6379`. Fail back by doing the opposite.

If you're ever wondering, you can see whether a Redis server is master or slave
by doing the `role` command in the `redis-cli`.

## Things I learned

Currently, `StackExchange.Redis` does not have sentinel support for graceful
failover, but it appears a pull request is in the pipeline. If you're looking
for high availability then perhaps look into [http://www.haproxy.org/](haproxy).

When providing two endpoints (one for master and slave):

```csharp
var testConnectionString = "localhost:7000,localhost:7001";
var config = ConfigurationOptions.Parse(testConnectionString);
```

The `StackExchange.Redis` client might realize the new master without having to
call `IConnectionMultiplexer.Connect` or more importantly
`IConnectionMultiplexer.Configure`. However, it seems to depend on the reason
why master went down in the first place.

- For manual failover with or without sentinels, the client will pick up on the
  new master, but may take several seconds to a minute.

- Performing `redis-cli debug segfault` on master will cause the sentinels to
  promote and the client to immediately switch to the new master. I suspect this
  is because the socket closed and perhaps triggering `StackExchange.Redis` to
  reconfigure.

- Performing `redis-cli debug sleep 100` on master will cause the sentinels to
  promote and the client will just error on `set` operations until your retry
  logic limit is exceeded or the sleep is done. One could use
  `IConnectionMultiplexer.Configure` on the
  `IConnectionMultiplexer.ErrorMessage` event. However, that will only work if
  you're _not_ using `CommandFlags.FireAndForget`.

- Don't expect the `StackExchange.Redis` connection retry settings to apply

I also did not realize until testing this and looking into it, that Redis
sentinels have no built in way to fail-back to a preferred master.

## Troubleshooting Sentinel Issues

If you see the following error when performing `docker-compose up`:

```
redis-sentinel3_1  | standard_init_linux.go:207: exec user process caused "no such file or directory"
redis-sentinel2_1  | standard_init_linux.go:207: exec user process caused "no such file or directory"
redis-sentinel_1   | standard_init_linux.go:207: exec user process caused "no such file or directory"
```

This is typically because of the CRLF line endings in the
`./redis-sentinel/sentinel-entrypoint.sh`.

The unix style LF line endings probably got replaced with CRLF due to a
`.gitconfig` setting of:

```
[core]
	autocrlf = true
```

Run `dos2unix` command on the `sentinel-entrypoint.sh` and `docker-compose up
--build` to fix the issue.
