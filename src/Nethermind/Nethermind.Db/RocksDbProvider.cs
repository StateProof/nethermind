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

using System.Collections.Generic;
using System.IO;
using Nethermind.Core;
using Nethermind.Store;

namespace Nethermind.Db
{
    // TODO: this is a copy paste from MemDbProvider (mainly commit / restore / snapshots), like most snapshotable classes, awaiting some refactoring
    public class RocksDbProvider : IDbProvider
    {
        private readonly StateTree _stateDb;
        private readonly IDb _codeDb;
        private readonly Dictionary<Address, StorageTree> _storageDbs = new Dictionary<Address, StorageTree>();

        public StateTree GetOrCreateStateDb()
        {
            return _stateDb;
        }

        public StorageTree GetOrCreateStorageDb(Address address)
        {
            if (!_storageDbs.ContainsKey(address))
            {
                var path = Path.Combine(_dbBasePath, DbOnTheRocks.StorageDbPath);
                var db = new DbOnTheRocks(path, address.Hex);
                _storageDbs[address] = new StorageTree(db);
            }

            return _storageDbs[address];
        }

        public IDb GetOrCreateCodeDb()
        {
            return _codeDb;
        }

        private readonly string _dbBasePath;
        private readonly ILogger _logger;

        public RocksDbProvider(string dbBasePath, ILogger logger)
        {
            _logger = logger;
            _dbBasePath = dbBasePath;
            _stateDb = new StateTree(new DbOnTheRocks(Path.Combine(_dbBasePath, DbOnTheRocks.StateDbPath)));
            _codeDb = new SnapshotableDb(new DbOnTheRocks(Path.Combine(_dbBasePath, DbOnTheRocks.CodeDbPath)));
        }

        public void Commit()
        {
            if (_logger.IsDebugEnabled) _logger.Debug("Committing all DBs");

            foreach (StorageTree db in _storageDbs.Values)
            {
                db.Commit();
            }
            
            _codeDb.Commit();
            _stateDb.Commit();
        }

        public void Rollback()
        {
            foreach (StorageTree db in _storageDbs.Values)
            {
                db.Rollback();
            }
            
            _stateDb.Rollback();
        }
    }
}