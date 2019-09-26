using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace netMIH
{
    /// <summary>
    /// MIH Index object. Utilises mulitple-index hashing as per https://www.cs.toronto.edu/~norouzi/research/papers/multi_index_hashing.pdf 
    /// </summary>
    public class Index
    {
        /// <summary>
        /// All unique categories referenced by loaded hashes
        /// </summary>
        private List<string> _categories = new List<string>();
        
        /// <summary>
        /// All hashes (saved as BitArray objects), mapped against array of respective categories' offset in <see cref="_categories"/>
        /// </summary>
        private List<Tuple<BitArray, IEnumerable<int>>> _items = new List<Tuple<BitArray, IEnumerable<int>>>();
        
        /// <summary>
        /// MIH index. Consists of an array (length = HashSize/WordLength) containing Dictionary of each substring of hash mapped to hash offset in <see cref="_items"/>
        /// </summary>
        private ConcurrentDictionary<string, List<int>>[] _index;
        
        /// <summary>
        /// Training data - Dictionary of hashes, mapped against offset of category in <see cref="_categories"/>. Cleared upon training of data (i.e. building of index) 
        /// </summary>
        private Dictionary<string, HashSet<int>> _trainingData = new Dictionary<string, HashSet<int>>();
        
        /// <summary>
        /// Is the index trained (i.e. read-only) and queryable
        /// </summary>
        public bool Trained { get; private set; } = false;
        
        /// <summary>
        /// Regex for testing passed hashes for consistency with (a) format (hex) and (b) hashsize
        /// </summary>
        /// <remarks>Ignores case, but hashes are stored as lower case</remarks>
        public Regex Regex { get; private set; } = null;
        
        /// <summary>
        /// Size of indexed hash in bits (e.g. 256 for PDQ)
        /// </summary>
        public int HashSize { get; private set; }
        
        /// <summary>
        /// Length of each word mapped as part of MIH process. 
        /// </summary>
        public int WordLength { get; private set; }
        
        /// <summary>
        /// Match threshold for use within indexing. Queries exceeding this threshold revert to linear lookups
        /// </summary>
        public int MatchThreshold { get; private set; }
        
        /// <summary>
        /// Max hamming distance to calculate window (i.e. permutations) for.
        /// </summary>
        public int WindowSize { get; private set; }
        
        /// <summary>
        /// Supported configurations for known algorithms
        /// </summary>
        public enum Configuration
        {
            /// <summary>
            /// PDQ algorithm
            /// </summary>
            /// <remarks>see https://github.com/facebook/ThreatExchange/blob/master/hashing/hashing.pdf</remarks>
            PDQ
            
        };
    
        /// <summary>
        /// Convenience method/constructor. Accepts configuration for known algorithms (currently PDQ).
        /// </summary>
        /// <param name="config">Configuration. Currently only PDQ supported</param>
        /// <exception cref="ArgumentException">Unknown/unsupported configuration requested.</exception>
        public Index(Configuration config)
        {
            switch (config)
            {
                case Configuration.PDQ:
                    HashSize = 256;
                    WordLength = 16;
                    MatchThreshold = 32;
                    WindowSize = MatchThreshold / WordLength;
                    break;
                default:
                    throw new ArgumentException("Unsupported Configuration passed");
            }

            Regex = new Regex("^[a-f0-9]{" + HashSize / 4 + "}$", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Manual constructor. Recommended only if you know what you're doing!
        /// </summary>
        /// <param name="hashSize">Hash size in bits</param>
        /// <param name="wordLength">Word length (in bits)</param>
        /// <param name="matchThreshold">Supported 'match' threshold (i.e. hamming distance)</param>
        /// <exception cref="ArgumentException">Thrown if invalid combination of arguments submitted</exception>
        public Index(int hashSize = 256, int wordLength = 16, int matchThreshold = 32)
        {
            if (hashSize % 8 != 0)
            {
                throw new ArgumentException("Provided hash size is not a multiple of 8");
            }

            
            if (hashSize % wordLength != 0)
            {
                throw new ArgumentException($"HashSize must be divisible by the wordLength. Received hashSize {hashSize} and wordLength {wordLength}");
            }

            if (matchThreshold % 2 != 0 || matchThreshold > hashSize)
            {
                throw new ArgumentException($"matchThreshold must be less than hashSize and divisible by 2. Received matchThreshold {matchThreshold} and hashSize {hashSize}");
            }
            
            this.HashSize = hashSize;
            this.WordLength = wordLength;
            this.MatchThreshold = matchThreshold;
            this.WindowSize = matchThreshold / wordLength;
            Regex = new Regex("^[a-f0-9]{" + HashSize / 4 + "}$", RegexOptions.IgnoreCase);
        }
        
        
        /// <summary>
        /// Return the number of items within the collection. Returns 0 if NOT trained
        /// </summary>
        /// <returns>If trained, number of hashes indexed. Else, 0. </returns>
        public long Count()
        {
            return _items.Count;
        }

        /// <summary>
        /// Add entries to index.
        /// </summary>
        /// <param name="items">Enumerable collection of hex strings (each with length <see cref="HashSize"/>/4)</param>
        /// <param name="category">Category to map against this collection of hashes</param>
        /// <exception cref="NotSupportedException">Index already trained and therefore read-only</exception>
        /// <exception cref="ArgumentException">Invalid hash received.</exception>
        /// <remarks> Hashes are stored and returned in lower case. Theoretically, categories could be replaced with unique identifiers for each PDQ (e.g. pointing to a DB reference).
        /// This has not been tested, though, so proceed with caution!</remarks>
        public void Update(IEnumerable<string> items, string category)
        {
            if (Trained)
            {
                throw new NotSupportedException("Index already trained.");
            }
            
            if (!_categories.Contains(category))
                _categories.Add(category);
            var offset = _categories.IndexOf(category);
            
            
            foreach (var hash in items)
            {
                if (!Regex.IsMatch(hash))
                {
                    throw new ArgumentException($"Invalid hex string received. Expected {HashSize/4} length. Received {hash}");
                }

                var cleanedHash = hash.ToLower();
                if (!_trainingData.ContainsKey(cleanedHash))
                {
                    _trainingData[cleanedHash] = new HashSet<int>();
                }
                
                _trainingData[cleanedHash].Add(offset);    
                
            }
        }

        /// <summary>
        /// Train this index (makes it read-only)
        /// </summary>
        /// <remarks>This can be a slow process! We've implemented parallel looping for part of the training,
        /// but there remains a brief, large memory overhead.</remarks>
        /// <returns>Number unique hashes within index</returns>
        public int Train()
        {
            if (Trained)
            {
                return 0;
            }


            _items = new List<Tuple<BitArray, IEnumerable<int>>>(_trainingData.Keys.Count);
            _index = new ConcurrentDictionary<string, List<int>>[HashSize / WordLength];
            for (var i = 0; i < _index.Count(); i++)
            {
                _index[i] = new ConcurrentDictionary<string, List<int>>();
            }
            
            //build item list. Originally did this in parallel with index generation, but unacceptable memory overhead in larger corpora
            foreach (var hash in _trainingData.Keys.ToList())
            {
                _items.Add(new Tuple<BitArray, IEnumerable<int>>(FromHex(hash), _trainingData[hash].ToArray()));
            }
            _trainingData.Clear();
            
            //loop through items and build index. This involves regenerating hex strings from bitarrays, but reduces memory overhead
            Parallel.For(0, _items.Count, entry =>
            {
                var hash = ToHex(_items.ElementAt(entry).Item1);
                for (var slot = 0; slot < HashSize / WordLength; slot++)
                {
                    var bits = new BitArray(WordLength);
                    for (var i = 0; i < WordLength; i++)
                    {
                        bits[i] = _items.ElementAt(entry).Item1[(slot * WindowSize) + i];
                    }

                    var sub = ToHex(bits);
                    // add substring value for each slot here
                    // TODO: Instead of substring, can we just select the relevant elements in the BitArray itself?
                    _index[slot].AddOrUpdate(hash.Substring((slot * WordLength) / 4, WordLength / 4), new List<int>() {entry}, (k, v) =>
                    {
                        v.Add(entry);
                        return v;
                    });

                }   
            });

            Trained = true;
            return _items.Count;

        }

  
    
        /// <summary>
        /// Convert provided BitArray to hex string
        /// </summary>
        /// <param name="bits">Hash as BitArray</param>
        /// <returns>hex string representing BitArray value</returns>
        /// <remarks>Converter based on solution found at <see href="https://stackoverflow.com/questions/37162727/c-sharp-bitarray-to-hex"/></remarks>
        public static string ToHex(BitArray bits)
        {
            var sb = new StringBuilder(bits.Length / 4);

            for (int i = 0; i < bits.Length; i += 4) {
                int v = (bits[i] ? 8 : 0) | 
                        (bits[i + 1] ? 4 : 0) | 
                        (bits[i + 2] ? 2 : 0) | 
                        (bits[i + 3] ? 1 : 0);

                sb.Append(v.ToString("x1")); // Or "X1"
            }

            return sb.ToString();
        }
        
        /// <summary>
        /// Convert hex string to BitArray representation
        /// </summary>
        /// <param name="hexData">hex formatted string (4 bits per char)</param>
        /// <example>
        /// <code>
        /// var bits = BitArray("358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071927");
        /// </code></example>
        /// <returns>BitArray representation of hex string</returns>
        /// <remarks>Based on converter located at <see href="https://stackoverflow.com/questions/4269737/function-convert-hex-string-to-bitarray-c-sharp"/> </remarks>
        public static BitArray FromHex(string hexData)
        {
            var ba = new BitArray(4 * hexData.Length);
            for (var i = 0; i < hexData.Length; i++)
            {
                var b = byte.Parse(hexData[i].ToString(), NumberStyles.HexNumber);
                for (var j = 0; j < 4; j++)
                {
                    ba.Set(i * 4 + j, (b & (1 << (3 - j))) != 0);
                }
            }
            return ba;
        
        }
        
        /// <summary>
        /// Query against this index. Uses MIH if maxDistance set lower than or equal to <see cref="MatchThreshold"/>
        /// </summary>
        /// <param name="hash">Hash for lookup, as hex string</param>
        /// <param name="maxDistance">Max hamming distance for similarity lookup (0 = identical). NOTE: linear search conducted if higher than <see cref="MatchThreshold"/></param>
        /// <returns>Enumerable to <see cref="Result"/> objects.</returns>
        /// <exception cref="NotSupportedException">Index not trained</exception>
        public IEnumerable<Result> Query(string hash, int maxDistance = 32)
        {
            if (!Trained)
            {
                throw new NotSupportedException("Index not trained yet");
            }

            var hashBits = FromHex(hash);
            if (maxDistance > MatchThreshold)
            {
                foreach (var (candidateHashBits, catOffsets) in _items)
                {
                    var hd = GetHamming(candidateHashBits, hashBits, maxDistance); 
                    if (hd > -1)
                    {
                        yield return new Result()
                        {
                            Hash = ToHex(candidateHashBits),
                            Distance = hd,
                            Categories = ListCategories(catOffsets).ToArray()
                        };
                    }
                }
            }
            else
            {
                var candidates = new HashSet<int>();
                for (var slot = 0; slot < HashSize / WordLength; slot++)
                {
                    //add substring value for each slot here
                    var tempString = hash.Substring((slot * WordLength)/ 4, WordLength / 4);
                    if (_index[slot].ContainsKey(tempString))
                    {
                        candidates.UnionWith(_index[slot][tempString]);
                    }
                }

                foreach (var i in candidates)
                {
                    var hd = GetHamming(_items[i].Item1, hashBits, maxDistance);
                    if (hd > -1)
                    {
                        yield return new Result()
                        {
                            Hash = ToHex(_items[i].Item1),
                            Distance = hd,
                            Categories = ListCategories(_items[i].Item2).ToArray()
                        };
                    }
                }
            }
        }
        
        /// <summary>
        /// List categories within index
        /// </summary>
        /// <param name="filter">Optional - return  only categories residing at provided indices</param>
        /// <returns>Enumerable of categories within index</returns>
        /// <exception cref="IndexOutOfRangeException">Offset provided in filter is not available within _categories.</exception>
        public IEnumerable<string> ListCategories(IEnumerable<int> filter = null)
        {
            if (filter == null)
            {
                foreach (var cat in _categories)
                {
                    yield return cat;
                }
            }
            else
            {
                foreach (var offset in filter)
                {
                    yield return _categories[offset];
                }
            }
        }
        
        /// <summary>
        /// Calculates hamming distance between two identical length BitArrays. 
        /// </summary>
        /// <param name="hash1">first hash for comparison</param>
        /// <param name="hash2">second hash for comparison</param>
        /// <param name="maxDistance">Optional - max hamming distance. Stop comparing if this figure is reached. </param>
        /// <returns>Hamming distance or -1 if max hamming distance provided and exceeded.</returns>
        /// <exception cref="ArgumentException">Thrown if two BitArrays are not of equal length</exception>
        public static int GetHamming(BitArray hash1, BitArray hash2, int maxDistance = -1)
        {
            if (maxDistance < 0)
            {
                maxDistance = hash1.Count;
            }

            if (hash1.Count != hash2.Count)
            {
                throw new ArgumentException("Hashes not of equal length.");
            }

            var hamming = 0;
            for (var i = 0; i < hash1.Count; i++)
            {
                if (hash1[i] == hash2[i]) continue;
                hamming++;
                if (hamming > maxDistance)
                {
                    return -1;
                }
            }

            return hamming;

        }
        
        /// <summary>
        /// Returns all permutations of provided word (bitarray) within hamming distance.
        /// </summary>
        /// <param name="word">Candidate word (as BitArray)</param>
        /// <param name="distance">Maximum hamming distance (inclusive)</param>
        /// <param name="position">Optional. Should not be provided by end user (used only in recursive calls)</param>
        /// <param name="entries">Optional. Should not be provided by end user (used only within recursive calls)</param>
        /// <returns>Enumerable of strings within provided hamming distance from provided word</returns>
        public static IEnumerable<string> GetWindow(BitArray word, int distance, int position = 0,
            HashSet<string> entries = null)
        {
            if (entries == null)
                entries = new HashSet<string>();

            if (position == word.Count)
            {
                var ret = new byte[(word.Count - 1) / 8 + 1];
                word.CopyTo(ret, 0);
                entries.Add(BitConverter.ToString(ret).Replace("-", string.Empty));
                return entries;
            }



            if (distance > 0)
            {
                var temp = word[position];
                foreach (var toggle in new[] {false, true})
                {
                    word[position] = toggle;
                    var distOffset = 0;
                    if (temp != toggle)
                    {
                        distOffset = -1;
                    }

                    entries.UnionWith(GetWindow(word, distance + distOffset, position + 1, entries));
                }

                word[position] = temp;
            }
            else
            {
                entries.UnionWith(GetWindow(word, distance, position + 1, entries));
            }

            return entries;
        }
    }

}