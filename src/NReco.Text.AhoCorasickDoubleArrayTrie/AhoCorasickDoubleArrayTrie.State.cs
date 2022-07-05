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
using System.Text;

namespace NReco.Text {

	public partial class AhoCorasickDoubleArrayTrie<V> {

		internal class State {
			protected readonly int depth;

			private State failure;

			private ISet<int> emits;

			public State()
			 : this(0) {
			}

			public State(int depth) {
				this.depth = depth;
			}

			public int Depth => this.depth;

			private int largestValueId = int.MinValue;

			public void AddEmit(int keyword) {
				if (this.emits == null) {
					this.emits = new HashSet<int>();
				}

				if (keyword > largestValueId) {
					this.largestValueId = keyword;
				}

				this.emits.Add(keyword);
			}

			public int LargestValueId {
				get => emits == null || emits.Count == 0
				  ? int.MinValue // ?? null
				  : this.largestValueId;
			}

			public void AddEmit(IEnumerable<int> emits) {
				foreach (int emit in emits) {
					AddEmit(emit);
				}
			}

			public ICollection<int> Emit =>
			  this.emits ?? (ICollection<int>)Array.Empty<int>();

			public bool IsAcceptable =>
			  this.depth > 0 && this.emits != null;

			public State Failure => this.failure;

			public void SetFailure(State failState, int[] fail) {
				this.failure = failState;
				fail[this.Index] = failState.Index;
			}

			private State NextState(char character, bool ignoreRootState) {
				this.Success.TryGetValue(character, out State nextState);
				if (!ignoreRootState && nextState == null && this.depth == 0) {
					nextState = this;
				}

				return nextState;
			}

			public State NextState(char character) =>
			  NextState(character, false);

			public State NextStateIgnoreRootState(char character) =>
			  NextState(character, true);

			public State AddState(char character) {
				State nextState = this.NextStateIgnoreRootState(character);
				if (nextState == null) {
					nextState = new State(this.depth + 1);
					this.Success[character] = nextState;
				}
				return nextState;
			}

			public IEnumerable<State> States =>
			  this.Success.Values;

			public IEnumerable<char> Transitions =>
			  this.Success.Keys;

			public override string ToString() {
				var sb = new StringBuilder("State{");
				sb.Append("depth=").Append(this.depth);
				sb.Append(", ID=").Append(this.Index);
				sb.Append(", emits=").Append(this.emits);
				sb.Append(", success=").Append(this.Success.Keys);
				sb.Append(", failureID=").Append(failure == null ? "-1" : failure.Index);
				sb.Append(", failure=").Append(failure);
				sb.Append('}');
				return sb.ToString();
			}

			public IDictionary<char, State> Success { get; } = new SortedDictionary<char, State>();

			public int Index { get; set; }
		}

	}

}
