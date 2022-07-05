/*
 *  Copyright 2017 Vitalii Fedorchenko
 *
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the Apache License version 2.
 *
 *  This C# implementation is a port of hankcs's https://github.com/hankcs/AhoCorasickDoubleArrayTrie (java)
 *  that licensed under the Apache 2.0 License (see http://www.apache.org/licenses/LICENSE-2.0).
 *
 *  Unless required by applicable law or agreed to in writing, software distributed on an
 *  "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace NReco.Text {
	/// <summary>
	/// An implementation of Aho Corasick algorithm based on Double Array Trie.
	/// </summary>
	public partial class AhoCorasickDoubleArrayTrie<V> {
		/// <summary>
		/// Check array of the Double Array Trie structure
		/// </summary>
		protected int[] check;

		/// <summary>
		/// Base array of the Double Array Trie structure
		/// </summary>
		protected int[] @base;

		/// <summary>
		/// Fail table of the Aho Corasick automata
		/// </summary>
		protected int[] fail;

		/// <summary>
		/// Output table of the Aho Corasick automata
		/// </summary>
		protected int[][] output;

		/// <summary>
		/// Outer value array
		/// </summary>
		protected V[] v;

		/// <summary>
		/// The length of every key.
		/// </summary>
		protected int[] l;

		/// <summary>
		/// The size of base and check array
		/// </summary>
		protected int size;

		protected bool ignoreCase;

		public AhoCorasickDoubleArrayTrie() {
		}

		public AhoCorasickDoubleArrayTrie(IEnumerable<KeyValuePair<string, V>> keywords)
			: this(keywords, false) {
		}

		public AhoCorasickDoubleArrayTrie(IEnumerable<KeyValuePair<string, V>> keywords, bool ignoreCase) {
			Build(keywords, ignoreCase);
		}

		/// <summary>
		/// Parse text and match all substrings.
		/// </summary>
		/// <param name="text">The text</param>
		/// <returns>a list of matches</returns>
		public IList<Hit> ParseText(string text) {
			int position = 1;
			int currentState = 0;
			IList<Hit> collectedEmits = new List<Hit>();
			bool ignoreCase = this.ignoreCase;
			for (int i = 0; i < text.Length; ++i) {
				char character = text[i];
				if (ignoreCase) {
					character = ToLowerCase(character);
				}

				currentState = GetState(currentState, character);
				StoreEmits(position, currentState, collectedEmits);
				++position;
			}

			return collectedEmits;
		}

		/// <summary>
		/// Parse text and match substrings (cancellable).
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="processor">A processor which handles matches (returns 'continue' flag).</param>
		public void ParseText(string text, Func<Hit, bool> processor) {
			int position = 1;
			int currentState = 0;
			bool ignoreCase = this.ignoreCase;
			char c;
			for (int chIdx = 0; chIdx < text.Length; ++chIdx) {
				c = text[chIdx];
				if (ignoreCase) {
					c = ToLowerCase(c);
				}

				currentState = GetState(currentState, c);
				int[] hitArray = output[currentState];
				if (hitArray != null) {
					foreach (int hit in hitArray) {
						V value = v == null ? default : v[hit];
						if (!processor(new Hit(position - l[hit], position, value, hit))) {
							return;
						}
					}
				}

				++position;
			}
		}

		/// <summary>
		/// Parse text and match all substrings with a handler.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="processor">A processor which handles matches.</param>
		public void ParseText(string text, Action<Hit> processor) =>
			ParseText(text, (hit) => { processor(hit); return true; });

		/// <summary>
		/// Parse text represented as char array.
		/// </summary>
		/// <param name="text">The text represented by a char array</param>
		/// <param name="processor">A processor which handles matches (returns 'continue' flag).</param>
		public void ParseText(IList<char> text, Func<Hit, bool> processor) =>
			ParseText(text, 0, text.Count, processor);

		/// <summary>
		/// Parse text in a char array buffer.
		/// </summary>
		/// <param name="text">char array buffer.</param>
		/// <param name="start">text start position.</param>
		/// <param name="length">text length in the char array.</param>
		/// <param name="processor">A processor which handles matches (returns 'continue' flag).</param>
		public void ParseText(IList<char> text, int start, int length, Func<Hit, bool> processor) {
			int position = 1;
			int currentState = 0;
			char c;
			int end = start + length;
			for (int chIdx = start; chIdx < end; chIdx++) {
				c = text[chIdx];
				if (ignoreCase) {
					c = ToLowerCase(c);
				}

				currentState = GetState(currentState, c);
				int[] hitArray = this.output[currentState];
				if (hitArray != null) {
					foreach (var hit in hitArray) {
						if (!processor(new Hit(position - l[hit], position, v[hit], hit))) {
							return;
						}
					}
				}

				++position;
			}
		}

		/// <summary>
		/// Checks that string contains at least one substring
		/// </summary>
		/// <param name="text">source text to check</param>
		/// <returns><see langword="true" /> if string contains at least one substring</returns>
		public bool Matches(string text) {
			int currentState = 0;
			bool ignoreCase = this.ignoreCase;
			for (int i = 0; i < text.Length; ++i) {
				char character = text[i];
				if (ignoreCase) {
					character = ToLowerCase(character);
				}

				currentState = GetState(currentState, character);
				int[] hitArray = output[currentState];
				if (hitArray != null) {
					return true;
				}
			}

			return false;
		}

		/**
		 * Search first match in string
		 *
		 * @param text source text to check
		 * @return first match or {@code null} if there are no matches
		 */
		public Hit? FindFirst(string text) {
			int position = 1;
			int currentState = 0;
			bool ignoreCase = this.ignoreCase;
			for (int i = 0; i < text.Length; ++i) {
				char character = text[i];
				if (ignoreCase) {
					character = ToLowerCase(character);
				}

				currentState = GetState(currentState, character);
				int[] hitArray = this.output[currentState];
				if (hitArray != null) {
					int hitIndex = hitArray[0];
					return new Hit(position - l[hitIndex], position, v[hitIndex], hitIndex);
				}

				++position;
			}

			return null;
		}

		private static char ToLowerCase(char ch) {
			if (ch < '\u0080') {
				// this is ascii char
				if ('A' <= ch && ch <= 'Z') {
					ch |= ' ';
				}
			} else {
				ch = char.ToLowerInvariant(ch);
			}

			return ch;
		}

		/// <summary>
		/// Gets the size of the keywords that could be matched by automata.
		/// </summary>
		public int Count => this.v.Length;

		/// <summary>
		/// Gets value by a string key.
		/// </summary>
		/// <param name="key">The key (substring that can be matched by automata).</param>
		/// <returns>The value.</returns>
		public V this[string key] {
			get {
				int index = this.ExactMatchSearch(key);
				if (index >= 0) {
					return this.v[index];
				}

				return default;
			}
		}

		/// <summary>
		/// Pick the value by index in value array.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The value.</returns>
		/// <remarks>Notice that to be more efficiently, this method DO NOT check the parameter.</remarks>
		public V this[int index] => this.v[index];

		// transmit state, supports failure function
		private int GetState(int currentState, char character) {
			int newCurrentState = TransitionWithRoot(currentState, character);
			while (newCurrentState == -1) {
				currentState = this.fail[currentState];
				newCurrentState = TransitionWithRoot(currentState, character);
			}

			return newCurrentState;
		}

		// store output
		private void StoreEmits(int position, int currentState, IList<Hit> collectedEmits) {
			int[] hitArray = this.output[currentState];
			if (hitArray != null) {
				foreach (int hit in hitArray) {
					collectedEmits.Add(new Hit(position - l[hit], position, v[hit], hit));
				}
			}
		}

		/// <summary>
		/// transition of a state
		/// </summary>
		protected int Transition(int current, char c) {
			//int b = current;
			int p = current + c + 1; // b + c + 1
			if (current == check[p]) {
				return @base[p];
			}

			return -1;
		}

		/// <summary>
		/// transition of a state, if the state is root and it failed, then returns the root
		/// </summary>
		protected int TransitionWithRoot(int nodePos, char c) {
			int b = @base[nodePos];
			int p = b + c + 1;
			if (b != check[p]) {
				if (nodePos == 0) {
					return 0;
				}

				return -1;
			}

			return p;
		}

		private static IEnumerable<KeyValuePair<string, V>> ToLowerCase(IEnumerable<KeyValuePair<string, V>> input) {
			foreach (var pair in input) {
				yield return new KeyValuePair<string, V>(pair.Key.ToLowerInvariant(), pair.Value);
			}
		}

		/// <summary>
		/// Build a AhoCorasickDoubleArrayTrie from a sequence of string key -> value pairs.
		/// </summary>
		public void Build(IEnumerable<KeyValuePair<string, V>> input, bool ignoreCase = false) {
			this.ignoreCase = ignoreCase;
			if (ignoreCase) {
				input = ToLowerCase(input);
			}

			new Builder(this).Build(input);
		}

		/// <summary>
		/// Match exactly by a key
		/// </summary>
		/// <param name="key">the key</param>
		/// <returns>the index of the key, you can use it as a perfect hash function.</returns>
		public int ExactMatchSearch(string key) =>
			ExactMatchSearch(key, 0, 0, 0);

		private int ExactMatchSearch(string key, int pos, int len, int nodePos) {
			if (len <= 0) {
				len = key.Length;
			}

			if (nodePos <= 0) {
				nodePos = 0;
			}

			const int result = -1;
			return GetMatched(pos, len, result, key, this.@base[nodePos]);
		}

		private int GetMatched(int pos, int len, int result, string key, int b1) {
			int b = b1;
			int p;

			for (int i = pos; i < len; i++) {
				p = b + key[i] + 1;
				if (b == this.check[p]) {
					b = this.@base[p];
				} else {
					return result;
				}
			}

			p = b; // transition through '\0' to check if it's the end of a word
			int n = this.@base[p];
			if (b == this.check[p]) // yes, it is.
			{
				result = -n - 1;
			}

			return result;
		}

		/// <summary>
		/// Save automata state into binary stream.
		/// </summary>
		public void Save(Stream output, bool saveValues) {
			using var binWr = new Write7BitEncodedBinaryWriter(output);
			binWr.Write((byte)3); // number of single-value props
			binWr.Write("saveValues");
			binWr.Write(saveValues);
			binWr.Write("size");
			binWr.Write(size);
			binWr.Write("ignoreCase");
			binWr.Write(ignoreCase);

			binWr.WriteIntArray(l);
			binWr.WriteIntArray(@base);
			binWr.WriteIntArray(check);
			binWr.WriteIntArray(fail);
			binWr.WriteIntIntArray(this.output);

			if (saveValues) {
				var vType = typeof(V);
				var typeCode = Type.GetTypeCode(vType);
				Action<Write7BitEncodedBinaryWriter, object> wrElem;
				if (typeCode != TypeCode.Object && (int)typeCode < Write7BitEncodedBinaryWriter.TypeCodeWriters.Length) {
					wrElem = Write7BitEncodedBinaryWriter.TypeCodeWriters[(int)typeCode];
				} else {
					throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "Cannot write values of type '{0}', only primitive types are supported.", vType));
				}
				binWr.Write7BitEncodedInt(v.Length);
				for (int i = 0; i < v.Length; i++) {
					wrElem(binWr, (object)this.v[i]);
				}
			}
		}

		/// <summary>
		/// Load automata state from specified binary stream.
		/// </summary>
		public void Load(Stream input) {
			using var binRdr = new Read7BitEncodedBinaryReader(input);
			var loadValues = true;

			var propsCount = binRdr.ReadByte();
			for (byte i = 0; i < propsCount; i++) {
				var propName = binRdr.ReadString();
				switch (propName) {
					case "saveValues":
						loadValues = binRdr.ReadBoolean();
						break;
					case "size":
						size = binRdr.ReadInt32();
						break;
					case "ignoreCase":
						ignoreCase = binRdr.ReadBoolean();
						break;
				}
			}

			this.l = binRdr.ReadIntArray();
			this.@base = binRdr.ReadIntArray();
			this.check = binRdr.ReadIntArray();
			this.fail = binRdr.ReadIntArray();
			this.output = binRdr.ReadIntIntArray();

			if (loadValues) {
				var vType = typeof(V);
				var typeCode = Type.GetTypeCode(vType);
				Func<Read7BitEncodedBinaryReader, object> readElem;
				if (typeCode != TypeCode.Object && (int)typeCode < Read7BitEncodedBinaryReader.TypeCodeReaders.Length) {
					readElem = Read7BitEncodedBinaryReader.TypeCodeReaders[(int)typeCode];
				} else {
					throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "Cannot read values of type '{0}', only primitive types are supported.", vType));
				}
				var vLen = binRdr.Read7BitEncodedInt();
				this.v = new V[vLen];
				for (int i = 0; i < v.Length; i++) {
					v[i] = (V)readElem(binRdr);
				}
			} else {
				this.v = null;
			}
		}

		/// <summary>
		/// Load automata state from specified binary stream. If values are not saved specified handler is used to restore them.
		/// </summary>
		public void Load(Stream input, Func<int, V> loadValueHandler) {
			Load(input);
			if (this.v == null && loadValueHandler != null) {
				this.v = new V[this.l.Length];
				for (int i = 0; i < this.l.Length; i++)
					this.v[i] = loadValueHandler(i);
			}
		}

		internal class Read7BitEncodedBinaryReader : BinaryReader {
			public Read7BitEncodedBinaryReader(Stream stream)
				: base(stream, new UTF8Encoding(), true) { }

			public new int Read7BitEncodedInt() {
				return base.Read7BitEncodedInt();
			}

			public int[] ReadIntArray() {
				var arrLen = base.Read7BitEncodedInt();
				if (arrLen < 0) {
					return null;
				}

				var arr = new int[arrLen];
				for (int i = 0; i < arr.Length; i++) {
					arr[i] = base.Read7BitEncodedInt();
				}

				return arr;
			}

			public int[][] ReadIntIntArray() {
				var arrLen = base.Read7BitEncodedInt();
				int[][] arr = new int[arrLen][];
				for (int i = 0; i < arr.Length; i++) {
					arr[i] = ReadIntArray();
				}

				return arr;
			}

			internal static readonly Func<Read7BitEncodedBinaryReader, object>[] TypeCodeReaders = new Func<Read7BitEncodedBinaryReader, object>[] {
				(_) => null, // null
				(_) => throw new NotSupportedException(), // read object!!
				(_) => DBNull.Value, // dbnull
				(rdr) => rdr.ReadBoolean(),
				(rdr) => rdr.ReadChar(),
				(rdr) => rdr.ReadSByte(),
				(rdr) => rdr.ReadByte(),
				(rdr) => rdr.ReadInt16(),
				(rdr) => rdr.ReadUInt16(),
				(rdr) => rdr.ReadInt32(),
				(rdr) => rdr.ReadUInt32(),
				(rdr) => rdr.ReadInt64(),
				(rdr) => rdr.ReadUInt64(),
				(rdr) => rdr.ReadSingle(),
				(rdr) => rdr.ReadDouble(),
				(rdr) => rdr.ReadDecimal(),
				(rdr) => DateTime.FromBinary(rdr.ReadInt64()),
				(_) => null, // 17 - not used typecode
				(rdr) => rdr.ReadString(),
			};
		}

		internal class Write7BitEncodedBinaryWriter : BinaryWriter {
			public Write7BitEncodedBinaryWriter(Stream stream)
				: base(stream, new UTF8Encoding(false, true), true) { }

			public new void Write7BitEncodedInt(int i) {
				base.Write7BitEncodedInt(i);
			}

			public void WriteIntArray(int[] arr) {
				if (arr == null) {
					base.Write7BitEncodedInt(-1);
					return;
				}
				base.Write7BitEncodedInt(arr.Length);
				for (long i = 0; i < arr.Length; i++) {
					base.Write7BitEncodedInt(arr[i]);
				}
			}

			public void WriteIntIntArray(int[][] arr) {
				base.Write7BitEncodedInt(arr.Length);
				for (int i = 0; i < arr.Length; i++) {
					WriteIntArray(arr[i]);
				}
			}

			internal static readonly Action<Write7BitEncodedBinaryWriter, object>[] TypeCodeWriters = new Action<Write7BitEncodedBinaryWriter, object>[] {
				(_, _) => { },
				(_, _) => throw new NotSupportedException(), //write object
				(_, _) => { },
				(wr, o) => wr.Write( (bool)o ),
				(wr, o) => wr.Write( (char)o ),
				(wr, o) => wr.Write( (sbyte)o ),
				(wr, o) => wr.Write( (byte)o ),
				(wr, o) => wr.Write( (short)o ),
				(wr, o) => wr.Write( (ushort)o ),
				(wr, o) => wr.Write( (int)o ),
				(wr, o) => wr.Write( (uint)o ),
				(wr, o) => wr.Write( (long)o ),
				(wr, o) => wr.Write( (ulong)o ),
				(wr, o) => wr.Write( (float)o ),
				(wr, o) => wr.Write( (double)o ),
				(wr, o) => wr.Write( (decimal)o ),
				(wr, o) => wr.Write( ((DateTime)o).ToBinary() ),
				(_, _) => { }, // 17 - not used typecode
				(wr, o) => wr.Write(Convert.ToString(o, CultureInfo.InvariantCulture))
			};
		}
	}
}
