//
// LayoutWrapper.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2013 Alexander Bothe
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using Xwt.Drawing;
using Mono.TextEditor.Highlighting;
using System.Collections.Generic;

namespace Mono.TextEditor
{
	public class LayoutWrapper : IDisposable
	{
		public int IndentSize {
			get;
			set;
		}

		public TextLayout Layout {
			get;
			private set;
		}

		public bool IsUncached {
			get;
			set;
		}

		public bool StartSet {
			get;
			set;
		}

		public IEnumerable<Chunk> Chunks {
			get;
			set;
		}

		public char[] LineChars {
			get;
			set;
		}

		public CloneableStack<Mono.TextEditor.Highlighting.Span> EolSpanStack {
			get;
			set;
		}

		int selectionStartIndex;

		public int SelectionStartIndex {
			get {
				return selectionStartIndex;
			}
			set {
				selectionStartIndex = value;
				StartSet = true;
			}
		}

		public int SelectionEndIndex {
			get;
			set;
		}

		public int PangoWidth {
			get;
			set;
		}

		public LayoutWrapper (TextLayout layout)
		{
			this.Layout = layout;
			this.IsUncached = false;
		}

		public void Dispose ()
		{
			if (Layout != null) {
				Layout.Dispose ();
				Layout = null;
			}
		}

		public class BackgroundColor
		{
			public readonly Color Color;
			public readonly int FromIdx;
			public readonly int ToIdx;

			public BackgroundColor (Color color, int fromIdx, int toIdx)
			{
				this.Color = color;
				this.FromIdx = fromIdx;
				this.ToIdx = toIdx;
			}
		}

		List<BackgroundColor> backgroundColors = null;

		public List<BackgroundColor> BackgroundColors {
			get {
				return backgroundColors ?? new List<BackgroundColor> ();
			}
		}

		public void AddBackground (Color color, int fromIdx, int toIdx)
		{
			if (backgroundColors == null)
				backgroundColors = new List<BackgroundColor> ();
			BackgroundColors.Add (new BackgroundColor (color, fromIdx, toIdx));
		}

		public uint TranslateToUTF8Index (uint textIndex, ref uint curIndex, ref uint byteIndex)
		{
			return TextViewMargin.TranslateToUTF8Index (LineChars, textIndex, ref curIndex, ref byteIndex);
		}
	}
}

