using System;
using System.IO;
using System.Linq;
using System.Text;
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
                var results = index.Query("5c336c7335202a7d7638fa3c7872f197fac1c3c9b193038929c96747d6243c3f", distance);
                var count = results.Count();
                watch.Stop();
                Console.WriteLine($"Query took {watch.ElapsedMilliseconds}ms. {count} results returned");
                if (count > 0)
                {
                    var sample = results.First();
                    var sb = new StringBuilder();
                    sb.Append("Hash: " + sample.Hash + ". Categories: ");
                    foreach (var cat in sample.Categories)
                    {
                        sb.Append(cat + " ");
                    }
                    Console.WriteLine(sb.ToString());
                }
            }

            
        }
    }
}