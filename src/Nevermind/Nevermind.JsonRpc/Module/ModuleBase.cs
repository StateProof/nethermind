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

using Nevermind.Core;
using Nevermind.Core.Crypto;
using Nevermind.JsonRpc.DataModel;

namespace Nevermind.JsonRpc.Module
{
    public abstract class ModuleBase
    {
        protected readonly ILogger Logger;
        protected readonly IConfigurationProvider ConfigurationProvider;

        protected ModuleBase(ILogger logger, IConfigurationProvider configurationProvider)
        {
            Logger = logger;
            ConfigurationProvider = configurationProvider;
        }

        protected Data Sha3(Data data)
        {
            var keccak = Keccak.Compute((byte[])data.Value);
            var keccakValue = keccak.ToString();
            return new Data(keccakValue);
        }

        public virtual void Initialize()
        {
        }
    }
}