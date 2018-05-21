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
using Nethermind.Core.Extensions;

namespace Nethermind.Store
{
    public class MemDb : IDb
    {
        private readonly Dictionary<byte[], byte[]> _db;

        private readonly Dictionary<byte[], byte[]> _pendingChanges;

        public byte[] this[byte[] key]
        {
            get => _pendingChanges.ContainsKey(key) ? _pendingChanges[key] : _db.ContainsKey(key) ? _db[key] : null;
            set => _pendingChanges.Add(key, value);
        }

        public void Remove(byte[] key)
        {
            _pendingChanges[key] = null;
        }

        public void Commit()
        {
            foreach (KeyValuePair<byte[], byte[]> pendingChange in _pendingChanges)
            {
                if (pendingChange.Value == null && _db.ContainsKey(pendingChange.Key))
                {
                    _db.Remove(pendingChange.Key);
                }

                if (pendingChange.Value != null)
                {
                    _db[pendingChange.Key] = pendingChange.Value;
                }
            }
            
            _pendingChanges.Clear();
        }

        public void Rollback()
        {
            _pendingChanges.Clear();
        }

        public MemDb()
        {
            _db = new Dictionary<byte[], byte[]>(1024, Bytes.EqualityComparer);
            _pendingChanges = new Dictionary<byte[], byte[]>(1024, Bytes.EqualityComparer);
        }
    }
}