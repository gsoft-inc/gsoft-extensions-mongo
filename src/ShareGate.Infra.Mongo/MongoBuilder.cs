﻿using ShareGate.Infra.Mongo.Security;
using Microsoft.Extensions.DependencyInjection;

namespace ShareGate.Infra.Mongo;

public sealed class MongoBuilder
{
    public MongoBuilder(IServiceCollection services)
    {
        this.Services = services;
    }

    public IServiceCollection Services { get; }

    public MongoBuilder AddEncryptor<TEncryptor>()
        where TEncryptor : class, IMongoValueEncryptor
    {
        this.Services.AddSingleton<IMongoValueEncryptor, TEncryptor>();
        return this;
    }

    public MongoBuilder AddMongo(string clientName, Action<MongoClientOptions> configure)
    {
        if (clientName == null)
        {
            throw new ArgumentNullException(nameof(clientName));
        }

        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        if (clientName.Length == 0)
        {
            throw new ArgumentException("Client name cannot be empty", nameof(clientName));
        }

        this.Services.Configure(clientName, configure);
        return this;
    }

    public MongoBuilder ConfigureStaticOptions(Action<MongoStaticOptions> configure)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        this.Services.Configure(configure);
        return this;
    }
}