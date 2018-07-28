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
            Console.WriteLine("byte array {0}   {1}", byteArray, byteArray.Length);
            var xx = Rlp.Encode(byteArray);
            Console.WriteLine("byte array {0}   {1}", xx, xx.Length);
            Console.WriteLine(xx.ToString(true));
            Console.ReadKey();
        }
    }
}
