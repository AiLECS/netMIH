using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
        private List<Tuple<BitArray, int[]>> _items = new List<Tuple<BitArray, int[]>>();
        
        /// <summary>
        /// MIH index. Consists of an array (length = HashSize/WordLength) containing Dictionary of each substring of hash mapped to hash offset in <see cref="_items"/>
        /// </summary>
        private Dictionary<string, int[]>[] _index;
        
        /// <summary>
        /// Training data - Dictionary of hashes, mapped against offset of category in <see cref="_categories"/>. Cleared upon training of data (i.e. building of index) 
        /// </summary>
        private Dictionary<string, HashSet<int>> _trainingData = new Dictionary<string, HashSet<int>>();
        
        /// <summary>
        /// Is the index trained (i.e. read-only) and queryable
        /// </summary>
        public bool Trained { get; private set; } = false;
        
        /// <summary>
        /// Regex for testing passed hashes for consistency with supplied hashsize
        /// </summary>
        private Regex _regex = null;
        
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
            /// PDQ algorithm - see https://github.com/facebook/ThreatExchange/blob/master/hashing/hashing.pdf
            /// </summary>
        PDQ};
    
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

    }
    
    /// <summary>
    /// Add entries to index.
    /// </summary>
    /// <param name="items">Enumerable collection of hex strings (each with length <see cref="HashSize"/>/4)</param>
    /// <param name="category">Category to map against this collection of hashes</param>
    /// <exception cref="NotSupportedException">Index already trained and therefore read-only</exception>
    /// <exception cref="ArgumentException">Invalid hash received.</exception>
    public void Update(IEnumerable<string> items, string category)
    {
        if (Trained)
        {
            throw new NotSupportedException("Index already trained.");
        }
        
        if (!_categories.Contains(category))
            _categories.Add(category);
        var offset = _categories.IndexOf(category);

        if (_regex == null)
        {
            _regex = new Regex("^[a-f0-9]{" + HashSize/4 + "}$");
            
        }
        foreach (var hash in items)
        {
            if (!_regex.IsMatch(hash))
            {
                throw new ArgumentException($"Invalid hex string received. Expected {HashSize/4} length. Received {hash}");
            }

            if (!_trainingData.ContainsKey(hash))
            {
                _trainingData[hash] = new HashSet<int>();
            }
            
            _trainingData[hash].Add(offset);    
            
        }
    }
    
    /// <summary>
    /// Train this index (makes it read-only)
    /// </summary>
    /// <returns>Number unique hashes within index</returns>
    public int Train()
    {
        if (Trained)
        {
            return 0;
        }
        
        _index = new Dictionary<string, int[]>[HashSize/WordLength];
        _items = new List<Tuple<BitArray, int[]>>(_trainingData.Keys.Count);
        for (var i = 0; i < _index.Count(); i++)
        {
            _index[i] = new Dictionary<string, int[]>();
        }
        
        
        var counter = 0;
        // Build item list and index.
        // Division of Wordlength and hashsize by 4 to reflect hex string format (4 bits per char) 
        foreach (var hash in _trainingData.Keys)
        {
            _items.Add(new Tuple<BitArray, int[]>(new BitArray(Encoding.ASCII.GetBytes(hash)), _trainingData[hash].ToArray()));
            for (var slot = 0; slot < HashSize / WordLength; slot ++)
            {
                //add substring value for each slot here
                var tempString = hash.Substring((slot * WordLength)/ 4, WordLength / 4);
                if (!_index[slot].ContainsKey(tempString))
                {
                    _index[slot].Add(tempString,new int[] {counter});
                }
                else
                {
                    var tempArray = new int[_index[slot][tempString].Length + 1];
                    tempArray[0] = counter;
                    _index[slot][tempString].CopyTo(tempArray,1);
                    _index[slot][tempString] = tempArray;
                }
            }

            counter++;
        }

        _trainingData = null;
        Trained = true;
        return counter;
    }
    
    /// <summary>
    /// Convert provided BitArray to hex string
    /// </summary>
    /// <param name="hashBits">Hash as BitArray</param>
    /// <returns>hex string representing BitArray value</returns>
    public static string ToHex(BitArray hashBits)
    {
        var ret = new byte[(hashBits.Count/8)];
        hashBits.CopyTo(ret, 0);
        return BitConverter.ToString(ret).Replace("-", string.Empty);
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
        
        var hashBits = new BitArray(Encoding.ASCII.GetBytes(hash));
        if (maxDistance > MatchThreshold)
        {
            foreach (var (candidatHashBits, catOffsets) in _items)
            {
                var hd = GetHamming(candidatHashBits, hashBits, maxDistance); 
                if (hd > -1)
                {
                    yield return new Result()
                    {
                        Hash = ToHex(candidatHashBits),
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
    /// <returns>Enumberable of valid categories</returns>
    public IEnumerable<string> ListCategories(int[] filter = null)
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
        /// <param name="position"></param>
        /// <param name="entries"></param>
        /// <returns></returns>
        public static IEnumerable<string> getWindow(BitArray word, int distance, int position = 0,
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

                    entries.UnionWith(getWindow(word, distance + distOffset, position + 1, entries));
                }

                word[position] = temp;
            }
            else
            {
                entries.UnionWith(getWindow(word, distance, position + 1, entries));
            }

            return entries;
        }
    }

}