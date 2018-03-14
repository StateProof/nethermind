﻿using Nethermind.Core.Crypto.ZkSnarks;
using NUnit.Framework;

namespace Nethermind.Core.Test.Crypto.ZkSnarks
{
    [TestFixture]
    public class Fp6Tests
    {
        [Test]
        public void Equals_works_with_nulls()
        {
            Assert.False(Fp6.One == null, "null to the right");
            Assert.False(null == Fp6.One, "null to the left");
            Assert.True((Fp6)null == null, "null both sides");
        }
        
        [Test]
        public void Zero_initializes()
        {
            Fp6 _ = Fp6.Zero;
            Assert.AreEqual(Fp2.Zero, _.A, "A");
            Assert.AreEqual(Fp2.Zero, _.B, "B");
            Assert.AreEqual(Fp2.Zero, _.C, "C");
        }

        [Test]
        public void One_initializes()
        {
            Fp6 _ = Fp6.One;
            Assert.AreEqual(Fp2.One, _.A, "A");
            Assert.AreEqual(Fp2.Zero, _.B, "B");
            Assert.AreEqual(Fp2.Zero, _.C, "C");
        }
        
        [Test]
        public void One_is_reused()
        {
            // ReSharper disable once EqualExpressionComparison
            Assert.True(ReferenceEquals(Fp6.One, Fp6.One));
        }

        [Test]
        public void Zero_is_reused()
        {
            // ReSharper disable once EqualExpressionComparison
            Assert.True(ReferenceEquals(Fp6.Zero, Fp6.Zero));
        }

        [Test]
        public void NonResidue_is_reused()
        {
            // ReSharper disable once EqualExpressionComparison
            Assert.True(ReferenceEquals(Fp6.NonResidue, Fp6.NonResidue));
        }
    }
}