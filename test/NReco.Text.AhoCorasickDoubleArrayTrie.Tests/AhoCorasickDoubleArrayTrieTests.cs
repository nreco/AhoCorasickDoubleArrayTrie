using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using System.Threading.Tasks;
using System.Globalization;

namespace NReco.Text {

	public sealed class AhoCorasickDoubleArrayTrieTests : IDisposable {
		private readonly ITestOutputHelper output;
		private readonly TextWriter origConsoleOut;
		private StringWriter consoleOut;

		public AhoCorasickDoubleArrayTrieTests(ITestOutputHelper output) {
			this.output = output;

			// catch Console.WriteLine if used for debug purposes
			this.origConsoleOut = Console.Out;
			this.consoleOut = new StringWriter();
			Console.SetOut(consoleOut);
		}

		public void Dispose() {
			var consoleOut = this.consoleOut;
			if (consoleOut != null) {
				this.consoleOut = null;
				this.output.WriteLine(consoleOut.ToString());
				Console.SetOut(origConsoleOut);
				consoleOut.Dispose();
			}
		}

		private static AhoCorasickDoubleArrayTrie<string> BuildASimpleAhoCorasickDoubleArrayTrie(params string[] keywords) {
			// Collect test data set
			var map = new Dictionary<string, string>();
			foreach (var key in keywords) {
				map[key] = key;
			}

			var acdat = new AhoCorasickDoubleArrayTrie<string>();
			acdat.Build(map);
			return acdat;
		}

		private void ValidateASimpleAhoCorasickDoubleArrayTrie(AhoCorasickDoubleArrayTrie<string> acdat, string text, string[] expected) {
			// Test it
			acdat.ParseText(text, (hit) => {
				output.WriteLine("[{0}:{1}]={2}", hit.Begin, hit.End, hit.Value);
				Assert.Equal(text.Substring(hit.Begin, hit.Length), hit.Value);
			});
			// Or simply use
			var wordList = acdat.ParseText(text);
			Assert.Equal(expected, wordList.Select(h => h.Value));
		}

		[Fact]
		public void TestBuildAndParseSimply() {
			var acdat = BuildASimpleAhoCorasickDoubleArrayTrie("hers", "his", "she", "he");
			ValidateASimpleAhoCorasickDoubleArrayTrie(acdat, "uhers", new[] { "he", "hers" });

			var acdat2 = BuildASimpleAhoCorasickDoubleArrayTrie("he", "she", "his", "her");
			ValidateASimpleAhoCorasickDoubleArrayTrie(acdat2, "herhehis", new[] { "he", "her", "he", "his" });
			ValidateASimpleAhoCorasickDoubleArrayTrie(acdat2, "hisher", new[] { "his", "she", "he", "her" });

			Assert.Equal("she", acdat2["she"]);
		}

		[Fact]
		public async Task TestSaveLoadAsync() {
			var acdat = BuildASimpleAhoCorasickDoubleArrayTrie("hers", "his", "she", "he");
			var acdat2 = new AhoCorasickDoubleArrayTrie<string>();
			using (var memStream = new MemoryStream()) {
				acdat.Save(memStream, true);

				output.WriteLine($"4 keywords, saved {memStream.Length} bytes");

				memStream.Position = 0;
				acdat2.Load(memStream);

				Assert.Equal(acdat.Count, acdat2.Count);
				Assert.Equal("his", acdat2["his"]);
				ValidateASimpleAhoCorasickDoubleArrayTrie(acdat2, "uhers", new[] { "he", "hers" });
			}

			// large dictionary
			var dictionary = await LoadDictionaryAsync("en.dictionary.txt").ConfigureAwait(true);
			var keywords = dictionary.Select(k => new KeyValuePair<string, string>(k, k));
			var acdat3 = new AhoCorasickDoubleArrayTrie<string>(keywords);
			using (var memStream2 = new MemoryStream()) {
				acdat3.Save(memStream2, false);
				output.WriteLine($"{dictionary.Count} keywords, saved {memStream2.Length} bytes (without values)");
			}

			// save without values
			acdat = BuildASimpleAhoCorasickDoubleArrayTrie("hers", "his", "she", "he");
			using (var memStream3 = new MemoryStream()) {
				acdat.Save(memStream3, false);
				acdat2 = new AhoCorasickDoubleArrayTrie<string>();
				memStream3.Position = 0;
				acdat2.Load(memStream3);
				acdat2.ParseText("uhers", (hit) => {
					Assert.Null(hit.Value); // values not loaded
				});
				memStream3.Position = 0;
				acdat2.Load(memStream3, (idx) => new[] { "hers", "his", "she", "he" }[idx]);
				ValidateASimpleAhoCorasickDoubleArrayTrie(acdat2, "uhers", new[] { "he", "hers" });
			}
		}

