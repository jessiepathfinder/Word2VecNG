using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;


namespace Word2VecNG.Train
{
	internal static class Program
	{
		private static int Tokenize(IReadOnlyDictionary<string, OptimizedTokenizerEntry> dict, Span<ushort> output, ReadOnlySpan<char> str, int maxtokensize, int specialTokenClasses)
		{
			if (maxtokensize < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(maxtokensize));
			}
			int pos = 0;
			int ctr2 = 0;
			for (int len = str.Length, outlen = output.Length; ctr2 < len & pos < outlen;)
			{
				StringBuilder sb = new StringBuilder();
				int token = -1;
				for (int i = ctr2++, stop = Math.Min(i + maxtokensize, len); i < stop; ++i)
				{
					sb.Append(str[i]);
					if (dict.TryGetValue(sb.ToString(), out OptimizedTokenizerEntry val))
					{
						token = val.value;
						ctr2 = i + 1;
						if (val.fastret)
						{
							break;
						}
					}
				}
				if (token > -1)
				{
					output[pos++] = (ushort)(token + specialTokenClasses);
				}
			}
			return pos;
		}
		private static Dictionary<string, OptimizedTokenizerEntry> OptimizeDictionary(IReadOnlyDictionary<string, ushort> input)
		{
			string[] keys = input.Keys.ToArray();
			int len = keys.Length;
			Dictionary<string, OptimizedTokenizerEntry> thedict = new Dictionary<string, OptimizedTokenizerEntry>(len);

			foreach (KeyValuePair<string, ushort> kvp in input)
			{
				bool fastret = true;
				string str = kvp.Key;

				for (int i = 0, sl = str.Length; i < len;)
				{
					string str2 = keys[i++];
					if (str2.Length > sl && str2.StartsWith(str))
					{
						fastret = false;
						break;
					}
				}
				thedict.Add(str, new OptimizedTokenizerEntry(kvp.Value, fastret));
			}
			return thedict;
		}
		private readonly struct OptimizedTokenizerEntry
		{
			public readonly ushort value;
			public readonly bool fastret;

			public OptimizedTokenizerEntry(ushort value, bool fastret)
			{
				this.value = value;
				this.fastret = fastret;
			}
		}
		private const int maxContextSize = 4096;
		private const int magicTokenClasses = 4;
		private const int unorderedContextLookaroundSize = 3;
		private const uint latentTokenSize = 2048;
		private const int orderedContextLookaroundSize = 7;
		private const ulong preHash = 3;


		private const uint hashesPerWord = 4;

		private const int unorderedPastLookaround = 5;
		private const int unorderedFutureLookaround = 5;

		private static ulong XorShift64(ulong x){
			x ^= x << 13;
			x ^= x >> 7;
			return x ^ (x << 17);
		}

		private static void MergeHash(ulong hash, int word, int[,] ints){
			for(ulong i = 0; i < preHash; ++i){
				hash = XorShift64(hash ^ i);
			}
			int sign = 1;
			for(uint i = 0; i < hashesPerWord; ++i){
				hash = XorShift64(hash ^ i);
				int ih = (int)(hash & 2147483647UL);
				ints[word, (ih / 4) % latentTokenSize] += sign;
				sign = -sign;
			}
		}
		

