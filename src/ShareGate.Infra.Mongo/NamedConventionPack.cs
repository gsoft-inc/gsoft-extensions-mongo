﻿using MongoDB.Bson.Serialization.Conventions;

namespace ShareGate.Infra.Mongo;

public abstract class NamedConventionPack : ConventionPack
{
    public abstract string Name { get; }

    public abstract bool TypeFilter(Type type);
}