		[Fact]
		public async Task TestBuildVeryLongWord() {
			var map = new Dictionary<string, string>();

			const int longWordLength = 20000;

			string word = await LoadTextAsync("cn.text.txt").ConfigureAwait(true);
			map[word[10..longWordLength]] = word[10..longWordLength];
			map[word[30..40]] = null;

			word = await LoadTextAsync("en.text.txt").ConfigureAwait(true);
			map[word[10..longWordLength]] = word[10..longWordLength];
			map[word[30..40]] = null;

			// Build an AhoCorasickDoubleArrayTrie
			var acdat = new AhoCorasickDoubleArrayTrie<string>();
			acdat.Build(map);

			var result = acdat.ParseText(word);

			Assert.Equal(2, result.Count);
			Assert.Equal(30, result[0].Begin);
			Assert.Equal(40, result[0].End);
			Assert.Equal(10, result[1].Begin);
			Assert.Equal(longWordLength, result[1].End);
		}

		[Theory]
		[InlineData("cn")]
		[InlineData("en")]
		public async Task TestBuildAndParseWithBigFile(string language) {
			// Load test data from disk
			var dictionary = await LoadDictionaryAsync($"{language}.dictionary.txt").ConfigureAwait(true);
			var text = await LoadTextAsync($"{language}.text.txt").ConfigureAwait(true);

			// You can use any type of Map to hold data
			var map = new Dictionary<string, string>();
			foreach (var key in dictionary) {
				map[key] = key;
			}

			// Build an AhoCorasickDoubleArrayTrie
			var acdat = new AhoCorasickDoubleArrayTrie<string>();
			acdat.Build(map);

			// Test it
			acdat.ParseText(text, (hit) =>
			  Assert.Equal(text.Substring(hit.Begin, hit.Length), hit.Value));
		}

		[Fact]
		public void TestParseCharArray() {
			var chars = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.".ToCharArray();
			var keywords = new[] { "dolor", "it" };


			var acdat = new AhoCorasickDoubleArrayTrie<string>(keywords.Select((k, i) => new KeyValuePair<string, string>(k, i.ToString(CultureInfo.InvariantCulture))));
			var collectedValues = new List<string>();
			acdat.ParseText(chars, hit => { collectedValues.Add(hit.Value); return true; });
			Assert.Equal(new[] { "0", "1", "1", "0" }, collectedValues);

			var collectedValues2 = new List<string>();
			acdat.ParseText(chars, 14, 10, hit => { collectedValues2.Add(hit.Value); return true; });
			Assert.Equal(new[] { "1" }, collectedValues2);
		}

		[Fact]
		public void TestCaseInsensitive() {
			const string text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";
			var keywords = new[] { "doLor", "iT" };

			var acdat = new AhoCorasickDoubleArrayTrie<string>();
			acdat.Build(keywords.Select((k, i) => new KeyValuePair<string, string>(k, i.ToString(CultureInfo.InvariantCulture))), true);
			var collectedValues = new List<string>();
			acdat.ParseText(text, hit => { collectedValues.Add(hit.Value); return true; });
			Assert.Equal(new[] { "0", "1", "1", "0" }, collectedValues);
		}

		[Fact]
		public void TestMatches() {
			var map = new Dictionary<string, int> {
				["space"] = 1,
				["keyword"] = 2,
				["ch"] = 3,
			};
			var trie = new AhoCorasickDoubleArrayTrie<int>();
			trie.Build(map);

			Assert.True(trie.Matches("space"));
			Assert.True(trie.Matches("keyword"));
			Assert.True(trie.Matches("ch"));
			Assert.True(trie.Matches("  ch"));
			Assert.True(trie.Matches("chkeyword"));
			Assert.True(trie.Matches("oooospace2"));
			Assert.False(trie.Matches("c"));
			Assert.False(trie.Matches(""));
			Assert.False(trie.Matches("spac"));
			Assert.False(trie.Matches("nothing"));
		}

