﻿using System.Collections.Concurrent;
using System.Security.Cryptography;
using GSoft.ComponentModel.DataAnnotations;
using GSoft.Infra.Mongo.Security;
using GSoft.Xunit.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GSoft.Infra.Mongo.Tests;

public sealed class MongoFixture : BaseIntegrationFixture
{
    public override IServiceCollection ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.TryAddSingleton<AmbientUserContext>();
        services.AddMongo(ConfigureApplicationVersion)

            // .UseEphemeralRealServer()
            .AddEncryptor<AmbientUserEncryptor>();

        services.Configure<MongoOptions>(this.Configuration.GetRequiredSection("Mongo"));

        return services;
    }

    private static void ConfigureApplicationVersion(MongoOptions options)
    {
        // options.Indexing.ApplicationVersionAccessor = () => Version.Parse("1.2.3");
    }

    private sealed class AmbientUserEncryptor : IMongoValueEncryptor
    {
        private readonly ConcurrentDictionary<string, byte[]> _aesKeys;
        private readonly AmbientUserContext _userContext;

        public AmbientUserEncryptor(AmbientUserContext userContext)
        {
            this._userContext = userContext;
            this._aesKeys = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);
        }

        public byte[] Encrypt(byte[] bytes, SensitivityScope sensitivityScope)
        {
            return Aes256Cbc.Encrypt(this.GetAesKey(sensitivityScope), bytes);
        }

        public byte[] Decrypt(byte[] bytes, SensitivityScope sensitivityScope)
        {
            return Aes256Cbc.Decrypt(this.GetAesKey(sensitivityScope), bytes);
        }

        private byte[] GetAesKey(SensitivityScope sensitivityScope)
        {
            return this._aesKeys.GetOrAdd(this.GetAesKeyId(sensitivityScope), _ => Aes256Cbc.CreateNewKey());
        }

        private string GetAesKeyId(SensitivityScope sensitivityScope)
        {
            return sensitivityScope switch
            {
                SensitivityScope.Application => "application",
                SensitivityScope.User => this._userContext.UserId ?? throw new InvalidOperationException("An ambient user ID is required"),
                SensitivityScope.Tenant => throw new NotSupportedException(),
                _ => throw new ArgumentOutOfRangeException(nameof(sensitivityScope)),
            };
        }
    }

    private static class Aes256Cbc
    {
        private const int KeySize = 256;
        private const int BlockSize = 128;
        private const int BitsPerByte = 8;

        private const int IVByteCount = BlockSize / BitsPerByte;

        public static byte[] Encrypt(byte[] key, byte[] bytes)
        {
            using var sourceStream = new MemoryStream(bytes, writable: false);
            using var destinationStream = new MemoryStream();

            Encrypt(key, sourceStream, destinationStream);
            return destinationStream.ToArray();
        }

        private static void Encrypt(byte[] key, Stream sourceStream, Stream destinationStream)
        {
            using var aes = CreateAes(key);
            var iv = aes.IV;

            using var encryptor = aes.CreateEncryptor();

            destinationStream.Write(iv, 0, iv.Length);
            destinationStream.Flush();

            using var cryptoStream = new CryptoStream(destinationStream, encryptor, CryptoStreamMode.Write);
            sourceStream.CopyTo(cryptoStream);
        }

        public static byte[] Decrypt(byte[] key, byte[] encryptedBytes)
        {
            using var sourceStream = new MemoryStream(encryptedBytes, writable: false);
            using var destinationStream = new MemoryStream();

            Decrypt(key, sourceStream, destinationStream);
            return destinationStream.ToArray();
        }

        private static void Decrypt(byte[] key, Stream sourceStream, Stream destinationStream)
        {
            var iv = new byte[IVByteCount];

            var bytesRead = sourceStream.Read(iv, 0, iv.Length);
            if (bytesRead < iv.Length)
            {
                throw new InvalidOperationException("IV is missing or invalid.");
            }

            using var aes = CreateAes(key, iv);
            using var decryptor = aes.CreateDecryptor();

            using var cryptoStream = new CryptoStream(sourceStream, decryptor, CryptoStreamMode.Read);
            cryptoStream.CopyTo(destinationStream);
        }

        public static byte[] CreateNewKey()
        {
            using var aes = CreateAes();
            return aes.Key;
        }

        private static Aes CreateAes(byte[]? key = null, byte[]? iv = null)
        {
            var aes = Aes.Create();

            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            if (key != null)
            {
                aes.Key = key;
            }
            else
            {
                aes.GenerateKey();
            }

            if (iv != null)
            {
                aes.IV = iv;
            }
            else
            {
                aes.GenerateIV();
            }

            return aes;
        }
    }
}