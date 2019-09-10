using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using netMIH;
using NUnit.Framework;
using NUnit.Framework.Interfaces;


namespace netMIHUnitTests
{
    [TestFixture]
    public class Tests
    {


        [SetUp]
        public void Setup()
        {

        }

        [Test]
        public void ConstructorChecksParameters()
        {
            var validHashSize = 256;
            var invalidHashSize = 254;
            Assert.Throws<ArgumentException>(() => new netMIH.Index(invalidHashSize),
                "Argument exception not thrown for invalid hash size");
            var x = new netMIH.Index(validHashSize);
            Assert.Pass("Valid hash size didn't result in exception");

            x = new netMIH.Index(Index.Configuration.PDQ);
            Assert.True(x.HashSize == 256, $"Test failed. Anticipated hashsize of 256. Received {x.HashSize} ");
            Assert.True(x.MatchThreshold == 32,
                $"Test failed. Anticipated threshold of 32. received {x.MatchThreshold}");
            Assert.True(x.WindowSize == 2,
                $"Test failed. Anticipated window size of 2 (MatchThreshold/WordSize). Received {x.WindowSize}");
            Assert.True(x.WordLength == 16, $"Test failed. Anticipated word length of 16. Received {x.WordLength}");
        }

        [Test]
        public void CorrectWindowsGenerated()
        {
            var windows = netMIH.Index.getWindow(new BitArray(Encoding.ASCII.GetBytes("8b")), 2, 0);
            Assert.IsTrue(windows.Count() == 137,
                $"HD 2 on word length 16 should generate 137 permutations. Received {windows.Count()} ");
            windows = netMIH.Index.getWindow(new BitArray(Encoding.ASCII.GetBytes("8b")), 1, 0);
            Assert.IsTrue(windows.Count() == 17,
                $"HD 1 on word length 16 should generate 17 permutations. Received {windows.Count()}");
        }

        [Test]
        public void IndexTests()
        {
            var hashes = new string[] {"358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071927","358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071928","358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071936"};
            var x = new Index(Index.Configuration.PDQ);
            x.Update(hashes, "ignorable");
            Assert.IsFalse(x.Trained, $"Expected index not to be trained, but reporting as True");

/*            Assert.Throws<NotSupportedException>(delegate()
                {
                    x.Query("358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071927");
                });
*/
            x.Train();
            var results = x.Query("358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071927", 0);
            Assert.IsTrue(results.Count()==1, $"Incorrect results length. Anticipated 1, received {results.Count()}");
        }


}
}