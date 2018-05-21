/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nethermind.Core.Extensions;
using Nethermind.Store;
using RocksDbSharp;

namespace Nethermind.Db
{
    public class DbOnTheRocks : IDb
    {
        public const string StorageDbPath = "state";
        public const string StateDbPath = "state";
        public const string CodeDbPath = "code";
        public const string BlocksDbPath = "blocks";
        public const string ReceiptsDbPath = "receipts";
        public const string BlockInfosDbPath = "blockInfos";

        private readonly ConcurrentDictionary<byte[], byte[]> _pendingChanges;
        
        private static readonly ConcurrentDictionary<string, RocksDb> DbsByPath = new ConcurrentDictionary<string, RocksDb>();

        private readonly RocksDb _db;
        private readonly string _dbPath;
        private readonly byte[] _prefix;

        public DbOnTheRocks(string dbPath, byte[] prefix = null) // TODO: check column families
        {
            if (!Directory.Exists("db"))
            {
                Directory.CreateDirectory("db");
            }

            _dbPath = dbPath;
            _prefix = prefix;
            DbOptions options = new DbOptions();
            options.SetCreateIfMissing(true);
            options.OptimizeForPointLookup(32);

            _db = DbsByPath.GetOrAdd(dbPath, path => RocksDb.Open(options, Path.Combine("db", path)));
            _pendingChanges = new ConcurrentDictionary<byte[], byte[]>(Enumerable.Empty<KeyValuePair<byte[], byte[]>>(), Bytes.EqualityComparer);
        }

        public byte[] this[byte[] key]
        {
            get
            {
                if (_dbPath.EndsWith(BlockInfosDbPath))
                {
                    StoreMetrics.BlockInfosDbReads ++;
                }
                else if (_dbPath.EndsWith(BlocksDbPath))
                {
                    StoreMetrics.BlocksDbReads++;
                }
                else if (_dbPath.EndsWith(StateDbPath))
                {
                    if (_prefix == null) StoreMetrics.StateDbReads++; else StoreMetrics.StorageDbReads++;
                }

                byte[] prefixedKey = _prefix == null ? key : Bytes.Concat(_prefix, key);
                return _pendingChanges.ContainsKey(prefixedKey) ? _pendingChanges[prefixedKey] : _db.Get(prefixedKey);
            }
            set
            {
                if (_dbPath.EndsWith(BlockInfosDbPath))
                {
                    StoreMetrics.BlockInfosDbWrites ++;
                }
                else if (_dbPath.EndsWith(BlocksDbPath))
                {
                    StoreMetrics.BlocksDbWrites++;
                }
                else if (_dbPath.EndsWith(StateDbPath))
                {
                    if (_prefix == null) StoreMetrics.StateDbWrites++; else StoreMetrics.StorageDbWrites++;
                }

                byte[] prefixedKey = _prefix == null ? key : Bytes.Concat(_prefix, key);
                _pendingChanges[prefixedKey] = value;
            }
        }

        public void Remove(byte[] key)
        {
            _pendingChanges[key] = null;
        }

        public void Commit()
        {
            WriteBatch batch = new WriteBatch();
            foreach (KeyValuePair<byte[],byte[]> pendingChange in _pendingChanges)
            {
                if (pendingChange.Value == null)
                {
                    batch.Delete(pendingChange.Key);
                }
                else
                {
                    batch.Put(pendingChange.Key, pendingChange.Value);
                }
            }
            
            _db.Write(batch);
            
//            WriteOptions options = new WriteOptions();
//            options.SetSync(false);
        }

        public void Rollback()
        {
            _pendingChanges.Clear();
        }
    }
}