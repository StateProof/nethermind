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
using Nethermind.Core;
using Nethermind.Core.Specs;

namespace Nethermind.Store
{
    public class MemDbProvider : IDbProvider
    {
        private readonly StateTree _stateDb = new StateTree(new MemDb());
        private readonly IDb _codeDb = new MemDb();
        private readonly Dictionary<Address, StorageTree> _storageDbs = new Dictionary<Address, StorageTree>();

        
        public StateTree GetOrCreateStateDb()
        {
            return _stateDb;
        }

        public StorageTree GetOrCreateStorageDb(Address address)
        {
            if (!_storageDbs.ContainsKey(address))
            {
                _storageDbs[address] = new StorageTree(new MemDb());
            }

            return _storageDbs[address];
        }

        public IDb GetOrCreateCodeDb()
        {
            return _codeDb;
        }
        
        private readonly ILogger _logger;

        public MemDbProvider(ILogger logger)
        {
            _logger = logger;
        }
        
        public void Commit()
        {
            if(_logger.IsDebugEnabled) _logger.Debug("Committing all DBs");

            foreach (StorageTree db in _storageDbs.Values)
            {
                db.Commit();
            }

            _codeDb.Commit();
            _stateDb.Commit();
        }

        public void Rollback()
        {
            if(_logger.IsDebugEnabled) _logger.Debug("Rolling back all DBs");

            foreach (StorageTree db in _storageDbs.Values)
            {
                db.Rollback();
            }

            _codeDb.Rollback();
            _stateDb.Rollback();
        }

        public void Commit(IReleaseSpec spec)
        {
            Commit();
        }
    }
}