		[JsonObject(MemberSerialization.Fields)]
		private sealed class WikipediaArticle
		{
			//SUPPRESS WARNINGS since fields will be reflectively set
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable CS0649
			public string title;
			public string[] section_titles;
			public string[] section_texts;
#pragma warning restore CS0649
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		}
		private static void Main(string[] args)
		{
			if(args.Length != 2){
				Console.WriteLine("Usage: Word2VecNG.Train [datadir] [save]");
				return;
			}
			string datadir = args[0];
			string save = args[1];
			if (!datadir.EndsWith(Path.DirectorySeparatorChar))
			{
				datadir += Path.DirectorySeparatorChar;
			}
			Console.WriteLine("Loading dictionary...");
			IReadOnlyDictionary<string, ushort>? dict = JsonConvert.DeserializeObject<IReadOnlyDictionary<string, ushort>>(File.ReadAllText(datadir + "encoder.json"));
			if (dict is null)
			{
				Console.WriteLine("Null encoder dictionary");
				return;
			}

			int maxlen = 0;
			int tokenclasses = 0;
			foreach (KeyValuePair<string, ushort> keyValuePair in dict)
			{
				maxlen = Math.Max(maxlen, keyValuePair.Key.Length);
				tokenclasses = Math.Max(keyValuePair.Value, tokenclasses);
			}
			//5 magic token types
			//[START_GPT], [END_GPT], [WIKI_SEPERATOR], [MASK]
			tokenclasses += magicTokenClasses + 1;
			int tokenClasses2 = tokenclasses;
			Console.WriteLine("Optimizing dictionary...");
			IReadOnlyDictionary<string, OptimizedTokenizerEntry>? dict1 = OptimizeDictionary(dict);



			Console.WriteLine("Loading ELI5 + WikiQA question answering dataset...");
			Queue<string> dataqueue = new Queue<string>();
			//File.ReadAllText(datadir + "QuestionAnsweringV2.jsonl.deflate")

			using (StreamReader reader = new StreamReader(new DeflateStream(new FileStream(datadir + "QuestionAnswering.jsonl.deflate", FileMode.Open, FileAccess.Read, FileShare.Read, 16777216, FileOptions.SequentialScan), CompressionMode.Decompress, false), Encoding.UTF8, false, 16777216, false))
			{
			read:
				string? line = reader.ReadLine();
				if (line is { })
				{
					dataqueue.Enqueue(line);
					goto read;
				}
			}
			string[]? questionanswering = dataqueue.ToArray();
			int wqlen2 = questionanswering.Length;

			Console.WriteLine("Loading simple english wikipedia dataset...");
			string[]? wikiarticles = File.ReadAllLines(datadir + "simplewiki-latest.jsonl");


			int wikilen2 = wikiarticles.Length;

			Console.WriteLine("Starting dataset tokenizers...");
			int wqlength = wqlen2;
			int wikilen = wikilen2;

			ConcurrentBag<ushort[]>? alldata = new();
			//ConcurrentBag<int[]>? classcounters = new();
			int threads = Environment.ProcessorCount;
			int wikisplit = 0;
			int loadprogress = 0;
			int wikiloadprogress = 0;
			Thread[] thrlist = new Thread[threads];


			for (int z = 0; z < threads; ++z)
			{
				int az = z;
				Thread thread = new Thread(() =>
				{
					int za = az;
					
					Span<ushort> encbuffer2 = stackalloc ushort[maxContextSize];
					int mywqlen = wqlength;
					string str1 = "Tokenized {0}/" + mywqlen + " question-answer pairs";
					int mywikilen = wikilen;
					//int[] counter = new int[tokenClasses2];
					//classcounters.Add(counter);
					//int sa2 = suboptimalSkipInitialTokens + 2;

					while (true)
					{
						int a = Interlocked.Increment(ref loadprogress);
						if (a > mywqlen)
						{
							break;
						}
						a -= 1;
						string raw = questionanswering[a];
						string[]? pair = JsonConvert.DeserializeObject<string[]>(raw);
						if (pair is null)
						{
							continue;
						}


						int size1 = Tokenize(dict1, encbuffer2, pair[0], maxlen, magicTokenClasses);
						if (size1 == maxContextSize)
						{
							continue;
						}




						encbuffer2[size1++] = 0; //user-to-GPT context switch
						if (size1 == maxContextSize)
						{
							continue;
						}
						//int encsize2 = size1;
						int ctd = Tokenize(dict1, encbuffer2[size1..], pair[1], maxlen, magicTokenClasses);
						if(ctd == 0){
							continue;
						}
						size1 += ctd;
						if (size1 < maxContextSize)
						{
							encbuffer2[size1++] = 1; //GPT-to-user context switch
						}


						alldata.Add(encbuffer2[..size1].ToArray());


						if ((a & 4095) == 4095)
						{
							Console.WriteLine(str1, a);
						}

					}

					str1 = "Tokenized {0}/" + mywikilen + " simple english wikipedia articles";

					while (true)
					{
						int a = Interlocked.Increment(ref wikiloadprogress);
						if (a > mywikilen)
						{
							return;
						}
						a -= 1;
						WikipediaArticle? wikipediaArticle = JsonConvert.DeserializeObject<WikipediaArticle>(wikiarticles[a]);
						if (wikipediaArticle is null)
						{
							continue;
						}
						string wikititle = wikipediaArticle.title;
						string lowertitle = wikititle.ToLower();

						//skip useless lists (TinyGPT is horrible with dealing with those)
						if (lowertitle.StartsWith("list of"))
						{
							continue;
						}
						if (lowertitle.StartsWith("lists of"))
						{
							continue;
						}
						int size2 = Tokenize(dict1, encbuffer2, wikititle, maxlen, magicTokenClasses);
						if (size2 == maxContextSize)
						{
							continue;
						}
						if (size2 == 0)
						{
							continue;
						}

						encbuffer2[size2++] = 2; //wikipedia article retrieval task

						if (size2 == maxContextSize)
						{
							continue;
						}
						//Span<ushort> encbuffer3 = encbuffer2[size2..];

						string[] section_texts = wikipediaArticle.section_texts;
						string[] section_titles = wikipediaArticle.section_titles;
						int len = Math.Min(section_texts.Length, section_titles.Length);
						for (int segmentid = 0; segmentid < len; ++segmentid)
						{
							string text = section_texts[segmentid];
							if (text.Length < 64)
							{
								continue; //too short to be useful
							}
							string title = section_titles[segmentid];

							//TinyGPT does not handle citations and references well
							switch (title.ToLower())
							{
								case "see also":
								case "references":
									continue;
							}
							int size1 = Tokenize(dict1, encbuffer2[size2..], title, maxlen, magicTokenClasses);
							if (size1 == 0)
							{
								continue;
							}
							size1 += size2;
							if (size1 == maxContextSize)
							{
								continue;
							}

							encbuffer2[size1++] = 0; //[START_GPT]
							if (size1 == maxContextSize)
							{
								continue;
							}

							int ctd = Tokenize(dict1, encbuffer2[size1..], text.Replace("'''", null).Replace("''", null), maxlen, magicTokenClasses);
							if (ctd == 0)
							{
								continue;
							}
							size1 += ctd;
							
							if (size1 < maxContextSize)
							{
								encbuffer2[size1++] = 1; //GPT-to-user context switch
							}
							

							alldata.Add(encbuffer2[..size1].ToArray());


						}
						if ((a & 4095) == 4095)
						{
							Console.WriteLine(str1, a);
						}
					}
				});
				thread.Name = "Dataset tokenizer thread #" + z;
				thread.IsBackground = true;
				thrlist[z] = thread;
				thread.Start();
			}
			
			Console.WriteLine("Waiting for dataset tokenization to complete...");
			foreach (Thread thr in thrlist)
			{
				thr.Join();
			}
			Console.WriteLine("Optimizing memory usage...");
			ushort[][] tokenized = alldata.ToArray();
			alldata = null;
			questionanswering = null;
			wikiarticles = null;
			dict1 = null;
			wqlen2 = wikisplit;
			
			int dataCount = tokenized.Length;
			string str1 = "Counted {0} out of " + dataCount + " dataset samples";
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

			Console.WriteLine("Computing hashed collocation count...");
			int[,] hashedCollocationCounts = new int[tokenclasses, latentTokenSize];
			uint[] wordctr = new uint[tokenclasses];
			Span<ulong> thespan = stackalloc ulong[(orderedContextLookaroundSize * 2) + 1];
			ulong seed = 0xd5cf8af46cd8fc52;
			for (int i = 0, stop = thespan.Length; i < stop; ){
				seed = XorShift64(seed);
				thespan[i++] = seed;
			}
			
			for (int i = 0; i < dataCount; ++i){
				ushort[] ushorts = tokenized[i];
				for(int z = 0, stop = ushorts.Length; z < stop; ++z){
					int current = ushorts[z];
					++wordctr[current];
					for(int start = Math.Max(z - unorderedContextLookaroundSize,0),end = Math.Min(z + unorderedContextLookaroundSize,stop);start < end; ++start){
						if(start == z){
							continue;
						}
						MergeHash((seed * 0x649f10d0UL) + 0x4b4e98ae7ee7c3bdUL, current, hashedCollocationCounts);
					}
					
					for (int start = Math.Max(z - orderedContextLookaroundSize, 0), end = Math.Min(z + orderedContextLookaroundSize, stop); start < end; ++start){
						if (start == z)
						{
							continue;
						}
						MergeHash((seed * 0xe77fa8d0UL) + thespan[(start - z) + orderedContextLookaroundSize], current, hashedCollocationCounts);
					}
					for (int start = Math.Max(z - unorderedPastLookaround, 0); start < z; ++start)
					{
						MergeHash((seed * 0x8bfc03b0UL) + 0xd86e703f668a6275, current, hashedCollocationCounts);
					}
					for (int start = z + 1, end = Math.Min(z + unorderedFutureLookaround, stop); start < end; ++start)
					{
						MergeHash((seed * 0xdcac3570UL) + 0x8f48b32bcb6ed716, current, hashedCollocationCounts);
					}
				}
				if((i & 4095) == 4095)
				{
					Console.WriteLine(str1, i);
				}
			}

			byte[] bytes = new byte[latentTokenSize * tokenclasses * 4];
			Span<float> floats = MemoryMarshal.Cast<byte, float>(bytes.AsSpan());
			Console.WriteLine("Flattening and rescaling...");
			for(uint x = 0,z = 0; x < tokenclasses; ++x, z += latentTokenSize){
				uint dc = wordctr[x];
				if(dc == 0){
					continue;
				}
				double casted = dc;

				for(uint y = 0; y < latentTokenSize; ++y){
					floats[(int)(z + y)] = (float)(hashedCollocationCounts[x,y] / casted);
				}
			}
			Console.WriteLine("Deflating and saving...");
			using DeflateStream ds = new DeflateStream(new FileStream(save, FileMode.Create, FileAccess.Write, FileShare.None, 16777216, FileOptions.SequentialScan), CompressionLevel.SmallestSize, false);
			ds.Write(bytes, 0, bytes.Length);

		}
	}
}