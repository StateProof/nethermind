﻿/*
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
using System.Threading;
using Nethermind.Secp256k1;

namespace Nethermind.Core.Crypto
{
    // TODO: remove entirely and handle private key more securely
    public class PrivateKey
    {
        private const int PrivateKeyLengthInBytes = 32;
        private PublicKey _publicKey;

        public PrivateKey(Hex key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.ByteLength != PrivateKeyLengthInBytes)
            {
                throw new ArgumentException($"{nameof(PrivateKey)} should be {PrivateKeyLengthInBytes} bytes long", nameof(key));
            }

            Hex = key;
        }

        public Hex Hex { get; }

        public PublicKey PublicKey => LazyInitializer.EnsureInitialized(ref _publicKey, ComputePublicKey);

        public Address Address => PublicKey.Address;

        protected bool Equals(PrivateKey other)
        {
            return Hex.Equals(other.Hex);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != GetType())
            {
                return false;
            }
            return Equals((PrivateKey)obj);
        }

        public override int GetHashCode()
        {
            return Hex.GetHashCode();
        }

        private PublicKey ComputePublicKey()
        {
            return new PublicKey(Proxy.GetPublicKey(Hex, false));
        }

        public override string ToString()
        {
            return Hex.ToString(true);
        }
    }
}