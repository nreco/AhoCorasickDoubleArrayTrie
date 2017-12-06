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
using System.IO;
using System.Reflection;
using System.Text;

namespace NReco.Text
{

	/// <summary>
	/// An implementation of Aho Corasick algorithm based on Double Array Trie.
	/// </summary>
	public partial class AhoCorasickDoubleArrayTrie<V>  {

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

		public AhoCorasickDoubleArrayTrie() {

		}

		public AhoCorasickDoubleArrayTrie(IEnumerable<KeyValuePair<string,V>> keywords) {
			Build(keywords);
		}

		/// <summary>
		/// Parse text and match all substrings.
		/// </summary>
		/// <param name="text">The text</param>
		/// <returns>a list of matches</returns>
		public List<Hit> ParseText(string text) {
			var collectedEmits = new List<Hit>();
			ParseText(text, (hit) => {
				collectedEmits.Add(hit);
				return true;
			});
			return collectedEmits;
		}

		/// <summary>
		/// Parse text and match substrings (cancellable).
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="processor">A processor which handles matches (returns 'continue' flag).</param>
		public void ParseText(string text, Func<Hit,bool> processor) {
			int position = 1;
			int currentState = 0;
			for (int chIdx = 0; chIdx < text.Length; ++chIdx) {
				currentState = getState(currentState, text[chIdx]);
				int[] hitArray = output[currentState];
				if (hitArray != null) {
					for (int i = 0; i < hitArray.Length; i++) {
						var hit = hitArray[i];
						// begin, end, value
						if (!processor(new Hit(position - l[hit], position, v[hit], hit)))
							return;
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
		public void ParseText(String text, Action<Hit> processor) {
			ParseText(text, (hit) => { processor(hit); return true; });
		}

		/// <summary>
		/// Parse text represented as char array.
		/// </summary>
		/// <param name="text">The text</param>
		/// <param name="processor">A processor which handles matches (returns 'continue' flag).</param>
		public void ParseText(char[] text, Func<Hit, bool> processor) {
			int position = 1;
			int currentState = 0;
			for (int chIdx=0; chIdx < text.Length; chIdx++) {
				char c = text[chIdx];
				currentState = getState(currentState, c);
				int[] hitArray = output[currentState];
				if (hitArray != null) {
					for (int i=0; i<hitArray.Length; i++) {
						var hit = hitArray[i];
						if (!processor( new Hit( position - l[hit], position, v[hit], hit ) ))
							return;
					}
				}
				++position;
			}
		}

		/// <summary>
		/// Gets the size of the keywords that could be matched by automata.
		/// </summary>
		public int Count {
			get {
				return v.Length;
			}
		}

		/// <summary>
		/// Gets value by a string key.
		/// </summary>
		/// <param name="key">The key (substring that can be matched by automata).</param>
		/// <returns>The value.</returns>
		public V this[string key] {
			get {
				int index = ExactMatchSearch(key);
				if (index >= 0) {
					return v[index];
				}
				return default(V);
			}
		}

		/// <summary>
		/// Pick the value by index in value array.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The value.</returns>
		/// <remarks>Notice that to be more efficiently, this method DONOT check the parameter.</remarks>
		public V this[int index] {
			get {
				return v[index];
			}
		}

		// transmit state, supports failure function
		private int getState(int currentState, char character) {
			int newCurrentState = transitionWithRoot(currentState, character);
			while (newCurrentState == -1) {
				currentState = fail[currentState];
				newCurrentState = transitionWithRoot(currentState, character);
			}
			return newCurrentState;
		}


		/// <summary>
		/// transition of a state
		/// </summary>
		protected int transition(int current, char c) {
			int b = current;
			int p;

			p = b + c + 1;
			if (b == check[p])
				b = @base[p];
			else
				return -1;

			p = b;
			return p;
		}

		/// <summary>
		/// transition of a state, if the state is root and it failed, then returns the root
		/// </summary>
		protected int transitionWithRoot(int nodePos, char c) {
			int b = @base[nodePos];
			int p;

			p = b + c + 1;
			if (b != check[p]) {
				if (nodePos == 0) return 0;
				return -1;
			}

			return p;
		}

		/// <summary>
		/// Build a AhoCorasickDoubleArrayTrie from a sequence of string key -> value pairs.
		/// </summary>
		public void Build(IEnumerable<KeyValuePair<String, V>> map) {
			new Builder(this).build(map);
		}

		/// <summary>
		/// Match exactly by a key
		/// </summary>
		/// <param name="key">the key</param>
		/// <returns>the index of the key, you can use it as a perfect hash function.</returns>
		public int ExactMatchSearch(String key) {
			return exactMatchSearch(key, 0, 0, 0);
		}

		private int exactMatchSearch(string key, int pos, int len, int nodePos) {
			if (len <= 0)
				len = key.Length;
			if (nodePos <= 0)
				nodePos = 0;

			return exactMatchSearch(key.ToCharArray(), pos, len, nodePos);
		}

		private int exactMatchSearch(char[] keyChars, int pos, int len, int nodePos) {
			int result = -1;

			int b = @base[nodePos];
			int p;

			for (int i = pos; i < len; i++) {
				p = b + (int)(keyChars[i]) + 1;
				if (b == check[p])
					b = @base[p];
				else
					return result;
			}

			p = b;
			int n = @base[p];
			if (b == check[p] && n < 0) {
				result = -n - 1;
			}
			return result;
		}


		/// <summary>
		/// Save automata state into binary stream.
		/// </summary>
		public void Save(Stream output, bool saveValues) {

			var binWr = new Write7BitEncodedBinaryWriter(output);
			binWr.Write((byte)2); // number of single-value props
			binWr.Write("saveValues");
			binWr.Write(saveValues);
			binWr.Write("size");
			binWr.Write(size);

			binWr.WriteIntArray(l);
			binWr.WriteIntArray(@base);
			binWr.WriteIntArray(check);
			binWr.WriteIntArray(fail);
			binWr.WriteIntIntArray(this.output);

			if (saveValues) {
				var vType = typeof(V);
				var typeCode = GetTypeCode(vType);
				Action<Write7BitEncodedBinaryWriter, object> wrElem;
				if (typeCode!=TypeCode.Object && (int)typeCode< Write7BitEncodedBinaryWriter.TypeCodeWriters.Length) {
					wrElem = Write7BitEncodedBinaryWriter.TypeCodeWriters[(int)typeCode];
				} else {
					throw new NotSupportedException(String.Format("Cannot write values of type '{0}', only primitive types are supported.", vType));
				}
				binWr.Write7BitEncodedInt(v.Length);
				for (int i = 0; i < v.Length; i++) {
					wrElem(binWr, (object)v[i]);
				}
			}
			/*out.writeObject(base);
			out.writeObject(check);
			out.writeObject(fail);
			out.writeObject(output);
			out.writeObject(l);
			out.writeObject(v);*/
		}

		/// <summary>
		/// Load automata state from specified binary stream.
		/// </summary>
		public void Load(Stream input) {
			var binRdr = new Read7BitEncodedBinaryReader(input);
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
				}
			}

			this.l = binRdr.ReadIntArray();
			this.@base = binRdr.ReadIntArray();
			this.check = binRdr.ReadIntArray();
			this.fail = binRdr.ReadIntArray();
			this.output = binRdr.ReadIntIntArray();

			if (loadValues) {
				var vType = typeof(V);
				var typeCode = GetTypeCode(vType);
				Func<Read7BitEncodedBinaryReader, object> readElem;
				if (typeCode != TypeCode.Object && (int)typeCode < Read7BitEncodedBinaryReader.TypeCodeReaders.Length) {
					readElem = Read7BitEncodedBinaryReader.TypeCodeReaders[(int)typeCode];
				} else {
					throw new NotSupportedException(String.Format("Cannot read values of type '{0}', only primitive types are supported.", vType));
				}
				var vLen = binRdr.Read7BitEncodedInt();
				this.v = new V[vLen];
				for (int i = 0; i < v.Length; i++) {
					v[i] = (V)readElem(binRdr);
				}
			}

		}

		private bool IsValueType(Type type) {
#if NET_STANDARD
			return type.GetTypeInfo().IsValueType;
#else
			return type.IsValueType;
#endif
		}

		private TypeCode GetTypeCode(Type type) {
#if NET_STANDARD
			if (type == null) {
				return TypeCode.Empty;
			} else if (type == typeof(Boolean)) {
				return TypeCode.Boolean;
			} else if (type == typeof(Char)) {
				return TypeCode.Char;
			} else if (type == typeof(SByte)) {
				return TypeCode.SByte;
			} else if (type == typeof(Byte)) {
				return TypeCode.Byte;
			} else if (type == typeof(Int16)) {
				return TypeCode.Int16;
			} else if (type == typeof(UInt16)) {
				return TypeCode.UInt16;
			} else if (type == typeof(Int32)) {
				return TypeCode.Int32;
			} else if (type == typeof(UInt32)) {
				return TypeCode.UInt32;
			} else if (type == typeof(Int64)) {
				return TypeCode.Int64;
			} else if (type == typeof(UInt64)) {
				return TypeCode.UInt64;
			} else if (type == typeof(Single)) {
				return TypeCode.Single;
			} else if (type == typeof(Double)) {
				return TypeCode.Double;
			} else if (type == typeof(Decimal)) {
				return TypeCode.Decimal;
			} else if (type == typeof(DateTime)) {
				return TypeCode.DateTime;
			} else if (type == typeof(String)) {
				return TypeCode.String;
			} else {
				return TypeCode.Object;
			}
#else
			return Type.GetTypeCode(type);
#endif
		}

		internal class Read7BitEncodedBinaryReader : BinaryReader {
			public Read7BitEncodedBinaryReader(Stream stream) : base(stream) { }

			public new int Read7BitEncodedInt() {
				return base.Read7BitEncodedInt();
			}

			public int[] ReadIntArray() {
				var arrLen = base.Read7BitEncodedInt();
				if (arrLen < 0)
					return null;
				var arr = new int[arrLen];
				for (int i = 0; i < arr.Length; i++) {
					arr[i] = base.Read7BitEncodedInt();
				}
				return arr;
			}

			public int[][] ReadIntIntArray() {
				var arrLen = base.Read7BitEncodedInt();
				var arr = new int[arrLen][];
				for (int i = 0; i < arr.Length; i++) {
					arr[i] = ReadIntArray();
				}
				return arr;
			}

			internal static readonly Func<Read7BitEncodedBinaryReader, object>[] TypeCodeReaders = new Func<Read7BitEncodedBinaryReader, object>[] {
				(rdr) => { return null; }, // null
				(rdr) => { throw new NotSupportedException(); }, // read object!! 
				(rdr) => { return DBNull.Value; }, // dbnull
				(rdr) => { return rdr.ReadBoolean(); },
				(rdr) => { return rdr.ReadChar(); },
				(rdr) => { return rdr.ReadSByte(); },
				(rdr) => { return rdr.ReadByte(); },
				(rdr) => { return rdr.ReadInt16(); },
				(rdr) => { return rdr.ReadUInt16(); },
				(rdr) => { return rdr.ReadInt32(); },
				(rdr) => { return rdr.ReadUInt32(); },
				(rdr) => { return rdr.ReadInt64(); },
				(rdr) => { return rdr.ReadUInt64(); },
				(rdr) => { return rdr.ReadSingle(); },
				(rdr) => { return rdr.ReadDouble(); },
				(rdr) => { return rdr.ReadDecimal(); },
				(rdr) => { return DateTime.FromBinary(rdr.ReadInt64()); },
				(rdr) => { return null; }, // 17 - not used typecode
				(rdr) => { return rdr.ReadString(); },
			};

		}

		internal class Write7BitEncodedBinaryWriter : BinaryWriter {
			public Write7BitEncodedBinaryWriter(Stream stream) : base(stream) { }

			public new void Write7BitEncodedInt(int i) {
				base.Write7BitEncodedInt(i);
			}

			public void WriteIntArray(int[] arr) {
				if (arr==null) {
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
				(wr,o) => { },
				(wr,o) => { throw new NotSupportedException(); }, //write object
				(wr,o) => { },
				(wr,o) => { wr.Write( (bool)o ); },
				(wr,o) => { wr.Write( (char)o ); },
				(wr,o) => { wr.Write( (sbyte)o ); },
				(wr,o) => { wr.Write( (byte)o ); },
				(wr,o) => { wr.Write( (short)o ); },
				(wr,o) => { wr.Write( (ushort)o ); },
				(wr,o) => { wr.Write( (int)o ); },
				(wr,o) => { wr.Write( (uint)o ); },
				(wr,o) => { wr.Write( (long)o ); },
				(wr,o) => { wr.Write( (ulong)o ); },
				(wr,o) => { wr.Write( (float)o ); },
				(wr,o) => { wr.Write( (double)o ); },
				(wr,o) => { wr.Write( (decimal)o ); },
				(wr,o) => { wr.Write( ((DateTime)o).ToBinary() ); },
				(wr,o) => { }, // 17 - not used typecode
				(wr,o) => { wr.Write(Convert.ToString(o)); }
			};

		}


	}


}
