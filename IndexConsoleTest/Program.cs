using System;
using System.IO;
using System.Linq;
using netMIH;
using Newtonsoft.Json;

namespace IndexConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var hashes = new string[] {"358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071927","358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071928","358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071936"};
/*
            var index = new netMIH.MIHIndex(MIHIndex.Configuration.PDQ);
            index.Update(hashes, "ignorable");
            index.Train();
            for (var i = 0; i < 10; i++)
            {
                var results = index.Query("358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071927", i);
                foreach (var res in results)
                {
                    Console.WriteLine(JsonConvert.SerializeObject(res));
                }    
            }
*/
            var index = new Index(Index.Configuration.PDQ);
            foreach (var file in Directory.EnumerateFiles("data", "*.PDQ"))
            {
                var h = File.ReadAllLines(file);
                Console.WriteLine($"Loaded {h.Length} hashes from {file}. Sample: {h[0]}");
                index.Update(h, file);
            }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var trained = index.Train();
            watch.Stop();
            Console.WriteLine($"Training took {watch.ElapsedMilliseconds}ms for {trained} unique records");
            
            foreach (var distance in new[] {0, 10, 32, 34,256})
            {
                watch.Restart();
                var results = index.Query("fc4d8e2130177f8f6ce2a03bd27fa8e6b1067a1ac8f0068037215df6491eee1f", distance);
                var count = results.Count();
                watch.Stop();
                Console.WriteLine($"Query took {watch.ElapsedMilliseconds}ms. {count} results returned");
            }

            
        }
    }
}