		[Fact]
		public void TestFirstMatch() {
			var map = new Dictionary<string, int> {
				["space"] = 1,
				["keyword"] = 2,
				["ch"] = 3,
			};
			var trie = new AhoCorasickDoubleArrayTrie<int>();
			trie.Build(map);

			AhoCorasickDoubleArrayTrie<int>.Hit? hit = trie.FindFirst("space");
			Assert.Equal(0, hit?.Begin);
			Assert.Equal(5, hit?.End);
			Assert.Equal(1, hit?.Value);

			hit = trie.FindFirst("a lot of garbage in the space ch");
			Assert.Equal(24, hit?.Begin);
			Assert.Equal(29, hit?.End);
			Assert.Equal(1, hit?.Value);

			Assert.Null(trie.FindFirst(""));
			Assert.Null(trie.FindFirst("value"));
			Assert.Null(trie.FindFirst("keywork"));
			Assert.Null(trie.FindFirst(" no pace"));
		}

		[Fact]
		public void TestCancellation() {
			// Collect test data set
			var map = new Dictionary<string, string>() {
				["foo"] = "foo",
				["bar"] = "bar",
			};

			// Build an AhoCorasickDoubleArrayTrie
			var acdat = new AhoCorasickDoubleArrayTrie<string>();
			acdat.Build(map);

			// count matches
			const string haystack = "sfwtfoowercwbarqwrcq";
			int count = 0;
			int countCancel = 0;
			acdat.ParseText(haystack, cancellingMatcher);
			acdat.ParseText(haystack, countingMatcher);

			Assert.Equal(1, countCancel);
			Assert.Equal(2, count);

			bool cancellingMatcher(AhoCorasickDoubleArrayTrie<string>.Hit _) {
				countCancel++;
				return false;
			}

			bool countingMatcher(AhoCorasickDoubleArrayTrie<string>.Hit _) {
				count++;
				return true;
			}
		}

		private async Task RunTestAsync(string dictionaryPath, string textPath) {
			ISet<string> dictionary = await LoadDictionaryAsync(dictionaryPath).ConfigureAwait(false);
			string text = await LoadTextAsync(textPath).ConfigureAwait(true);

			var ahoCorasickDoubleArrayTrie = new AhoCorasickDoubleArrayTrie<string>();
			var dictionaryMap = new Dictionary<string, string>();
			foreach (string word in dictionary) {
				dictionaryMap[word] = word;  // we use the same text as the property of a word
			}

			var swBuild = new Stopwatch();
			ahoCorasickDoubleArrayTrie.Build(dictionaryMap, true);
			swBuild.Stop();
			output.WriteLine("Automata build time: {0}ms.\n", swBuild.ElapsedMilliseconds);

			// Let's test the speed of the two Aho-Corasick automata
			output.WriteLine("Parsing document which contains {0} characters, with a dictionary of {1} words.\n", text.Length, dictionary.Count);
			var sw = new Stopwatch();
			sw.Start();
			int hitCount = 0;
			ahoCorasickDoubleArrayTrie.ParseText(text, _ => hitCount++);
			sw.Stop();
			Assert.True(hitCount > 0);
			output.WriteLine("{0}ms, speed {1:0.##} char/s", sw.ElapsedMilliseconds, text.Length / (sw.ElapsedMilliseconds / 1000.0));
		}

		[Fact]
		public void TestBuildEmptyTrie() {
			var acdat = new AhoCorasickDoubleArrayTrie<string>();
			var map = new Dictionary<string, string>();
			acdat.Build(map);
			Assert.Equal(0, acdat.Count);
			var hits = acdat.ParseText("uhers");
			Assert.Empty(hits);
		}

		[Theory]
		[InlineData("cn")]
		[InlineData("en")]
		public async Task TestBenchmark(string language) {
			await RunTestAsync($"{language}.dictionary.txt", $"{language}.text.txt").ConfigureAwait(true);
		}

		private static async Task<string> LoadTextAsync(string path) {
			const string resPrefix = "NReco.Text.testdata.";
			using var resStream = typeof(AhoCorasickDoubleArrayTrieTests).Assembly.GetManifestResourceStream(resPrefix + path)
			  ?? throw new InvalidDataException("Embedded resource does not exists at this path");
			using var rdr = new StreamReader(resStream, leaveOpen: true);
			return await rdr.ReadToEndAsync().ConfigureAwait(false);
		}

		private static async Task<ISet<string>> LoadDictionaryAsync(string path) {
			const string resPrefix = "NReco.Text.testdata.";
			var dictionary = new HashSet<string>();

			var resStream = typeof(AhoCorasickDoubleArrayTrieTests).Assembly.GetManifestResourceStream(resPrefix + path)
			  ?? throw new InvalidDataException("Embedded resource does not exists at this path");
			using var rdr = new StreamReader(resStream);
			string line;
			while ((line = await rdr.ReadLineAsync().ConfigureAwait(false)) != null) {
				dictionary.Add(line);
			}

			return dictionary;
		}
	}
}
