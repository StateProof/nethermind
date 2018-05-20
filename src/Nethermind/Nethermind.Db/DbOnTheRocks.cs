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

using System;
using System.Collections.Concurrent;
using System.IO;
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

        private static readonly ConcurrentDictionary<string, RocksDb> DbsByPath = new ConcurrentDictionary<string, RocksDb>();

        private readonly RocksDb _db;
        private readonly string _dbPath;
        private readonly byte[] _prefix;

        private readonly WriteBatch _writeBatch = new WriteBatch();

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
                
                return _db.Get(_prefix == null ? key : Bytes.Concat(_prefix, key));
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

                _db.Put(_prefix == null ? key : Bytes.Concat(_prefix, key), value);
//                _writeBatch.Put(_prefix == null ? key : Bytes.Concat(_prefix, key), value);
            }
        }

        public void Remove(byte[] key)
        {
            _db.Remove(_prefix == null ? key : Bytes.Concat(_prefix, key));
        }

        public void Commit()
        {
            //throw new NotImplementedException();
            //WriteOptions options = new WriteOptions();
            //options.SetSync(false); // TODO: check transaction or serialized call?
            //if (_prefix == null) StoreMetrics.StateDbWrites++; else StoreMetrics.StorageDbWrites++;
            //_db.Write(_writeBatch, options);
            //if (_writeBatch.Count() != 0)
            //{
            //    throw new InvalidOperationException("Write batch not cleared after writing");
            //}
        }

        public void Restore()
        {
            _writeBatch.Clear();
        }
    }
}