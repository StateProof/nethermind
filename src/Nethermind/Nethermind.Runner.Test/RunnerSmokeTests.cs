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

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Network;
using Nethermind.Network.Discovery;
using Nethermind.Runner.Runners;
using NUnit.Framework;

namespace Nethermind.Runner.Test
{
    [TestFixture]
    public class RunnerSmokeTests
    {
        [Test]
        public void SmokeTest()
        {
            //var host = "http://localhost:100012";

            //Bootstrap.ConfigureContainer(new JsonConfigProvider(), new NetworkConfigurationProvider(new NetworkHelper(NullLogger.Instance)), new PrivateKeyProvider(new CryptoRandom()), new SimpleConsoleLogger(), new InitParams() );

            //var webHost = WebHost.CreateDefaultBuilder()
            //    .UseStartup<Startup>()
            //    .UseUrls(host)
            //    .Build();

            //var ethereumRunner = webHost.Services.GetService<IEthereumRunner>();
            //var discoveryRunner = webHost.Services.GetService<IDiscoveryRunner>();

            //Assert.IsNotNull(ethereumRunner);
            //Assert.IsNotNull(discoveryRunner);
        }
    }
}