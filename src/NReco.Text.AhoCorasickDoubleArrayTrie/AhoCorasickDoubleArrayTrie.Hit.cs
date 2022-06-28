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

using System.Globalization;

namespace NReco.Text {

	public partial class AhoCorasickDoubleArrayTrie<V> {
		/// <summary>
		/// A match result.
		/// </summary>
		public struct Hit {
			/// <summary>
			/// The beginning index, inclusive.
			/// </summary>
			public readonly int Begin;

			/// <summary>
			/// The ending index, exclusive.
			/// </summary>
			public readonly int End;

			/// <summary>
			/// The length of matched substring.
			/// </summary>
			public int Length => this.End - this.Begin;

			/// <summary>
			/// The value assigned to the keyword.
			/// </summary>
			public readonly V Value;

			/// <summary>
			/// The index of the keyword
			/// </summary>
			public readonly int Index;

			public Hit(int begin, int end, V value, int index) {
				this.Begin = begin;
				this.End = end;
				this.Value = value;
				this.Index = index;
			}

			public override string ToString() {
				return string.Format(CultureInfo.InvariantCulture, "[{0}:{1}]={2}", Begin, End, Value);
			}
		}
	}
}
