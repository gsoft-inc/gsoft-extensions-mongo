﻿namespace GSoft.Extensions.Mongo;

[AttributeUsage(AttributeTargets.Class)]
public sealed class NoIndexNeededAttribute : Attribute
{
    public NoIndexNeededAttribute(string reason)
    {
        this.Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    public string Reason { get; }
}