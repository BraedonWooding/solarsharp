using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace SolarSharp.Interpreter.Security.Cryptography
{
    /// <summary>
    /// High-performance BouncyCastle cryptographic operations with advanced object pooling.
    /// Features thread-safe caching, object recycling, and memory-efficient resource management.
    /// No System.Security.Cryptography dependencies - professional-grade cross-platform crypto.
    /// </summary>
    public static class BouncyCastleCryptography
    {
        #region Object Pools and Caches

        // High-performance object pools with size limits
        private static readonly ConcurrentObjectPool<ISigner> _signerPool = new(
            "SHA256withRSA",
            () => SignerUtilities.GetSigner("SHA256withRSA"),
            maxSize: 16
        );
        private static readonly ConcurrentObjectPool<IDigest> _digestPool = new(
            "SHA-256",
            () => DigestUtilities.GetDigest("SHA-256"),
            maxSize: 8
        );

        // Certificate parsing cache with LRU eviction
        private static readonly ThreadSafeLRUCache<string, X509Certificate> _certificateCache = new(
            capacity: 1000
        );

        // Public key hash cache for performance
        private static readonly ThreadSafeLRUCache<string, string> _publicKeyHashCache = new(
            capacity: 500
        );

        // Statistics tracking for monitoring
        private static long _signatureVerifications;
        private static long _hashComputations;
        private static long _certificateParses;
        private static long _cacheHits;

        #endregion

        #region High-Performance Cryptographic Operations

        /// <summary>
        /// Verify a digital signature using pooled BouncyCastle cryptographic primitives.
        /// Optimized with object pooling and thread-safe resource management.
        /// </summary>
        /// <param name="data">The data that was signed</param>
        /// <param name="signature">The signature to verify</param>
        /// <param name="publicKey">BouncyCastle public key for verification</param>
        /// <returns>Success with verification result, or Failure with error details</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<bool, CryptographicError> VerifySignature(
            byte[] data,
            byte[] signature,
            AsymmetricKeyParameter publicKey
        )
        {
            Interlocked.Increment(ref _signatureVerifications);

            // Rent signer from pool for maximum performance
            var signerWrapper = _signerPool.Rent();
            try
            {
                var signer = signerWrapper.Object;

                // Reset signer state for reuse
                signer.Reset();
                signer.Init(false, publicKey);
                signer.BlockUpdate(data, 0, data.Length);

                var isValid = signer.VerifySignature(signature);

                return Result.Success<bool, CryptographicError>(isValid);
            }
            catch (Exception ex)
            {
                return Result.Failure<bool, CryptographicError>(
                    new CryptographicError($"Signature verification failed: {ex.Message}")
                );
            }
            finally
            {
                // Always return to pool for reuse
                _signerPool.Return(signerWrapper);
            }
        }

        /// <summary>
        /// Extract public key from BouncyCastle X509 certificate.
        /// Pure function with no side effects.
        /// </summary>
        /// <param name="certificate">BouncyCastle X509 certificate</param>
        /// <returns>Success with public key, or Failure with error details</returns>
        public static Result<AsymmetricKeyParameter, CryptographicError> ExtractPublicKey(
            X509Certificate certificate
        )
        {
            try
            {
                var publicKey = certificate.GetPublicKey();
                return Result.Success<AsymmetricKeyParameter, CryptographicError>(publicKey);
            }
            catch (Exception ex)
            {
                return Result.Failure<AsymmetricKeyParameter, CryptographicError>(
                    new CryptographicError($"Public key extraction failed: {ex.Message}")
                );
            }
        }

        /// <summary>
        /// Compute SHA-256 hash of public key with caching and pooled digest objects.
        /// Optimized for high-throughput scenarios with intelligent caching.
        /// </summary>
        /// <param name="publicKey">BouncyCastle public key to hash</param>
        /// <returns>Success with hex-encoded hash, or Failure with error details</returns>
        public static Result<string, CryptographicError> ComputePublicKeyHash(
            AsymmetricKeyParameter publicKey
        )
        {
            try
            {
                // Extract public key bytes for cache key
                var keyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey);
                var keyBytes = keyInfo.GetEncoded();
                var cacheKey = Convert.ToBase64String(keyBytes);

                // Check cache first for maximum performance
                if (_publicKeyHashCache.TryGetValue(cacheKey, out var cachedHash))
                {
                    Interlocked.Increment(ref _cacheHits);
                    return Result.Success<string, CryptographicError>(cachedHash);
                }

                Interlocked.Increment(ref _hashComputations);

                // Rent digest from pool for performance
                var digestWrapper = _digestPool.Rent();
                try
                {
                    var digest = digestWrapper.Object;

                    // Reset digest state for reuse
                    digest.Reset();

                    // Compute hash
                    var hash = new byte[digest.GetDigestSize()];
                    digest.BlockUpdate(keyBytes, 0, keyBytes.Length);
                    digest.DoFinal(hash, 0);

                    // Convert to hex (using optimized method)
                    var hexHash = BytesToHex(hash);

                    // Cache result for future use
                    _publicKeyHashCache.TryAdd(cacheKey, hexHash);

                    return Result.Success<string, CryptographicError>(hexHash);
                }
                finally
                {
                    // Always return to pool for reuse
                    _digestPool.Return(digestWrapper);
                }
            }
            catch (Exception ex)
            {
                return Result.Failure<string, CryptographicError>(
                    new CryptographicError($"Public key hash computation failed: {ex.Message}")
                );
            }
        }

        /// <summary>
        /// Parse X509 certificate from PEM or DER bytes with intelligent caching.
        /// Optimized for scenarios where the same certificates are parsed repeatedly.
        /// </summary>
        /// <param name="certificateBytes">Certificate data in PEM or DER format</param>
        /// <returns>Success with parsed certificate, or Failure with error details</returns>
        public static Result<X509Certificate, CryptographicError> ParseCertificate(
            byte[] certificateBytes
        )
        {
            try
            {
                // Create cache key from certificate bytes
                var cacheKey = Convert.ToBase64String(certificateBytes);

                // Check cache first for maximum performance
                if (_certificateCache.TryGetValue(cacheKey, out var cachedCertificate))
                {
                    Interlocked.Increment(ref _cacheHits);
                    return Result.Success<X509Certificate, CryptographicError>(cachedCertificate);
                }

                Interlocked.Increment(ref _certificateParses);

                // Parse certificate (expensive operation)
                var parser = new X509CertificateParser();
                var certificate = parser.ReadCertificate(certificateBytes);

                if (certificate == null)
                {
                    return Result.Failure<X509Certificate, CryptographicError>(
                        new CryptographicError("Certificate parsing returned null - invalid format")
                    );
                }

                // Cache the parsed certificate for future use
                _certificateCache.TryAdd(cacheKey, certificate);

                return Result.Success<X509Certificate, CryptographicError>(certificate);
            }
            catch (Exception ex)
            {
                return Result.Failure<X509Certificate, CryptographicError>(
                    new CryptographicError($"Certificate parsing failed: {ex.Message}")
                );
            }
        }

        /// <summary>
        /// Create digital signature using pooled BouncyCastle cryptographic primitives.
        /// Optimized for high-throughput signing operations.
        /// </summary>
        /// <param name="data">The data to sign</param>
        /// <param name="privateKey">BouncyCastle private key for signing</param>
        /// <returns>Success with signature bytes, or Failure with error details</returns>
        public static Result<byte[], CryptographicError> CreateSignature(
            byte[] data,
            AsymmetricKeyParameter privateKey
        )
        {
            try
            {
                // Ensure we have a private key
                if (!privateKey.IsPrivate)
                {
                    return Result.Failure<byte[], CryptographicError>(
                        new CryptographicError("Cannot sign with public key - private key required")
                    );
                }

                // Rent signer from pool for maximum performance
                var signerWrapper = _signerPool.Rent();
                try
                {
                    var signer = signerWrapper.Object;

                    // Reset signer state for reuse
                    signer.Reset();
                    signer.Init(true, privateKey); // true = signing mode
                    signer.BlockUpdate(data, 0, data.Length);

                    var signature = signer.GenerateSignature();
                    return Result.Success<byte[], CryptographicError>(signature);
                }
                finally
                {
                    // Always return to pool for reuse
                    _signerPool.Return(signerWrapper);
                }
            }
            catch (Exception ex)
            {
                return Result.Failure<byte[], CryptographicError>(
                    new CryptographicError($"Signature creation failed: {ex.Message}")
                );
            }
        }

        /// <summary>
        /// Compute SHA-256 hash of data using pooled BouncyCastle digesters.
        /// Optimized for high-throughput hashing operations.
        /// </summary>
        /// <param name="data">Data to hash</param>
        /// <returns>Success with hash bytes, or Failure with error details</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<byte[], CryptographicError> ComputeHash(byte[] data)
        {
            try
            {
                Interlocked.Increment(ref _hashComputations);

                // Rent digest from pool for maximum performance
                var digestWrapper = _digestPool.Rent();
                try
                {
                    var digest = digestWrapper.Object;

                    // Reset digest state for reuse
                    digest.Reset();

                    // Compute hash
                    var hash = new byte[digest.GetDigestSize()];
                    digest.BlockUpdate(data, 0, data.Length);
                    digest.DoFinal(hash, 0);

                    return Result.Success<byte[], CryptographicError>(hash);
                }
                finally
                {
                    // Always return to pool for reuse
                    _digestPool.Return(digestWrapper);
                }
            }
            catch (Exception ex)
            {
                return Result.Failure<byte[], CryptographicError>(
                    new CryptographicError($"Hash computation failed: {ex.Message}")
                );
            }
        }

        #endregion

        #region Performance Monitoring and Management

        /// <summary>
        /// Get performance statistics for monitoring and optimization.
        /// </summary>
        public static CryptographicPerformanceStats GetPerformanceStats()
        {
            return new CryptographicPerformanceStats(
                SignatureVerifications: Interlocked.Read(ref _signatureVerifications),
                HashComputations: Interlocked.Read(ref _hashComputations),
                CertificateParses: Interlocked.Read(ref _certificateParses),
                CacheHits: Interlocked.Read(ref _cacheHits),
                SignerPoolSize: _signerPool.Size,
                DigestPoolSize: _digestPool.Size,
                CertificateCacheSize: _certificateCache.Count,
                PublicKeyHashCacheSize: _publicKeyHashCache.Count
            );
        }

        /// <summary>
        /// Reset performance counters for benchmarking.
        /// </summary>
        public static void ResetPerformanceCounters()
        {
            Interlocked.Exchange(ref _signatureVerifications, 0);
            Interlocked.Exchange(ref _hashComputations, 0);
            Interlocked.Exchange(ref _certificateParses, 0);
            Interlocked.Exchange(ref _cacheHits, 0);
        }

        /// <summary>
        /// Clear cached cryptographic objects and reset pools. Call during application shutdown.
        /// </summary>
        public static void ClearCaches()
        {
            _signerPool.Clear();
            _digestPool.Clear();
            _certificateCache.Clear();
            _publicKeyHashCache.Clear();
            ResetPerformanceCounters();
        }

        /// <summary>
        /// Warm up the cryptographic pools by pre-allocating objects.
        /// Call during application startup for better first-request performance.
        /// </summary>
        public static void WarmupPools()
        {
            // Pre-allocate signer objects
            for (var i = 0; i < 4; i++)
            {
                var wrapper = _signerPool.Rent();
                _signerPool.Return(wrapper);
            }

            // Pre-allocate digest objects
            for (var i = 0; i < 2; i++)
            {
                var wrapper = _digestPool.Rent();
                _digestPool.Return(wrapper);
            }
        }

        #endregion

        #region Optimized Helper Methods

        /// <summary>
        /// High-performance byte array to hex conversion.
        /// Optimized for cryptographic hash output.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string BytesToHex(byte[] bytes)
        {
            const string hexChars = "0123456789abcdef";
            var result = new char[bytes.Length * 2];

            for (var i = 0; i < bytes.Length; i++)
            {
                var b = bytes[i];
                result[i * 2] = hexChars[b >> 4];
                result[i * 2 + 1] = hexChars[b & 0xF];
            }

            return new string(result);
        }

        #endregion
    }

    /// <summary>
    /// Represents a cryptographic error that occurred during BouncyCastle operations.
    /// </summary>
    public sealed record CryptographicError(string Message)
    {
        public override string ToString() => $"CryptographicError: {Message}";
    }

    /// <summary>
    /// Performance statistics for BouncyCastle cryptographic operations.
    /// </summary>
    public sealed record CryptographicPerformanceStats(
        long SignatureVerifications,
        long HashComputations,
        long CertificateParses,
        long CacheHits,
        int SignerPoolSize,
        int DigestPoolSize,
        int CertificateCacheSize,
        int PublicKeyHashCacheSize
    )
    {
        /// <summary>
        /// Calculate cache hit ratio as a percentage.
        /// </summary>
        public double CacheHitRatio =>
            (SignatureVerifications + HashComputations + CertificateParses) > 0
                ? (double)CacheHits
                    / (SignatureVerifications + HashComputations + CertificateParses)
                    * 100.0
                : 0.0;

        /// <summary>
        /// Get total cryptographic operations performed.
        /// </summary>
        public long TotalOperations =>
            SignatureVerifications + HashComputations + CertificateParses;

        /// <summary>
        /// Create a formatted performance report.
        /// </summary>
        public string ToReport()
        {
            return $@"BouncyCastle Performance Statistics:
  Operations:
    - Signature Verifications: {SignatureVerifications:N0}
    - Hash Computations: {HashComputations:N0}
    - Certificate Parses: {CertificateParses:N0}
    - Total Operations: {TotalOperations:N0}
  Caching:
    - Cache Hits: {CacheHits:N0}
    - Cache Hit Ratio: {CacheHitRatio:F2}%
  Pool Sizes:
    - Signer Pool: {SignerPoolSize}
    - Digest Pool: {DigestPoolSize}
  Cache Sizes:
    - Certificate Cache: {CertificateCacheSize}
    - Public Key Hash Cache: {PublicKeyHashCacheSize}";
        }
    }

    #region High-Performance Infrastructure Classes

    /// <summary>
    /// Thread-safe object pool with size limits for optimal memory usage.
    /// </summary>
    internal sealed class ConcurrentObjectPool<T>
        where T : class
    {
        private readonly ConcurrentQueue<PooledObject<T>> _objects = new();
        private readonly Func<T> _objectGenerator;
        private readonly int _maxSize;
        private volatile int _currentSize;

        public ConcurrentObjectPool(string name, Func<T> objectGenerator, int maxSize = 32)
        {
            _objectGenerator =
                objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _maxSize = maxSize;
        }

        public int Size => _currentSize;

        public PooledObject<T> Rent()
        {
            if (_objects.TryDequeue(out var pooledObject))
            {
                Interlocked.Decrement(ref _currentSize);
                return pooledObject;
            }

            // Create new object if pool is empty
            return new PooledObject<T>(_objectGenerator(), this);
        }

        public void Return(PooledObject<T> pooledObject)
        {
            if (pooledObject == null || _currentSize >= _maxSize)
                return;

            _objects.Enqueue(pooledObject);
            Interlocked.Increment(ref _currentSize);
        }

        public void Clear()
        {
            while (_objects.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _currentSize);
            }
        }
    }

    /// <summary>
    /// Wrapper for pooled objects to ensure proper return to pool.
    /// </summary>
    internal sealed class PooledObject<T>
        where T : class
    {
        public T Object { get; }
        private readonly ConcurrentObjectPool<T> _pool;

        internal PooledObject(T obj, ConcurrentObjectPool<T> pool)
        {
            Object = obj;
            _pool = pool;
        }
    }

    /// <summary>
    /// Thread-safe LRU cache with automatic eviction for memory efficiency.
    /// </summary>
    internal sealed class ThreadSafeLRUCache<TKey, TValue>
        where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, CacheNode<TKey, TValue>> _cache = new();
        private readonly int _capacity;
        private volatile CacheNode<TKey, TValue> _head;
        private volatile CacheNode<TKey, TValue> _tail;
        private readonly object _listLock = new object();

        public ThreadSafeLRUCache(int capacity)
        {
            _capacity = capacity;

            // Initialize sentinel nodes
            _head = new CacheNode<TKey, TValue>(default!, default!);
            _tail = new CacheNode<TKey, TValue>(default!, default!);
            _head.Next = _tail;
            _tail.Prev = _head;
        }

        public int Count => _cache.Count;

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // Move to front (most recently used)
                lock (_listLock)
                {
                    MoveToFront(node);
                }
                value = node.Value;
                return true;
            }

            value = default!;
            return false;
        }

        public bool TryAdd(TKey key, TValue value)
        {
            var newNode = new CacheNode<TKey, TValue>(key, value);

            if (_cache.TryAdd(key, newNode))
            {
                lock (_listLock)
                {
                    AddToFront(newNode);

                    // Evict least recently used if over capacity
                    if (_cache.Count > _capacity)
                    {
                        EvictLRU();
                    }
                }
                return true;
            }

            return false;
        }

        public void Clear()
        {
            _cache.Clear();
            lock (_listLock)
            {
                _head.Next = _tail;
                _tail.Prev = _head;
            }
        }

        private void MoveToFront(CacheNode<TKey, TValue> node)
        {
            // Remove from current position
            node.Prev.Next = node.Next;
            node.Next.Prev = node.Prev;

            // Add to front
            AddToFront(node);
        }

        private void AddToFront(CacheNode<TKey, TValue> node)
        {
            node.Next = _head.Next;
            node.Prev = _head;
            _head.Next.Prev = node;
            _head.Next = node;
        }

        private void EvictLRU()
        {
            var lru = _tail.Prev;
            if (lru != _head)
            {
                // Remove from list
                lru.Prev.Next = _tail;
                _tail.Prev = lru.Prev;

                // Remove from dictionary
                _cache.TryRemove(lru.Key, out _);
            }
        }
    }

    /// <summary>
    /// Node for the LRU cache linked list.
    /// </summary>
    internal sealed class CacheNode<TKey, TValue>
    {
        public TKey Key { get; }
        public TValue Value { get; }
        public volatile CacheNode<TKey, TValue> Next;
        public volatile CacheNode<TKey, TValue> Prev;

        public CacheNode(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }

    #endregion
}
