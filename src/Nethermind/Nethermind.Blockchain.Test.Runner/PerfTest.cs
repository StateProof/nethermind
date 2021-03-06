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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Ethereum.Test.Base;

namespace Nethermind.Blockchain.Test.Runner
{
    public class PerfTest : BlockchainTestBase, ITestInRunner
    {
        public async Task<CategoryResult> RunTests(string subset, string testWildcard, int iterations = 1)
        {
            List<string> failingTests = new List<string>();
            long totalMs = 0L;
            Console.WriteLine($"RUNNING {subset}");
            Stopwatch stopwatch = new Stopwatch();
            IEnumerable<BlockchainTest> tests = LoadTests(subset);
            bool isNewLine = true;
            foreach (BlockchainTest test in tests)
            {
                if (testWildcard != null && !test.Name.Contains(testWildcard))
                {
                    continue;
                }

                stopwatch.Reset();
                for (int i = 0; i < iterations; i++)
                {
                    Setup(null);
                    try
                    {
                        await RunTest(test, stopwatch);
                    }
                    catch (Exception e)
                    {
                        failingTests.Add(test.Name);
                        ConsoleColor mem = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        if (!isNewLine)
                        {
                            Console.WriteLine();
                            isNewLine = true;
                        }

                        Console.WriteLine($"  {test.Name,-80} {e.GetType().Name}");
                        Console.ForegroundColor = mem;
                    }
                }

                long ns = 1_000_000_000L * stopwatch.ElapsedTicks / Stopwatch.Frequency;
                long ms = 1_000L * stopwatch.ElapsedTicks / Stopwatch.Frequency;
                totalMs += ms;
                if (ms > 100)
                {
                    if (!isNewLine)
                    {
                        Console.WriteLine();
                        isNewLine = true;
                    }

                    Console.WriteLine($"  {test.Name,-80}{ns / iterations,14}ns{ms / iterations,8}ms");
                }
                else
                {
                    Console.Write(".");
                    isNewLine = false;
                }
            }

            if (!isNewLine)
            {
                Console.WriteLine();
            }

            return new CategoryResult(totalMs, failingTests.ToArray());
        }
    }
}