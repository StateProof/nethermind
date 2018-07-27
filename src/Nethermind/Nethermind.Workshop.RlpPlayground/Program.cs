using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Encoding;

namespace Nethermind.Workshop.RlpPlayground
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            byte[] byteArray = new byte[2] {1, 2};
            Console.WriteLine(Rlp.Encode(byteArray).ToString(true));
            Console.ReadKey();
        }
    }
}